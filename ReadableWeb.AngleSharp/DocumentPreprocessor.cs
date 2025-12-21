using AngleSharp.Dom;
using AngleSharp;
using Mayordomo.Web.Extractor.Abstractions;

namespace Mayordomo.Web.Extractor.AngleSharp;

public class DocumentPreprocessor : IDocumentPreprocessor
{
    private IHtmlDocument? _document;

    public void Prepare<TDoc>(TDoc doc)
    {
        if (doc is string html)
        {
            var context = BrowsingContext.New(Configuration.Default);
            _document = context.OpenAsync(req => req.Content(html)).Result;
        }
        else if (doc is IHtmlDocument d)
        {
            _document = d;
        }
    }
}
