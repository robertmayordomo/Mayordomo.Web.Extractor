using System.Net;
using System.Text;
using BenchmarkDotNet.Attributes;
using Mayordomo.Web.Extractor.Abstractions.Models;
using Mayordomo.Web.Extractor.Cache;
using Mayordomo.Web.Extractor.Configuration;
using Mayordomo.Web.Extractor.Extraction;
using Mayordomo.Web.Extractor.HtmlAgilityPack;

namespace Mayordomo.Web.Extractor.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class HttpArticleExtractorBenchmarks
{
    private const string SampleHtml = """
                                      <!doctype html>
                                      <html lang='en'>
                                      <head>
                                          <meta charset='utf-8'>
                                       <title>HTTP Benchmark Article</title>
                                          <meta name='author' content='Benchmark Author' />
                                      <meta property='article:published_time' content='2025-01-15T10:00:00Z' />
                                      <meta property='og:site_name' content='Benchmark Site' />
                                      </head>
                                      <body>
                                          <article>
                                      <h1>HTTP Article Extraction Benchmark</h1>
                                       <p>This article is used to benchmark the HTTP article extraction process including HTML fetching and parsing.</p>
                                      <p>Multiple paragraphs ensure realistic content extraction performance measurement across different scenarios.</p>
                                      <p>The content should be substantial enough to trigger all extraction algorithms including metadata parsing and content scoring.</p>
                                      <figure>
                                       <img src='https://example.com/benchmark-image.jpg' 
                                      srcset='https://example.com/benchmark-400.jpg 400w, https://example.com/benchmark-800.jpg 800w'
                                      alt='Benchmark image' />
                                      <figcaption>Image caption for benchmark testing.</figcaption>
                                              </figure>
                                            <p>Additional content after the image to ensure complete article structure.</p>
                                          </article>
                                      </body>
                                      </html>
                                      """;

    private HttpArticleExtractor _extractorWithCache = null!;
    private HttpArticleExtractor _extractorWithoutCache = null!;
    private HttpClient _httpClient = null!;
    private string _testUrl = null!;

    private static ReadabilityExtractor GetExtractor()
    {
        return new ReadabilityExtractor(new DefaultLocaleInferrer(), new DefaultMetadataExtractor(), new ReadabilityContentExtractor(), new DocumentPreprocessor(), new ImageProcessor());
    }


    [GlobalSetup]
    public void Setup()
    {
        _testUrl = "https://benchmark.example.com/article";

        // Create fake HTTP handler
        var handler = new FakeHttpHandler(SampleHtml);
        _httpClient = new HttpClient(handler);

        var readabilityExtractor = GetExtractor();
        var cache = new InMemoryArticleCache(TimeSpan.FromMinutes(30));
        var options = new ArticleExtractionOptions();

        _extractorWithCache = new HttpArticleExtractor(_httpClient, readabilityExtractor, cache, null, options);
        _extractorWithoutCache = new HttpArticleExtractor(_httpClient, readabilityExtractor, null, null, options);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _httpClient?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public async Task<ArticleContent> ExtractFromUrl_FirstRequest()
    {
        var url = $"{_testUrl}?id={Guid.NewGuid()}"; // Unique URL to avoid cache
        return await _extractorWithoutCache.ExtractFromUrlAsync(url);
    }

    [Benchmark]
    public async Task<ArticleContent> ExtractFromUrl_WithCache_ColdCache()
    {
        var url = $"{_testUrl}?cached={Guid.NewGuid()}";
        return await _extractorWithCache.ExtractFromUrlAsync(url);
    }

    [IterationSetup(Target = nameof(ExtractFromUrl_WithCache_WarmCache))]
    public async Task WarmupCache()
    {
        // Pre-populate cache
        await _extractorWithCache.ExtractFromUrlAsync(_testUrl);
    }

    [Benchmark]
    public async Task<ArticleContent> ExtractFromUrl_WithCache_WarmCache()
    {
        return await _extractorWithCache.ExtractFromUrlAsync(_testUrl);
    }

    [Benchmark]
    public async Task<ArticleContent> ExtractFromUrl_ForceRefresh()
    {
        return await _extractorWithCache.ExtractFromUrlAsync(_testUrl, forceRefresh: true);
    }

    // Fake HTTP message handler for benchmarking
    private class FakeHttpHandler : HttpMessageHandler
    {
        private readonly string _html;

        public FakeHttpHandler(string html)
        {
            _html = html;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_html, Encoding.UTF8, "text/html"),
                RequestMessage = request
            };

            return Task.FromResult(response);
        }
    }
}