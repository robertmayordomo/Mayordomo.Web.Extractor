using ReadableWeb.Abstractions.Models;

namespace ReadableWeb.Abstractions;

public interface IImageProcessor
{
    List<ArticleImage> ExtractImages(string html, string? pageUrl);
}