using ReadableWeb.Cache;
using ReadableWeb.Configuration;
using ReadableWeb.Extraction;
using ReadableWeb.HtmlAgilityPack;
using Shouldly;
using Xunit;

namespace ReadableWeb.Tests
{
    public class HttpArticleExtractorTests
    {
        private const string SampleHtml = """

                                          <!doctype html>
                                          <html lang='en'>
                                          <head>
                                              <meta charset='utf-8'>
                                              <title>Unit Test Article Title</title>
                                              <meta name='author' content='Jane Doe' />
                                              <meta property='article:published_time' content='2025-11-25T10:03:00Z' />
                                              <meta property='article:modified_time' content='2025-11-26T12:15:00Z' />
                                              <meta property='og:site_name' content='Unit Test News' />
                                          </head>
                                          <body>
                                              <header>Some header stuff</header>
                                              <article>
                                                  <p>This is the first paragraph of the article, which should have enough length to be considered substantial content for the scoring algorithm to work properly.</p>
                                                  <p>This is the second paragraph of the article, adding even more content to ensure that the extractor considers this container as the main article body.</p>
                                                  <figure>
                                                      <img src='https://example.com/image-main.jpg' alt='Main image' width='800' height='450'
                                                           srcset='https://example.com/image-main-400.jpg 400w, https://example.com/image-main-800.jpg 800w' />
                                                      <figcaption>Main image caption.</figcaption>
                                                  </figure>
                                              </article>
                                              <footer>Footer links and junk</footer>
                                          </body>
                                          </html>
                                          """;

        [Fact]
        public async Task ExtractFromUrl_UsesHtmlFromHttpClient_AndExtractsFields()
        {
            var url = "https://example.com/test-article";
            int requests = 0;

            HttpClient client = FakeHttpMessageHandler.CreateClient(req =>
            {
                requests++;
                return FakeHttpMessageHandler.HtmlResponse(SampleHtml);
            });

            var cache = new InMemoryArticleCache(TimeSpan.FromMinutes(30));
            var extractorCore = GetExtractor();
            var options = new ArticleExtractionOptions();
            var httpExtractor = new HttpArticleExtractor(client, extractorCore, cache, logger: null, options: options);

            var result = await httpExtractor.ExtractFromUrlAsync(url);

            requests.ShouldBe(1);
            result.ShouldNotBeNull();
            result.Url.ShouldBe(url);
            result.Title.ShouldBe("Unit Test Article Title");
            result.Author.ShouldBe("Jane Doe");
            result.SiteName.ShouldBe("Unit Test News");
            result.TextContent.ShouldNotBeNullOrWhiteSpace();
            result.PublishedTime.ShouldNotBeNull();
            result.ModifiedTime.ShouldNotBeNull();
            result.Images.ShouldNotBeEmpty();
        }

        [Fact]
        public async Task ExtractFromUrl_UsesCacheOnSecondCall()
        {
            var url = "https://example.com/test-article";
            int requests = 0;

            HttpClient client = FakeHttpMessageHandler.CreateClient(req =>
            {
                requests++;
                return FakeHttpMessageHandler.HtmlResponse(SampleHtml);
            });

            var cache = new InMemoryArticleCache(TimeSpan.FromMinutes(30));
            var httpExtractor = new HttpArticleExtractor(client, GetExtractor(), cache, logger: null, options: new ArticleExtractionOptions());

            var first = await httpExtractor.ExtractFromUrlAsync(url);
            var second = await httpExtractor.ExtractFromUrlAsync(url);

            requests.ShouldBe(1);
            second.ShouldBeSameAs(first);
        }

        private static ReadabilityExtractor GetExtractor()
        {
            return new ReadabilityExtractor(new DefaultLocaleInferrer(), new DefaultMetadataExtractor(), new ReadabilityContentExtractor(), new DocumentPreprocessor(), new ImageProcessor());
        }

        [Fact]
        public async Task ExtractFromUrl_ForceRefresh_BypassesCache()
        {
            var url = "https://example.com/test-article";
            int requests = 0;

            HttpClient client = FakeHttpMessageHandler.CreateClient(req =>
            {
                requests++;
                return FakeHttpMessageHandler.HtmlResponse(SampleHtml);
            });

            var cache = new InMemoryArticleCache(TimeSpan.FromMinutes(30));
            var httpExtractor = new HttpArticleExtractor(client, GetExtractor(), cache, logger: null, options: new ArticleExtractionOptions());

            var first = await httpExtractor.ExtractFromUrlAsync(url);
            var second = await httpExtractor.ExtractFromUrlAsync(url, forceRefresh: true);

            requests.ShouldBe(2);
            second.Title.ShouldBe(first.Title);
        }

        [Fact]
        public async Task ExtractFromUrl_UsesFinalUrlAfterRedirect()
        {
            var originalUrl = "https://short.example.com/abc";
            var finalUrl = "https://www.example.com/full-article-path";

            HttpClient client = FakeHttpMessageHandler.CreateClient(req =>
            {
                var response = FakeHttpMessageHandler.HtmlResponse(SampleHtml);
                response.RequestMessage = new HttpRequestMessage(HttpMethod.Get, finalUrl);
                return response;
            });

            var cache = new InMemoryArticleCache(TimeSpan.FromMinutes(30));
            var httpExtractor = new HttpArticleExtractor(client, GetExtractor(), cache, logger: null, options: new ArticleExtractionOptions());

            var article = await httpExtractor.ExtractFromUrlAsync(originalUrl);

            article.Url.ShouldBe(finalUrl);

            var articleFromOriginal = await httpExtractor.ExtractFromUrlAsync(originalUrl);
            var articleFromFinal = await httpExtractor.ExtractFromUrlAsync(finalUrl);

            articleFromOriginal.ShouldBeSameAs(article);
            articleFromFinal.ShouldBeSameAs(article);
        }

        [Fact]
        public async Task ExtractFromUrl_CachesImagesToDisk_WhenEnabled()
        {
            var url = "https://example.com/test-article-with-images";
            int htmlRequests = 0;
            int imageRequests = 0;

            var tempDir = Path.Combine(Path.GetTempPath(), "article_images_test_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);

            HttpClient client = FakeHttpMessageHandler.CreateClient(req =>
            {
                if (req.RequestUri!.AbsoluteUri.EndsWith(".jpg"))
                {
                    imageRequests++;
                    return FakeHttpMessageHandler.BinaryResponse(new byte[] { 1, 2, 3, 4 });
                }
                htmlRequests++;
                return FakeHttpMessageHandler.HtmlResponse(SampleHtml);
            });

            var cache = new InMemoryArticleCache(TimeSpan.FromMinutes(30));
            var options = new ArticleExtractionOptions
            {
                EnableImageFileCache = true,
                ImageFileCachePath = tempDir,
                ImageFileCacheBaseUrl = "/article-images"
            };
            var httpExtractor = new HttpArticleExtractor(client, GetExtractor(), cache, logger: null, options: options);

            var article = await httpExtractor.ExtractFromUrlAsync(url);

            htmlRequests.ShouldBe(1);
            imageRequests.ShouldBeGreaterThanOrEqualTo(1);
            article.Images.ShouldNotBeEmpty();

            var firstImage = article.Images[0];
            if (!string.IsNullOrWhiteSpace(firstImage.LocalPath))
            {
                File.Exists(firstImage.LocalPath).ShouldBeTrue();
            }

            Directory.Delete(tempDir, recursive: true);
        }
    }
}
