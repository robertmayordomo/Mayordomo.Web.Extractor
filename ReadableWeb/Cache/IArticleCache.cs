using ReadableWeb.Abstractions.Models;

namespace ReadableWeb.Cache;

public interface IArticleCache
{
    bool TryGet(string url, out ArticleContent? article);
    void Set(string url, ArticleContent article, TimeSpan? ttl = null);
}