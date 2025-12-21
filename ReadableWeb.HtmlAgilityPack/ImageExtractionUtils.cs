using System.Text.RegularExpressions;
using HtmlAgilityPack;
using ReadableWeb.Abstractions;
using ReadableWeb.Abstractions.Models;

namespace ReadableWeb.HtmlAgilityPack;

public class ImageProcessor : IImageProcessor
{
    private static readonly Regex SrcSetItemRegex =
        new(@"(?<url>\S+)\s+(?<width>\d+)w", RegexOptions.Compiled);

    public List<ArticleImage> ExtractImages(string html, string? pageUrl) 
        => ExtractImages(GetHtmlDocument<HtmlDocument>(html));

    private List<ArticleImage> ExtractImages(HtmlDocument doc)
    {
        var result = new Dictionary<string, ArticleImage>(StringComparer.OrdinalIgnoreCase);

        foreach (var img in doc.DocumentNode.SelectNodes(".//img"))
        {
            AddInlineImage(img, result);
        }

        AddMetaImages(doc, result);
        AddJsonLdImages(doc, result);

        return result.Values.ToList();
    }

    private static void AddInlineImage(HtmlNode img, IDictionary<string, ArticleImage> result)
    {
        string? src =
            img.GetAttributeValue("data-src", null) ??
            img.GetAttributeValue("data-original", null) ??
            img.GetAttributeValue("data-lazy-src", null) ??
            img.GetAttributeValue("src", null);

        if (string.IsNullOrWhiteSpace(src))
            return;

        src = src.Trim();

        string? alt = img.GetAttributeValue("alt", null);
        string? widthAttr = img.GetAttributeValue("width", null);
        string? heightAttr = img.GetAttributeValue("height", null);

        int? width = TryParseInt(widthAttr);
        int? height = TryParseInt(heightAttr);

        string? caption = null;
        var figure = img.Ancestors("figure").FirstOrDefault();
        if (figure != null)
        {
            var captionNode = figure.SelectSingleNode(".//figcaption");
            if (captionNode != null)
            {
                caption = ExtractionUtils.NormalizeWhitespace(captionNode.InnerText);
                if (caption == "") caption = null;
            }
        }

        var variants = new List<ArticleImageVariant>();
        var srcset = img.GetAttributeValue("srcset", null);
        if (!string.IsNullOrWhiteSpace(srcset))
        {
            variants.AddRange(ParseSrcset(srcset, ArticleImageRole.SrcsetVariant));
        }

        if (figure != null)
        {
            foreach (var source in figure.SelectNodes(".//source") ?? Enumerable.Empty<HtmlNode>())
            {
                AddSourceVariants(source, variants);
            }
        }
        else
        {
            foreach (var source in img.ParentNode.SelectNodes(".//source") ?? Enumerable.Empty<HtmlNode>())
            {
                AddSourceVariants(source, variants);
            }
        }

        if (!result.TryGetValue(src, out var image))
        {
            image = new ArticleImage
            {
                Url = src,
                Alt = alt,
                Caption = caption,
                Role = ArticleImageRole.Inline,
                Width = width,
                Height = height,
                Variants = new List<ArticleImageVariant>()
            };
            result[src] = image;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(image.Alt)) image.Alt = alt;
            if (string.IsNullOrWhiteSpace(image.Caption)) image.Caption = caption;
            if (image.Width == null) image.Width = width;
            if (image.Height == null) image.Height = height;
        }

