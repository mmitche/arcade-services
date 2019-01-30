// <auto-generated>
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// </auto-generated>

namespace Microsoft.DotNet.Maestro.Client.Models
{
    using Newtonsoft.Json;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    public partial class Build
    {
        /// <summary>
        /// Initializes a new instance of the Build class.
        /// </summary>
        public Build()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the Build class.
        /// </summary>
        public Build(int? id = default(int?), string repository = default(string), string branch = default(string), string commit = default(string), string buildNumber = default(string), System.DateTimeOffset? dateProduced = default(System.DateTimeOffset?), IList<Channel> channels = default(IList<Channel>), IList<Asset> assets = default(IList<Asset>), IList<BuildRef> dependencies = default(IList<BuildRef>))
        {
            Id = id;
            Repository = repository;
            Branch = branch;
            Commit = commit;
            BuildNumber = buildNumber;
            DateProduced = dateProduced;
            Channels = channels;
            Assets = assets;
            Dependencies = dependencies;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "id")]
        public int? Id { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "repository")]
        public string Repository { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "branch")]
        public string Branch { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "commit")]
        public string Commit { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "buildNumber")]
        public string BuildNumber { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "dateProduced")]
        public System.DateTimeOffset? DateProduced { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "channels")]
        public IList<Channel> Channels { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "assets")]
        public IList<Asset> Assets { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "dependencies")]
        public IList<BuildRef> Dependencies { get; set; }

    }
}
