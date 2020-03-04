// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommandLine;
using Microsoft.DotNet.Darc.Operations;

namespace Microsoft.DotNet.Darc.Options
{
    [Verb("duplicate-channel", HelpText = "Duplicate flow from one channel to another. " +
        "Given a new target branch, source and target channels, will duplicate the default channels " +
        "from the source channel to the target channel with the new branches, as well as any subscriptions that source " +
        "from the original channel or target the new default channel branch.")]
    internal class DuplicateChannelCommandLineOptions : CommandLineOptions
    {
        [Option("source-channel", Required = true, HelpText = "Source channel to duplicate flow from.")]
        public string SourceChannel { get; set; }

        [Option("target-channel", Required = true, HelpText = "Source channel to duplicate flow to. Will create this channel if it does not exist.")]
        public string TargetChannel { get; set; }

        [Option("target-branch", Required = true, HelpText = "Branch that should be used as the target ")]
        public string TargetBranch { get; set; }

        public override Operation GetOperation()
        {
            return new DuplicateChannelOperation(this);
        }
    }
}
