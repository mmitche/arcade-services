// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Microsoft.DotNet.Darc.Models.PopUps
{
    /// <summary>
    /// Helper class for YAML encoding/decoding purposes.
    /// This is used so that we can have friendly alias names for elements.
    /// </summary>
    class SubscriptionData
    {
        public const string channelElement = "Channel";
        public const string sourceRepoElement = "Source Repository URL";
        public const string targetRepoElement = "Target Repository URL";
        public const string targetBranchElement = "Target Branch";
        public const string updateFrequencyElement = "Update Frequency";
        public const string mergePolicyElement = "Merge Policies";
        public const string batchableElement = "Batchable";
        public const string enabledElement = "Enabled";

        [YamlMember(Alias = channelElement, ApplyNamingConventions = false)]
        public string Channel { get; set; }

        [YamlMember(Alias = sourceRepoElement, ApplyNamingConventions = false)]
        public string SourceRepository { get; set; }

        [YamlMember(Alias = targetRepoElement, ApplyNamingConventions = false)]
        public string TargetRepository { get; set; }

        [YamlMember(Alias = targetBranchElement, ApplyNamingConventions = false)]
        public string TargetBranch { get; set; }

        [YamlMember(Alias = updateFrequencyElement, ApplyNamingConventions = false)]
        public string UpdateFrequency { get; set; }

        [YamlMember(Alias = batchableElement, ApplyNamingConventions = false)]
        public string Batchable { get; set; }

        [YamlMember(Alias = enabledElement, ApplyNamingConventions = false)]
        public string Enabled { get; set; }

        [YamlMember(Alias = mergePolicyElement, ApplyNamingConventions = false)]
        public List<MergePolicyData> MergePolicies { get; set; }
    }
}
