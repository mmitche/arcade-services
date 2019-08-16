// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommandLine;
using Microsoft.DotNet.Darc.Operations;
using System.Collections.Generic;

namespace Microsoft.DotNet.Darc.Options
{
    [Verb("update-subscriptions", HelpText = "Update one or more existing subscriptions in the editor.")]
    class UpdateSubscriptionsCommandLineOptions : SubscriptionsCommandLineOptions
    {
        [Option("read-stdin", HelpText = "Interactive mode style (YAML), but read input from stdin.")]
        public bool ReadStandardIn { get; set; }

        [Option("quiet", HelpText = "Do not confirm subscription updates. Update immediately.")]
        public bool Quiet { get; set; }

        public override Operation GetOperation()
        {
            return new UpdateSubscriptionsOperation(this);
        }
    }
}
