using Mayordomo.Web.Extractor.Abstractions.Models;

namespace Mayordomo.Web.Extractor.Abstractions;

public interface IImageProcessor
{
    List<ArticleImage> ExtractImages(string html, string? pageUrl);
}