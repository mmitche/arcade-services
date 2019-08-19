// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Maestro.Client.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using YamlDotNet.Serialization;

namespace Microsoft.DotNet.Darc.Models.PopUps
{
    /// <summary>
    ///     Class representing a single subscriptions setting.
    ///     
    ///     This class would ideally be eliminated, and we could use nullable types
    ///     across the board, but that is a .NET Core 3/C# feature that isn't available here.
    /// </summary>
    /// <typeparam name="T">Type of setting</typeparam>
    public class SubscriptionSetting<T>
    {
        public bool UseOriginal { get; set; }
        public T Value { get; set; }

        public static SubscriptionSetting<T> OriginalValue = new SubscriptionSetting<T> { UseOriginal = true };
        public static SubscriptionSetting<T> NewValue(T setting)
        {
            return new SubscriptionSetting<T>
            {
                UseOriginal = false,
                Value = setting
            };
        }
    }

    public class UpdateSubscriptionsPopUp : EditorPopUp
    {
        private readonly ILogger _logger;

        private SubscriptionData _yamlData;

        public SubscriptionSetting<string> Channel { get; private set; }

        public SubscriptionSetting<string> SourceRepository { get; private set; }

        public SubscriptionSetting<string> TargetRepository { get; private set; }

        public SubscriptionSetting<string> TargetBranch { get; private set; }

        public SubscriptionSetting<bool> Batchable { get; private set; }

        public SubscriptionSetting<string> UpdateFrequency { get; private set; }

        public SubscriptionSetting<bool> Enabled { get; private set; }

        public SubscriptionSetting<List<MergePolicy>> MergePolicies { get; private set; }

        private string GetCommonSettingForDisplay(string currentValue, string nextValue)
        {
            return string.IsNullOrEmpty(currentValue) || currentValue == nextValue ? nextValue : VariousValuesString;
        }

        private static List<MergePolicyData> VariousValuesMergePolicyData = new List<MergePolicyData>
        {
            new MergePolicyData
            {
                Name = VariousValuesString,
                Properties = new Dictionary<string, object> { { VariousValuesString, VariousValuesString } }
            }
        };

        /// <summary>
        ///     Process the initial subscription set and set up the YAML data.
        ///     The yaml data is presented to the user as the actual value if all values are shared,
        ///     or a 'various values' tag if there are differences. The intended behavior is that if a user does not
        ///     update the various values tag with a new value, the original values will remain.
        ///     
        ///     Merge policy data is hard. Determine common lists of names is quite simple, but the dictionary of
        ///     merge policy properties involves specializing the comparison for each object type (mostly lists). To avoid a ton
        ///     of complexity around this area, we always leave the merge data as 'various values' unless there is only a single subscription,
        ///     in which case, we use the data itself
        /// </summary>
        /// <param name="subscriptions">Subscription set.</param>
        private SubscriptionData ProcessInitialSubscriptionSet(IEnumerable<Subscription> subscriptions)
        {
            SubscriptionData yamlData = new SubscriptionData();
            foreach (Subscription subscription in subscriptions)
            {
                yamlData.Channel = GetCommonSettingForDisplay(yamlData.Channel, subscription.Channel.Name);
                yamlData.SourceRepository = GetCommonSettingForDisplay(yamlData.SourceRepository, subscription.SourceRepository);
                yamlData.TargetBranch = GetCommonSettingForDisplay(yamlData.TargetBranch, subscription.TargetBranch);
                yamlData.TargetRepository = GetCommonSettingForDisplay(yamlData.TargetRepository, subscription.TargetRepository);
                yamlData.Batchable = GetCommonSettingForDisplay(yamlData.Batchable, subscription.Policy.Batchable.ToString());
                yamlData.UpdateFrequency = GetCommonSettingForDisplay(yamlData.UpdateFrequency, subscription.Policy.UpdateFrequency.ToString());
                yamlData.Enabled = GetCommonSettingForDisplay(yamlData.Enabled, subscription.Enabled.ToString());
                yamlData.MergePolicies = GetCommonMergePolicyLists(yamlData.MergePolicies, MergePoliciesPopUpHelpers.ConvertMergePolicies(subscription.Policy.MergePolicies));
            }

            return yamlData;
        }

