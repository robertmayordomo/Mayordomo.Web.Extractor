using System.Globalization;

namespace Mayordomo.Web.Extractor.Abstractions;

public interface IContentExtractor
{
    (string TextContent, string Excerpt) ExtractMainContent<T>(T doc, CultureInfo culture);
}