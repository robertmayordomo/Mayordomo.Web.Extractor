using ReadableWeb.Abstractions;
using ReadableWeb.Abstractions.Models;

namespace ReadableWeb.Extraction;

public class ReadabilityExtractor
{
    private readonly ILocaleInferrer _localeInferrer;
    private readonly IMetadataExtractor _metadataExtractor;
    private readonly IContentExtractor _contentExtractor;
    private readonly IDocumentPreprocessor _documentPreprocessor;
    private readonly IImageProcessor _imageProcessor;

    public ReadabilityExtractor(
        ILocaleInferrer localeInferrer,
        IMetadataExtractor metadataExtractor,
        IContentExtractor contentExtractor,
        IDocumentPreprocessor documentPreprocessor,
        IImageProcessor imageProcessor)
    {
        _localeInferrer = localeInferrer ?? throw new ArgumentNullException(nameof(localeInferrer));
        _metadataExtractor = metadataExtractor ?? throw new ArgumentNullException(nameof(metadataExtractor));
        _contentExtractor = contentExtractor ?? throw new ArgumentNullException(nameof(contentExtractor));
        _documentPreprocessor = documentPreprocessor ?? throw new ArgumentNullException(nameof(documentPreprocessor));
        _imageProcessor = imageProcessor ?? throw new ArgumentException(nameof(imageProcessor));
    }

    public ArticleContent Extract(string html, ExtractionOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(html))
            throw new ArgumentException("HTML must not be empty.", nameof(html));

        options ??= new ExtractionOptions();

        _documentPreprocessor.Prepare(html);

        var culture = options.CultureOverride ?? _localeInferrer.InferCulture(html, options.Url);

        var article = new ArticleContent
        {
            Url = options.Url ?? string.Empty,
            DetectedCulture = culture,
            Title = _metadataExtractor.ExtractTitle(html),
            SiteName = _metadataExtractor.ExtractSiteName(html),
            Author = _metadataExtractor.ExtractAuthor(html)
        };

        (article.PublishedTime, article.ModifiedTime) = _metadataExtractor.ExtractDates(html, culture);

        var (textContent, excerpt) = _contentExtractor.ExtractMainContent(html, culture);

        article.TextContent = textContent;
        article.Excerpt = excerpt;
        article.Images = _imageProcessor.ExtractImages(html, options.Url);

        return article;
    }
}