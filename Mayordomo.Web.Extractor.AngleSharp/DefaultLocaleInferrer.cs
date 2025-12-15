using System.Globalization;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Mayordomo.Web.Extractor.Abstractions;

namespace Mayordomo.Web.Extractor.AngleSharp;

public class DefaultLocaleInferrer : ILocaleInferrer
{
    private static IDocument GetHtmlDocument(string html)
    {
        var context = BrowsingContext.New(Configuration.Default);
        return context.OpenAsync(req => req.Content(html)).Result;
    }

    public CultureInfo InferCulture<T>(T doc, string? url)
    {
        IHtmlDocument document = doc switch
        {
            string html => GetHtmlDocument(html),
            IHtmlDocument d => d,
            _ => throw new ArgumentException("Invalid document type", nameof(doc))
        };

        var lang = document.DocumentElement?.GetAttribute("lang");
        if (!string.IsNullOrWhiteSpace(lang))
        {
            try
            {
                return new CultureInfo(lang);
            }
            catch { }
        }

        if (!string.IsNullOrWhiteSpace(url))
        {
            try
            {
                var host = new Uri(url).Host;
                var parts = host.Split('.');
                var tld = parts.LastOrDefault() ?? "";
                return new CultureInfo(tld);
            }
            catch { }
        }

        return CultureInfo.InvariantCulture;
    }
}
