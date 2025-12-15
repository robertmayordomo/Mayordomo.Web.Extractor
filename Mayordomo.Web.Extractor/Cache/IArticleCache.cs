using Mayordomo.Web.Extractor.Abstractions.Models;

namespace Mayordomo.Web.Extractor.Cache;

public interface IArticleCache
{
    bool TryGet(string url, out ArticleContent? article);
    void Set(string url, ArticleContent article, TimeSpan? ttl = null);
}