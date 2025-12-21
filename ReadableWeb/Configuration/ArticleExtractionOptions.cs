namespace ReadableWeb.Configuration
{
    /// <summary>
    /// Global configuration for article extraction, caching, and image/file behavior.
    /// Bound from configuration and injected into HttpArticleExtractor.
    /// </summary>
    public class ArticleExtractionOptions
    {
        // Parser selection
        public ParserType Parser { get; set; } = ParserType.HtmlAgilityPack;

        // Redis / caching
        public bool UseRedis { get; set; } = false;
        public string? RedisConnectionString { get; set; }
        public int RedisDatabase { get; set; } = 0;
        public string RedisKeyPrefix { get; set; } = "article:";
        public TimeSpan RedisDefaultTtl { get; set; } = TimeSpan.FromHours(1);
        public TimeSpan MemoryCacheDefaultTtl { get; set; } = TimeSpan.FromMinutes(10);
        public bool FallbackToMemoryOnRedisFailure { get; set; } = true;

        // Image file cache
        /// <summary>
        /// If true, article images will be downloaded and cached to the file system.
        /// </summary>
        public bool EnableImageFileCache { get; set; } = false;

        /// <summary>
        /// Root directory where cached images are stored (must be writable).
        /// e.g. "wwwroot/article-images"
        /// </summary>
        public string? ImageFileCachePath { get; set; }

        /// <summary>
        /// Optional base URL that maps to ImageFileCachePath for serving images.
        /// e.g. "/article-images"
        /// </summary>
        public string? ImageFileCacheBaseUrl { get; set; }

        /// <summary>
        /// Maximum image size to download, in bytes. Default: 5 MB.
        /// </summary>
        public int MaxImageDownloadBytes { get; set; } = 5 * 1024 * 1024;

        /// <summary>
        /// If true, image download errors are logged and ignored; extraction continues.
        /// </summary>
        public bool IgnoreImageDownloadErrors { get; set; } = true;
    }
}