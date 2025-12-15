using Mayordomo.Web.Extractor.Cache;
using Mayordomo.Web.Extractor.Configuration;
using Mayordomo.Web.Extractor.Extraction;
using Mayordomo.Web.Extractor.AngleSharp;
using Shouldly;
using Xunit;

namespace Mayordomo.Web.Extractor.AngleSharp.Tests
{
    public class HttpArticleExtractorTests
    {
        private const string SampleHtml = @"""

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

        private static ReadabilityExtractor GetExtractor()
        {
            return new ReadabilityExtractor(new DefaultLocaleInferrer(), new DefaultMetadataExtractor(), new ReadabilityContentExtractor(), new DocumentPreprocessor(), new ImageProcessor());
        }

    }
}
