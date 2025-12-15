using System.Globalization;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Mayordomo.Web.Extractor.Abstractions;
using Mayordomo.Web.Extractor.HtmlAgilityPack;

namespace Mayordomo.Web.Extractor.AngleSharp;

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
        public IElement Node { get; set; } = null!;
        public double Score { get; set; }
    }

    public (string TextContent, string Excerpt) ExtractMainContent<T>(T doc, CultureInfo culture)
    {
        if (doc is string html)
            return ExtractMainContentInternal(ParseHtml(html).Result, culture);

        if (doc is IHtmlDocument htmlDocument)
            return ExtractMainContentInternal(htmlDocument, culture);

        throw new ArgumentException("Document must be of type IHtmlDocument", nameof(doc));
    }

    private static async Task<IDocument> ParseHtml(string html)
    {
        var context = BrowsingContext.New();
        return await context.OpenAsync(req => req.Content(html));
    }

    private (string TextContent, string Excerpt) ExtractMainContentInternal(IDocument doc, CultureInfo culture)
    {
        var topCandidate = FindTopCandidate(doc, out var candidates);

        IElement articleNode;
        if (topCandidate == null)
        {
            articleNode = doc.QuerySelector("body") ?? doc.DocumentElement!;
        }
        else
        {
            articleNode = BuildArticleNode(topCandidate, candidates);
        }

        CleanupArticleNode(articleNode);

        var textContent = ExtractionUtils.NormalizeWhitespace(articleNode.TextContent);
        var excerpt = ExtractionUtils.BuildExcerpt(textContent, culture);

        return (textContent, excerpt);
    }

    private IElement? FindTopCandidate(IDocument doc, out Dictionary<IElement, Candidate> candidates)
    {
        candidates = new Dictionary<IElement, Candidate>(ReferenceEqualityComparer<IElement>.Default);

        var nodes = doc.QuerySelectorAll("p,pre,td");

        foreach (var node in nodes)
        {
            var innerText = ExtractionUtils.GetInnerText(node);
            if (innerText.Length < 25)
                continue;

            var parent = node.ParentElement;
            var grandParent = parent?.ParentElement;
            if (parent == null) continue;

            var contentScore = 0d;

            contentScore += 1;
            contentScore += innerText.Count(c => c == ',' || c == '?');
            contentScore += Math.Min(Math.Floor(innerText.Length / 100d), 3);

            void InitializeNode(IElement n, Dictionary<IElement, Candidate> dictionary)
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

    private double ClassWeight(IElement node)
    {
        double weight = 0;

        string classAndId =
            (node.ClassName + " " + (node.GetAttribute("id") ?? "")).ToLowerInvariant();

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

        if (node.TagName.Equals("article", StringComparison.OrdinalIgnoreCase))
            weight += 25;

        if (node.TagName.Equals("section", StringComparison.OrdinalIgnoreCase))
            weight += 5;

        if (node.TagName.Equals("div", StringComparison.OrdinalIgnoreCase))
            weight += 5;

        return weight;
    }

    private IElement BuildArticleNode(IElement topCandidate, Dictionary<IElement, Candidate> candidates)
    {
        var parent = topCandidate.ParentElement ?? topCandidate;
        var output = parent.Owner!.CreateElement("div");
        output.SetAttribute("id", "readability-content");

        double topScore = candidates.TryGetValue(topCandidate, out var topCand)
            ? topCand.Score
            : 0;

        double siblingScoreThreshold = Math.Max(10, topScore * 0.2);

        foreach (var sibling in parent.Children.ToList())
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
            else if (sibling.TagName.Equals("p", StringComparison.OrdinalIgnoreCase))
            {
                string txt = ExtractionUtils.GetInnerText(sibling);
                double ld = ExtractionUtils.LinkDensity(sibling);
                if (txt.Length > 80 && ld < 0.25)
                    append = true;
                else if (txt.Length > 0 && ld == 0)
                    append = true;
            }

            if (!append) continue;

            var clone = sibling.Clone(true) as IElement;
            if (clone != null) output.AppendChild(clone);
        }

        return output;
    }

    private void CleanupArticleNode(IElement articleNode)
    {
        foreach (var n in articleNode.QuerySelectorAll("*").OfType<IElement>())
        {
            n.RemoveAttribute("style");
        }

        foreach (var junk in articleNode.QuerySelectorAll("form,iframe,object,embed,nav,aside").ToList())
        {
            junk.Remove();
        }

        foreach (var el in articleNode.QuerySelectorAll("*").OfType<IElement>().ToList())
        {
            string text = ExtractionUtils.NormalizeWhitespace(el.TextContent);
            double ld = ExtractionUtils.LinkDensity(el);

            bool isParagraphLike = string.Equals(el.TagName, "p", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(el.TagName, "div", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(el.TagName, "section", StringComparison.OrdinalIgnoreCase);

            if (isParagraphLike)
            {
                if (text.Length < 25 && !el.QuerySelector("img,embed,object").Any())
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
