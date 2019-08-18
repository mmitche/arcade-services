// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Models;
using Microsoft.DotNet.Darc.Models.PopUps;
using Microsoft.DotNet.Maestro.Client.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace Microsoft.DotNet.Darc.Tests
{
    public class UpdateSubscriptionPopUpTests
    {
        /// <summary>
        ///     A single subscription sent to the update method
        ///     should have all the subscription information in the original form.
        /// </summary>
        [Fact]
        public void ExpectedUnchangedContentsTest1()
        {
            Guid newGuid = Guid.NewGuid();
            Channel sourceChannel = new Channel(1, "Test channel", "random", ImmutableList<ReleasePipeline>.Empty);
            SubscriptionPolicy policy = new SubscriptionPolicy(false, UpdateFrequency.EveryBuild);
            ImmutableList<MergePolicy> mergePolicies = ImmutableList.Create(
                new MergePolicy { Name = "Standard", Properties = null }
            );
            policy.MergePolicies = mergePolicies;

            Subscription subscription = new Subscription(newGuid, true,
                "https://github.com/maestro-auth-test/maestro-test1",
                "https://github.com/maestro-auth-test/maestro-test2", "master")
            {
                Channel = sourceChannel,
                Policy = policy
            };

            UpdateSubscriptionsPopUp popUp = new UpdateSubscriptionsPopUp(null, NullLogger.Instance, new List<Subscription> { subscription },
                ImmutableList<string>.Empty, ImmutableList<string>.Empty, Constants.AvailableFrequencies,
                Constants.AvailableMergePolicyYamlHelp);

            // Check the non-comment lines against the expected lines.
            List<Line> actualLines = popUp.Contents.Where(line => !line.IsComment && !string.IsNullOrEmpty(line.Text)).ToList();

            List<string> expectedLines = new List<string>
            {
                $"Channel: {sourceChannel.Name}",
                "Source Repository URL: https://github.com/maestro-auth-test/maestro-test1",
                "Target Repository URL: https://github.com/maestro-auth-test/maestro-test2",
                "Target Branch: master",
                "Update Frequency: EveryBuild",
                "Batchable: False",
                "Enabled: True",
                "Merge Policies:",
                "- Name: Standard",
                "  Properties: {}",
            };

            CompareLines(expectedLines, actualLines);

            // Make no changes, process the results, and we should get the same subscription
            // info out on the other side.

            popUp.ProcessContents(popUp.Contents);

            Assert.False(popUp.Enabled.UseOriginal);
            Assert.Equal(subscription.Enabled, popUp.Enabled.Value);

            Assert.False(popUp.Channel.UseOriginal);
            Assert.Equal(subscription.Channel.Name, popUp.Channel.Value);

            Assert.False(popUp.Batchable.UseOriginal);
            Assert.Equal(subscription.Policy.Batchable, popUp.Batchable.Value);

            Assert.False(popUp.SourceRepository.UseOriginal);
            Assert.Equal(subscription.SourceRepository, popUp.SourceRepository.Value);

            Assert.False(popUp.TargetRepository.UseOriginal);
            Assert.Equal(subscription.TargetRepository, popUp.TargetRepository.Value);

            Assert.False(popUp.TargetBranch.UseOriginal);
            Assert.Equal(subscription.TargetBranch, popUp.TargetBranch.Value);

            Assert.False(popUp.MergePolicies.UseOriginal);
            Assert.Equal(subscription.Policy.MergePolicies.Count, popUp.MergePolicies.Value.Count);
            Assert.Equal(subscription.Policy.MergePolicies[0].Name, popUp.MergePolicies.Value[0].Name);
            Assert.Equal(ImmutableDictionary<string, JToken>.Empty, popUp.MergePolicies.Value[0].Properties);
        }

        [Fact]
        public void ExpectedUnchangedContentsTest2()
        {
            Channel sourceChannel = new Channel(1, "Test channel", "random", ImmutableList<ReleasePipeline>.Empty);
            SubscriptionPolicy policy = new SubscriptionPolicy(false, UpdateFrequency.EveryBuild);
            ImmutableList<MergePolicy> mergePolicies = ImmutableList.Create(
                new MergePolicy { Name = "Standard", Properties = null }
            );
            policy.MergePolicies = mergePolicies;

            Subscription subscription1 = new Subscription(Guid.NewGuid(), true,
                "https://github.com/maestro-auth-test/maestro-test1",
                "https://github.com/maestro-auth-test/maestro-test2", "master")
            {
                Channel = sourceChannel,
                Policy = policy
            };

            // Second subscription will go to another repository
            Subscription subscription2 = new Subscription(Guid.NewGuid(), true,
                "https://github.com/maestro-auth-test/maestro-test1",
                "https://github.com/maestro-auth-test/maestro-test3", "master")
            {
                Channel = sourceChannel,
                Policy = policy
            };

            UpdateSubscriptionsPopUp popUp = new UpdateSubscriptionsPopUp(null, NullLogger.Instance, new List<Subscription> { subscription1, subscription2 },
                ImmutableList<string>.Empty, ImmutableList<string>.Empty, Constants.AvailableFrequencies,
                Constants.AvailableMergePolicyYamlHelp);

            // Check the non-comment lines against the expected lines.
            List<Line> actualLines = popUp.Contents.Where(line => !line.IsComment && !string.IsNullOrEmpty(line.Text)).ToList();

            List<string> expectedLines = new List<string>
            {
                $"Channel: {sourceChannel.Name}",
                "Source Repository URL: https://github.com/maestro-auth-test/maestro-test1",
                "Target Repository URL: <various values>",
                "Target Branch: master",
                "Update Frequency: EveryBuild",
                "Batchable: False",
                "Enabled: True",
                "Merge Policies:",
                "- Name: <various values>",
                "  Properties:",
                "  Properties: <various values>",
            };

            CompareLines(expectedLines, actualLines);

            // Make no changes, process the results, and we should get the same subscription
            // info out on the other side.

            popUp.ProcessContents(popUp.Contents);

            Assert.False(popUp.Enabled.UseOriginal);
            Assert.Equal(subscription1.Enabled, popUp.Enabled.Value);

            Assert.False(popUp.Channel.UseOriginal);
            Assert.Equal(subscription1.Channel.Name, popUp.Channel.Value);

            Assert.False(popUp.Batchable.UseOriginal);
            Assert.Equal(subscription1.Policy.Batchable, popUp.Batchable.Value);

            Assert.False(popUp.SourceRepository.UseOriginal);
            Assert.Equal(subscription1.SourceRepository, popUp.SourceRepository.Value);

            // Because contents weren't changed, supposed to use original value of the target repositories
            Assert.True(popUp.TargetRepository.UseOriginal);

            Assert.False(popUp.TargetBranch.UseOriginal);
            Assert.Equal(subscription1.TargetBranch, popUp.TargetBranch.Value);

            Assert.False(popUp.MergePolicies.UseOriginal);
            Assert.Equal(subscription1.Policy.MergePolicies.Count, popUp.MergePolicies.Value.Count);
            Assert.Equal(subscription1.Policy.MergePolicies[0].Name, popUp.MergePolicies.Value[0].Name);
            Assert.Equal(ImmutableDictionary<string, JToken>.Empty, popUp.MergePolicies.Value[0].Properties);
        }

        private void CompareLines(List<string> expected, List<Line> actual)
        {
            Assert.Equal(expected.Count, actual.Count);
            for (int i = 0; i< expected.Count; i++)
            {
                Assert.Equal(expected[i], actual[i].Text);
            }
        }
    }
}