        MergeVariants(image.Variants, variants);
    }

    private static IEnumerable<ArticleImageVariant> ParseSrcset(string srcset, ArticleImageRole role)
    {
        var variants = new List<ArticleImageVariant>();
        var parts = srcset.Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (var raw in parts)
        {
            var item = raw.Trim();
            var match = SrcSetItemRegex.Match(item);
            if (!match.Success)
            {
                var tokens = item.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length > 0)
                {
                    variants.Add(new ArticleImageVariant
                    {
                        Url = tokens[0],
                        Width = null,
                        Height = null,
                        MimeType = null,
                        Role = role
                    });
                }
                continue;
            }

            string url = match.Groups["url"].Value;
            int? width = int.TryParse(match.Groups["width"].Value, out var w) ? w : (int?)null;

            variants.Add(new ArticleImageVariant
            {
                Url = url,
                Width = width,
                Height = null,
                MimeType = null,
                Role = role
            });
        }

        return variants;
    }

    private static void AddSourceVariants(HtmlNode source, List<ArticleImageVariant> variants)
    {
        var srcset = source.GetAttributeValue("srcset", null);
        if (string.IsNullOrWhiteSpace(srcset)) return;

        var mime = source.GetAttributeValue("type", null);
        var srcVariants = ParseSrcset(srcset, ArticleImageRole.SourceVariant).ToList();

        if (!string.IsNullOrWhiteSpace(mime))
        {
            foreach (var v in srcVariants)
                v.MimeType = mime;
        }

        variants.AddRange(srcVariants);
    }

    private static void AddMetaImages(HtmlDocument doc, IDictionary<string, ArticleImage> result)
    {
        var metaProps = new[]
        {
            "og:image",
            "og:image:url",
            "og:image:secure_url",
            "twitter:image",
            "twitter:image:src"
        };

        foreach (var prop in metaProps)
        {
            var nodes = doc.DocumentNode.SelectNodes($"//meta[@property='{prop}' or @name='{prop}']")
                        ?? Enumerable.Empty<HtmlNode>();

            foreach (var node in nodes)
            {
                var url = node.GetAttributeValue("content", null);
                if (string.IsNullOrWhiteSpace(url))
                    continue;

                url = url.Trim();

                var variantRole = prop.StartsWith("og:", StringComparison.OrdinalIgnoreCase)
                    ? ArticleImageRole.OpenGraph
                    : ArticleImageRole.TwitterCard;

                if (!result.TryGetValue(url, out var image))
                {
                    image = new ArticleImage
                    {
                        Url = url,
                        Role = ArticleImageRole.Social,
                        Variants = new List<ArticleImageVariant>()
                    };
                    result[url] = image;
                }

                image.Variants.Add(new ArticleImageVariant
                {
                    Url = url,
                    Role = variantRole,
                    MimeType = null
                });
            }
        }
    }

    private static void AddJsonLdImages(HtmlDocument doc, IDictionary<string, ArticleImage> result)
    {
        var scripts = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']")
                      ?? Enumerable.Empty<HtmlNode>();

        foreach (var script in scripts)
        {
            var json = script.InnerText;
            if (string.IsNullOrWhiteSpace(json)) continue;

            var matches = Regex.Matches(json, @"""image""\s*:\s*(\{[^}]*\}|\[[^\]]*\]|""[^""]*"")",
                RegexOptions.IgnoreCase);

            foreach (Match m in matches)
            {
                var chunk = m.Groups[1].Value;

                var urlMatches = Regex.Matches(chunk, @"""url""\s*:\s*""(?<url>[^""]+)""",
                    RegexOptions.IgnoreCase);

                if (urlMatches.Count == 0 && chunk.StartsWith("\"") && chunk.EndsWith("\""))
                {
                    var simpleUrl = chunk.Trim('"');
                    AddJsonLdImageUrl(simpleUrl, result);
                }
                else
                {
                    foreach (Match um in urlMatches)
                    {
                        var url = um.Groups["url"].Value;
                        AddJsonLdImageUrl(url, result);
                    }
                }
            }
        }
    }

    private static void AddJsonLdImageUrl(string url, IDictionary<string, ArticleImage> result)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        url = url.Trim();

        if (!result.TryGetValue(url, out var image))
        {
            image = new ArticleImage
            {
                Url = url,
                Role = ArticleImageRole.JsonLd,
                Variants = new List<ArticleImageVariant>()
            };
            result[url] = image;
        }

        image.Variants.Add(new ArticleImageVariant
        {
            Url = url,
            Role = ArticleImageRole.JsonLd,
            MimeType = null
        });
    }

    private static int? TryParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (int.TryParse(value, out var i)) return i;
        return null;
    }

    private static void MergeVariants(List<ArticleImageVariant> existing, IEnumerable<ArticleImageVariant> incoming)
    {
        foreach (var v in incoming)
        {
            if (existing.Any(e => string.Equals(e.Url, v.Url, StringComparison.OrdinalIgnoreCase)))
                continue;
            existing.Add(v);
        }
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