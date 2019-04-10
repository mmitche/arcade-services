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
    public class HealthAlert
    {
        public string Message { get; set; }
        public List<string> SuggestedActions { get; set; }
    }

    internal class GetHealthOperation : Operation
    {
        GetHealthCommandLineOptions _options;
        public GetHealthOperation(GetHealthCommandLineOptions options)
            : base(options)
        {
            _options = options;
        }

        /// <summary>
        /// Deletes a channel by name
        /// </summary>
        /// <returns></returns>
        public override async Task<int> ExecuteAsync()
        {
            IRemote barOnlyRemote = RemoteFactory.GetBarOnlyRemote(_options, Logger);
            var matchingChannels = (await barOnlyRemote.GetChannelsAsync()).Where(c => c.Name.Contains(_options.Channel));
            if (matchingChannels.Count() > 1)
            {
                Console.WriteLine($"Found more than one channel matching '{_options.Channel}', please specify more completely:");
                foreach (Channel channel in matchingChannels)
                {
                    Console.WriteLine($"  {channel.Name}");
                }
                return Constants.ErrorCode;
            }
            else if (!matchingChannels.Any())
            {
                Console.WriteLine($"Found no channels matching '{_options.Channel}'");
                return Constants.ErrorCode;
            }

            Channel matchingChannel = matchingChannels.Single();

            await GetOverallHealth(matchingChannel, _options.Channel);
        }

        public async Task GetOverallHealth(Channel channel, string repo)
        {
            Console.WriteLine("Overall Health Alerts");
            var convergenceHealth = await GetConvergenceHealth();
        }

        /// <summary>
        ///     Determine whether convergence is possible for the repo in the given channel.
        /// </summary>
        /// <param name="channel">Channel</param>
        /// <param name="repo">Repository uri</param>
        /// <returns>List of health alerts.  List is empty if no alerts.</returns>
        public async Task<List<HealthAlert>> GetConvergenceHealth(Channel channel, string repo)
        {
            IRemote barOnlyRemote = RemoteFactory.GetBarOnlyRemote(_options, Logger);
            // Retrive all subscriptions.
            var subscriptions = barOnlyRemote.GetSubscriptionsAsync();
            // Retrieve all default channels
            var defaultChannels = barOnlyRemote.GetDefaultChannelsAsync();
        }
    }
}
