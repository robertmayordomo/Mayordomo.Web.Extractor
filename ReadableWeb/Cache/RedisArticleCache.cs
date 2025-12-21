using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReadableWeb.Abstractions.Models;
using StackExchange.Redis;

namespace ReadableWeb.Cache;

public class RedisArticleCache : IArticleCache
{
    private readonly IDatabase _db;
    private readonly string _keyPrefix;
    private readonly TimeSpan _defaultTtl;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly ILogger<RedisArticleCache>? _logger;

    private record ArticleCacheEntry(
        string Title,
        string TextContent,
        string Excerpt,
        string SiteName,
        string Url,
        string Author,
        DateTime? PublishedTime,
        DateTime? ModifiedTime,
        string? DetectedCultureName
    );

    public RedisArticleCache(
        IConnectionMultiplexer connectionMultiplexer,
        int db = 0,
        string keyPrefix = "article:",
        TimeSpan? defaultTtl = null,
        ILogger<RedisArticleCache>? logger = null)
    {
        if (connectionMultiplexer == null)
            throw new ArgumentNullException(nameof(connectionMultiplexer));

        _db = connectionMultiplexer.GetDatabase(db);
        _keyPrefix = keyPrefix ?? "";
        _defaultTtl = defaultTtl ?? TimeSpan.FromMinutes(30);
        _logger = logger;

        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    private string BuildKey(string url) => _keyPrefix + url;

    public bool TryGet(string url, out ArticleContent? article)
    {
        article = null;
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var key = BuildKey(url);
        RedisValue value = _db.StringGet(key);

        if (value.IsNullOrEmpty)
        {
            _logger?.LogDebug("Redis cache MISS for URL {Url}", url);
            return false;
        }

        try
        {
            var entry = JsonSerializer.Deserialize<ArticleCacheEntry>(value.ToString(), _serializerOptions);
            if (entry == null)
            {
                _logger?.LogWarning("Redis cache entry deserialized to null for URL {Url}", url);
                _db.KeyDelete(key);
                return false;
            }

            CultureInfo? culture = null;
            if (!string.IsNullOrWhiteSpace(entry.DetectedCultureName))
            {
                try
                {
                    culture = new CultureInfo(entry.DetectedCultureName);
                }
                catch
                {
                    _logger?.LogWarning(
                        "Invalid culture name '{Culture}' in Redis cache for URL {Url}",
                        entry.DetectedCultureName, url);
                }
            }

            article = new ArticleContent
            {
                Title = entry.Title,
                TextContent = entry.TextContent,
                Excerpt = entry.Excerpt,
                SiteName = entry.SiteName,
                Url = entry.Url,
                Author = entry.Author,
                PublishedTime = entry.PublishedTime,
                ModifiedTime = entry.ModifiedTime,
                DetectedCulture = culture
            };

            _logger?.LogDebug("Redis cache HIT for URL {Url}", url);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex,
                "Failed to deserialize Redis cache entry for URL {Url}. Deleting key.", url);
            _db.KeyDelete(key);
            return false;
        }
    }

    public void Set(string url, ArticleContent article, TimeSpan? ttl = null)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL must not be empty.", nameof(url));
        if (article == null)
            throw new ArgumentNullException(nameof(article));

        var key = BuildKey(url);

        var entry = new ArticleCacheEntry(
            Title: article.Title ?? "",
            TextContent: article.TextContent ?? "",
            Excerpt: article.Excerpt ?? "",
            SiteName: article.SiteName ?? "",
            Url: article.Url ?? url,
            Author: article.Author ?? "",
            PublishedTime: article.PublishedTime,
            ModifiedTime: article.ModifiedTime,
            DetectedCultureName: article.DetectedCulture?.Name
        );

        var json = JsonSerializer.Serialize(entry, _serializerOptions);
        var expiry = ttl ?? _defaultTtl;

        _db.StringSet(key, json, expiry);
        _logger?.LogDebug("Stored article in Redis cache for URL {Url} with TTL {Ttl}", url, expiry);
    }
}