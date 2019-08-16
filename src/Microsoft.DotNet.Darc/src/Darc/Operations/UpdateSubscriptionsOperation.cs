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
    class UpdateSubscriptionsOperation : Operation
    {
        UpdateSubscriptionsCommandLineOptions _options;

        public UpdateSubscriptionsOperation(UpdateSubscriptionsCommandLineOptions options)
            : base(options)
        {
            _options = options;
        }

        /// <summary>
        /// Implements the 'update-subscriptions' operation
        /// 
        /// The update-subscriptions operation will update an existing set of subscriptions.
        /// Based on an origin set of filters (there must be at least one filter), get the subscriptions
        /// matching those filters. Then determine properties that are the same, and those that are different
        /// between the subscriptions.  Properties that are the same stay the same in the popup.  Properties that
        /// are different show up tagged appropriately.  Changing any field will apply the changes to all subscriptions
        /// </summary>
        public override async Task<int> ExecuteAsync()
        {
            IRemote remote = RemoteFactory.GetBarOnlyRemote(_options, Logger);

            if (!_options.HasAtLeastOneFilter())
            {
                Console.WriteLine("Please specify at least one subscription filter.");
                return Constants.ErrorCode;
            }

            var subscriptions = (await remote.GetSubscriptionsAsync()).Where(subscription =>
            {
                return _options.SubcriptionFilter(subscription);
            });

            if (subscriptions.Count() == 0)
            {
                Console.WriteLine("No subscriptions found matching the specified criteria.");
                return Constants.ErrorCode;
            }

            // Detemrine 
        }
    }
}
