namespace Mayordomo.Web.Extractor.Abstractions.Models;

public class ArticleImage
{
    public string Url { get; set; } = "";
    public string? Alt { get; set; }
    public string? Caption { get; set; }
    public ArticleImageRole Role { get; set; } = ArticleImageRole.Unknown;
    public int? Width { get; set; }
    public int? Height { get; set; }

    /// <summary>Absolute path on disk where this image is cached (if enabled).</summary>
    public string? LocalPath { get; set; }

    /// <summary>Public URL corresponding to the cached file (optional).</summary>
    public string? LocalUrl { get; set; }

    public List<ArticleImageVariant> Variants { get; set; } = new();
}