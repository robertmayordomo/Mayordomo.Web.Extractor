# Mayordomo.Web.Extractor.Benchmarks

This project contains BenchmarkDotNet performance benchmarks for the Mayordomo.Web.Extractor library.

## Running Benchmarks

### Run all benchmarks
```bash
dotnet run -c Release
```

### Run specific benchmark class
```bash
dotnet run -c Release --filter *ReadabilityExtractorBenchmarks*
dotnet run -c Release --filter *HttpArticleExtractorBenchmarks*
```

### Run with specific runtime
```bash
dotnet run -c Release --runtimes net10.0
```

## Benchmark Classes

### ReadabilityExtractorBenchmarks
Tests the core HTML extraction and parsing performance with:
- Simple articles (baseline)
- Complex articles with rich metadata
- Image-heavy articles with multiple srcsets
- Extraction without URL

### HttpArticleExtractorBenchmarks
Tests the full HTTP extraction pipeline including:
- First request (no cache)
- Cold cache performance
- Warm cache performance (cache hits)
- Force refresh behavior

## Sample Data

The `SampleData` directory contains realistic HTML files for more comprehensive benchmarking scenarios.

## Results

After running benchmarks, results will be saved to `BenchmarkDotNet.Artifacts/results/` directory with detailed performance metrics including:
- Mean execution time
- Memory allocations
- Standard deviation
- Percentiles

## Tips

- Always run benchmarks in **Release** mode for accurate results
- Close other applications to minimize interference
- Run multiple iterations for consistent results
- BenchmarkDotNet will automatically warmup before measuring
