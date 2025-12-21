namespace ReadableWeb.Abstractions.Models;

public enum ArticleImageRole
{
    Unknown = 0,
    Inline = 1,
    Lead = 2,
    Social = 3,
    Meta = 4,
    SrcsetVariant = 10,
    SourceVariant = 11,
    OpenGraph = 20,
    TwitterCard = 21,
    JsonLd = 30
}