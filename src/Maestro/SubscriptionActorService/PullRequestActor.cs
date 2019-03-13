// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Maestro.Contracts;
using Maestro.Data;
using Maestro.Data.Models;
using Maestro.MergePolicies;
using Microsoft.DotNet.DarcLib;
using Microsoft.DotNet.ServiceFabric.ServiceHost.Actors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions.Internal;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Data;
using Asset = Maestro.Contracts.Asset;
using AssetData = Microsoft.DotNet.Maestro.Client.Models.AssetData;

namespace SubscriptionActorService
{
    namespace unused
    {
        // class needed to appease service fabric build time generation of actor code
        [StatePersistence(StatePersistence.Persisted)]
        public class PullRequestActor : Actor, IPullRequestActor, IRemindable
        {
            public PullRequestActor(ActorService actorService, ActorId actorId) : base(actorService, actorId)
            {
            }

            public Task<string> RunActionAsync(string method, string arguments)
            {
                throw new NotImplementedException();
            }

            public Task UpdateAssetsAsync(Guid subscriptionId, int buildId, string sourceSha, List<Asset> assets)
            {
                throw new NotImplementedException();
            }

            public Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
            {
                throw new NotImplementedException();
            }
        }
    }

    /// <summary>
    ///     A service fabric actor implementation that is responsible for creating and updating pull requests for dependency
    ///     updates.
    /// </summary>
    public class PullRequestActor : IPullRequestActor, IRemindable, IActionTracker
    {
        /// <summary>
        ///     Creates a new PullRequestActor
        /// </summary>
        /// <param name="id">
        ///     The actor id for this actor.
        ///     If it is a <see cref="Guid" /> actor id, then it is required to be the id of a non-batched subscription in the
        ///     database
        ///     If it is a <see cref="string" /> actor id, then it MUST be an actor id created with
        ///     <see cref="PullRequestActorId.Create(string, string)" /> for use with all subscriptions targeting the specified
        ///     repository and branch.
        /// </param>
        /// <param name="provider"></param>
        public PullRequestActor(ActorId id, IServiceProvider provider)
        {
            Id = id;
            if (Id.Kind == ActorIdKind.Guid)
            {
                Implementation =
                    ActivatorUtilities.CreateInstance<NonBatchedPullRequestActorImplementation>(provider, id);
            }
            else if (Id.Kind == ActorIdKind.String)
            {
                Implementation = ActivatorUtilities.CreateInstance<BatchedPullRequestActorImplementation>(provider, id);
            }
        }

        public PullRequestActorImplementation Implementation { get; }

        public ActorId Id { get; }

        public Task TrackSuccessfulAction(string action, string result)
        {
            return ((IActionTracker) Implementation).TrackSuccessfulAction(action, result);
        }

        public Task TrackFailedAction(string action, string result, string method, string arguments)
        {
            return ((IActionTracker) Implementation).TrackFailedAction(action, result, method, arguments);
        }

        public Task<string> RunActionAsync(string method, string arguments)
        {
            return ((IPullRequestActor) Implementation).RunActionAsync(method, arguments);
        }

        public Task UpdateAssetsAsync(Guid subscriptionId, int buildId, string sourceSha, List<Asset> assets)
        {
            return ((IPullRequestActor) Implementation).UpdateAssetsAsync(subscriptionId, buildId, sourceSha, assets);
        }

        public async Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
        {
            if (reminderName == PullRequestActorImplementation.PullRequestCheck)
            {
                await Implementation.SynchronizeInProgressPullRequestAsync();
            }
            else if (reminderName == PullRequestActorImplementation.PullRequestUpdate)
            {
                await Implementation.RunProcessPendingUpdatesAsync();
            }
            else
            {
                throw new ReminderNotFoundException(reminderName);
            }
        }
    }

    public abstract class PullRequestActorImplementation : IPullRequestActor, IActionTracker
    {
        public const string PullRequestCheck = "pullRequestCheck";
        public const string PullRequestUpdate = "pullRequestUpdate";
        public const string PullRequest = "pullRequest";

        protected PullRequestActorImplementation(
            ActorId id,
            IReminderManager reminders,
            IActorStateManager stateManager,
            IMergePolicyEvaluator mergePolicyEvaluator,
            BuildAssetRegistryContext context,
            IRemoteFactory darcFactory,
            ILoggerFactory loggerFactory,
            IActionRunner actionRunner,
            Func<ActorId, ISubscriptionActor> subscriptionActorFactory)
        {
            Id = id;
            Reminders = reminders;
            StateManager = stateManager;
            MergePolicyEvaluator = mergePolicyEvaluator;
            Context = context;
            DarcRemoteFactory = darcFactory;
            ActionRunner = actionRunner;
            SubscriptionActorFactory = subscriptionActorFactory;
            Logger = loggerFactory.CreateLogger(TypeNameHelper.GetTypeDisplayName(GetType()));
        }

