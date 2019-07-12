// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Helpers;
using Microsoft.DotNet.Darc.Options;
using Microsoft.DotNet.DarcLib;
using Microsoft.DotNet.Maestro.Client.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc.Operations
{
    /// <summary>
    ///     Diff two dependency graphs based on different commits in a repository.
    ///     
    ///     How should this diff actually work? The basic requirement is that it can
    ///     identify changes between trees, filtering away commits that only touch the 
    ///     eng/Version.Details.xml and other associated files.
    ///     
    ///     Given two coherent graphs with a few nodes, this would be pretty simple. Build the
    ///     graph of each commit. Take the flat sha/repo list and match it up between base and diff,
    ///     look at commit diff for each line, and filter away dependency updates. Afterwards we'd be left
    ///     with a set of commits that represent the diff.
    ///     
    ///     In the real world, this is rife with corner cases:
    ///     - Added/Removed nodes between graphs
    ///     - Incoherency - If incoherency appears to disappears deep in the graph, is that interesting?
    ///       Contextually, if core-setup does not updated in core-sdk, then no corefx diff is represented in core-sdk,
    ///       but corefx might change elsewhere in the graph. Does it make sense to note those changes?
    ///     
    ///     So that begs the question as to what the desired behavior actually is in the presense of
    ///     these scenarios.
    ///     - Added/removed nodes - Note the added or removed node, but do not show the full diff
    ///       of the added or removed node.
    ///     - Incoherency - Maybe note which subtrees are becoming coherent or incoherent?
    /// </summary>
    internal class DiffOperation : Operation
    {
        private DiffCommandLineOptions _options;

        public DiffOperation(DiffCommandLineOptions options)
            : base(options)
        {
            _options = options;
        }

        public override async Task<int> ExecuteAsync()
        {
            RemoteFactory remoteFactory = new RemoteFactory(_options);

            
        }
    }
}
