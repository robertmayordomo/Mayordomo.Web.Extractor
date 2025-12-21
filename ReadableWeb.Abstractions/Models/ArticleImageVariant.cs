namespace ReadableWeb.Abstractions.Models;

public class ArticleImageVariant
{
    public string Url { get; set; } = "";
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? MimeType { get; set; }
    public ArticleImageRole Role { get; set; } = ArticleImageRole.Unknown;
    public string? LocalPath { get; set; }
    public string? LocalUrl { get; set; }
}