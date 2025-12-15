using System.Globalization;

namespace Mayordomo.Web.Extractor.Abstractions;

public interface IMetadataExtractor
{
    string ExtractTitle<T>(T doc);
    string ExtractSiteName<T>(T doc);
    string ExtractAuthor<T>(T doc);
    (DateTime? Published, DateTime? Modified) ExtractDates<T>(T doc, CultureInfo culture);
}