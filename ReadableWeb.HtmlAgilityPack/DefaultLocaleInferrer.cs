using System.Collections.Concurrent;
using System.Globalization;
using HtmlAgilityPack;
using ReadableWeb.Abstractions;

namespace ReadableWeb.HtmlAgilityPack;

public class DefaultLocaleInferrer : ILocaleInferrer
{
    private static readonly ConcurrentDictionary<string, CultureInfo> HostCultureCache =
        new(StringComparer.OrdinalIgnoreCase);

    public CultureInfo InferCulture(HtmlDocument doc, string? url)
    {
        string? host = null;
        if (!string.IsNullOrWhiteSpace(url))
        {
            try { host = new Uri(url).Host.ToLowerInvariant(); }
            catch { }
        }

        CultureInfo? cachedHostCulture = null;
        if (host != null)
        {
            HostCultureCache.TryGetValue(host, out cachedHostCulture);
        }

        string? lang = null;

        var htmlNode = doc.DocumentNode.SelectSingleNode("//html");
        if (htmlNode != null)
        {
            lang = htmlNode.GetAttributeValue("lang", null) ??
                   htmlNode.GetAttributeValue("xml:lang", null);
        }

        if (string.IsNullOrWhiteSpace(lang))
        {
            var metaLang = doc.DocumentNode.SelectSingleNode("//meta[@http-equiv='content-language' or @name='language']");
            lang = metaLang?.GetAttributeValue("content", null);
        }

        if (!string.IsNullOrWhiteSpace(lang))
        {
            try
            {
                lang = lang.Trim();
                if (lang!.Length == 2)
                {
                    lang = lang switch
                    {
                        "en" => "en-US",
                        "fr" => "fr-FR",
                        "de" => "de-DE",
                        "es" => "es-ES",
                        "pt" => "pt-PT",
                        _ => $"{lang}-{lang.ToUpperInvariant()}"
                    };
                }

                var culture = new CultureInfo(lang);
                if (host != null)
                    HostCultureCache[host] = culture;

                return culture;
            }
            catch
            {
            }
        }

        if (cachedHostCulture != null)
            return cachedHostCulture;

        CultureInfo? tldCulture = null;
        if (!string.IsNullOrWhiteSpace(host))
        {
            var tldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {".fr", "fr-FR"},
                {".de", "de-DE"},
                {".es", "es-ES"},
                {".it", "it-IT"},
                {".nl", "nl-NL"},
                {".no", "no-NO"},
                {".se", "sv-SE"},
                {".dk", "da-DK"},
                {".pl", "pl-PL"},
                {".pt", "pt-PT"},
                {".br", "pt-BR"},
                {".ru", "ru-RU"},
                {".cn", "zh-CN"},
                {".jp", "ja-JP"},
                {".kr", "ko-KR"},
                {".co.uk", "en-GB"},
                {".uk", "en-GB"},
                {".ca", "en-CA"},
                {".au", "en-AU"}
            };

            foreach (var kv in tldMap)
            {
                if (host.EndsWith(kv.Key, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        tldCulture = new CultureInfo(kv.Value);
                    }
                    catch { }
                    break;
                }
            }
        }

        if (tldCulture != null && host != null)
        {
            HostCultureCache[host] = tldCulture;
            return tldCulture;
        }

        var fallback = CultureInfo.GetCultureInfo("en-US");
        if (host != null)
            HostCultureCache.TryAdd(host, fallback);

        return fallback;
    }

    public CultureInfo InferCulture<T>(T doc, string? url)
    {
        return doc switch
        {
            string html => InferCulture(GetHtmlDocument<T>(html), url),
            HtmlDocument htmlDocument => InferCulture(htmlDocument, url),
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
