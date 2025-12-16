Mayordomo.Web.Extractor
=======================

Overview
--------
Small, modular .NET 10 library and tools to extract article content, images and metadata from web pages. The solution contains extractors, abstractions, HTML parsing implementations, tests and benchmarks.

Projects
--------
- `Mayordomo.Web.Extractor.Abstractions` — public interfaces for extractors and processors
- `Mayordomo.Web.Extractor` — composition and higher-level services
- `Mayordomo.Web.Extractor.HtmlAgilityPack` — HTML Agility Pack based extractor implementation
- `Mayordomo.Web.Extractor.AngleSharp` — AngleSharp based extractor implementation
- `Mayordomo.Web.Extractor.Tests` — unit tests
- `Mayordomo.Web.Extractor.Benchmarks` — benchmark projects
- `Mayordomo.Web.Extractor.TestConsole` — sample/test console app

Requirements
------------
- .NET 10 SDK
- Optional: `dotnet-ef` or other tooling only if needed for local tasks

Usage examples
--------------
Quick extraction via the default HTTP helper:

```csharp
using Mayordomo.Web.Extractor.Extraction;

var extractor = HttpArticleExtractor.CreateDefault();
var article = await extractor.ExtractFromUrlAsync(
    "https://www.example.com/news/story");

Console.WriteLine(article.Title);
Console.WriteLine(article.Excerpt);
foreach (var image in article.Images)
{
    Console.WriteLine($"Image: {image.Url}");
}
```

Register the library in an ASP.NET Core or worker service using the provided DI extension:

```csharp
using Mayordomo.Web.Extractor;
using Mayordomo.Web.Extractor.Configuration;

builder.Services.AddArticleExtraction(builder.Configuration, "ArticleExtraction", options =>
{
    options.EnableImageFileCache = true;
    options.ImageFileCachePath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot/images");
});
```

Expose the extractor through a minimal API endpoint:

```csharp
using Mayordomo.Web.Extractor.Extraction;

var app = builder.Build();

app.MapGet("/api/article", async (
        [FromServices] IHttpArticleExtractor extractor,
        [FromQuery] string url,
        CancellationToken token) =>
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Results.BadRequest("Url query parameter is required.");
        }

        var article = await extractor.ExtractFromUrlAsync(url, cancellationToken: token);
        return Results.Ok(article);
    })
   .Produces<ArticleContent>()
   .WithName("GetArticle");

app.Run();
```

Or wire it up in a conventional API controller:

```csharp
using Mayordomo.Web.Extractor.Extraction;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class ArticleController : ControllerBase
{
    private readonly IHttpArticleExtractor _extractor;

    public ArticleController(IHttpArticleExtractor extractor)
    {
        _extractor = extractor;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string url, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return BadRequest("Url query parameter is required.");
        }

        var article = await _extractor.ExtractFromUrlAsync(url, cancellationToken: token);
        return Ok(article);
    }
}
```

Example configuration section consumed by `AddArticleExtraction`:

```json
{
  "ArticleExtraction": {
    "Parser": "HtmlAgilityPack",
    "UseRedis": false,
    "EnableImageFileCache": true,
    "ImageFileCachePath": "wwwroot/article-images",
    "ImageFileCacheBaseUrl": "/article-images",
    "IgnoreImageDownloadErrors": true
  }
}
```

Build
-----
Restore and build all projects:

```bash
dotnet restore
dotnet build --configuration Release
```

Run tests
---------
Run unit tests from solution root:

```bash
dotnet test
```

Run benchmarks
--------------
Benchmarks use BenchmarkDotNet. Run from the benchmark project directory:

```bash
dotnet run -c Release -p Mayordomo.Web.Extractor.Benchmarks
```

Package and publish
-------------------
This repository includes a GitHub Actions workflow to pack and publish NuGet packages: `.github/workflows/publish-nuget.yml`.
The workflow builds and packs with a version based on commit count and pushes packages to NuGet when the `NUGET_API_KEY` secret is provided.

Dependency updates
------------------
Dependabot configuration is provided in `.github/dependabot.yml` to open weekly PRs for NuGet package updates.

Contributing
------------
- Open issues or PRs for bugs and improvements
- Follow existing coding conventions in the repository
- Update or add tests for behavior changes

License
-------
No license file included in the repository. Add a `LICENSE` file if you intend to open source this code.

Contact
-------
For local development questions, run the sample console app `Mayordomo.Web.Extractor.TestConsole` or inspect tests in `Mayordomo.Web.Extractor.Tests`.
