using System.Globalization;

namespace Mayordomo.Web.Extractor.Abstractions;

public interface ILocaleInferrer
{
    CultureInfo InferCulture<T>(T doc, string? url);
}