using ReadableWeb.Extraction;
using ReadableWeb.HtmlAgilityPack;
using Xunit;
using Xunit.Abstractions;

namespace ReadableWeb.Tests
{
    public class DetailedBbcDebugTest
    {
        private readonly ITestOutputHelper _output;

        public DetailedBbcDebugTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task DetailedDebugBbcArticleExtraction()
        {
            var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "bbc-article.html");
            var html = await File.ReadAllTextAsync(fixturePath);
            
            _output.WriteLine($"=== HTML LENGTH: {html.Length}");
            
            // Check if the text exists in the HTML
            var searchText = "Her reaction to it now";
            var index = html.IndexOf(searchText, StringComparison.OrdinalIgnoreCase);
            _output.WriteLine($"=== TEXT FOUND IN HTML AT INDEX: {index}");
            
            if (index >= 0)
            {
                var context = html.Substring(Math.Max(0, index - 200), Math.Min(400, html.Length - Math.Max(0, index - 200)));
                _output.WriteLine($"=== CONTEXT: {context}");
            }
            
            var extractor = new ReadabilityExtractor(
                new DefaultLocaleInferrer(), 
                new DefaultMetadataExtractor(), 
                new ReadabilityContentExtractor(), 
                new DocumentPreprocessor(), 
                new ImageProcessor());
                
            var article = extractor.Extract(html, new ExtractionOptions { Url = "https://www.bbc.co.uk/news/articles/c20kymmxmxgo" });
            
            _output.WriteLine($"=== EXTRACTED TEXT CONTENT LENGTH: {article.TextContent?.Length ?? 0}");
            _output.WriteLine($"=== FIRST 2000 CHARS: {article.TextContent?.Substring(0, Math.Min(2000, article.TextContent.Length))}");
            _output.WriteLine($"=== LAST 2000 CHARS: {article.TextContent?.Substring(Math.Max(0, (article.TextContent?.Length ?? 0) - 2000))}");
            _output.WriteLine($"=== CONTAINS 'Her reaction': {article.TextContent?.Contains("Her reaction", StringComparison.OrdinalIgnoreCase)}");
            
            // Search for the text in the extracted content
            var extractedIndex = article.TextContent?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) ?? -1;
            _output.WriteLine($"=== TEXT FOUND IN EXTRACTION AT INDEX: {extractedIndex}");
        }
    }
}
