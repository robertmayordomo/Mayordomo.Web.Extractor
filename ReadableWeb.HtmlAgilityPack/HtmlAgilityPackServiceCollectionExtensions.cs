using Microsoft.Extensions.DependencyInjection;
using ReadableWeb.Abstractions;

namespace ReadableWeb.HtmlAgilityPack;

public static class HtmlAgilityPackServiceCollectionExtensions
{
    /// <summary>
    /// Register HtmlAgilityPack-based extractor implementations.
    /// </summary>
    public static IServiceCollection UseHtmlAgilityPackExtractors(this IServiceCollection services)
    {
        // Register HtmlAgilityPack implementations for core extraction interfaces
        services.AddSingleton<ILocaleInferrer, DefaultLocaleInferrer>();
        services.AddSingleton<IMetadataExtractor, DefaultMetadataExtractor>();
        services.AddSingleton<IContentExtractor, ReadabilityContentExtractor>();
        services.AddSingleton<IDocumentPreprocessor, DocumentPreprocessor>();
        services.AddSingleton<IImageProcessor, ImageProcessor>();

        return services;
    }
}
