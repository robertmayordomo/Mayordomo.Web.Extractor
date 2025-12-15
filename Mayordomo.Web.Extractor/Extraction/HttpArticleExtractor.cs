using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Mayordomo.Web.Extractor.Abstractions.Models;
using Mayordomo.Web.Extractor.Cache;
using Mayordomo.Web.Extractor.Configuration;
using Microsoft.Extensions.Logging;
using Validated.Primitives.Validation;
using Validated.Primitives.ValueObjects;

namespace Mayordomo.Web.Extractor.Extraction;

public interface IHttpArticleExtractor
{
    Task<ArticleContent> ExtractFromUrlAsync(
        string url,
        CultureInfo? overrideCulture = null,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);
}

public class HttpArticleExtractor : IHttpArticleExtractor
{
    private readonly HttpClient _httpClient;
    private readonly ReadabilityExtractor _extractor;
    private readonly IArticleCache? _cache;
    private readonly ILogger<HttpArticleExtractor>? _logger;
    private readonly ArticleExtractionOptions _options;

    public HttpArticleExtractor(
        HttpClient httpClient,
        ReadabilityExtractor? extractor = null,
        IArticleCache? cache = null,
        ILogger<HttpArticleExtractor>? logger = null,
        ArticleExtractionOptions? options = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
        _cache = cache ?? new InMemoryArticleCache();
        _logger = logger;
        _options = options ?? new ArticleExtractionOptions();
    }

    public static HttpArticleExtractor CreateDefault(TimeSpan? timeout = null)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10
        };

        var httpClient = new HttpClient(handler)
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(15)
        };

        httpClient.DefaultRequestHeaders.UserAgent.Clear();
        httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("ReadabilityExtractor", "1.0"));

        return new HttpArticleExtractor(httpClient, cache: new InMemoryArticleCache(), options: new ArticleExtractionOptions());
    }

    public async Task<ArticleContent> ExtractFromUrlAsync(
        string url,
        CultureInfo? overrideCulture = null,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var originalUrl = WebsiteUrl.TryCreate(url);
        if (!originalUrl.Result.IsValid)
        {
            throw new ValueObjectValidationException(typeof(WebsiteUrl), originalUrl.Result);
        }

        _logger?.LogDebug("Starting extraction for {Url}, forceRefresh={ForceRefresh}", url, forceRefresh);

        var validUrl = originalUrl.Value!.Value;
        if (!forceRefresh && _cache != null && _cache.TryGet(validUrl, out var cached) && cached != null)
        {
            _logger?.LogDebug("Article cache HIT for {Url}", originalUrl);
            return cached;
        }

        _logger?.LogDebug("Article cache MISS for {Url}, issuing HTTP GET", originalUrl);

        using var request = new HttpRequestMessage(HttpMethod.Get, validUrl);
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        ).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger?.LogWarning("Non-success status code {StatusCode} for {Url}",
                response.StatusCode, originalUrl);
        }

        response.EnsureSuccessStatusCode();

        var finalUri = response.RequestMessage?.RequestUri;
        var finalUrl = finalUri?.ToString() ?? validUrl;

        _logger?.LogDebug("Fetched article. Original URL: {OriginalUrl}, Final URL: {FinalUrl}, Status: {StatusCode}",
            originalUrl, finalUrl, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        var options = new ExtractionOptions
        {
            Url = finalUrl,
            CultureOverride = overrideCulture
        };

        ArticleContent article;
        try
        {
            article = _extractor.Extract(html, options);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Extraction failed for {FinalUrl}", finalUrl);
            throw;
        }

        article.Url = finalUrl;

        if (_options.EnableImageFileCache && !string.IsNullOrWhiteSpace(_options.ImageFileCachePath))
        {
            try
            {
                await CacheImagesToFileSystemAsync(article, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (_options.IgnoreImageDownloadErrors)
                {
                    _logger?.LogWarning(ex,
                        "Error while caching images for {FinalUrl}. Continuing without image file cache.",
                        finalUrl);
                }
                else
                {
                    _logger?.LogError(ex,
                        "Error while caching images for {FinalUrl}. Rethrowing as configured.",
                        finalUrl);
                    throw;
                }
            }
        }

        if (_cache != null)
        {
            _logger?.LogDebug("Caching article for final URL {FinalUrl} and original URL {OriginalUrl}",
                finalUrl, originalUrl);

            _cache.Set(finalUrl, article);
            if (!string.Equals(validUrl, finalUrl, StringComparison.OrdinalIgnoreCase))
            {
                _cache.Set(validUrl, article);
            }
        }

        _logger?.LogInformation("Successfully extracted article for {FinalUrl}", finalUrl);
        return article;
    }

    private async Task CacheImagesToFileSystemAsync(ArticleContent article, CancellationToken cancellationToken)
    {
        var root = _options.ImageFileCachePath!;
        Directory.CreateDirectory(root);

        var processed = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

        async Task<(string? localPath, string? localUrl)> cacheOneAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return (null, null);

            if (!processed.Add(url))
                return (null, null);

            var (fileName, extension) = BuildFileNameFromUrl(url);
            var filePath = Path.Combine(root, fileName + extension);

            if (!File.Exists(filePath))
            {
                _logger?.LogDebug("Downloading image {Url} to {Path}", url, filePath);

                using var response = await _httpClient.GetAsync(url,
                        HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    _logger?.LogWarning("Skipping image {Url} due to status code {StatusCode}", url, response.StatusCode);
                    return (null, null);
                }

                var contentLength = response.Content.Headers.ContentLength;
                if (contentLength.HasValue && contentLength.Value > _options.MaxImageDownloadBytes)
                {
                    _logger?.LogWarning(
                        "Skipping image {Url} because Content-Length {Size} exceeds limit {Limit} bytes",
                        url, contentLength.Value, _options.MaxImageDownloadBytes);
                    return (null, null);
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var fileStream = File.Create(filePath);
                await stream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _logger?.LogDebug("Image already cached for URL {Url} at {Path}", url, filePath);
            }

            string? localUrl = null;
            if (!string.IsNullOrWhiteSpace(_options.ImageFileCacheBaseUrl))
            {
                localUrl = _options.ImageFileCacheBaseUrl.TrimEnd('/') + "/" + Path.GetFileName(filePath);
            }

            return (filePath, localUrl);
        }

        foreach (var img in article.Images)
        {
            var (localPath, localUrl) = await cacheOneAsync(img.Url).ConfigureAwait(false);
            if (localPath != null)
            {
                img.LocalPath = localPath;
                img.LocalUrl = localUrl;
            }

            foreach (var variant in img.Variants)
            {
                var (vPath, vUrl) = await cacheOneAsync(variant.Url).ConfigureAwait(false);
                if (vPath != null)
                {
                    variant.LocalPath = vPath;
                    variant.LocalUrl = vUrl;
                }
            }
        }
    }

    private static (string fileName, string extension) BuildFileNameFromUrl(string url)
    {
        string ext = ".bin";

        try
        {
            var uri = new Uri(url, UriKind.RelativeOrAbsolute);
            var lastSegment = uri.IsAbsoluteUri ? uri.AbsolutePath : url;
            var guessedExt = Path.GetExtension(lastSegment);
            if (!string.IsNullOrWhiteSpace(guessedExt) && guessedExt.Length <= 6)
            {
                ext = guessedExt;
            }
        }
        catch
        {
        }

        using var sha = SHA256.Create();
        var hashBytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(url));
        var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

        return (hash, ext);
    }
}