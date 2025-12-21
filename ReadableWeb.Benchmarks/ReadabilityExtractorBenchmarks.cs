using BenchmarkDotNet.Attributes;
using Mayordomo.Web.Extractor.Abstractions.Models;
using Mayordomo.Web.Extractor.Extraction;
using Mayordomo.Web.Extractor.HtmlAgilityPack;

namespace Mayordomo.Web.Extractor.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class ReadabilityExtractorBenchmarks
{
    private string _complexHtml = null!;
    private ReadabilityExtractor _extractor = null!;
    private string _imageHeavyHtml = null!;
    private string _simpleHtml = null!;

    private static ReadabilityExtractor GetExtractor()
    {
        return new ReadabilityExtractor(new DefaultLocaleInferrer(), new DefaultMetadataExtractor(), new ReadabilityContentExtractor(), new DocumentPreprocessor(), new ImageProcessor());
    }


    [GlobalSetup]
    public void Setup()
    {
        _extractor = GetExtractor();

        // Simple HTML - minimal structure
        _simpleHtml = """
                      <!doctype html>
                      <html lang='en'>
                      <head>
                       <meta charset='utf-8'>
                        <title>Simple Article</title>
                       <meta name='author' content='John Doe' />
                        </head>
                       <body>
                      <article>
                         <p>This is a simple article with minimal content for baseline performance testing.</p>
                      <p>Just a few paragraphs to ensure proper extraction.</p>
                      </article>
                         </body>
                      </html>
                      """;

        // Complex HTML - realistic news article
        _complexHtml = """
                               <!doctype html>
                             <html lang='en'>
                                   <head>
                         <meta charset='utf-8'>
                               <title>Complex News Article with Rich Metadata</title>
                       <meta name='author' content='Jane Doe' />
                                     <meta property='article:published_time' content='2025-01-15T10:00:00Z' />
                            <meta property='article:modified_time' content='2025-01-15T14:30:00Z' />
                              <meta property='og:site_name' content='Tech News Daily' />
                        <meta property='og:title' content='Breaking: New Technology Breakthrough' />
                                 <meta property='og:description' content='Scientists have made a groundbreaking discovery...' />
                        <meta property='og:image' content='https://example.com/og-image.jpg' />
                       <script type='application/ld+json'>
                               {
                                      "@context": "https://schema.org",
                        "@type": "NewsArticle",
                       "headline": "Complex News Article",
                          "image": {
                       "url": "https://example.com/jsonld-image.jpg"
                        },
                        "datePublished": "2025-01-15T10:00:00Z"
                         }
                              </script>
                                  </head>
                       <body>
                         <header>
                             <nav>
                          <a href='/home'>Home</a>
                            <a href='/tech'>Technology</a>
                            <a href='/science'>Science</a>
                             </nav>
                                </header>
                          <aside class='sidebar'>
                            <div class='ad'>Advertisement</div>
                          <div class='related'>
                                <h3>Related Articles</h3>
                               <ul>
                                <li><a href='/article1'>Article 1</a></li>
                          <li><a href='/article2'>Article 2</a></li>
                                      </ul>
                           </div>
                            </aside>
                                <main>
                             <article>
                                 <h1>Complex News Article with Rich Metadata</h1>
                             <div class='meta'>
                                      <span class='author'>By Jane Doe</span>
                        <time datetime='2025-01-15T10:00:00Z'>January 15, 2025</time>
                        </div>
                            <p>This is the introductory paragraph that provides context for the article. It should be substantial enough to be detected as the main content by the extraction algorithm.</p>
                       <p>The second paragraph continues the narrative with additional details about the breakthrough discovery. Scientists around the world are celebrating this achievement which promises to revolutionize the industry.</p>
                             <p>Further elaboration on the topic with technical details and expert opinions. This paragraph adds depth to the article and ensures the content extraction algorithm has enough material to work with.</p>
                           <h2>Background Information</h2>
                           <p>Historical context and background information relevant to understanding the significance of this discovery. Multiple paragraphs help establish the article structure.</p>
                              <p>Additional context with references to previous research and related developments in the field.</p>
                           <h2>Technical Details</h2>
                         <p>In-depth technical explanation of the methodology and findings. This section contains specialized terminology and detailed analysis.</p>
                            <p>Further technical discussion with data points and statistical analysis to support the claims made in the article.</p>
                            <h2>Expert Commentary</h2>
                               <p>Quotes and commentary from leading experts in the field provide additional credibility and perspective on the breakthrough.</p>
                             <p>Multiple expert opinions are presented to give a balanced view of the discovery and its implications.</p>
                              <h2>Future Implications</h2>
                                   <p>Discussion of how this discovery might impact the industry and society in the coming years. Speculation based on expert analysis.</p>
                                <p>Concluding remarks that tie together the various threads of the article and provide a forward-looking perspective.</p>
                              </article>
                            </main>
                           <footer>
                             <div class='footer-links'>
                              <a href='/about'>About Us</a>
                                <a href='/contact'>Contact</a>
                         <a href='/privacy'>Privacy Policy</a>
                                </div>
                                 <p>&copy; 2025 Tech News Daily. All rights reserved.</p>
                           </footer>
                              </body>
                                    </html>
                       """;

        // Image-heavy HTML
        _imageHeavyHtml = """
                          <!doctype html>
                          <html lang='en'>
                          <head>
                          <meta charset='utf-8'>
                          <title>Photo Gallery Article</title>
                          <meta name='author' content='Photo Journalist' />
                          <meta property='og:image' content='https://example.com/gallery-og.jpg' />
                          </head>
                          <body>
                          <article>
                          <h1>Amazing Photo Gallery</h1>
                          <p>This article showcases multiple images with various attributes and srcsets.</p>
                               <figure>
                          <img src='https://example.com/photo1.jpg'
                          srcset='https://example.com/photo1-400.jpg 400w, https://example.com/photo1-800.jpg 800w, https://example.com/photo1-1200.jpg 1200w'
                          alt='Photo 1 description' width='1200' height='800' />
                             <figcaption>First photo caption with detailed description.</figcaption>
                          </figure>
                          <p>Interleaved text content between images.</p>
                                <figure>
                          <img src='https://example.com/photo2.jpg'
                             srcset='https://example.com/photo2-400.jpg 400w, https://example.com/photo2-800.jpg 800w, https://example.com/photo2-1200.jpg 1200w'
                          alt='Photo 2 description' width='1200' height='800' />
                          <figcaption>Second photo caption.</figcaption>
                          </figure>
                          <p>More text content.</p>
                          <figure>
                            <img src='https://example.com/photo3.jpg'
                          srcset='https://example.com/photo3-400.jpg 400w, https://example.com/photo3-800.jpg 800w'
                          alt='Photo 3 description' />
                               <figcaption>Third photo caption.</figcaption>
                          </figure>
                          <figure>
                          <img src='https://example.com/photo4.jpg'
                          alt='Photo 4 description' />
                          </figure>
                                   <figure>
                          <img src='https://example.com/photo5.jpg'
                            srcset='https://example.com/photo5-400.jpg 400w, https://example.com/photo5-800.jpg 800w, https://example.com/photo5-1200.jpg 1200w, https://example.com/photo5-1600.jpg 1600w'
                          alt='Photo 5 description' width='1600' height='1200' />
                          <figcaption>Fifth photo with high resolution options.</figcaption>
                          </figure>
                          <p>Concluding text after all images.</p>
                          </article>
                          </body>
                          </html>
                          """;
    }

    [Benchmark(Baseline = true)]
    public ArticleContent ExtractSimpleArticle()
    {
        return _extractor.Extract(_simpleHtml, new ExtractionOptions { Url = "https://example.com/simple" });
    }

    [Benchmark]
    public ArticleContent ExtractComplexArticle()
    {
        return _extractor.Extract(_complexHtml, new ExtractionOptions { Url = "https://example.com/complex" });
    }

    [Benchmark]
    public ArticleContent ExtractImageHeavyArticle()
    {
        return _extractor.Extract(_imageHeavyHtml, new ExtractionOptions { Url = "https://example.com/gallery" });
    }

    [Benchmark]
    public ArticleContent ExtractWithoutUrl()
    {
        return _extractor.Extract(_complexHtml);
    }
}