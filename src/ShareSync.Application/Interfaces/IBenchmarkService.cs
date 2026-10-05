using ShareSync.Application.DTOs.Benchmarks;

namespace ShareSync.Application.Interfaces;

public interface IBenchmarkService
{
    Task<List<BenchmarkDto>> GetAvailableBenchmarksAsync(CancellationToken cancellationToken = default);
    Task<BenchmarkComparisonDto> ComparePortfolioAsync(BenchmarkComparisonRequestDto request, int userId, CancellationToken cancellationToken = default);
}
