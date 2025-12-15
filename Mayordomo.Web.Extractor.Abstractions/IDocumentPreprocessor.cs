
namespace Mayordomo.Web.Extractor.Abstractions;

public interface IDocumentPreprocessor
{
    void Prepare<TDoc>(TDoc doc);
}

