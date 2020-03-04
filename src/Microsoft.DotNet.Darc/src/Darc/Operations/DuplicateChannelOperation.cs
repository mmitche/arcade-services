// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Options;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc.Operations
{
    internal class DuplicateChannelOperation : Operation
    {
        DuplicateChannelCommandLineOptions _options;
        public DuplicateChannelOperation(DuplicateChannelCommandLineOptions options)
            : base(options)
        {
            _options = options;
        }

        /// <summary>
        ///     Assigns a build to a channel.
        /// </summary>
        /// <returns>Process exit code.</returns>
        public override Task<int> ExecuteAsync()
        {
            return Task.FromResult(Constants.SuccessCode);
        }
    }
}
