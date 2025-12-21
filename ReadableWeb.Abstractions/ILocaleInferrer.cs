using System.Globalization;

namespace ReadableWeb.Abstractions;

public interface ILocaleInferrer
{
    CultureInfo InferCulture<T>(T doc, string? url);
}