using System.Globalization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Mayordomo.Web.Extractor.Abstractions;

namespace Mayordomo.Web.Extractor.HtmlAgilityPack;

public class DefaultMetadataExtractor : IMetadataExtractor
{
    private string ExtractTitle(HtmlDocument doc)
    {
        string title = "";

        var titleNode = doc.DocumentNode.SelectSingleNode("//title");
        if (titleNode != null)
            title = ExtractionUtils.NormalizeWhitespace(titleNode.InnerText);

        var ogTitle = doc.DocumentNode
            .SelectSingleNode("//meta[@property='og:title' or @name='og:title']")
            ?.GetAttributeValue("content", null);

        if (!string.IsNullOrWhiteSpace(ogTitle))
        {
            var normOg = ExtractionUtils.NormalizeWhitespace(ogTitle);
            if (normOg.Length > 0 && Math.Abs(normOg.Length - title.Length) > 10)
                title = normOg;
        }

        return title;
    }

    private string ExtractSiteNameInternal(HtmlDocument doc)
    {
        var ogSiteName = doc.DocumentNode
            .SelectSingleNode("//meta[@property='og:site_name']")
            ?.GetAttributeValue("content", null);

        if (!string.IsNullOrWhiteSpace(ogSiteName))
            return ExtractionUtils.NormalizeWhitespace(ogSiteName);

        return "";
    }

    private string ExtractAuthorInternal(HtmlDocument doc)
    {
        var authorMetaPaths = new[]
        {
            "//meta[@name='author']",
            "//meta[@property='article:author']",
            "//meta[@property='og:article:author']",
            "//meta[@name='byl']",
            "//meta[@name='dc.creator']",
            "//meta[@itemprop='author']"
        };

        foreach (var path in authorMetaPaths)
        {
            var node = doc.DocumentNode.SelectSingleNode(path);
            var val = node?.GetAttributeValue("content", null);
            if (!string.IsNullOrWhiteSpace(val))
                return ExtractionUtils.NormalizeWhitespace(val);
        }

        var bylineNodes = doc.DocumentNode
                              .SelectNodes("//*[contains(translate(@class, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'), 'byline') or contains(@id, 'byline')]")
                          ?? Enumerable.Empty<HtmlNode>();

        foreach (var node in bylineNodes)
        {
            var txt = ExtractionUtils.GetInnerText(node);
            if (txt.Length > 3 && txt.Length < 200)
            {
                var cleaned = Regex.Replace(txt, @"^\s*by\s+", "", RegexOptions.IgnoreCase);
                return cleaned.Trim();
            }
        }

        var bodyText = ExtractionUtils.GetInnerText(doc.DocumentNode);
        var match = Regex.Match(bodyText, @"by\s+([A-Z][a-z]+(?:\s+[A-Z][a-z]+)+)");
        if (match.Success)
            return match.Groups[1].Value.Trim();

        return "";
    }

    private (DateTime? Published, DateTime? Modified) ExtractDatesInternal(HtmlDocument doc, CultureInfo culture)
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
            foreach (var node in doc.DocumentNode.SelectNodes($"//meta[@name='{name}' or @property='{name}']")
                                 ?? Enumerable.Empty<HtmlNode>())
            {
                var val = node.GetAttributeValue("content", null);
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

        var timeNodes = doc.DocumentNode
                            .SelectNodes("//time|//*[contains(@class,'date') or contains(@id,'date')]")
                        ?? Enumerable.Empty<HtmlNode>();

        foreach (var node in timeNodes)
        {
            var datetimeAttr = node.GetAttributeValue("datetime", null);
            var text = ExtractionUtils.GetInnerText(node);

            var parsed = ParseDate(datetimeAttr, culture) ?? ParseDate(text, culture);
            if (parsed == null) continue;

            string classAttr = node.GetAttributeValue("class", "").ToLowerInvariant();
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
            string html => ExtractTitle(GetHtmlDocument<T>(html)),
            HtmlDocument htmlDocument => ExtractTitle(htmlDocument),
            _ => throw new ArgumentException("Invalid document type", nameof(doc))
        };
    }

    public string ExtractSiteName<T>(T doc)
    {
        return doc switch
        {
            string html => ExtractSiteNameInternal(GetHtmlDocument<T>(html)),
            HtmlDocument htmlDocument => ExtractSiteNameInternal(htmlDocument),
            _ => throw new ArgumentException("Invalid document type", nameof(doc))
        };
    }

    public string ExtractAuthor<T>(T doc)
    {
        return doc switch
        {
            string html => ExtractAuthorInternal(GetHtmlDocument<T>(html)),
            HtmlDocument htmlDocument => ExtractAuthorInternal(htmlDocument),
            _ => throw new ArgumentException("Invalid document type", nameof(doc))
        };
    }

    public (DateTime? Published, DateTime? Modified) ExtractDates<T>(T doc, CultureInfo culture)
    {
        return doc switch
        {
            string html => ExtractDatesInternal(GetHtmlDocument<T>(html), culture),
            HtmlDocument htmlDocument => ExtractDatesInternal(htmlDocument, culture),
            _ => throw new ArgumentException("Invalid document type", nameof(doc))
        };
    }

    private static HtmlDocument GetHtmlDocument<T>(string html)
    {
        var value = new HtmlDocument
        {
            OptionFixNestedTags = true,
            OptionCheckSyntax = false
        };
        value.LoadHtml(html);
        return value;
    }
}