        private List<MergePolicyData> GetCommonMergePolicyLists(List<MergePolicyData> currentValue, List<MergePolicyData> nextValue)
        {
            if (currentValue == null)
            {
                return nextValue;
            }
            else if (MergePoliciesPopUpHelpers.MergePolicyListsAreEqual(currentValue, nextValue))
            {
                return currentValue;
            }
            else
            {
                return VariousValuesMergePolicyData;
            }
        }

        public UpdateSubscriptionsPopUp(string path,
                                    ILogger logger,
                                    IEnumerable<Subscription> subscriptions,
                                    IEnumerable<string> suggestedChannels,
                                    IEnumerable<string> suggestedRepositories,
                                    IEnumerable<string> availableUpdateFrequencies,
                                    IEnumerable<string> availableMergePolicyHelp)
            : base(path)
        {
            _logger = logger;

            // Walk the subscriptions and determine 

            _yamlData = ProcessInitialSubscriptionSet(subscriptions);

            ISerializer serializer = new SerializerBuilder().Build();

            string yaml = serializer.Serialize(_yamlData);

            string[] lines = yaml.Split(Environment.NewLine);

            // Initialize line contents.  Augment the input lines with suggestions and explanation
            Contents = new Collection<Line>(new List<Line>
            {
                new Line($"Use this form to update the values of the following subscriptions:", true),
            });

            foreach (var subscription in subscriptions)
            {
                Contents.Add(new Line($"  {UxHelpers.GetSubscriptionDescription(subscription)}", true));
            }

            Contents.Add(new Line($"Note that if you are setting 'Is batchable' to true you need to remove all Merge Policies.", true));
            Contents.Add(new Line());

            foreach (string line in lines)
            {
                Contents.Add(new Line(line));
            }

            // Add helper comments
            Contents.Add(new Line($"Suggested repository URLs for '{SubscriptionData.sourceRepoElement}':", true));

            foreach (string suggestedRepo in suggestedRepositories)
            {
                Contents.Add(new Line($"  {suggestedRepo}", true));
            }

            Contents.Add(new Line("", true));
            Contents.Add(new Line("Suggested Channels", true));

            foreach (string suggestedChannel in suggestedChannels)
            {
                Contents.Add(new Line($"  {suggestedChannel}", true));
            }

            Contents.Add(new Line("", true));
            Contents.Add(new Line("Available Merge Policies", true));

            foreach (string mergeHelp in availableMergePolicyHelp)
            {
                Contents.Add(new Line($"  {mergeHelp}", true));
            }
        }

