using System.Globalization;

namespace ReadableWeb.Abstractions;

public interface IContentExtractor
{
    (string TextContent, string Excerpt) ExtractMainContent<T>(T doc, CultureInfo culture);
}