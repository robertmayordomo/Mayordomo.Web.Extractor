using Mayordomo.Web.Extractor.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Mayordomo.Web.Extractor.AngleSharp;

public static class AngleSharpServiceCollectionExtensions
{
    public static IServiceCollection UseAngleSharpExtractors(this IServiceCollection services)
    {
        services.AddSingleton<ILocaleInferrer, DefaultLocaleInferrer>();
        services.AddSingleton<IMetadataExtractor, DefaultMetadataExtractor>();
        services.AddSingleton<IContentExtractor, ReadabilityContentExtractor>();
        services.AddSingleton<IDocumentPreprocessor, DocumentPreprocessor>();
        services.AddSingleton<IImageProcessor, ImageProcessor>();

        return services;
    }
}
