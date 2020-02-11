// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Dotnet.GitHub.Authentication
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddGitHubTokenProvider(this IServiceCollection services)
        {
            return services
                .AddSingleton<GitHubAppTokenProvider, GitHubAppTokenProvider>()
                .AddSingleton<IGitHubTokenProvider, GitHubTokenProvider>();
        }
    }
}
