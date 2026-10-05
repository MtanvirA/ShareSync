using ShareSync.Application.DTOs.Benchmarks;
using ShareSync.Application.Interfaces;

namespace ShareSync.Application.Services;

public class BenchmarkDataProvider : IBenchmarkDataProvider
{
    private readonly Dictionary<string, BenchmarkDto> _benchmarks;
    private readonly Dictionary<string, List<(DateTime Date, decimal Value)>> _priceHistory;

    public BenchmarkDataProvider()
    {
        _benchmarks = new Dictionary<string, BenchmarkDto>(StringComparer.OrdinalIgnoreCase)
        {
            ["DSEX"] = new BenchmarkDto
            {
                BenchmarkCode = "DSEX",
                Name = "DSE Broad Index (DSEX)",
                Description = "Benchmark index covering major free-float market capitalization on the Dhaka Stock Exchange.",
                Provider = "Dhaka Stock Exchange Reference Series",
                CurrentValue = 7131.79m,
                BaseValue = 6000.00m
            },
            ["DS30"] = new BenchmarkDto
            {
                BenchmarkCode = "DS30",
                Name = "DSE 30 Index (DS30)",
                Description = "Blue-chip index of the 30 leading investable companies listed on the Dhaka Stock Exchange.",
                Provider = "Dhaka Stock Exchange Reference Series",
                CurrentValue = 2425.50m,
                BaseValue = 2000.00m
            },
            ["DSEG"] = new BenchmarkDto
            {
                BenchmarkCode = "DSEG",
                Name = "DSE Shariah Index (DSEX Shariah)",
                Description = "Islamic compliance index reflecting Shariah-compliant equities on the Dhaka Stock Exchange.",
                Provider = "Dhaka Stock Exchange Reference Series",
                CurrentValue = 1530.00m,
                BaseValue = 1250.00m
            }
        };

        // Reference calibrated history series for historical comparisons
        _priceHistory = new Dictionary<string, List<(DateTime Date, decimal Value)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["DSEX"] = new List<(DateTime Date, decimal Value)>
            {
                (new DateTime(2025, 01, 01), 6000.00m),
                (new DateTime(2025, 04, 01), 6080.00m),
                (new DateTime(2025, 07, 01), 6150.00m),
                (new DateTime(2025, 10, 01), 6245.00m),
                (new DateTime(2026, 01, 01), 6245.00m),
                (new DateTime(2026, 02, 01), 6320.00m),
                (new DateTime(2026, 03, 01), 6410.00m),
                (new DateTime(2026, 04, 01), 6520.00m),
                (new DateTime(2026, 05, 01), 6610.00m),
                (new DateTime(2026, 06, 01), 6730.00m),
                (new DateTime(2026, 07, 01), 6850.00m),
                (new DateTime(2026, 08, 01), 6940.00m),
                (new DateTime(2026, 09, 01), 7020.00m),
                (new DateTime(2026, 10, 01), 7131.79m)
            },
            ["DS30"] = new List<(DateTime Date, decimal Value)>
            {
                (new DateTime(2025, 01, 01), 2050.00m),
                (new DateTime(2025, 07, 01), 2120.00m),
                (new DateTime(2026, 01, 01), 2180.00m),
                (new DateTime(2026, 04, 01), 2240.00m),
                (new DateTime(2026, 07, 01), 2320.00m),
                (new DateTime(2026, 09, 01), 2380.00m),
                (new DateTime(2026, 10, 01), 2425.50m)
            },
            ["DSEG"] = new List<(DateTime Date, decimal Value)>
            {
                (new DateTime(2025, 01, 01), 1300.00m),
                (new DateTime(2025, 07, 01), 1350.00m),
                (new DateTime(2026, 01, 01), 1390.00m),
                (new DateTime(2026, 04, 01), 1430.00m),
                (new DateTime(2026, 07, 01), 1475.00m),
                (new DateTime(2026, 09, 01), 1500.00m),
                (new DateTime(2026, 10, 01), 1530.00m)
            }
        };
    }

    public BenchmarkDataProvider(
        Dictionary<string, BenchmarkDto> customBenchmarks,
        Dictionary<string, List<(DateTime Date, decimal Value)>> customPriceHistory)
    {
        _benchmarks = new Dictionary<string, BenchmarkDto>(customBenchmarks, StringComparer.OrdinalIgnoreCase);
        _priceHistory = new Dictionary<string, List<(DateTime Date, decimal Value)>>(customPriceHistory, StringComparer.OrdinalIgnoreCase);
    }

    public List<BenchmarkDto> GetAvailableBenchmarks()
    {
        return _benchmarks.Values.ToList();
    }

    public BenchmarkDto? GetBenchmark(string benchmarkCode)
    {
        if (string.IsNullOrWhiteSpace(benchmarkCode)) return null;
        return _benchmarks.TryGetValue(benchmarkCode.Trim(), out var b) ? b : null;
    }

    public decimal GetBenchmarkValue(string benchmarkCode, DateTime date)
    {
        var code = (benchmarkCode ?? "DSEX").Trim();
        if (!_priceHistory.TryGetValue(code, out var series) || series.Count == 0)
        {
            return _benchmarks.TryGetValue(code, out var def) ? def.CurrentValue : 6000.00m;
        }

        var targetDate = date.Date;

        // Exact match
        var exact = series.FirstOrDefault(s => s.Date.Date == targetDate);
        if (exact != default)
        {
            return exact.Value;
        }

        // Before earliest
        if (targetDate <= series.First().Date)
        {
            return series.First().Value;
        }

        // After latest
        if (targetDate >= series.Last().Date)
        {
            return series.Last().Value;
        }

        // Find latest point on or before targetDate
        var prior = series.Where(s => s.Date.Date <= targetDate).OrderBy(s => s.Date).LastOrDefault();
        if (prior != default)
        {
            return prior.Value;
        }

        return series.First().Value;
    }
}
