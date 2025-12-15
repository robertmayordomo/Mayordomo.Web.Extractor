using HtmlAgilityPack;
using Mayordomo.Web.Extractor.Abstractions;

namespace Mayordomo.Web.Extractor.HtmlAgilityPack;

public class DocumentPreprocessor : IDocumentPreprocessor
{
    private static readonly string[] DivToPTags = ["a","blockquote","dl","div","img","ol","p","pre","table","ul","li"];

    public void Prepare<TDoc>(TDoc doc)
    {
        switch (doc)
        {
            case HtmlDocument htmlDoc:
                PrepareInternal(htmlDoc);
                return;
            case string html:
                PrepareInternal(GetHtmlDocument<HtmlDocument>(html));
                return;
            default:
                throw new ArgumentException("Invalid document type", nameof(doc));
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

    public void PrepareInternal(HtmlDocument doc)
    {
        var body = doc.DocumentNode.SelectSingleNode("//body");
        if (body == null) return;

        foreach (var node in doc.DocumentNode
                     .SelectNodes("//script|//style|//noscript") ?? Enumerable.Empty<HtmlNode>())
        {
            node.Remove();
        }

        foreach (var comment in doc.DocumentNode
                     .DescendantsAndSelf()
                     .Where(n => n.NodeType == HtmlNodeType.Comment)
                     .ToList())
        {
            comment.Remove();
        }

        foreach (var div in body.SelectNodes(".//div") ?? Enumerable.Empty<HtmlNode>())
        {
            if (ShouldConvertDivToP(div))
            {
                div.Name = "p";
            }
        }
    }

    private static bool ShouldConvertDivToP(HtmlNode div)
    {
        bool hasBlock = div.ChildNodes
            .Any(n => n.NodeType == HtmlNodeType.Element &&
                      DivToPTags.Contains(n.Name, System.StringComparer.OrdinalIgnoreCase));

        if (!hasBlock) return true;

        var childElements = div.ChildNodes.Where(n => n.NodeType == HtmlNodeType.Element).ToList();
        if (childElements.All(n => n.Name.Equals("br", System.StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }
}