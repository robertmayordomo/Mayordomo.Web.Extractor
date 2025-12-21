using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReadableWeb.Abstractions;
using ReadableWeb.Cache;
using ReadableWeb.Configuration;
using ReadableWeb.Extraction;
using ReadableWeb.HtmlAgilityPack;
using StackExchange.Redis;

namespace ReadableWeb
{
    public static class ArticleExtractionServiceCollectionExtensions
    {
        public static IServiceCollection AddArticleExtraction(
            this IServiceCollection services,
            Action<ArticleExtractionOptions>? configure = null)
        {
            var options = new ArticleExtractionOptions();
            configure?.Invoke(options);
            return services.AddArticleExtractionCore(options);
        }

        public static IServiceCollection AddArticleExtraction(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName = "ArticleExtraction",
            Action<ArticleExtractionOptions>? configure = null)
        {
            var options = new ArticleExtractionOptions();

            var section = configuration.GetSection(sectionName);
            if (section.Exists())
            {
                section.Bind(options);
            }

            configure?.Invoke(options);

            return services.AddArticleExtractionCore(options);
        }

        private static IServiceCollection AddArticleExtractionCore(
            this IServiceCollection services,
            ArticleExtractionOptions options)
        {
            services.AddSingleton(options);

            services.AddSingleton<IArticleCache>(sp =>
            {
                var loggerFactory = sp.GetService<ILoggerFactory>();
                var redisLogger = loggerFactory?.CreateLogger<RedisArticleCache>();
                var memLogger = loggerFactory?.CreateLogger<InMemoryArticleCache>();

                if (options.UseRedis && !string.IsNullOrWhiteSpace(options.RedisConnectionString))
                {
                    try
                    {
                        redisLogger?.LogInformation(
                            "Attempting Redis connection for article cache using {ConnectionString}, db {Db}",
                            options.RedisConnectionString, options.RedisDatabase);

                        var mux = ConnectionMultiplexer.Connect(options.RedisConnectionString);

                        redisLogger?.LogInformation("Connected to Redis for article cache.");

                        return new RedisArticleCache(
                            connectionMultiplexer: mux,
                            db: options.RedisDatabase,
                            keyPrefix: options.RedisKeyPrefix,
                            defaultTtl: options.RedisDefaultTtl,
                            logger: redisLogger);
                    }
                    catch (Exception ex)
                    {
                        if (!options.FallbackToMemoryOnRedisFailure)
                        {
                            redisLogger?.LogError(ex,
                                "Failed to connect to Redis for article cache and fallback is disabled.");
                            throw;
                        }

                        memLogger?.LogWarning(ex,
                            "Failed to connect to Redis for article cache. Falling back to InMemoryArticleCache.");
                        return new InMemoryArticleCache(options.MemoryCacheDefaultTtl);
                    }
                }

                memLogger?.LogInformation(
                    "Using InMemoryArticleCache (Redis disabled or no connection string provided).");

                return new InMemoryArticleCache(options.MemoryCacheDefaultTtl);
            });

            if (options.Parser == ParserType.HtmlAgilityPack)
            {
                services.UseHtmlAgilityPackExtractors();
            }
            else
            {
                //call the angle sharp registration
            }

            // ReadabilityExtractor depends on ILocaleInferrer, IMetadataExtractor, IContentExtractor being registered by the consumer
            services.AddSingleton<ReadabilityExtractor>(sp =>
                {
                    var locale = sp.GetRequiredService<ILocaleInferrer>();
                    var metadata = sp.GetRequiredService<IMetadataExtractor>();
                    var content = sp.GetRequiredService<IContentExtractor>();
                    var documentPreprocessor = sp.GetRequiredService<IDocumentPreprocessor>();
                    var imageProcessor = sp.GetRequiredService<IImageProcessor>();
                    return new ReadabilityExtractor(locale, metadata, content, documentPreprocessor, imageProcessor);
                });

            services.AddHttpClient<IHttpArticleExtractor,HttpArticleExtractor>()
                .ConfigureHttpClient(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(15);
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("ReadabilityExtractor/1.0");
                })
                .AddTypedClient((client, sp) =>
                {
                    var extractor = sp.GetRequiredService<ReadabilityExtractor>();
                    var cache = sp.GetRequiredService<IArticleCache>();
                    var logger = sp.GetService<ILogger<HttpArticleExtractor>>();
                    var options = sp.GetRequiredService<ArticleExtractionOptions>();
                    return new HttpArticleExtractor(client, extractor, cache, logger, options);
                });

            return services;
        }
    }
}
