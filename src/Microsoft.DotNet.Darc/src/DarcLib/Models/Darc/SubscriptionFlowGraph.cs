// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.DotNet.Maestro.Client.Models;

namespace Microsoft.DotNet.DarcLib.Models.Darc
{
    /// <summary>
    ///     This graph build
    /// </summary>
    public class SubscriptionFlowGraph
    {
        public static async System.Threading.Tasks.Task<SubscriptionFlowGraph> BuildAsync(
            IRemoteFactory remoteFactory,
            ILogger logger)
        {
            var remote = await remoteFactory.GetBarOnlyRemoteAsync(logger);
            // Get all default channels and all subscriptions to begin the graph build
            // process
            List<DefaultChannel> defaultChannels = (await remote.GetDefaultChannelsAsync()).ToList();
            List<Subscription> subscriptions = (await remote.GetSubscriptionsAsync()).ToList();

            // Create a visitation stack and push all subscritions onto it.
            // We will visit all nodes.
            Stack<Subscription> toVisit = new Stack<Subscription>(subscriptions);
            // Create a BV of visited subscriptions
            HashSet<string> visitedSubscriptions = new HashSet<string>();

            while (toVisit.Any())
            {
                var currentSubscription = toVisit.Pop();
            }
        }
    }
}
