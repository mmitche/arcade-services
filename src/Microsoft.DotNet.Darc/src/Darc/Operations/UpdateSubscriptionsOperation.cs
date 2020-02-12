// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Helpers;
using Microsoft.DotNet.Darc.Models.PopUps;
using Microsoft.DotNet.Darc.Options;
using Microsoft.DotNet.DarcLib;
using Microsoft.DotNet.Maestro.Client;
using Microsoft.DotNet.Maestro.Client.Models;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc.Operations
{
    class UpdateSubscriptionsOperation : Operation
    {
        UpdateSubscriptionsCommandLineOptions _options;

        public UpdateSubscriptionsOperation(UpdateSubscriptionsCommandLineOptions options)
            : base(options)
        {
            _options = options;
        }

        /// <summary>
        /// Implements the 'update-subscriptions' operation
        /// 
        /// The update-subscriptions operation will update an existing set of subscriptions.
        /// Based on an origin set of filters (there must be at least one filter), get the subscriptions
        /// matching those filters. Then determine properties that are the same, and those that are different
        /// between the subscriptions. Properties that are the same stay the same in the popup. Properties that
        /// are different show up tagged appropriately. Changing any field will apply the changes to all subscriptions.
        /// </summary>
        public override async Task<int> ExecuteAsync()
        {
            IRemote remote = RemoteFactory.GetBarOnlyRemote(_options, Logger);

            if (!_options.HasAnyFilters())
            {
                Console.WriteLine("Please specify at least one subscription filter.");
                return Constants.ErrorCode;
            }

            List<Subscription> subscriptions = (await _options.FilterSubscriptions(remote)).ToList();

            if (subscriptions.Count == 0)
            {
                Console.WriteLine("No subscriptions found matching the specified criteria.");
                return Constants.ErrorCode;
            }

            // Pop-up the subscription dialog with the list of subscriptions that can be updated.
            return await UpdateSubscriptionsAsync(subscriptions, remote);
        }

        public async Task<int> UpdateSubscriptionsAsync(List<Subscription> subscriptions, IRemote remote)
        {
            try
            {
                var suggestedRepos = remote.GetSubscriptionsAsync();
                var suggestedChannels = remote.GetChannelsAsync();

                UpdateSubscriptionsPopUp updateSubscriptionPopUp = new UpdateSubscriptionsPopUp(
                    "update-subscription/update-subscription-todo",
                    Logger,
                    subscriptions,
                    (await suggestedChannels).Select(suggestedChannel => suggestedChannel.Name),
                    (await suggestedRepos).SelectMany(subs => new List<string> { subs.SourceRepository, subs.TargetRepository }).ToHashSet(),
                    Constants.AvailableFrequencies,
                    Constants.AvailableMergePolicyYamlHelp);

                UxManager uxManager = new UxManager(_options.GitLocation, Logger);

                int exitCode = uxManager.PopUp(updateSubscriptionPopUp);

                if (exitCode != Constants.SuccessCode)
                {
                    return exitCode;
                }

                // Now interpret the outputs. There are two basic cases:
                // - Target elements (branch and repo) of the subscriptions don't change
                // - Target elements do change.
                // In case where the target elements do change, we actually need to create a new
                // subscription and remove the old one.

                // What we want to do is build up a list of actions for updating the subscriptions to the
                // the new versions

                List<Func<Task<Subscription>>> updates = new List<Func<Task<Subscription>>>();
                foreach (Subscription subscription in subscriptions)
                {
                    string updatedChannelName = updateSubscriptionPopUp.Channel.UseOriginal ? subscription.Channel.Name : updateSubscriptionPopUp.Channel.Value;
                    string updatedSourceRepo = updateSubscriptionPopUp.SourceRepository.UseOriginal ? subscription.SourceRepository : updateSubscriptionPopUp.SourceRepository.Value;
                    string updatedTargetRepo = updateSubscriptionPopUp.TargetRepository.UseOriginal ? subscription.TargetRepository : updateSubscriptionPopUp.TargetRepository.Value;
                    string updatedTargetBranch = updateSubscriptionPopUp.TargetBranch.UseOriginal ? subscription.TargetBranch : updateSubscriptionPopUp.TargetBranch.Value;
                    UpdateFrequency updatedUpdateFrequency = updateSubscriptionPopUp.UpdateFrequency.UseOriginal ? subscription.Policy.UpdateFrequency : Enum.Parse<UpdateFrequency>(updateSubscriptionPopUp.UpdateFrequency.Value);
                    bool updatedBatchable = updateSubscriptionPopUp.Batchable.UseOriginal ? subscription.Policy.Batchable : updateSubscriptionPopUp.Batchable.Value;
                    IImmutableList<MergePolicy> updatedMergePolicies = updateSubscriptionPopUp.MergePolicies.UseOriginal ? subscription.Policy.MergePolicies : updateSubscriptionPopUp.MergePolicies.Value.ToImmutableList();
                    bool updatedEnabled = updateSubscriptionPopUp.Enabled.UseOriginal ? subscription.Enabled : updateSubscriptionPopUp.Enabled.Value;

                    // Compute these so that we can pass them along to the printing helper
                    // and not recompare all over the place.
                    bool channelChanged = !subscription.Channel.Name.Equals(updatedChannelName, StringComparison.OrdinalIgnoreCase);
                    bool sourceRepoChanged = !subscription.SourceRepository.Equals(updatedSourceRepo, StringComparison.OrdinalIgnoreCase);
                    bool targetRepoChanged = !subscription.TargetRepository.Equals(updatedTargetRepo, StringComparison.OrdinalIgnoreCase);
                    bool targetBranchChanged = !subscription.TargetBranch.Equals(updatedTargetBranch, StringComparison.OrdinalIgnoreCase);
                    bool updateFrequencyChanged = subscription.Policy.UpdateFrequency != updatedUpdateFrequency;
                    bool batchableChanged = subscription.Policy.Batchable != updatedBatchable;
                    bool enabledChanged = subscription.Enabled != updatedEnabled;
                    // Use a simple object comparison here.  We won't know whether the user reordered a list of ignored checks or something
                    // and produced a functionally equivalent list, but it will be good enough.
                    // Note that the following willl also change the merge policies:
                    // - batchability - also implies that merge merge policies change, since it means the merge policies will come from elsewhere.
                    // - targetRepo/targetBranch if batchable - Merge policies usually change in this case.
                    bool mergePoliciesChanged = batchableChanged ||
                        (updatedBatchable && (targetRepoChanged || targetBranchChanged)) ||
                        subscription.Policy.MergePolicies != updatedMergePolicies;
                    bool anyUpdate = channelChanged || sourceRepoChanged || targetRepoChanged || targetBranchChanged ||
                        updateFrequencyChanged || batchableChanged || enabledChanged ||
                        subscription.Policy.MergePolicies != updatedMergePolicies;

                    if (!anyUpdate)
                    {
                        // No update necessary.
                        continue;
                    }

                    if (updates.Count == 0)
                    {
                        Console.WriteLine("The following subscriptions will be updated:");
                        Console.WriteLine();
                    }

                    // Print the information about the updated subscription
                    await PrintSubscriptionTransformationHelperAsync(subscription, remote, channelChanged, updatedChannelName, sourceRepoChanged, updatedSourceRepo,
                        targetRepoChanged, updatedTargetRepo, targetBranchChanged, updatedTargetBranch, updateFrequencyChanged, updatedUpdateFrequency,
                        enabledChanged, updatedEnabled, batchableChanged, updatedBatchable, mergePoliciesChanged, updatedMergePolicies);

                    // First, determine whether we will need to create an all new subscription.
                    if (subscription.TargetBranch.Equals(updatedTargetBranch, StringComparison.OrdinalIgnoreCase) ||
                        subscription.TargetRepository.Equals(updatedTargetRepo, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"This subscription will be deleted and replaced with a new version:");

                        // Create a func that will delete the old subscription, create a new one, and potentially run an update
                        // to disable it afterward.
                        updates.Add(new Func<Task<Subscription>>(async () =>
                        {
                            await remote.DeleteSubscriptionAsync(subscription.Id.ToString());

                            Subscription newSubscription = await remote.CreateSubscriptionAsync(updatedChannelName,
                                updatedSourceRepo, updatedTargetRepo, updatedTargetBranch, updatedUpdateFrequency, updatedBatchable, updatedMergePolicies.ToList());

                            if (!updatedEnabled)
                            {
                                newSubscription = await remote.UpdateSubscriptionAsync(newSubscription.Id.ToString(), new SubscriptionUpdate { Enabled = false }); ;
                            }

                            return newSubscription;
                        }));
                    }
                    else
                    {
                        // This ends up just being a simple subscription update.
                        SubscriptionUpdate update = new SubscriptionUpdate()
                        {
                            ChannelName = updatedChannelName,
                            Enabled = updatedEnabled,
                            // Start the policy using the original subscription policy, the update based on other params
                            Policy = subscription.Policy,
                            SourceRepository = updatedSourceRepo
                        };
                        update.Policy.Batchable = updatedBatchable;
                        update.Policy.UpdateFrequency = updatedUpdateFrequency;
                        update.Policy.MergePolicies = updatedMergePolicies;

                        // Create a func that runs the subscription update
                        updates.Add(new Func<Task<Subscription>>(async () =>
                        {
                            return await remote.UpdateSubscriptionAsync(subscription.Id.ToString(), update);
                        }));
                    }
                }

                // Confirmation and then apply the updates. Note the updated subscription ids.
                Console.WriteLine("Applying updates...");

                return Constants.SuccessCode;
            }
            catch (RestApiException e) when (e.Response.Status == (int)System.Net.HttpStatusCode.BadRequest)
            {
                // Could have been some kind of validation error (e.g. channel doesn't exist)
                Logger.LogError($"Failed to update subscription: {e.Response.Content}");
                return Constants.ErrorCode;
            }
            catch (Exception e)
            {
                Logger.LogError(e, $"Failed to update subscription.");
                return Constants.ErrorCode;
            }
        }

        private async Task PrintSubscriptionTransformationHelperAsync(Subscription before,
            IRemote remote,
            bool channelChanged, string updatedChannelName,
            bool sourceRepoChanged, string updatedSourceRepo,
            bool targetRepoChanged, string updatedTargetRepo,
            bool targetBranchChanged, string updatedTargetBranch,
            bool updateFrequencyChanged, UpdateFrequency updatedUpdateFrequency,
            bool enabledChanged, bool updatedEnabled,
            bool batchableChanged, bool updatedBatchable,
            bool mergePoliciesChanged, IEnumerable<MergePolicy> updatedMergePolicies)
        {
            // If the short info line for the subscription has changed, then print it in two lines,
            // with the changed bits below the first line. Otherwise, print normally.

            // Compute the maximum width of each field.
            // The intention is to print something like:
            // https://github.com/dotnet/standard (.NET Core 3 Dev)     ==> 'https://github.com/dotnet/core-setup' ('release/3.0')       
            //                                    to                                                               to
            //                                    (.NET Core 3 Release) ==>                                        ('release/3.0-preview9')

            int sourceRepoFieldWidth = Math.Max(before.SourceRepository.Length, updatedSourceRepo.Length);
            int channelFieldWidth = Math.Max(before.Channel.Name.Length, updatedChannelName.Length);
            int targetRepoFieldWidth = Math.Max(before.TargetRepository.Length, updatedTargetRepo.Length);
            int targetBranchFieldWidth = Math.Max(before.TargetBranch.Length, updatedTargetBranch.Length);

            Console.WriteLine($"{before.SourceRepository.PadRight(sourceRepoFieldWidth, ' ')} ({before.Channel.Name.PadRight(channelFieldWidth, ' ')}) ==> " +
                $"{before.TargetRepository.PadRight(targetRepoFieldWidth, ' ')} ({before.TargetBranch.PadRight(targetBranchFieldWidth, ' ')})");

            if (sourceRepoChanged || channelChanged || targetRepoChanged || targetBranchChanged)
            {
                // Now write out the "to" line
                const string differDesignator = "to";
                string sourceRepoToField = (sourceRepoChanged ? differDesignator.PadRight(sourceRepoFieldWidth, ' ') : new string(' ', sourceRepoFieldWidth));
                string channelToField = (channelChanged ? differDesignator.PadRight(channelFieldWidth, ' ') : new string(' ', channelFieldWidth));
                string targetRepoToField = (targetRepoChanged ? differDesignator.PadRight(targetRepoFieldWidth, ' ') : new string(' ', targetRepoFieldWidth));
                string targetBranchToField = (targetBranchChanged ? differDesignator.PadRight(targetBranchFieldWidth, ' ') : new string(' ', targetBranchFieldWidth));

                Console.WriteLine($"{sourceRepoToField} ({channelToField}) ==> {targetRepoToField} ({targetBranchToField})");

                // Now write out any differing fields.  Reuse the "to" field in cases where there is no diff.
                Console.WriteLine($"{(sourceRepoChanged ? updatedSourceRepo : sourceRepoToField)} ({(channelChanged ? updatedChannelName : channelToField)}) ==> " +
                    $"{(targetRepoChanged ? updatedTargetRepo : targetRepoToField)} ({(targetBranchChanged ? updatedTargetBranch : targetBranchToField)})");
            }

            // For the rest of this, we want to print something like this:
            //   - Id: ab3e7877-80c8-433e-94d8-08d690bc143a -> <New subscription will be generated>
            //   - Update Frequency: EveryBuild                  
            //   - Enabled: True -> False
            //   - Batchable: False
            //   - Merge Policies: -> - Merge Policies:
            //     Standard        ->   AllChecksSuccessful
            //                          ignoreChecks =
            //                                 [
            //                                   "WIP",
            //                                   "license/cla"
            //                                 ]
            //                          NoExtraCommits
            // All this is pretty simple except for the merge policies. After obtaining the original merge policy description,
            // we should get the description string, break it down by line, find the maximal width of the policy strings, then append any diffs, line by line
            // onto those strings.  Then print the whole deal if there is any diff

            // Now print out the remaining subscription details, with before -> after notation when things change.
            string updatedIdInfo = (targetRepoChanged || targetBranchChanged) ? " -> <New subscription will be generated>" : string.Empty;
            Console.WriteLine($"  - Id: {before.Id}{updatedIdInfo}");

            string updatedFrequencyInfo = updateFrequencyChanged ? $" -> {updatedUpdateFrequency.ToString()}" : string.Empty;
            Console.WriteLine($"  - Update Frequency: {before.Policy.UpdateFrequency}{updatedFrequencyInfo}");

            string updatedEnabledInfo = enabledChanged ? $" -> {updatedEnabled.ToString()}" : string.Empty;
            Console.WriteLine($"  - Enabled: {before.Enabled}{updatedEnabledInfo}");
            
            string updatedBatchableInfo = batchableChanged ? $" -> {updatedBatchable.ToString()}" : string.Empty;
            Console.WriteLine($"  - Batchable: {before.Policy.Batchable}{updatedBatchableInfo}");

            // Now for merge policies
            IEnumerable<MergePolicy> beforeMergePolicies = before.Policy.MergePolicies;
            bool beforeMergePoliciesFromRepo = before.Policy.Batchable;
            if (beforeMergePoliciesFromRepo == true)
            {
                beforeMergePolicies = await remote.GetRepositoryMergePoliciesAsync(before.TargetRepository, before.TargetBranch);
            }

            string beforeMergePoliciesDescription = UxHelpers.GetMergePoliciesDescription(beforeMergePolicies, beforeMergePoliciesFromRepo, "  ");
            string afterMergePoliciesDescription = string.Empty;

            // After merge policies, if anything has changed.
            if (mergePoliciesChanged)
            {
                IEnumerable<MergePolicy> afterMergePolicies = updatedMergePolicies;
                if (updatedBatchable == true)
                {
                    afterMergePolicies = await remote.GetRepositoryMergePoliciesAsync(updatedTargetRepo, updatedTargetBranch);
                }

                afterMergePoliciesDescription = UxHelpers.GetMergePoliciesDescription(afterMergePolicies, updatedBatchable);

                // Break down the before into lines, grab the maximum width, and then pad each line by that plus
                // an arrow and print the second set of lines.
                string[] beforeMergePoliciesLines = beforeMergePoliciesDescription.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
                string[] afterMergePoliciesLines = afterMergePoliciesDescription.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

                int maxWidth = beforeMergePoliciesLines.Max(line => line.Length);

                for (int i = 0; i < Math.Max(beforeMergePoliciesLines.Length, afterMergePoliciesLines.Length); i++)
                {
                    // There will always be a merge policies line of some sort
                    if (i < beforeMergePoliciesLines.Length && i < afterMergePoliciesLines.Length)
                    {
                        Console.WriteLine($"{beforeMergePoliciesLines[i].PadRight(maxWidth, ' ')} -> {afterMergePoliciesLines[i]}");
                    }
                    else if (i < beforeMergePoliciesLines.Length)
                    {
                        // Missing the after, so print without an arrow or padding
                        Console.WriteLine(beforeMergePoliciesLines[i]);
                    }
                    else
                    {
                        // Missing the before, so print the padding and then the after
                        Console.WriteLine($"{new string(' ', maxWidth)} -> {afterMergePoliciesLines[i]}");
                    }
                }
            }
            else
            {
                // Avoid the line breakdown and all that.
                Console.WriteLine(beforeMergePoliciesDescription);
            }

            Console.WriteLine();
        }
    }
}
