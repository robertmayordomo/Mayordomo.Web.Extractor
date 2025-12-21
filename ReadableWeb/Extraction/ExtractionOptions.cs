using System.Globalization;

namespace ReadableWeb.Extraction;

/// <summary>
/// Per-extraction options (optional URL and culture override).
/// </summary>
public class ExtractionOptions
{
    public string? Url { get; set; }
    public CultureInfo? CultureOverride { get; set; }
}