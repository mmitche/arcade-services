// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Maestro.Client.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DarcLib
{
    /// <summary>
    ///     This graph build
    /// </summary>
    public class DependencyFlowGraph
    {
        public static async Task<DependencyFlowGraph> BuildAsync(
            IRemoteFactory remoteFactory,
            ILogger logger)
        {
            var remote = await remoteFactory.GetBarOnlyRemoteAsync(logger);
            // Get all default channels and all subscriptions to begin the graph build
            // process
            List<DefaultChannel> defaultChannels = (await remote.GetDefaultChannelsAsync()).ToList();
            List<Subscription> subscriptions = (await remote.GetSubscriptionsAsync()).ToList();

            // Create a visitation stack and push all subscritions onto it.
            // We will visit all subscriptions, which represent edges in the flow graph
            Stack<Subscription> toVisit = new Stack<Subscription>(subscriptions);
            // Create a BV of visited subscriptions
            HashSet<string> visitedSubscriptions = new HashSet<string>();

            // Dictionary of nodes. Key is the repo+branch
            Dictionary<string, DependencyFlowNode> nodes = new Dictionary<string, DependencyFlowNode>(
                StringComparer.OrdinalIgnoreCase);

            while (toVisit.Any())
            {
                Subscription currentSubscription = toVisit.Pop();

                // Get a node for the target of this subscription
                var flowNode = GetOrCreateNode(currentSubscription.TargetRepository, currentSubscription.TargetBranch, nodes);

                
            }
        }

        private static string NormalizeBranch(string branch)
        {
            // Normalize branch names. Branch names may have "refs/heads" prepended.
            // Remove if they do.
            const string refsHeadsPrefix = "refs/heads/";
            string normalizedBranch = branch;
            if (normalizedBranch.StartsWith("refs/heads/"))
            {
                normalizedBranch = normalizedBranch.Substring(refsHeadsPrefix.Length);
            }

            return normalizedBranch;
        }



        private static DependencyFlowNode GetOrCreateNode(
            string repo,
            string branch,
            Dictionary<string, DependencyFlowNode> nodes)
        {
            string normalizedBranch = NormalizeBranch(branch);
            string key = $"{repo}@{normalizedBranch}";
            if (nodes.TryGetValue(key, out DependencyFlowNode existingNode))
            {
                return existingNode;
            }
            else
            {
                DependencyFlowNode newNode = new DependencyFlowNode(repo, normalizedBranch);
                nodes.Add(key, newNode);
                return newNode;
            }
        }
    }
}
