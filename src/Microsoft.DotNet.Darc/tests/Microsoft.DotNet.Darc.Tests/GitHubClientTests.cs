// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.DarcLib;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.DotNet.Darc.Tests
{
    public class SimpleCacheEntry : ICacheEntry
    {
        private object _key;
        private object _value;

        public SimpleCacheEntry(object key)
        {
            _key = key;
        }

        public object Key => _key;

        public object Value { get => _value; set => _value = value; }
        public DateTimeOffset? AbsoluteExpiration { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public TimeSpan? AbsoluteExpirationRelativeToNow { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public TimeSpan? SlidingExpiration { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public IList<IChangeToken> ExpirationTokens => throw new NotImplementedException();

        public IList<PostEvictionCallbackRegistration> PostEvictionCallbacks => throw new NotImplementedException();

        public CacheItemPriority Priority { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public long? Size { get => 0; set => Size = 0; }

        public void Dispose() { }
    }

    public class SimpleCache : IMemoryCache
    {
        public int CacheHits { get; set; }
        public int CacheMisses { get; set; }
        public ConcurrentDictionary<object, ICacheEntry> cache = new ConcurrentDictionary<object, ICacheEntry>();

        public ICacheEntry CreateEntry(object key)
        {
            var newEntry = new SimpleCacheEntry(key);
            return cache.AddOrUpdate(key, new SimpleCacheEntry(key), (existingKey, existingValue) => newEntry);
        }

        public void Dispose()
        {
            
        }

        public void Remove(object key)
        {
            cache.Remove(key, out ICacheEntry unused);
        }

        public bool TryGetValue(object key, out object value)
        {
            if (cache.TryGetValue(key, out ICacheEntry existingEntry))
            {
                CacheHits++;
                value = existingEntry.Value;
                return true;
            }
            else
            {
                CacheMisses++;
                value = null;
                return false;
            }
        }
    }

    public class GitHubClientTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        private async void TreeItemCacheTest(bool enableCache)
        {
            SimpleCache cache = enableCache ? new SimpleCache() : null;
            Mock<GitHubClient> client = new Mock<GitHubClient>(null, NullLogger.Instance, null, cache);

            List<(string, string, Octokit.TreeItem)> treeItemsToGet = new List<(string, string, Octokit.TreeItem)>
            {
                ("a", "b", new Octokit.TreeItem("path", "mode", Octokit.TreeType.Blob, 10, "1", "https://url")),
                ("a", "b", new Octokit.TreeItem("path", "mode", Octokit.TreeType.Blob, 10, "2", "https://url")),
                ("a", "b", new Octokit.TreeItem("path", "mode", Octokit.TreeType.Blob, 10, "3", "https://url")),
                ("a", "b", new Octokit.TreeItem("path", "mode", Octokit.TreeType.Blob, 10, "4", "https://url")),
                ("dotnet", "corefx", new Octokit.TreeItem("path", "mode", Octokit.TreeType.Blob, 10, "11", "https://url")),
                ("dotnet", "corefx", new Octokit.TreeItem("path", "mode", Octokit.TreeType.Blob, 10, "12", "https://url")),
            };

            // Mock up the github client
            var octoKitClientMock = new Mock<Octokit.IGitHubClient>();
            var octoKitGitMock = new Mock<Octokit.IGitDatabaseClient>();
            var octoKitBlobClientMock = new Mock<Octokit.IBlobsClient>();
            Octokit.Blob blob = new Octokit.Blob("foo", "content", Octokit.EncodingType.Utf8, "somesha", 10);

            foreach (var treeItem in treeItemsToGet)
            {
                octoKitBlobClientMock.Setup(m => m.Get(treeItem.Item1, treeItem.Item2, treeItem.Item3.Sha)).ReturnsAsync(blob);
            }

            octoKitGitMock.Setup(m => m.Blob).Returns(octoKitBlobClientMock.Object);
            octoKitClientMock.Setup(m => m.Git).Returns(octoKitGitMock.Object);
            client.Setup(m => m.Client).Returns(octoKitClientMock.Object);

            // Request all but the last tree item in the list, then request the full set, then again.
            // For the cache scenario, we should have no cache hits on first pass, n-1 on the second, and N on the last
            // For the no-cache scenario, we simply not crash.

            for (int i = 0; i < treeItemsToGet.Count - 1; i++)
            {
                await client.Object.GetGitTreeItem("path", treeItemsToGet[i].Item3, treeItemsToGet[i].Item1, treeItemsToGet[i].Item2);
            }

            if (enableCache)
            {
                Assert.Equal(treeItemsToGet.Count - 1, cache.CacheMisses);
                Assert.Equal(0, cache.CacheHits);
            }

            // Request full set
            for (int i = 0; i < treeItemsToGet.Count; i++)
            {
                await client.Object.GetGitTreeItem("path", treeItemsToGet[i].Item3, treeItemsToGet[i].Item1, treeItemsToGet[i].Item2);
            }

            if (enableCache)
            {
                Assert.Equal(treeItemsToGet.Count, cache.CacheMisses);
                Assert.Equal(treeItemsToGet.Count - 1, cache.CacheHits);
            }

            // Request full set
            for (int i = 0; i < treeItemsToGet.Count; i++)
            {
                await client.Object.GetGitTreeItem("path", treeItemsToGet[i].Item3, treeItemsToGet[i].Item1, treeItemsToGet[i].Item2);
            }

            if (enableCache)
            {
                Assert.Equal(treeItemsToGet.Count, cache.CacheMisses);
                Assert.Equal(treeItemsToGet.Count - 1 + treeItemsToGet.Count, cache.CacheHits);
            }
        }
    }
}
