// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Helpers;
using Microsoft.DotNet.Darc.Models.PopUps;
using Microsoft.DotNet.Darc.Options;
using Microsoft.DotNet.DarcLib;
using Microsoft.DotNet.Maestro.Client;
using Microsoft.DotNet.Maestro.Client.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc.Operations
{
    class UpdateSubscriptionOperation : Operation
    {
        UpdateSubscriptionCommandLineOptions _options;

        public UpdateSubscriptionOperation(UpdateSubscriptionCommandLineOptions options)
            : base(options)
        {
            _options = options;
        }

        /// <summary>
        /// Implements the 'update-subscription' operation
        /// </summary>
        /// <param name="options"></param>
        public override Task<int> ExecuteAsync()
        {
            // Deprecate.  Tell the user they should use update-subscriptions instead
            Console.WriteLine("update-subscription has been removed. Please use the 'update-subscriptions' command instead");
            return Task.FromResult(Constants.ErrorCode);
        }
    }
}
