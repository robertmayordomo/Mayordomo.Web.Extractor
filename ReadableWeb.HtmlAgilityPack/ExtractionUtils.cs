using System.Text;
using HtmlAgilityPack;

namespace ReadableWeb.HtmlAgilityPack;

public static class ExtractionUtils
{
    public static string GetInnerText(HtmlNode node)
    {
        if (node == null) return "";
        string text = HtmlEntity.DeEntitize(node.InnerText ?? "");
        return NormalizeWhitespace(text);
    }

    public static string NormalizeWhitespace(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var sb = new StringBuilder(text.Length);
        bool inWs = false;

        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!inWs)
                {
                    sb.Append(' ');
                    inWs = true;
                }
            }
            else
            {
                sb.Append(c);
                inWs = false;
            }
        }

        return sb.ToString().Trim();
    }

    public static double LinkDensity(HtmlNode node)
    {
        var linkNodes = node.SelectNodes(".//a") ?? new HtmlNodeCollection(null);
        if (linkNodes.Count == 0) return 0.0;

        double textLength = GetInnerText(node).Length;
        if (textLength == 0) return 0.0;

        double linkLength = 0;
        foreach (var a in linkNodes)
            linkLength += GetInnerText(a).Length;

        return linkLength / textLength;
    }

    public static string BuildExcerpt(string textContent, System.Globalization.CultureInfo culture, int maxLen = 200)
    {
        if (string.IsNullOrWhiteSpace(textContent)) return "";

        var sentences = textContent.Split(new[] { '.', '!', '?' },
            System.StringSplitOptions.RemoveEmptyEntries);

        var sb = new StringBuilder();
        foreach (var s in sentences)
        {
            var trimmed = NormalizeWhitespace(s);
            if (trimmed.Length == 0) continue;

            if (sb.Length + trimmed.Length + 2 > maxLen)
                break;

            if (sb.Length > 0) sb.Append(". ");
            sb.Append(trimmed);
        }

        if (sb.Length == 0 && textContent.Length > maxLen)
            return NormalizeWhitespace(textContent.Substring(0, maxLen)) + "…";

        return sb.ToString();
    }
}