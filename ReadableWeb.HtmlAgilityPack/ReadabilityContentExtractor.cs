using System.Globalization;
using HtmlAgilityPack;
using ReadableWeb.Abstractions;

namespace ReadableWeb.HtmlAgilityPack;

public class ReadabilityContentExtractor : IContentExtractor
{
    private readonly string[] _positiveSignals =
    {
        "article", "body", "content", "entry", "hentry",
        "main", "page", "pagination", "post", "text",
        "blog", "story"
    };

    private readonly string[] _negativeSignals =
    {
        "comment", "combx", "contact", "footer", "foot",
        "footnote", "meta", "nav", "navbar", "rss",
        "shoutbox", "sidebar", "sponsor", "shopping",
        "tags", "tool", "widget", "promo", "related",
        "social", "sharing", "share", "subscribe", "ad",
        "advert", "banner"
    };

    private class Candidate
    {
        public HtmlNode Node { get; set; } = null!;
        public double Score { get; set; }
    }

    public (string TextContent, string Excerpt) ExtractMainContent<T>(T doc, CultureInfo culture)
    {
        if (doc is string html)
            return ExtractMainContentInternal(GetHtmlDocument(html), culture);

        if (doc is HtmlDocument htmlDocument)
            return ExtractMainContentInternal(htmlDocument, culture);

        throw new ArgumentException("Document must be of type HtmlDocument", nameof(doc));
    }


    private static HtmlDocument GetHtmlDocument(string html)
    {
        var value = new HtmlDocument
        {
            OptionFixNestedTags = true,
            OptionCheckSyntax = false
        };
        value.LoadHtml(html);
        return value;
    }
    private (string TextContent, string Excerpt) ExtractMainContentInternal(HtmlDocument doc, CultureInfo culture)
    {
        var topCandidate = FindTopCandidate(doc, out var candidates);
        var articleAncestor = topCandidate?
            .AncestorsAndSelf()
            .FirstOrDefault(n => n.Name.Equals("article", StringComparison.OrdinalIgnoreCase));

        HtmlNode articleNode;
        if (articleAncestor != null)
        {
            articleNode = articleAncestor.Clone();
        }
        else if (topCandidate == null)
        {
            articleNode = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;
        }
        else
        {
            articleNode = BuildArticleNode(topCandidate, candidates);
        }

        CleanupArticleNode(articleNode);

        var decodedText = HtmlEntity.DeEntitize(articleNode.InnerText);
        var textContent = ExtractionUtils.NormalizeWhitespace(decodedText);
        var excerpt = ExtractionUtils.BuildExcerpt(textContent, culture);

        return (textContent, excerpt);
    }

    private HtmlNode? FindTopCandidate(HtmlDocument doc, out Dictionary<HtmlNode, Candidate> candidates)
    {
        candidates = new Dictionary<HtmlNode, Candidate>();

        var nodes = doc.DocumentNode
                        .SelectNodes("//p|//pre|//td")
                    ?? new HtmlNodeCollection(null);

        foreach (var node in nodes)
        {
            var innerText = ExtractionUtils.GetInnerText(node);
            if (innerText.Length < 15)
                continue;

            var parent = node.ParentNode;
            var grandParent = parent?.ParentNode;
            if (parent == null) continue;

            var contentScore = 0d;

            contentScore += 1;
            contentScore += innerText.Count(c => c == ',' || c == '，');
            contentScore += Math.Min(Math.Floor(innerText.Length / 100d), 3);

            void InitializeNode(HtmlNode n, Dictionary<HtmlNode, Candidate> dictionary)
            {
                if (!dictionary.ContainsKey(n))
                {
                    dictionary[n] = new Candidate
                    {
                        Node = n,
                        Score = ClassWeight(n)
                    };
                }
            }

            InitializeNode(parent, candidates);
            candidates[parent].Score += contentScore;

            if (grandParent != null)
            {
                InitializeNode(grandParent, candidates);
                candidates[grandParent].Score += contentScore / 2.0;
            }
        }

        foreach (var c in candidates.Values)
        {
            double ld = ExtractionUtils.LinkDensity(c.Node);
            c.Score *= (1.0 - ld);
        }

        var top = candidates.Values
            .OrderByDescending(c => c.Score)
            .FirstOrDefault();

        return top?.Node;
    }