        public override int ProcessContents(IList<Line> contents)
        {
            SubscriptionData outputYamlData;

            try
            {
                string yamlString = contents.Aggregate<Line, string>("", (current, line) => $"{current}{System.Environment.NewLine}{line.Text}");
                IDeserializer serializer = new DeserializerBuilder().Build();
                outputYamlData = serializer.Deserialize<SubscriptionData>(yamlString);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to parse input yaml.  Please see help for correct format.");
                return Constants.ErrorCode;
            }

            _yamlData.Batchable = ParseSetting(outputYamlData.Batchable, _yamlData.Batchable, false);
            if (_yamlData.Batchable == VariousValuesString)
            {
                Batchable = SubscriptionSetting<bool>.OriginalValue;
            }
            else if (!bool.TryParse(outputYamlData.Batchable, out bool batchable))
            {
                _logger.LogError("Batchable is not a valid boolean value.");
                return Constants.ErrorCode;
            }
            else
            {
                Batchable = SubscriptionSetting<bool>.NewValue(batchable);
            }

            _yamlData.Enabled = ParseSetting(outputYamlData.Enabled, _yamlData.Enabled, false);
            if (_yamlData.Enabled == VariousValuesString)
            {
                Enabled = SubscriptionSetting<bool>.OriginalValue;
            }
            else if (!bool.TryParse(outputYamlData.Enabled, out bool enabled))
            {
                _logger.LogError("Enabled is not a valid boolean value.");
                return Constants.ErrorCode;
            }
            else
            {
                Enabled = SubscriptionSetting<bool>.NewValue(enabled);
            }

            // Parse and check the input fields
            _yamlData.Channel = ParseSetting(outputYamlData.Channel, _yamlData.Channel, false);
            if (_yamlData.Channel == VariousValuesString)
            {
                Channel = SubscriptionSetting<string>.OriginalValue;
            }
            else if (string.IsNullOrEmpty(_yamlData.Channel))
            {
                _logger.LogError("Channel must be non-empty");
                return Constants.ErrorCode;
            }
            else
            {
                Channel = SubscriptionSetting<string>.NewValue(_yamlData.Channel);
            }

            _yamlData.SourceRepository = ParseSetting(outputYamlData.SourceRepository, _yamlData.SourceRepository, false);
            if (_yamlData.SourceRepository == VariousValuesString)
            {
                SourceRepository = SubscriptionSetting<string>.OriginalValue;
            }
            else if (string.IsNullOrEmpty(_yamlData.SourceRepository))
            {
                _logger.LogError("Source repository URL must be non-empty");
                return Constants.ErrorCode;
            }
            else
            {
                SourceRepository = SubscriptionSetting<string>.NewValue(_yamlData.SourceRepository);
            }

            _yamlData.TargetRepository = ParseSetting(outputYamlData.TargetRepository, _yamlData.TargetRepository, false);
            if (_yamlData.TargetRepository == VariousValuesString)
            {
                TargetRepository = SubscriptionSetting<string>.OriginalValue;
            }
            else if (string.IsNullOrEmpty(_yamlData.TargetRepository))
            {
                _logger.LogError("Target repository URL must be non-empty");
                return Constants.ErrorCode;
            }
            else
            {
                TargetRepository = SubscriptionSetting<string>.NewValue(_yamlData.TargetRepository);
            }

            _yamlData.TargetBranch = ParseSetting(outputYamlData.TargetBranch, _yamlData.TargetBranch, false);
            if (_yamlData.TargetBranch == VariousValuesString)
            {
                TargetBranch = SubscriptionSetting<string>.OriginalValue;
            }
            else if (string.IsNullOrEmpty(_yamlData.TargetBranch))
            {
                _logger.LogError("Target branch must be non-empty");
                return Constants.ErrorCode;
            }
            else
            {
                TargetBranch = SubscriptionSetting<string>.NewValue(_yamlData.TargetBranch);
            }

            _yamlData.UpdateFrequency = ParseSetting(outputYamlData.UpdateFrequency, _yamlData.UpdateFrequency, false);
            if (_yamlData.UpdateFrequency == VariousValuesString)
            {
                UpdateFrequency = SubscriptionSetting<string>.OriginalValue;
            }
            else if (string.IsNullOrEmpty(_yamlData.UpdateFrequency) ||
                    !Constants.AvailableFrequencies.Contains(_yamlData.UpdateFrequency, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogError($"Frequency should be provided and should be one of the following: " +
                    $"'{string.Join("', '", Constants.AvailableFrequencies)}'");
                return Constants.ErrorCode;
            }
            else
            {
                UpdateFrequency = SubscriptionSetting<string>.NewValue(_yamlData.TargetBranch);
            }

            // Validate the merge policies
            _yamlData.MergePolicies = outputYamlData.MergePolicies;
            if (outputYamlData.MergePolicies != null &&
                outputYamlData.MergePolicies.Count == 1 &&
                outputYamlData.MergePolicies[0].Name == EditorPopUp.VariousValuesString)
            {
                MergePolicies = SubscriptionSetting<List<MergePolicy>>.OriginalValue;
            }
            else
            {
                List<MergePolicy> convertedMergePolicies = MergePoliciesPopUpHelpers.ConvertMergePolicies(outputYamlData.MergePolicies);
                if (!MergePoliciesPopUpHelpers.ValidateMergePolicies(convertedMergePolicies, _logger))
                {
                    return Constants.ErrorCode;
                }
                MergePolicies = SubscriptionSetting<List<MergePolicy>>.NewValue(convertedMergePolicies);
            }

            return Constants.SuccessCode;
        }
    }
}
