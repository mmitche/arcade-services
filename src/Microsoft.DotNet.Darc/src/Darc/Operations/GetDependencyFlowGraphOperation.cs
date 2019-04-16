// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Helpers;
using Microsoft.DotNet.Darc.Options;
using Microsoft.DotNet.DarcLib;
using Microsoft.DotNet.DarcLib.Helpers;
using Microsoft.DotNet.Maestro.Client.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc.Operations
{
    internal class GetDependencyFlowGraphOperation : Operation
    {
        private GetDependencyFlowGraphCommandLineOptions _options;

        public GetDependencyFlowGraphOperation(GetDependencyFlowGraphCommandLineOptions options)
            : base(options)
        {
            _options = options;
        }

        public override async Task<int> ExecuteAsync()
        {
            try
            {
                RemoteFactory remoteFactory = new RemoteFactory(_options);
                var barOnlyRemote = await remoteFactory.GetBarOnlyRemoteAsync(Logger);
                Channel arcadeLatestChannel = await barOnlyRemote.GetChannelAsync(".NET Tools - Latest");
                // Add one default for arcade-validation to .NET Tools - Latest
                List<DefaultChannel> additionalDefaults = new List<DefaultChannel>
                {
                    new DefaultChannel(0, "https://github.com/dotnet/arcade")
                    {
                        Branch = "refs/heads/master",
                        Channel = arcadeLatestChannel
                    }
                };

                DependencyFlowGraph flowGraph = await DependencyFlowGraph.BuildAsync(remoteFactory, Logger, additionalDefaults);

                if (!string.IsNullOrEmpty(_options.GraphVizOutputFile))
                {
                    await LogGraphViz(flowGraph);
                }

                return Constants.SuccessCode;
            }
            catch (Exception exc)
            {
                Logger.LogError(exc, "Something failed while getting the dependency graph.");
                return Constants.ErrorCode;
            }
        }

        private void FilterGraph(DependencyFlowGraph graph)
        {
            if (string.IsNullOrEmpty(_options.Channel))
            {
                return;
            }

            HashSet<DependencyFlowNode> unreachableNodes = new HashSet<DependencyFlowNode>(graph.Nodes);
            HashSet<DependencyFlowEdge> unreachableEdges = new HashSet<DependencyFlowEdge>(graph.Edges);
            Stack<DependencyFlowNode> nodes = new Stack<DependencyFlowNode>();

            // Start with initial set of nodes with outputs to the target channel.
            IEnumerable<DependencyFlowNode> nodesOnChannel = graph.Nodes.Where(
                n => n.OutputChannels.Any(c => c.Contains(_options.Channel, StringComparison.OrdinalIgnoreCase)));

            // Walk each root
            foreach (DependencyFlowNode rootNodes in graph.Nodes)
            {
                if (!rootNodes.OutputChannels.Any(c => c.Contains(_options.Channel, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                nodes.Push(rootNodes);

                while (nodes.TryPop(out DependencyFlowNode currentNode))
                {
                    if (!unreachableNodes.Contains(currentNode))
                    {
                        // Nothing to do
                        continue;
                    }
                    unreachableNodes.Remove(currentNode);
                    foreach (var inputEdge in currentNode.IncomingEdges)
                    {
                        if (IsInterestingEdge(inputEdge))
                        {
                            unreachableEdges.Remove(inputEdge);
                        }
                        // Push the inputs onto the stack.
                        nodes.Push(inputEdge.From);
                    }
                }
            }

            // Now walk the graph and eliminate any edges or nodes that
            foreach (var node in unreachableNodes)
            {
                graph.RemoveNode(node);
            }

            foreach (var edge in unreachableEdges)
            {
                graph.RemoveEdge(edge);
            }
        }

        private bool IsInterestingEdge(DependencyFlowEdge edge)
        {
            if (!_options.IncludeDisabledEdges &&
                (!edge.Subscription.Enabled || edge.Subscription.Policy.UpdateFrequency == SubscriptionPolicyUpdateFrequency.None))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        ///     Log the graph in graphviz (dot) format.
        /// </summary>
        /// <param name="graph">Graph to log</param>
        /// <remarks>
        /// Example of a graphviz graph description
        ///  
        /// digraph graphname {
        ///    a -> b -> c;
        ///    b -> d;
        /// }
        ///  
        /// For more info see https://www.graphviz.org/
        /// </remarks>
        /// <returns>Async task</returns>
        private async Task LogGraphViz(DependencyFlowGraph graph)
        {
            FilterGraph(graph);
            using (StreamWriter writer = OutputFormattingHelpers.GetOutputFileStreamOrConsole(_options.GraphVizOutputFile))
            {
                await writer.WriteLineAsync("digraph repositoryGraph {");
                await writer.WriteLineAsync("    node [shape=record]");
                foreach (DependencyFlowNode node in graph.Nodes)
                {
                    // Check channel of sub.
                    if (!string.IsNullOrEmpty(_options.Channel))
                    {
                        if (!node.InputChannels.Any(c => c.Contains(_options.Channel, StringComparison.OrdinalIgnoreCase)) &&
                            !node.OutputChannels.Any(c => c.Contains(_options.Channel, StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }
                    }

                    StringBuilder nodeBuilder = new StringBuilder();

                    // First add the node name
                    nodeBuilder.Append($"    {OutputFormattingHelpers.CalculateGraphVizNodeName(node)}");

                    // Then add the label.  label looks like [label="<info here>"]
                    nodeBuilder.Append("[label=\"");

                    // Append friendly repo name
                    nodeBuilder.Append(OutputFormattingHelpers.GetSimpleRepoName(node.Repository));
                    nodeBuilder.Append(@"\n");

                    // Append branch name
                    nodeBuilder.Append(node.Branch);

                    // Append end of label and end of node.
                    nodeBuilder.Append("\"];");

                    // Write it out.
                    await writer.WriteLineAsync(nodeBuilder.ToString());
                }

                // Now write all the edges
                foreach (DependencyFlowEdge edge in graph.Edges)
                {
                    // Check channel of sub.
                    if (!string.IsNullOrEmpty(_options.Channel) &&
                        !edge.Subscription.Channel.Name.Contains(_options.Channel, StringComparison.OrdinalIgnoreCase) )
                    {
                        continue;
                    }

                    string fromNode = OutputFormattingHelpers.CalculateGraphVizNodeName(edge.From);
                    string toNode = OutputFormattingHelpers.CalculateGraphVizNodeName(edge.To);
                    string label = $"{edge.Subscription.Channel.Name} ({edge.Subscription.Policy.UpdateFrequency})";
                    await writer.WriteLineAsync($"    {fromNode} -> {toNode}");
                }

                await writer.WriteLineAsync("}");
            }
        }
    }
}
