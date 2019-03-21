// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.DarcLib
{
    public class DependencyDetail
    {
        private string _coherentParentDependencyName;
        private string _commonChildDependencyName;

        public DependencyDetail() { }
        public DependencyDetail(DependencyDetail other)
        {
            Name = other.Name;
            Version = other.Version;
            RepoUri = other.RepoUri;
            Commit = other.Commit;
            Pinned = other.Pinned;
            Type = other.Type;
            CoherentParentDependencyName = other.CoherentParentDependencyName;
            CommonChildDependencyName = other.CommonChildDependencyName;
        }

        public string Name { get; set; }

        /// <summary>
        ///     Version of dependency.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        ///     Source repository uri that the dependency was produced from.
        /// </summary>
        public string RepoUri { get; set; }

        /// <summary>
        ///     Source commit that the dependency was produced from.
        /// </summary>
        public string Commit { get; set; }

        /// <summary>
        ///     True if the dependency should not be updated, false otherwise.
        /// </summary>
        public bool Pinned { get; set; }

        /// <summary>
        ///     Type of dependency (e.g. Product or Toolset).
        /// </summary>
        public DependencyType Type { get; set; }

        /// <summary>
        ///     Another dependency for which this dependency must be coherent with.
        ///     This means:
        ///     If I have 3 repositories which have a potentially incoherent dependency structure:
        ///     A
        ///     |\
        ///     B |
        ///     \ |
        ///      C
        ///     A different version of C could appear in A and B.
        ///     This may not be a problem, or it could be undesirable.
        ///     This can be resolved to be always coherent by identifying that A's dependency on C
        ///     must be coherent with parent B. Specifically, this means that the build that produced B must
        ///     also have an input build that produced C.
        ///     
        ///     Concretely for .NET Core, core-setup has a dependency on Microsoft.Private.CoreFx.NETCoreApp produced
        ///     in corefx, and Microsoft.NETCore.Runtime.CoreCLR produced in coreclr.  corefx has a dependency on
        ///     Microsoft.NETCore.Runtime.CoreCLR. This means that when updating Microsoft.Private.CoreFx.NETCoreApp
        ///     in core-setup, also update Microsoft.NETCore.Runtime.CoreCLR to the version used to produce that
        ///     Microsoft.Private.CoreFx.NETCoreApp. By corrolary, that means Microsoft.NETCore.Runtime.CoreCLR cannot
        ///     be updated unless that version exists in the subtree of Microsoft.Private.CoreFx.NETCoreApp.
        ///     
        ///     Coherent parent dependencies are specified in Version.Details.xml as follows:
        ///     <![CDATA[
        ///         <Dependency Name="Microsoft.NETCore.App" Version="1.0.0-beta.19151.1" >
        ///             <Uri>https://github.com/dotnet/core-setup</Uri>
        ///             <Sha>abcd</Sha>
        ///         </Dependency>
        ///         <Dependency Name="Microsoft.Private.CoreFx.NETCoreApp" Version="1.2.3" CoherentParentDependency="Microsoft.NETCore.App">
        ///             <Uri>https://github.com/dotnet/corefx</Uri>
        ///             <Sha>defg</Sha>
        ///         </Dependency>
        ///      ]]>
        /// </summary>
        /// 
        public string CoherentParentDependencyName
        {
            get => _coherentParentDependencyName;
            set
            {
                if (!string.IsNullOrEmpty(_commonChildDependencyName))
                {
                    throw new DarcException("Common child and coherent parent restrictions cannot be combined.");
                }
                _coherentParentDependencyName = value;
            }
        }

        /// <summary>
        ///     All dependencies with this common child dependency name will be updated
        ///     to versions that have the same common child dependency version.  For example:
        ///     
        ///        repo A
        ///            / \
        ///           /   \
        ///      dep B     C dep
        ///          \    /
        ///           \  /
        ///        dep  D
        ///     
        ///     In this case, in repo A has dependencies on B and C, and those have dependencies on D.
        ///     A lists B and C has having "CommonChild" D.
        ///     
        ///     When updating B and C, darc will search backwards for versions of B and C that have the same common
        ///     dependency version D and update to those new versions.
        ///     
        ///     If A contains an edge to D:
        ///     
        ///        repo A
        ///            /|\
        ///           / | \
        ///      dep B  |  C dep
        ///          \  | /
        ///           \ |/
        ///        dep  D
        ///     
        ///     Then dependency D referenced in A is also set to the same version referenced in both B and C
        ///     and is not pulled forward to latest.
        ///     
        ///     If B or C are updated (new build produced) without an update to D, then the subscription updating
        ///     B or C will operate as normal.
        /// </summary>
        public string CommonChildDependencyName
        {
            get => _commonChildDependencyName;
            set
            {
                if (!string.IsNullOrEmpty(_coherentParentDependencyName))
                {
                    throw new DarcException("Common child and coherent parent restrictions cannot be combined.");
                }
                _commonChildDependencyName = value;
            }
        }
    }
}