        public ILogger Logger { get; }
        public ActorId Id { get; }
        public IReminderManager Reminders { get; }
        public IActorStateManager StateManager { get; }
        public IMergePolicyEvaluator MergePolicyEvaluator { get; }
        public BuildAssetRegistryContext Context { get; }
        public IRemoteFactory DarcRemoteFactory { get; }
        public IActionRunner ActionRunner { get; }
        public Func<ActorId, ISubscriptionActor> SubscriptionActorFactory { get; }

        public async Task TrackSuccessfulAction(string action, string result)
        {
            RepositoryBranchUpdate update = await GetRepositoryBranchUpdate();

            update.Action = action;
            update.ErrorMessage = result;
            update.Method = null;
            update.Arguments = null;
            update.Success = true;
            await Context.SaveChangesAsync();
        }

        public async Task TrackFailedAction(string action, string result, string method, string arguments)
        {
            RepositoryBranchUpdate update = await GetRepositoryBranchUpdate();

            update.Action = action;
            update.ErrorMessage = result;
            update.Method = method;
            update.Arguments = arguments;
            update.Success = false;
            await Context.SaveChangesAsync();
        }

        public Task<string> RunActionAsync(string method, string arguments)
        {
            return ActionRunner.RunAction(this, method, arguments);
        }

        Task IPullRequestActor.UpdateAssetsAsync(Guid subscriptionId, int buildId, string sourceSha, List<Asset> assets)
        {
            return ActionRunner.ExecuteAction(() => UpdateAssetsAsync(subscriptionId, buildId, sourceSha, assets));
        }

        protected abstract Task<(string repository, string branch)> GetTargetAsync();

        protected abstract Task<IReadOnlyList<MergePolicyDefinition>> GetMergePolicyDefinitions();

        private async Task<string> GetSourceRepositoryAsync(Guid subscriptionId)
        {
            Subscription subscription = await Context.Subscriptions.FindAsync(subscriptionId);
            return subscription?.SourceRepository;
        }

        /// <summary>
        /// Retrieve the build number from a database build id.
        /// </summary>
        /// <param name="buildId">Build id</param>
        /// <returns>Build number (e.g. 123245.6)</returns>
        private async Task<string> GetBuildNumberAsync(int buildId)
        {
            Build build = await Context.Builds.FindAsync(buildId);
            return build?.AzureDevOpsBuildNumber;
        }

        public Task RunProcessPendingUpdatesAsync()
        {
            return ActionRunner.ExecuteAction(() => ProcessPendingUpdatesAsync());
        }

        /// <summary>
        ///     Process any pending pull request updates stored in the <see cref="PullRequestUpdate" />
        ///     actor state key.
        /// </summary>
        /// <returns>
        ///     An <see cref="ActionResult{bool}" /> containing:
        ///     <see langword="true" /> if updates have been applied; <see langword="false" /> otherwise.
        /// </returns>
        [ActionMethod("Processing pending updates")]
        public async Task<ActionResult<bool>> ProcessPendingUpdatesAsync()
        {
            ConditionalValue<List<UpdateAssetsParameters>> maybeUpdates =
                await StateManager.TryGetStateAsync<List<UpdateAssetsParameters>>(PullRequestUpdate);
            List<UpdateAssetsParameters> updates = maybeUpdates.HasValue ? maybeUpdates.Value : null;
            if (updates == null || updates.Count < 1)
            {
                await Reminders.TryUnregisterReminderAsync(PullRequestUpdate);
                return ActionResult.Create(false, "No Pending Updates");
            }

            (InProgressPullRequest pr, bool canUpdate) = await SynchronizeInProgressPullRequestAsync();

            if (pr != null && !canUpdate)
            {
                return ActionResult.Create(false, "PR cannot be updated.");
            }

            string result;
            if (pr != null)
            {
                await UpdatePullRequestAsync(pr, updates);
                result = $"Pull Request '{pr.Url}' updated.";
            }
            else
            {
                string prUrl = await CreatePullRequestAsync(updates);
                if (prUrl == null)
                {
                    result = "No changes required, no pull request created.";
                }
                else
                {
                    result = $"Pull Request '{prUrl}' created.";
                }
            }

            await StateManager.RemoveStateAsync(PullRequestUpdate);
            await Reminders.TryUnregisterReminderAsync(PullRequestUpdate);

            return ActionResult.Create(true, "Pending updates applied. " + result);
        }

