using System.Globalization;

namespace Mayordomo.Web.Extractor.Abstractions.Models;

public class ArticleContent
{
    public string Title { get; set; } = "";
    public string TextContent { get; set; } = "";
    public string Excerpt { get; set; } = "";
    public string SiteName { get; set; } = "";
    public string Url { get; set; } = "";
    public string Author { get; set; } = "";
    public DateTime? PublishedTime { get; set; }
    public DateTime? ModifiedTime { get; set; }
    public CultureInfo? DetectedCulture { get; set; }
    public List<ArticleImage> Images { get; set; } = new();
}