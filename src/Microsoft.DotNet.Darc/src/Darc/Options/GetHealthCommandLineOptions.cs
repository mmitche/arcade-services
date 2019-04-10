// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommandLine;
using Microsoft.DotNet.Darc.Operations;

namespace Microsoft.DotNet.Darc.Options
{
    [Verb("get-health", HelpText = "Get health of a repository in a channel")]
    internal class GetHealthCommandLineOptions : CommandLineOptions
    {
        [Option("repo", Required = true, HelpText = "Repository URI")]
        public string Repository { get; set; }

        [Option("channel", Required = true, HelpText = "Channel")]
        public string Channel { get; set; }

        public override Operation GetOperation()
        {
            return new GetHealthOperation(this);
        }
    }
}
