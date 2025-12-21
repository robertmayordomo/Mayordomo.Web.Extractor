ReadableWeb
=======================

Overview
--------
Small, modular .NET 10 library and tools to extract article content, images and metadata from web pages. The solution contains extractors, abstractions, HTML parsing implementations, tests and benchmarks.

Projects
--------
- `ReadableWeb.Abstractions` — public interfaces for extractors and processors
- `ReadableWeb` — composition and higher-level services
- `ReadableWeb.HtmlAgilityPack` — HTML Agility Pack based extractor implementation
- `ReadableWeb.AngleSharp` — AngleSharp based extractor implementation
- `ReadableWeb.Tests` — unit tests
- `ReadableWeb.Benchmarks` — benchmark projects
- `ReadableWeb.TestConsole` — sample/test console app

Requirements
------------
- .NET 10 SDK
- Optional: `dotnet-ef` or other tooling only if needed for local tasks

Usage examples
--------------
Quick extraction via the default HTTP helper:

```csharp
using ReadableWeb.Extraction;

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
using ReadableWeb;
using ReadableWeb.Configuration;

builder.Services.AddArticleExtraction(builder.Configuration, "ArticleExtraction", options =>
{
    options.EnableImageFileCache = true;
    options.ImageFileCachePath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot/images");
});
```

Caching
-------
ReadableWeb supports two complementary caching mechanisms to improve performance and reduce bandwidth when extracting articles with images:

1) Image file cache (local filesystem)

- Enable this with `EnableImageFileCache = true` in the configuration or by setting the option in the DI extension. When enabled the extractor will download image assets referenced by the extracted article and persist them to the specified `ImageFileCachePath` on disk.
- `ImageFileCacheBaseUrl` should be set to the public base URL segment where the cached images will be served from (for example `/article-images`). The extractor will rewrite image URLs in the returned `ArticleContent` to point at the base URL plus the cached filename.
- `ImageFileCachePath` is a filesystem path relative to your application root (or an absolute path). Make sure the directory is writable by the process and is served by your web server (for example, place it under `wwwroot` in ASP.NET Core or configure static file serving for that path).
- `IgnoreImageDownloadErrors = true` prevents extraction from failing when individual image downloads fail. When false, image download failures may surface as errors.

Example configuration for local image caching:

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

2) Distributed cache (Redis)

- If `UseRedis = true` the library will use the configured distributed cache (typically backed by Redis) to store extract results and/or intermediate data depending on your configuration. This reduces repeated extraction work for the same URLs across multiple instances.
- To use Redis, register and configure `IDistributedCache` (for example `Microsoft.Extensions.Caching.StackExchangeRedis`) in your application and make sure the `ArticleExtraction` configuration section enables `UseRedis`.

Example configuration snippet enabling Redis + image cache:

```json
{
  "ArticleExtraction": {
    "Parser": "HtmlAgilityPack",
    "UseRedis": true,
    "EnableImageFileCache": true,
    "ImageFileCachePath": "wwwroot/article-images",
    "ImageFileCacheBaseUrl": "/article-images",
    "IgnoreImageDownloadErrors": true
  }
}
```

Behavior notes and operational tips
----------------------------------
- Cached images are intended to be served as static files. Ensure your web server serves files from `ImageFileCachePath` at the `ImageFileCacheBaseUrl` you configured.
- The library may use deterministic filenames (for example based on a hash of the original image URL) — treat the cache directory as opaque and prefer clearing it via administrative processes when you need to invalidate images.
- If you enable a distributed cache (Redis) you should configure an appropriate TTL and eviction policy on the cache side if you need automatic expiration; the library relies on the configured `IDistributedCache` implementation for storage semantics.
- When `IgnoreImageDownloadErrors` is enabled the extraction will still return article text and metadata even if some images fail to download. Disable this setting during debugging to surface download issues.

Expose the extractor through a minimal API endpoint:

```csharp
using ReadableWeb.Extraction;

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
using ReadableWeb.Extraction;
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
dotnet run -c Release -p ReadableWeb.Benchmarks
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
For local development questions, run the sample console app `ReadableWeb.TestConsole` or inspect tests in `ReadableWeb.Tests`.