    private double ClassWeight(HtmlNode node)
    {
        double weight = 0;

        string classAndId =
            (node.GetAttributeValue("class", "") + " " +
             node.GetAttributeValue("id", ""))
            .ToLowerInvariant();

        foreach (var neg in _negativeSignals)
        {
            if (classAndId.Contains(neg))
                weight -= 25;
        }

        foreach (var pos in _positiveSignals)
        {
            if (classAndId.Contains(pos))
                weight += 25;
        }

        if (node.Name.Equals("article", StringComparison.OrdinalIgnoreCase))
            weight += 25;

        if (node.Name.Equals("section", StringComparison.OrdinalIgnoreCase))
            weight += 5;

        if (node.Name.Equals("div", StringComparison.OrdinalIgnoreCase))
            weight += 5;

        return weight;
    }

    private HtmlNode BuildArticleNode(HtmlNode topCandidate, Dictionary<HtmlNode, Candidate> candidates)
    {
        var parent = topCandidate.ParentNode ?? topCandidate;
        var output = HtmlNode.CreateNode("<div id='readability-content'></div>");

        double topScore = candidates.TryGetValue(topCandidate, out var topCand)
            ? topCand.Score
            : 0;

        double siblingScoreThreshold = Math.Max(10, topScore * 0.2);

        foreach (var sibling in parent.ChildNodes.ToList())
        {
            bool append = false;

            if (sibling == topCandidate)
            {
                append = true;
            }
            else if (candidates.TryGetValue(sibling, out var siblingCandidate) &&
                     siblingCandidate.Score >= siblingScoreThreshold)
            {
                append = true;
            }
            else if (sibling.Name.Equals("p", StringComparison.OrdinalIgnoreCase))
            {
                string txt = ExtractionUtils.GetInnerText(sibling);
                double ld = ExtractionUtils.LinkDensity(sibling);
                if (txt.Length > 80 && ld < 0.25)
                    append = true;
                else if (txt.Length > 0 && ld == 0)
                    append = true;
            }

            if (!append) continue;

            var clone = sibling.Clone();
            output.AppendChild(clone);
        }

        return output;
    }

    private void CleanupArticleNode(HtmlNode articleNode)
    {
        foreach (var n in articleNode.DescendantsAndSelf()
                     .Where(n => n.NodeType == HtmlNodeType.Element))
        {
            n.Attributes.Remove("style");
        }

        foreach (var junk in articleNode.SelectNodes(".//form|.//iframe|.//object|.//embed|.//nav|.//aside")
                             ?? Enumerable.Empty<HtmlNode>())
        {
            junk.Remove();
        }

        foreach (var el in articleNode.Descendants()
                     .Where(n => n.NodeType == HtmlNodeType.Element)
                     .ToList())
        {
            string text = ExtractionUtils.NormalizeWhitespace(el.InnerText);
            double ld = ExtractionUtils.LinkDensity(el);

            bool isParagraphLike = el.Name.Equals("p", StringComparison.OrdinalIgnoreCase) ||
                                   el.Name.Equals("div", StringComparison.OrdinalIgnoreCase) ||
                                   el.Name.Equals("section", StringComparison.OrdinalIgnoreCase);

            if (isParagraphLike)
            {
                if (text.Length < 25 && el.SelectNodes(".//img|.//embed|.//object") == null)
                {
                    el.Remove();
                    continue;
                }

                if (ld > 0.5)
                {
                    el.Remove();
                    continue;
                }
            }
        }
    }
}
