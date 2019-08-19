// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Models;
using Microsoft.DotNet.Darc.Models.PopUps;
using Microsoft.DotNet.Maestro.Client.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.WorkItemTracking.Process.WebApi.Models;
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
                "- Name: Standard",
                "  Properties: {}",
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

        /// <summary>
        ///     Make a third subscription and differ two of the subscriptions by all except the target repository.
        /// </summary>
        [Fact]
        public void ExpectedUnchangedContentsTest3()
        {
            Channel sourceChannel1 = new Channel(1, "Test channel1", "random", ImmutableList<ReleasePipeline>.Empty);
            Channel sourceChannel2 = new Channel(1, "Test channel2", "random", ImmutableList<ReleasePipeline>.Empty);
            
            SubscriptionPolicy policy1 = new SubscriptionPolicy(false, UpdateFrequency.EveryBuild);
            ImmutableList<MergePolicy> mergePolicies1 = ImmutableList.Create(
                new MergePolicy { Name = "Standard", Properties = null }
            );
            policy1.MergePolicies = mergePolicies1;

            Dictionary<string, JToken> allChecksSuccessfulPolicyProperties = new Dictionary<string, JToken>
            { { "ignoreChecks", JToken.FromObject(new List<string> { "WIP", "license/foo" }) } };

            SubscriptionPolicy policy2 = new SubscriptionPolicy(true, UpdateFrequency.EveryWeek);
            policy2.MergePolicies = ImmutableList<MergePolicy>.Empty;

            Subscription subscription1 = new Subscription(Guid.NewGuid(), true,
                "https://github.com/maestro-auth-test/maestro-test1",
                "https://github.com/maestro-auth-test/maestro-test2", "master")
            {
                Channel = sourceChannel1,
                Policy = policy1
            };

            Subscription subscription2 = new Subscription(Guid.NewGuid(), true,
                "https://github.com/maestro-auth-test/maestro-test3",
                "https://github.com/maestro-auth-test/maestro-test2", "release/3.0")
            {
                Channel = sourceChannel2,
                Policy = policy2
            };

            Subscription subscription3 = new Subscription(Guid.NewGuid(), true,
                "https://github.com/maestro-auth-test/maestro-test2",
                "https://github.com/maestro-auth-test/maestro-test2", "release/3.0")
            {
                Channel = sourceChannel2,
                Policy = policy2
            };

            UpdateSubscriptionsPopUp popUp = new UpdateSubscriptionsPopUp(null, NullLogger.Instance, new List<Subscription> { subscription1, subscription2, subscription3 },
                ImmutableList<string>.Empty, ImmutableList<string>.Empty, Constants.AvailableFrequencies,
                Constants.AvailableMergePolicyYamlHelp);

            // Check the non-comment lines against the expected lines.
            List<Line> actualLines = popUp.Contents.Where(line => !line.IsComment && !string.IsNullOrEmpty(line.Text)).ToList();

            List<string> expectedLines = new List<string>
            {
                $"Channel: {EditorPopUp.VariousValuesString}",
                $"Source Repository URL: {EditorPopUp.VariousValuesString}",
                "Target Repository URL: https://github.com/maestro-auth-test/maestro-test2",
                $"Target Branch: {EditorPopUp.VariousValuesString}",
                $"Update Frequency: {EditorPopUp.VariousValuesString}",
                $"Batchable: {EditorPopUp.VariousValuesString}",
                "Enabled: True",
                $"Merge Policies:",
                $"- Name: {EditorPopUp.VariousValuesString}",
                $"  Properties:",
                $"    {EditorPopUp.VariousValuesString}: {EditorPopUp.VariousValuesString}",
            };

            CompareLines(expectedLines, actualLines);

            // Make no changes, process the results, and we should get the same subscription
            // info out on the other side.

            popUp.ProcessContents(popUp.Contents);

            Assert.False(popUp.Enabled.UseOriginal);
            Assert.Equal(subscription1.Enabled, popUp.Enabled.Value);

            Assert.True(popUp.Channel.UseOriginal);

            Assert.True(popUp.Batchable.UseOriginal);

            Assert.True(popUp.SourceRepository.UseOriginal);

            // Because contents weren't changed, supposed to use original value of the target repositories
            Assert.False(popUp.TargetRepository.UseOriginal);
            Assert.Equal(subscription1.TargetRepository, popUp.TargetRepository.Value);

            Assert.True(popUp.TargetBranch.UseOriginal);

            Assert.True(popUp.MergePolicies.UseOriginal);
        }

        /// <summary>
        ///     A single subscription sent to the update method,
        ///     updated with new values.
        /// </summary>
        [Fact]
        public void ExpectedChangedContentsTest1()
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
                "Channel: Test channel",
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

            // Change the contents
            List<Line> updatedLines = new List<Line>
            {
                new Line(),
                new Line("Channel: Another channel"),
                new Line("Source Repository URL: https://github.com/maestro-auth-test/maestro-test3"),
                new Line("Target Repository URL: https://github.com/maestro-auth-test/maestro-test4"),
                new Line("Target Branch: release/3.0"),
                new Line("Update Frequency: EveryWeek"),
                new Line("Batchable: True"),
                new Line("Enabled: False"),
                new Line("Merge Policies:"),
                new Line(),
            };

            // Make no changes, process the results, and we should get the same subscription
            // info out on the other side.

            popUp.ProcessContents(updatedLines);

            Assert.False(popUp.Enabled.UseOriginal);
            Assert.False(popUp.Enabled.Value);

            Assert.False(popUp.Channel.UseOriginal);
            Assert.Equal("Another channel", popUp.Channel.Value);

            Assert.False(popUp.Batchable.UseOriginal);
            Assert.True(popUp.Batchable.Value);

            Assert.False(popUp.SourceRepository.UseOriginal);
            Assert.Equal("https://github.com/maestro-auth-test/maestro-test3", popUp.SourceRepository.Value);

            Assert.False(popUp.TargetRepository.UseOriginal);
            Assert.Equal("https://github.com/maestro-auth-test/maestro-test4", popUp.TargetRepository.Value);

            Assert.False(popUp.TargetBranch.UseOriginal);
            Assert.Equal("release/3.0", popUp.TargetBranch.Value);

            Assert.False(popUp.MergePolicies.UseOriginal);
            Assert.Null(popUp.MergePolicies.Value);
        }

        /// <summary>
        ///     Update 3 subscriptions to have new values for the target branch and target repo
        /// </summary>
        [Fact]
        public void ExpectedChangedContentsTest2()
        {
            Channel sourceChannel1 = new Channel(1, "Test channel1", "random", ImmutableList<ReleasePipeline>.Empty);
            Channel sourceChannel2 = new Channel(1, "Test channel2", "random", ImmutableList<ReleasePipeline>.Empty);

            SubscriptionPolicy policy1 = new SubscriptionPolicy(false, UpdateFrequency.EveryBuild);
            ImmutableList<MergePolicy> mergePolicies1 = ImmutableList.Create(
                new MergePolicy { Name = "Standard", Properties = null }
            );
            policy1.MergePolicies = mergePolicies1;

            Dictionary<string, JToken> allChecksSuccessfulPolicyProperties = new Dictionary<string, JToken>
            { { "ignoreChecks", JToken.FromObject(new List<string> { "WIP", "license/foo" }) } };

            SubscriptionPolicy policy2 = new SubscriptionPolicy(true, UpdateFrequency.EveryWeek);
            policy2.MergePolicies = ImmutableList<MergePolicy>.Empty;

            Subscription subscription1 = new Subscription(Guid.NewGuid(), true,
                "https://github.com/maestro-auth-test/maestro-test1",
                "https://github.com/maestro-auth-test/maestro-test2", "master")
            {
                Channel = sourceChannel1,
                Policy = policy1
            };

            Subscription subscription2 = new Subscription(Guid.NewGuid(), true,
                "https://github.com/maestro-auth-test/maestro-test3",
                "https://github.com/maestro-auth-test/maestro-test2", "release/3.0")
            {
                Channel = sourceChannel2,
                Policy = policy2
            };

            Subscription subscription3 = new Subscription(Guid.NewGuid(), true,
                "https://github.com/maestro-auth-test/maestro-test2",
                "https://github.com/maestro-auth-test/maestro-test2", "release/3.0")
            {
                Channel = sourceChannel2,
                Policy = policy2
            };

            UpdateSubscriptionsPopUp popUp = new UpdateSubscriptionsPopUp(null, NullLogger.Instance, new List<Subscription> { subscription1, subscription2, subscription3 },
                ImmutableList<string>.Empty, ImmutableList<string>.Empty, Constants.AvailableFrequencies,
                Constants.AvailableMergePolicyYamlHelp);

            // Check the non-comment lines against the expected lines.
            List<Line> actualLines = popUp.Contents.Where(line => !line.IsComment && !string.IsNullOrEmpty(line.Text)).ToList();

            List<string> expectedLines = new List<string>
            {
                $"Channel: {EditorPopUp.VariousValuesString}",
                $"Source Repository URL: {EditorPopUp.VariousValuesString}",
                "Target Repository URL: https://github.com/maestro-auth-test/maestro-test2",
                $"Target Branch: {EditorPopUp.VariousValuesString}",
                $"Update Frequency: {EditorPopUp.VariousValuesString}",
                $"Batchable: {EditorPopUp.VariousValuesString}",
                "Enabled: True",
                $"Merge Policies:",
                $"- Name: {EditorPopUp.VariousValuesString}",
                $"  Properties:",
                $"    {EditorPopUp.VariousValuesString}: {EditorPopUp.VariousValuesString}",
            };

            CompareLines(expectedLines, actualLines);

            // Change the contents
            List<Line> updatedLines = new List<Line>
            {
                new Line(),
                new Line($"Channel: {EditorPopUp.VariousValuesString}"),
                new Line($"Source Repository URL: {EditorPopUp.VariousValuesString}"),
                new Line("Target Repository URL: https://github.com/maestro-auth-test/maestro-test10"),
                new Line("Target Branch: release/3.0"),
                new Line($"Update Frequency: {EditorPopUp.VariousValuesString}"),
                new Line($"Batchable: {EditorPopUp.VariousValuesString}"),
                new Line($"Enabled: False"),
                new Line($"Merge Policies:"),
                // Remove the properties bit of this, should not cause issues and should still have the original value
                new Line($"- Name: {EditorPopUp.VariousValuesString}"),
                new Line(),
            };

            // Make no changes, process the results, and we should get the same subscription
            // info out on the other side.

            popUp.ProcessContents(updatedLines);

            Assert.False(popUp.Enabled.UseOriginal);
            Assert.False(popUp.Enabled.Value);

            Assert.True(popUp.Channel.UseOriginal);

            Assert.True(popUp.Batchable.UseOriginal);

            Assert.True(popUp.SourceRepository.UseOriginal);

            Assert.False(popUp.TargetRepository.UseOriginal);
            Assert.Equal("https://github.com/maestro-auth-test/maestro-test10", popUp.TargetRepository.Value);

            Assert.False(popUp.TargetBranch.UseOriginal);
            Assert.Equal("release/3.0", popUp.TargetBranch.Value);

            Assert.True(popUp.MergePolicies.UseOriginal);
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
