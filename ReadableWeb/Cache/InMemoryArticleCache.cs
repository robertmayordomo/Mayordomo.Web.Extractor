using System.Collections.Concurrent;
using ReadableWeb.Abstractions.Models;

namespace ReadableWeb.Cache;

public class InMemoryArticleCache : IArticleCache
{
    private class CacheItem
    {
        public ArticleContent Article { get; init; } = null!;
        public DateTimeOffset ExpiresAt { get; init; }
    }

    private readonly ConcurrentDictionary<string, CacheItem> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly TimeSpan _defaultTtl;

    public InMemoryArticleCache(TimeSpan? defaultTtl = null)
    {
        _defaultTtl = defaultTtl ?? TimeSpan.FromMinutes(10);
    }

    public bool TryGet(string url, out ArticleContent? article)
    {
        article = null;
        if (!_cache.TryGetValue(url, out var item))
            return false;

        if (DateTimeOffset.UtcNow > item.ExpiresAt)
        {
            _cache.TryRemove(url, out _);
            return false;
        }

        article = item.Article;
        return true;
    }

    public void Set(string url, ArticleContent article, TimeSpan? ttl = null)
    {
        var expires = DateTimeOffset.UtcNow + (ttl ?? _defaultTtl);
        var item = new CacheItem
        {
            Article = article,
            ExpiresAt = expires
        };

        _cache[url] = item;
    }
}