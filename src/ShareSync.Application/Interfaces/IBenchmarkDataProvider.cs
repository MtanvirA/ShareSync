using ShareSync.Application.DTOs.Benchmarks;

namespace ShareSync.Application.Interfaces;

public interface IBenchmarkDataProvider
{
    List<BenchmarkDto> GetAvailableBenchmarks();
    BenchmarkDto? GetBenchmark(string benchmarkCode);
    decimal GetBenchmarkValue(string benchmarkCode, DateTime date);
}
