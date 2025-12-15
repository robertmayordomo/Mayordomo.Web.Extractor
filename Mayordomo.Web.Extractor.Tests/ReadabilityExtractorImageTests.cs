using Mayordomo.Web.Extractor.Abstractions.Models;
using Mayordomo.Web.Extractor.Extraction;
using Mayordomo.Web.Extractor.HtmlAgilityPack;
using Shouldly;
using Xunit;

namespace Mayordomo.Web.Extractor.Tests;

public class ReadabilityExtractorImageTests
{
    private static ReadabilityExtractor GetExtractor()
    {
        return new ReadabilityExtractor(new DefaultLocaleInferrer(), new DefaultMetadataExtractor(),
            new ReadabilityContentExtractor(), new DocumentPreprocessor(), new ImageProcessor());
    }


    [Fact]
    public void Extract_InlineAndMetaImages_AssignsRolesAndVariants()
    {
        var html = """
                   <!doctype html>
                   <html lang='en'>
                   <head>
                       <title>Image Test</title>
                       <meta property='og:image' content='https://example.com/og-image.jpg' />
                       <script type='application/ld+json'>
                       {
                           "@context": "https://schema.org",
                           "@type": "NewsArticle",
                           "headline": "Image Test",
                           "image": {
                             "url": "https://example.com/jsonld-image.jpg"
                           }
                       }
                       </script>
                   </head>
                   <body>
                   <article>
                       <figure>
                           <img src='https://example.com/inline.jpg'
                                srcset='https://example.com/inline-400.jpg 400w, https://example.com/inline-800.jpg 800w'
                                alt='Inline image' />
                           <figcaption>Inline caption</figcaption>
                       </figure>
                   </article>
                   </body>
                   </html>
                   """;

        var extractor = GetExtractor();
        var article = extractor.Extract(html, new ExtractionOptions { Url = "https://example.com/image-test" });

        article.Images.ShouldNotBeEmpty();

        var inline = article.Images.FirstOrDefault(i => i.Role == ArticleImageRole.Inline);
        inline.ShouldNotBeNull();
        inline!.Alt.ShouldBe("Inline image");
        inline.Caption.ShouldBe("Inline caption");
        inline.Variants.Count.ShouldBeGreaterThanOrEqualTo(2);

        var og = article.Images.FirstOrDefault(i => i.Variants.Any(v => v.Role == ArticleImageRole.OpenGraph));
        og.ShouldNotBeNull();

        var variantCount = article.Images.SelectMany(a => a.Variants).Count();
        var variantTypes = string.Join(",",
            article.Images.SelectMany(a => a.Variants).Select(a => a.Role.ToString()).Distinct());

        var jsonld = article.Images.FirstOrDefault(i => i.Role == ArticleImageRole.JsonLd);
        jsonld.ShouldNotBeNull(
            $"{article.Images.Count} images found, {variantCount} variants Found, {variantTypes} variant types found");
    }
}