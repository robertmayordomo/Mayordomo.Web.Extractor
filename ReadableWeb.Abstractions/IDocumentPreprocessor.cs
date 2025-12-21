
namespace ReadableWeb.Abstractions;

public interface IDocumentPreprocessor
{
    void Prepare<TDoc>(TDoc doc);
}

