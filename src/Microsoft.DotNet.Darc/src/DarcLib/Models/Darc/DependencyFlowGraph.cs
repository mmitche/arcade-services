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
        public DependencyFlowGraph(List<DependencyFlowNode> nodes, List<DependencyFlowEdge> edges)
        {
            Nodes = nodes;
            Edges = edges;
        }

        public List<DependencyFlowNode> Nodes { get; set; }
        public List<DependencyFlowEdge> Edges { get; set; }

        public static async Task<DependencyFlowGraph> BuildAsync(
            IRemoteFactory remoteFactory,
            ILogger logger,
            List<DefaultChannel> additionalDefaults = null)
        {
            var remote = await remoteFactory.GetBarOnlyRemoteAsync(logger);
            // Get all default channels and all subscriptions to begin the graph build
            // process
            List<DefaultChannel> defaultChannels = (await remote.GetDefaultChannelsAsync()).ToList();
            List<Subscription> subscriptions = (await remote.GetSubscriptionsAsync()).ToList();

            // If there are more additional defaults, add them in
            if (additionalDefaults != null)
            {
                defaultChannels.AddRange(additionalDefaults);
            }

            // Dictionary of nodes. Key is the repo+branch
            Dictionary<string, DependencyFlowNode> nodes = new Dictionary<string, DependencyFlowNode>(
                StringComparer.OrdinalIgnoreCase);
            List<DependencyFlowEdge> edges = new List<DependencyFlowEdge>();

            // First create all the channel nodes. There may be disconnected
            // nodes in the graph, so we must process all channels and all subscriptions
            foreach (DefaultChannel channel in defaultChannels)
            {
                DependencyFlowNode flowNode = GetOrCreateNode(channel.Repository, channel.Branch, nodes);
                // Add a the output mapping.
                flowNode.OutputChannels.Add(channel.Channel.Name);
            }

            // Process all subscriptions (edges)
            foreach (Subscription subscription in subscriptions)
            {
                // Get the target of the subscription
                DependencyFlowNode destinationNode = GetOrCreateNode(subscription.TargetRepository, subscription.TargetBranch, nodes);
                // Add the input channel for the node
                destinationNode.InputChannels.Add(subscription.Channel.Name);
                // Translate the input channel + repo to a default channel,
                // and if one is found, an input node.
                IEnumerable<DefaultChannel> inputDefaultChannels = defaultChannels.Where(d => d.Channel.Name == subscription.Channel.Name &&
                                                               d.Repository.Equals(subscription.SourceRepository, StringComparison.OrdinalIgnoreCase));
                foreach (DefaultChannel defaultChannel in inputDefaultChannels)
                {
                    DependencyFlowNode sourceNode = GetOrCreateNode(defaultChannel.Repository, defaultChannel.Branch, nodes);

                    DependencyFlowEdge newEdge = new DependencyFlowEdge(sourceNode, destinationNode, subscription);
                    destinationNode.IncomingEdges.Add(newEdge);
                    sourceNode.OutgoingEdges.Add(newEdge);
                    edges.Add(newEdge);
                }
            }

            return new DependencyFlowGraph(nodes.Select(kv => kv.Value).ToList(), edges);
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