        /// <summary>
        ///     Synchronizes an in progress pull request.
        ///     This will update current state if the pull request has been manually closed or merged.
        ///     This will evaluate merge policies on an in progress pull request and merge the pull request if policies allow.
        /// </summary>
        /// <returns>
        ///     A <see cref="ValueTuple{InProgressPullRequest, bool}" /> containing:
        ///     The current open pull request if one exists, and
        ///     <see langword="true" /> if the open pull request can be updated; <see langword="false" /> otherwise.
        /// </returns>
        public virtual async Task<(InProgressPullRequest pr, bool canUpdate)> SynchronizeInProgressPullRequestAsync()
        {
            ConditionalValue<InProgressPullRequest> maybePr =
                await StateManager.TryGetStateAsync<InProgressPullRequest>(PullRequest);
            if (maybePr.HasValue)
            {
                InProgressPullRequest pr = maybePr.Value;
                if (string.IsNullOrEmpty(pr.Url))
                {
                    // somehow a bad PR got in the collection, remove it
                    await StateManager.RemoveStateAsync(PullRequest);
                    return (null, false);
                }

                bool? result = await ActionRunner.ExecuteAction(() => SynchronizePullRequestAsync(pr.Url));
                if (result == true)
                {
                    return (pr, true);
                }

                if (result == false)
                {
                    return (pr, false);
                }
            }

            await Reminders.TryUnregisterReminderAsync(PullRequestCheck);
            return (null, false);
        }

        /// <summary>
        ///     Synchronizes a pull request
        /// </summary>
        /// <param name="prUrl"></param>
        /// <returns>
        ///     An <see cref="ActionResult{bool?}" /> containing:
        ///     <see langref="null" /> when there is no pull request;
        ///     <see langref="true" /> when there is a PR and it can be updated;
        ///     <see langref="false" /> when there is a PR and it cannot be updated
        /// </returns>
        [ActionMethod("Synchronizing Pull Request: '{url}'")]
        private async Task<ActionResult<bool?>> SynchronizePullRequestAsync(string prUrl)
        {
            ConditionalValue<InProgressPullRequest> maybePr =
                await StateManager.TryGetStateAsync<InProgressPullRequest>(PullRequest);
            if (!maybePr.HasValue || maybePr.Value.Url != prUrl)
            {
                return ActionResult.Create(
                    (bool?) null,
                    $"Not Applicable: Pull Request '{prUrl}' is not tracked by maestro anymore.");
            }

            (string targetRepository, string targetBranch) = await GetTargetAsync();
            IRemote darc = await DarcRemoteFactory.GetRemoteAsync(targetRepository, Logger);

            InProgressPullRequest pr = maybePr.Value;
            PrStatus status = await darc.GetPullRequestStatusAsync(prUrl);
            ActionResult<bool?> checkPolicyResult = null;
            switch (status)
            {
                case PrStatus.Open:
                    checkPolicyResult = await CheckMergePolicyAsync(prUrl, darc);
                    if (checkPolicyResult.Result == true)
                    {
                        goto case PrStatus.Merged;
                    }

                    if (checkPolicyResult.Result == false)
                    {
                        return ActionResult.Create((bool?) true, checkPolicyResult.Message);
                    }

                    return ActionResult.Create((bool?) false, checkPolicyResult.Message);
                case PrStatus.Merged:
                    await UpdateSubscriptionsForMergedPRAsync(pr.ContainedSubscriptions);

                    goto case PrStatus.Closed;
                case PrStatus.Closed:
                    await StateManager.RemoveStateAsync(PullRequest);
                    break;
                default:
                    Logger.LogError("Unknown pr status '{status}'", status);
                    break;
            }

            if (checkPolicyResult != null)
            {
                return ActionResult.Create((bool?) null, checkPolicyResult.Message);
            }

            return ActionResult.Create((bool?) null, $"PR Has been manually {status}");
        }

