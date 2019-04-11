// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.DarcLib
{
    public class DependencyFlowNode
    {
        public DependencyFlowNode(string repository, string branch)
        {
            Repository = repository;
            Branch = branch;
            OutputChannels = new List<string>();
            InputChannels = new List<string>();
        }

        public readonly string Repository;
        public readonly string Branch;

        public List<string> OutputChannels { get; set; }
        public List<string> InputChannels { get; set; }

        public List<DependencyFlowEdge> PushesTo { get; set; }
        public List<DependencyFlowEdge> PullsFrom { get; set; }
    }
}
