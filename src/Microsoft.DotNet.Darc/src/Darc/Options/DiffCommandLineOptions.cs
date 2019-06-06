// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommandLine;
using Microsoft.DotNet.Darc.Operations;

namespace Microsoft.DotNet.Darc.Options
{
    [Verb("diff", HelpText = "Diff two dependency graphs to determine the real changes between the two.")]
    internal class DiffCommandLineOptions : CommandLineOptions
    {
        [Option("repo", HelpText = "Repository to diff the graph for.")]
        public string RepoUri { get; set; }

        [Option("base", HelpText = "Base commit.")]
        public string BaseCommit { get; set; }

        [Option("compare", HelpText = "Commit to compare against base.")]
        public string CompareCommit { get; set; }

        public override Operation GetOperation()
        {
            return new DiffOperation(this);
        }
    }
}