        private async Task<ActionResult<bool?>> CheckMergePolicyAsync(string prUrl, IRemote darc)
        {
            IReadOnlyList<MergePolicyDefinition> policyDefinitions = await GetMergePolicyDefinitions();
            MergePolicyEvaluationResult result = await MergePolicyEvaluator.EvaluateAsync(
                prUrl,
                darc,
                policyDefinitions);

            if (result.Failed || result.Pending)
            {
                await UpdateStatusCommentAsync(
                    darc,
                    prUrl,
                    $@"## Auto-Merge Status
This pull request has not been merged because Maestro++ is waiting on the following merge policies.

{string.Join("\n", result.Results.OrderBy(r => r.Policy == null ? " " : r.Policy.Name).Select(DisplayPolicy))}");
                return ActionResult.Create(
                    result.Pending ? (bool?) null : false,
                    $"NOT Merged: PR '{prUrl}' failed policies {string.Join(", ", result.Results.Where(r => r.Success == null || r.Success == false).Select(r => r.Policy?.Name + r.Message))}");
            }

            if (result.Succeeded)
            {
                var merged = false;
                try
                {
                    await darc.MergePullRequestAsync(prUrl, new MergePullRequestParameters());
                    merged = true;
                }
                catch
                {
                    // Failure to merge is not exceptional, report on it.
                }

                await UpdateStatusCommentAsync(
                    darc,
                    prUrl,
                    $@"## Auto-Merge Status
This pull request {(merged ? "has been merged" : "will be merged")} because the following merge policies have succeeded.

{string.Join("\n", result.Results.OrderBy(r => r.Policy == null ? " " : r.Policy.Name).Select(DisplayPolicy))}");

                if (merged)
                {
                    return ActionResult.Create(
                        (bool?) true,
                        $"Merged: PR '{prUrl}' passed policies {string.Join(", ", policyDefinitions.Select(p => p.Name))}");
                }

                return ActionResult.Create((bool?) false, $"NOT Merged: PR '{prUrl}' has merge conflicts.");
            }

            return ActionResult.Create((bool?) false, "NOT Merged: There are no merge policies");
        }

        private string DisplayPolicy(MergePolicyEvaluationResult.SingleResult result)
        {
            if (result.Policy == null)
            {
                return $"- ❌ **{result.Message}**";
            }

            if (result.Success == null)
            {
                return $"- ❓ **{result.Message}**";
            }

            if (result.Success == true)
            {
                return $"- ✔️ **{result.Policy.DisplayName}** Succeeded" + (result.Message == null
                           ? ""
                           : $" - {result.Message}");
            }

            return $"- ❌ **{result.Policy.DisplayName}** {result.Message}";
        }

        private Task UpdateStatusCommentAsync(IRemote darc, string prUrl, string message)
        {
            return darc.CreateOrUpdatePullRequestStatusCommentAsync(prUrl, message);
        }

        private async Task UpdateSubscriptionsForMergedPRAsync(
            IEnumerable<SubscriptionPullRequestUpdate> subscriptionPullRequestUpdates)
        {
            foreach (SubscriptionPullRequestUpdate update in subscriptionPullRequestUpdates)
            {
                ISubscriptionActor actor = SubscriptionActorFactory(new ActorId(update.SubscriptionId));
                await actor.UpdateForMergedPullRequestAsync(update.BuildId);
            }
        }

        /// <summary>
        ///     Applies or queues asset updates for the target repository and branch from the given build and list of assets.
        /// </summary>
        /// <param name="subscriptionId">The id of the subscription the update comes from</param>
        /// <param name="buildId">The build that the updated assets came from</param>
        /// <param name="sourceSha">The commit hash that built the assets</param>
        /// <param name="assets">The list of assets</param>
        /// <remarks>
        ///     This function will queue updates if there is a pull request and it is currently not-updateable.
        ///     A pull request is considered "not-updateable" based on merge policies.
        ///     If at least one merge policy calls <see cref="IMergePolicyEvaluationContext.Pending" /> and
        ///     no merge policy calls <see cref="IMergePolicyEvaluationContext.Fail" /> then the pull request is considered
        ///     not-updateable.
        /// </remarks>
        /// <returns></returns>
        [ActionMethod("Updating assets for subscription: {subscriptionId}, build: {buildId}")]
        public async Task<ActionResult<object>> UpdateAssetsAsync(
            Guid subscriptionId,
            int buildId,
            string sourceSha,
            List<Asset> assets)
        {
            (InProgressPullRequest pr, bool canUpdate) = await SynchronizeInProgressPullRequestAsync();

            var updateParameter = new UpdateAssetsParameters
            {
                SubscriptionId = subscriptionId,
                BuildId = buildId,
                SourceSha = sourceSha,
                Assets = assets
            };
            if (pr != null && !canUpdate)
            {
                await StateManager.AddOrUpdateStateAsync(
                    PullRequestUpdate,
                    new List<UpdateAssetsParameters> {updateParameter},
                    (n, old) =>
                    {
                        old.Add(updateParameter);
                        return old;
                    });
                await Reminders.TryRegisterReminderAsync(
                    PullRequestUpdate,
                    Array.Empty<byte>(),
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromMinutes(5));
                return ActionResult.Create<object>(
                    null,
                    $"Current Pull request '{pr.Url}' cannot be updated, update queued.");
            }

            if (pr != null)
            {
                await UpdatePullRequestAsync(pr, new List<UpdateAssetsParameters> {updateParameter});
                return ActionResult.Create<object>(null, $"Pull Request '{pr.Url}' updated.");
            }

            string prUrl = await CreatePullRequestAsync(new List<UpdateAssetsParameters> {updateParameter});
            if (prUrl == null)
            {
                return ActionResult.Create<object>(null, "Updates require no changes, no pull request created.");
            }

            return ActionResult.Create<object>(null, $"Pull request '{prUrl}' created.");
        }

        /// <summary>
        ///     Compute the title for a pull request.
        /// </summary>
        /// <param name="inProgressPr">Current in progress pull request information</param>
        /// <returns>Pull request title</returns>
        private async Task<string> ComputePullRequestTitleAsync(InProgressPullRequest inProgressPr, string targetBranch)
        {
            // Get the unique subscription IDs. It may be possible for a coherency update
            // to not have any contained subscription.  In this case
            // we return a different title.
            var uniqueSubscriptionIds = inProgressPr.ContainedSubscriptions.Select(
                subscription => subscription.SubscriptionId).ToHashSet();

            if (uniqueSubscriptionIds.Count > 0)
            {
                // We'll either list out the repos involved (in a shortened form)
                // or we'll list out the number of repos that are involved.
                // Start building up the list. If we reach a max length, then backtrack and
                // just note the number of input subscriptions.
                string baseTitle = $"[{targetBranch}] Update dependencies from ";
                StringBuilder titleBuilder = new StringBuilder(baseTitle);
                bool prefixComma = false;
                const int maxTitleLength = 80;
                foreach (Guid subscriptionId in uniqueSubscriptionIds)
                {
                    string repoName = await GetSourceRepositoryAsync(subscriptionId);

                    // Strip down repo name.
                    repoName = repoName?.Replace("https://github.com/", "");
                    repoName = repoName?.Replace("https://dev.azure.com/", "");
                    repoName = repoName?.Replace("_git/", "");
                    string repoNameForTitle = prefixComma ? $", {repoName}" : repoName;

                    if (titleBuilder.Length + repoNameForTitle?.Length > maxTitleLength)
                    {
                        return $"{baseTitle} {uniqueSubscriptionIds.Count} repositories";
                    }
                    else
                    {
                        titleBuilder.Append(repoNameForTitle);
                    }
                }

                return titleBuilder.ToString();
            }
            else
            {
                return $"[{targetBranch}] Update dependencies to ensure coherency";
            }
        }

        /// <summary>
        ///     Creates a pull request from the given updates.
        /// </summary>
        /// <param name="updates"></param>
        /// <returns>The pull request url when a pr was created; <see langref="null" /> if no PR is necessary</returns>
        private async Task<string> CreatePullRequestAsync(List<UpdateAssetsParameters> updates)
        {
            (string targetRepository, string targetBranch) = await GetTargetAsync();
            IRemote darcRemote = await DarcRemoteFactory.GetRemoteAsync(targetRepository, Logger);

            List<(UpdateAssetsParameters update, List<DependencyDetail> deps)> requiredUpdates =
                await GetRequiredUpdates(updates, DarcRemoteFactory, targetRepository, targetBranch);

            if (requiredUpdates.Count < 1)
            {
                return null;
            }

            string newBranchName = $"darc-{targetBranch}-{Guid.NewGuid()}";
            await darcRemote.CreateNewBranchAsync(targetRepository, targetBranch, newBranchName);

            try
            {
                using (var description = new StringWriter())
                {
                    description.WriteLine("This pull request updates the following dependencies");
                    description.WriteLine();

                    await CommitUpdatesAsync(requiredUpdates, description, darcRemote, targetRepository, newBranchName);

                    var inProgressPr = new InProgressPullRequest
                    {
                        // Calculate the subscriptions contained within the
                        // update. Coherency updates do not have subscription info.
                        ContainedSubscriptions = requiredUpdates
                                .Where (u => !u.update.IsCoherencyUpdate)
                                .Select(
                                u => new SubscriptionPullRequestUpdate
                                {
                                    SubscriptionId = u.update.SubscriptionId,
                                    BuildId = u.update.BuildId
                                })
                            .ToList()
                    };

                    string prUrl = await darcRemote.CreatePullRequestAsync(
                        targetRepository,
                        new PullRequest
                        {
                            Title = await ComputePullRequestTitleAsync(inProgressPr, targetBranch),
                            Description = description.ToString(),
                            BaseBranch = targetBranch,
                            HeadBranch = newBranchName
                        });

                    if (!string.IsNullOrEmpty(prUrl))
                    {
                        inProgressPr.Url = prUrl;

                        await StateManager.SetStateAsync(PullRequest, inProgressPr);
                        await StateManager.SaveStateAsync();
                        await Reminders.TryRegisterReminderAsync(
                            PullRequestCheck,
                            null,
                            TimeSpan.FromMinutes(5),
                            TimeSpan.FromMinutes(5));
                        return prUrl;
                    }

                    // Something wrong happened when trying to create the PR but didn't throw an exception (probably there was no diff). 
                    // We need to delete the branch also in this case.
                    await darcRemote.DeleteBranchAsync(targetRepository, newBranchName);
                    return null;
                }
            }
            catch
            {
                await darcRemote.DeleteBranchAsync(targetRepository, newBranchName);
                throw;
            }
        }

        /// <summary>
        /// Commit a dependency update to a target branch 
        /// </summary>
        /// <param name="requiredUpdates">Version updates to apply</param>
        /// <param name="description">
        ///     A string writer that the PR description should be written to. If this an update
        ///     to an existing PR, this will contain the existing PR description.
        /// </param>
        /// <param name="darc">Remote darc interface</param>
        /// <param name="targetRepository">Target repository that the updates should be applied to</param>
        /// <param name="newBranchName">Target branch the updates should be to</param>
        /// <returns></returns>
        private async Task CommitUpdatesAsync(
            List<(UpdateAssetsParameters update, List<DependencyDetail> deps)> requiredUpdates,
            StringWriter description,
            IRemote darc,
            string targetRepository,
            string newBranchName)
        {
            // First run through non-coherency and then do a coherency
            // message if one exists.
            List<(UpdateAssetsParameters update, List<DependencyDetail> deps)> nonCoherencyUpdates =
                requiredUpdates.Where(u => !u.update.IsCoherencyUpdate).ToList();
            // Should max one coherency update
            (UpdateAssetsParameters update, List<DependencyDetail> deps) coherencyUpdate =
                requiredUpdates.Where(u => u.update.IsCoherencyUpdate).SingleOrDefault();

            foreach ((UpdateAssetsParameters update, List<DependencyDetail> deps) in nonCoherencyUpdates)
            {
                string sourceRepository = await GetSourceRepositoryAsync(update.SubscriptionId);
                string buildNumber = await GetBuildNumberAsync(update.BuildId);
                using (var message = new StringWriter())
                {
                    message.WriteLine($"Update dependencies from {sourceRepository} build {buildNumber}");
                    message.WriteLine();
                    message.WriteLine("This change updates the following dependencies");
                    description.WriteLine($"Updates from {sourceRepository}");
                    description.WriteLine();
                    foreach (DependencyDetail dep in deps)
                    {
                        message.WriteLine($"- {dep.Name} - {dep.Version}");
                        description.WriteLine($"- {dep.Name} - {dep.Version}");
                    }

                    message.WriteLine();
                    description.WriteLine();

                    await darc.CommitUpdatesAsync(targetRepository, newBranchName, deps, message.ToString());
                }
            }

            if (coherencyUpdate.update != null)
            {
                using (var message = new StringWriter())
                {
                    message.WriteLine("Coherency updates");
                    message.WriteLine();

                    // Sort out the coherency updates into a list for each repo
                    Dictionary<string, List<DependencyDetail>> updatesPerRepo =
                        new Dictionary<string, List<DependencyDetail>>();
                    foreach (DependencyDetail dep in coherencyUpdate.deps)
                    {
                        if (updatesPerRepo.TryGetValue(dep.RepoUri, out List<DependencyDetail> existingList))
                        {
                            existingList.Add(dep);
                        }
                        else
                        {
                            updatesPerRepo.Add(dep.RepoUri, new List<DependencyDetail> { dep });
                        }
                    }
                    foreach (KeyValuePair<string, List<DependencyDetail>> perRepoUpdate in updatesPerRepo)
                    {
                        description.WriteLine($"Updates from {perRepoUpdate.Key}");
                        description.WriteLine();
                        foreach (DependencyDetail dep in perRepoUpdate.Value)
                        {
                            message.WriteLine($"- {dep.Name} - {dep.Version}");
                            description.WriteLine($"- {dep.Name} - {dep.Version}");
                        }
                    }

                    message.WriteLine();
                    description.WriteLine();

                    await darc.CommitUpdatesAsync(targetRepository, newBranchName, coherencyUpdate.deps, message.ToString());
                }
            }
        }

        private async Task UpdatePullRequestAsync(InProgressPullRequest pr, List<UpdateAssetsParameters> updates)
        {
            (string targetRepository, string targetBranch) = await GetTargetAsync();
            IRemote darcRemote = await DarcRemoteFactory.GetRemoteAsync(targetRepository, Logger);

            List<(UpdateAssetsParameters update, List<DependencyDetail> deps)> requiredUpdates =
                await GetRequiredUpdates(updates, DarcRemoteFactory, targetRepository, targetBranch);

            if (requiredUpdates.Count < 1)
            {
                return;
            }

            PullRequest pullRequest = await darcRemote.GetPullRequestAsync(pr.Url);
            string headBranch = pullRequest.HeadBranch;

            // Update the list of contained subscriptions with the new subscription update.
            // Replace all existing updates for the subscription id with the new update.
            // This avoids a potential issue where we may update the last applied build id
            // on the subscription to an older build id.
            foreach ((UpdateAssetsParameters update, List<DependencyDetail> deps) update in requiredUpdates)
            {
                pr.ContainedSubscriptions.RemoveAll(s => s.SubscriptionId == update.update.SubscriptionId);
            }
            pr.ContainedSubscriptions.AddRange(
                requiredUpdates.Select(
                    u => new SubscriptionPullRequestUpdate
                    {
                        SubscriptionId = u.update.SubscriptionId,
                        BuildId = u.update.BuildId
                    }));

            using (var description = new StringWriter(new StringBuilder(pullRequest.Description)))
            {
                await CommitUpdatesAsync(requiredUpdates, description, darcRemote, targetRepository, headBranch);

                pullRequest.Description = description.ToString();
                pullRequest.Title = await ComputePullRequestTitleAsync(pr, targetBranch);
                await darcRemote.UpdatePullRequestAsync(pr.Url, pullRequest);
            }

            await StateManager.SetStateAsync(PullRequest, pr);
            await StateManager.SaveStateAsync();
            await Reminders.TryRegisterReminderAsync(
                PullRequestCheck,
                null,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// Given a set of input updates from builds, determine what updates
        /// are required in the target repository.
        /// </summary>
        /// <param name="updates">Updates</param>
        /// <param name="targetRepository">Target repository to calculate updates for</param>
        /// <param name="branch">Target branch</param>
        /// <param name="remoteFactory">Darc remote factory</param>
        /// <returns>List of updates and dependencies that need updates.</returns>
        /// <remarks>
        ///     This is done in two passes.  The first pass runs through and determines the non-coherency
        ///     updates required based on the input updates.  The second pass uses the repo state + the
        ///     updates from the first pass to determine what else needs to change based on the coherency metadata.
        /// </remarks>
        private async Task<List<(UpdateAssetsParameters update, List<DependencyDetail> deps)>> GetRequiredUpdates(
            List<UpdateAssetsParameters> updates,
            IRemoteFactory remoteFactory,
            string targetRepository,
            string branch)
        {
            // Get a remote factory for the target repo
            IRemote darc = await remoteFactory.GetRemoteAsync(targetRepository, Logger);

            var requiredUpdates = new List<(UpdateAssetsParameters update, List<DependencyDetail> deps)>();
            // Existing details 
            List<DependencyDetail> existingDependencies = (await darc.GetDependenciesAsync(targetRepository, branch)).ToList();

            foreach (UpdateAssetsParameters update in updates)
            {
                IEnumerable<AssetData> assetData = update.Assets.Select(
                    a => new AssetData(false)
                    {
                        Name = a.Name,
                        Version = a.Version
                    });
                List<DependencyUpdate> dependenciesToUpdate = await darc.GetRequiredNonCoherencyUpdatesAsync(
                    targetRepository,
                    update.SourceSha,
                    assetData,
                    existingDependencies);

                if (dependenciesToUpdate.Count < 1)
                {
                    // No dependencies need to be updated.
                    await UpdateSubscriptionsForMergedPRAsync(
                        new List<SubscriptionPullRequestUpdate>
                        {
                            new SubscriptionPullRequestUpdate
                            {
                                SubscriptionId = update.SubscriptionId,
                                BuildId = update.BuildId
                            }
                        });
                    continue;
                }

                List<DependencyDetail> targetOfUpdates = dependenciesToUpdate.Select(u => u.To).ToList();
                // Update the existing details list
                foreach (DependencyUpdate dependencyUpdate in dependenciesToUpdate)
                {
                    existingDependencies.Remove(dependencyUpdate.From);
                    existingDependencies.Add(dependencyUpdate.To);
                }
                requiredUpdates.Add((update, targetOfUpdates));
            }

            // Once we have applied all of non coherent updates, then we need to run a coherency check on the
            // dependencies.
            List<DependencyUpdate> coherencyUpdates =
                await darc.GetRequiredCoherencyUpdatesAsync(existingDependencies, remoteFactory);

            if (coherencyUpdates.Any())
            {
                // For the update asset parameters, we don't have any information on the source of the update,
                // since coherency can be run even without any updates.
                UpdateAssetsParameters coherencyUpdateParameters = new UpdateAssetsParameters
                {
                    IsCoherencyUpdate = true
                };
                requiredUpdates.Add((coherencyUpdateParameters, coherencyUpdates.Select(u => u.To).ToList()));
            }

            return requiredUpdates;
        }

        private async Task<RepositoryBranchUpdate> GetRepositoryBranchUpdate()
        {
            (string repo, string branch) = await GetTargetAsync();
            RepositoryBranchUpdate update = await Context.RepositoryBranchUpdates.FindAsync(repo, branch);
            if (update == null)
            {
                RepositoryBranch repoBranch = await GetRepositoryBranch(repo, branch);
                Context.RepositoryBranchUpdates.Add(
                    update = new RepositoryBranchUpdate {RepositoryBranch = repoBranch});
            }
            else
            {
                Context.RepositoryBranchUpdates.Update(update);
            }

            return update;
        }

        private async Task<RepositoryBranch> GetRepositoryBranch(string repo, string branch)
        {
            RepositoryBranch repoBranch = await Context.RepositoryBranches.FindAsync(repo, branch);
            if (repoBranch == null)
            {
                Context.RepositoryBranches.Add(
                    repoBranch = new RepositoryBranch
                    {
                        RepositoryName = repo,
                        BranchName = branch
                    });
            }
            else
            {
                Context.RepositoryBranches.Update(repoBranch);
            }

            return repoBranch;
        }

        [DataContract]
        public class UpdateAssetsParameters
        {
            [DataMember]
            public Guid SubscriptionId { get; set; }

            [DataMember]
            public int BuildId { get; set; }

            [DataMember]
            public string SourceSha { get; set; }

            [DataMember]
            public List<Asset> Assets { get; set; }

            /// <summary>
            ///     If true, this is a coherency update and not driven by specific
            ///     subscription ids (e.g. could be multiple if driven by a batched subscription)
            /// </summary>
            [DataMember]
            public bool IsCoherencyUpdate { get; set; }
        }
    }

    /// <summary>
    ///     A <see cref="PullRequestActorImplementation" /> that reads its Merge Policies and Target information from a
    ///     non-batched subscription object
    /// </summary>
    public class NonBatchedPullRequestActorImplementation : PullRequestActorImplementation
    {
        private readonly Lazy<Task<Subscription>> _lazySubscription;

        public NonBatchedPullRequestActorImplementation(
            ActorId id,
            IReminderManager reminders,
            IActorStateManager stateManager,
            IMergePolicyEvaluator mergePolicyEvaluator,
            BuildAssetRegistryContext context,
            IRemoteFactory darcFactory,
            ILoggerFactory loggerFactory,
            IActionRunner actionRunner,
            Func<ActorId, ISubscriptionActor> subscriptionActorFactory) : base(
            id,
            reminders,
            stateManager,
            mergePolicyEvaluator,
            context,
            darcFactory,
            loggerFactory,
            actionRunner,
            subscriptionActorFactory)
        {
            _lazySubscription = new Lazy<Task<Subscription>>(RetrieveSubscription);
        }

        public Guid SubscriptionId => Id.GetGuidId();

        private async Task<Subscription> RetrieveSubscription()
        {
            Subscription subscription = await Context.Subscriptions.FindAsync(SubscriptionId);
            if (subscription == null)
            {
                await Reminders.TryUnregisterReminderAsync(PullRequestCheck);
                await Reminders.TryUnregisterReminderAsync(PullRequestUpdate);
                await StateManager.TryRemoveStateAsync(PullRequest);

                throw new SubscriptionException($"Subscription '{SubscriptionId}' was not found...");
            }

            return subscription;
        }

        private Task<Subscription> GetSubscription()
        {
            return _lazySubscription.Value;
        }

        protected override async Task<(string repository, string branch)> GetTargetAsync()
        {
            Subscription subscription = await GetSubscription();
            return (subscription.TargetRepository, subscription.TargetBranch);
        }

        protected override async Task<IReadOnlyList<MergePolicyDefinition>> GetMergePolicyDefinitions()
        {
            Subscription subscription = await GetSubscription();
            return (IReadOnlyList<MergePolicyDefinition>) subscription.PolicyObject.MergePolicies ??
                   Array.Empty<MergePolicyDefinition>();
        }

        public override async Task<(InProgressPullRequest pr, bool canUpdate)> SynchronizeInProgressPullRequestAsync()
        {
            Subscription subscription = await GetSubscription();
            if (subscription == null)
            {
                return (null, false);
            }

            return await base.SynchronizeInProgressPullRequestAsync();
        }
    }

    /// <summary>
    ///     A <see cref="PullRequestActorImplementation" /> for batched subscriptions that reads its Target and Merge Policies
    ///     from the configuration for a repository
    /// </summary>
    public class BatchedPullRequestActorImplementation : PullRequestActorImplementation
    {
        public BatchedPullRequestActorImplementation(
            ActorId id,
            IReminderManager reminders,
            IActorStateManager stateManager,
            IMergePolicyEvaluator mergePolicyEvaluator,
            BuildAssetRegistryContext context,
            IRemoteFactory darcFactory,
            ILoggerFactory loggerFactory,
            IActionRunner actionRunner,
            Func<ActorId, ISubscriptionActor> subscriptionActorFactory) : base(
            id,
            reminders,
            stateManager,
            mergePolicyEvaluator,
            context,
            darcFactory,
            loggerFactory,
            actionRunner,
            subscriptionActorFactory)
        {
        }

        private (string repository, string branch) Target => PullRequestActorId.Parse(Id);

        protected override Task<(string repository, string branch)> GetTargetAsync()
        {
            return Task.FromResult((Target.repository, Target.branch));
        }

        protected override async Task<IReadOnlyList<MergePolicyDefinition>> GetMergePolicyDefinitions()
        {
            RepositoryBranch repositoryBranch =
                await Context.RepositoryBranches.FindAsync(Target.repository, Target.branch);
            return (IReadOnlyList<MergePolicyDefinition>) repositoryBranch.PolicyObject?.MergePolicies ??
                   Array.Empty<MergePolicyDefinition>();
        }
    }
}
