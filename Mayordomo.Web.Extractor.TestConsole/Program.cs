using Mayordomo.Web.Extractor.Extraction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mayordomo.Web.Extractor.TestConsole
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            using var host = Host.CreateDefaultBuilder(args).ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                        .AddEnvironmentVariables()
                        .AddCommandLine(args);
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddArticleExtraction(context.Configuration, "ArticleExtraction");
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Information);
                })
                .Build();

            var extractor = host.Services.GetRequiredService<HttpArticleExtractor>();

            var url = args.Length > 0 
                ? args[0] 
                : "https://www.theguardian.com/law/2025/nov/27/government-to-ditch-day-one-unfair-dismissal-policy-from-workers-rights-bill";

            if (string.IsNullOrWhiteSpace(url))
            {
                Console.Write("Enter article URL: ");
                url = Console.ReadLine();
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                Console.WriteLine("No URL provided. Exiting.");
                return;
            }

            Console.WriteLine($"Fetching article: {url}");
            try
            {
                var article = await extractor.ExtractFromUrlAsync(url);

                Console.WriteLine();
                Console.WriteLine("=== Article ===");
                Console.WriteLine($"Title:    {article.Title}");
                Console.WriteLine($"Author:   {article.Author}");
                Console.WriteLine($"Site:     {article.SiteName}");
                Console.WriteLine($"Published:{article.PublishedTime}");
                Console.WriteLine();
                Console.WriteLine("Excerpt:");
                Console.WriteLine(article.Excerpt);
                Console.WriteLine();
                Console.WriteLine($"Images ({article.Images.Count}):");
                foreach (var img in article.Images)
                {
                    Console.WriteLine($" - Role: {img.Role}  Url: {img.Url}");
                    if (!string.IsNullOrWhiteSpace(img.LocalUrl) || !string.IsNullOrWhiteSpace(img.LocalPath))
                    {
                        Console.WriteLine($"   Cached: {img.LocalUrl ?? img.LocalPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error extracting article:");
                Console.WriteLine(ex.ToString());
                Console.ReadLine();
            }
        }
    }
}