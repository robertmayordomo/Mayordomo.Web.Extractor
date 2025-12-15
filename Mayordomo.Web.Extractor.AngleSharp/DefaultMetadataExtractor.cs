using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp;
using Mayordomo.Web.Extractor.Abstractions;

namespace Mayordomo.Web.Extractor.AngleSharp;

public class DefaultMetadataExtractor : IMetadataExtractor
{
    private static IHtmlDocument GetHtmlDocument(string html)
    {
        var context = BrowsingContext.New(Configuration.Default);
        return context.OpenAsync(req => req.Content(html)).Result;
    }

    private string ExtractTitle(IHtmlDocument doc)
    {
        string title = "";

        var titleNode = doc.QuerySelector("title");
        if (titleNode != null)
            title = ExtractionUtils.NormalizeWhitespace(titleNode.TextContent);

        var ogTitle = doc.QuerySelector("meta[property='og:title'],meta[name='og:title']")
            ?.GetAttribute("content");

        if (!string.IsNullOrWhiteSpace(ogTitle))
        {
            var normOg = ExtractionUtils.NormalizeWhitespace(ogTitle);
            if (normOg.Length > 0 && Math.Abs(normOg.Length - title.Length) > 10)
                title = normOg;
        }

        return title;
    }

    private string ExtractSiteNameInternal(IHtmlDocument doc)
    {
        var ogSiteName = doc.QuerySelector("meta[property='og:site_name']")
            ?.GetAttribute("content");

        if (!string.IsNullOrWhiteSpace(ogSiteName))
            return ExtractionUtils.NormalizeWhitespace(ogSiteName);

        return "";
    }

    private string ExtractAuthorInternal(IHtmlDocument doc)
    {
        var authorMetaPaths = new[]
        {
            "meta[name='author']",
            "meta[property='article:author']",
            "meta[property='og:article:author']",
            "meta[name='byl']",
            "meta[name='dc.creator']",
            "meta[itemprop='author']"
        };

        foreach (var path in authorMetaPaths)
        {
            var node = doc.QuerySelector(path);
            var val = node?.GetAttribute("content");
            if (!string.IsNullOrWhiteSpace(val))
                return ExtractionUtils.NormalizeWhitespace(val);
        }

        var bylineNodes = doc.QuerySelectorAll("*[class*='byline'],*[id*='byline']");

        foreach (var node in bylineNodes.OfType<IElement>())
        {
            var txt = ExtractionUtils.GetInnerText(node);
            if (txt.Length > 3 && txt.Length < 200)
            {
                var cleaned = Regex.Replace(txt, @"^\s*by\s+", "", RegexOptions.IgnoreCase);
                return cleaned.Trim();
            }
        }

        var bodyText = ExtractionUtils.GetInnerText(doc.DocumentElement!);
        var match = Regex.Match(bodyText, @"by\s+([A-Z][a-z]+(?:\s+[A-Z][a-z]+)+)");
        if (match.Success)
            return match.Groups[1].Value.Trim();

        return "";
    }

    private (DateTime? Published, DateTime? Modified) ExtractDatesInternal(IHtmlDocument doc, CultureInfo culture)
    {
        DateTime? published = null;
        DateTime? modified = null;

        static DateTime? ParseDate(string? value, CultureInfo culture)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces, out var dt))
                return dt.ToUniversalTime();

            if (DateTime.TryParse(value, culture,
                    DateTimeStyles.AllowWhiteSpaces, out dt))
                return dt;

            if (long.TryParse(value, out var epoch))
            {
                try
                {
                    if (epoch > 1000000000000) epoch /= 1000;
                    return DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
                }
                catch { }
            }

            var normalized = value.Replace("de ", "").Replace("le ", "").Trim();
            if (DateTime.TryParse(normalized, culture, DateTimeStyles.AllowWhiteSpaces, out dt))
                return dt;

            return null;
        }

        var metaNames = new[]
        {
            "article:published_time",
            "article:modified_time",
            "og:published_time",
            "og:updated_time",
            "publish_date",
            "pubdate",
            "timestamp",
            "date",
            "datePublished",
            "dateModified",
            "dc.date",
            "dc.date.issued",
            "dc.date.modified"
        };

        foreach (var name in metaNames)
        {
            foreach (var node in doc.QuerySelectorAll($"meta[name='{name}'],meta[property='{name}']"))
            {
                var val = (node as IElement)?.GetAttribute("content");
                var parsed = ParseDate(val, culture);

                if (parsed != null)
                {
                    if (name.Contains("modified", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("updated", StringComparison.OrdinalIgnoreCase))
                        modified ??= parsed;
                    else
                        published ??= parsed;
                }
            }
        }

        var timeNodes = doc.QuerySelectorAll("time, *[class*='date'], *[id*='date']");

        foreach (var node in timeNodes.OfType<IElement>())
        {
            var datetimeAttr = node.GetAttribute("datetime");
            var text = ExtractionUtils.GetInnerText(node);

            var parsed = ParseDate(datetimeAttr, culture) ?? ParseDate(text, culture);
            if (parsed == null) continue;

            string classAttr = (node.GetAttribute("class") ?? "").ToLowerInvariant();
            if (classAttr.Contains("mod") || classAttr.Contains("update"))
                modified ??= parsed;
            else
                published ??= parsed;
        }

        return (published, modified);
    }

    public string ExtractTitle<T>(T doc)
    {
        return doc switch
        {
            string html => ExtractTitle(GetHtmlDocument(html)),
            IHtmlDocument htmlDocument => ExtractTitle(htmlDocument),
            _ => throw new ArgumentException("Invalid document type", nameof(doc))
        };
    }

    public string ExtractSiteName<T>(T doc)
    {
        return doc switch
        {
            string html => ExtractSiteNameInternal(GetHtmlDocument(html)),
            IHtmlDocument htmlDocument => ExtractSiteNameInternal(htmlDocument),
            _ => throw new ArgumentException("Invalid document type", nameof(doc))
        };
    }

    public string ExtractAuthor<T>(T doc)
    {
        return doc switch
        {
            string html => ExtractAuthorInternal(GetHtmlDocument(html)),
            IHtmlDocument htmlDocument => ExtractAuthorInternal(htmlDocument),
            _ => throw new ArgumentException("Invalid document type", nameof(doc))
        };
    }

    public (DateTime? Published, DateTime? Modified) ExtractDates<T>(T doc, CultureInfo culture)
    {
        return doc switch
        {
            string html => ExtractDatesInternal(GetHtmlDocument(html), culture),
            IHtmlDocument htmlDocument => ExtractDatesInternal(htmlDocument, culture),
            _ => throw new ArgumentException("Invalid document type", nameof(doc))
        };
    }
}
