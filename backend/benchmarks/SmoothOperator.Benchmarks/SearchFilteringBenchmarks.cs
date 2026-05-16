using BenchmarkDotNet.Attributes;

namespace SmoothOperator.Benchmarks;

/// <summary>
/// Compares naive case-insensitive substring filtering against the pre-computed
/// lowercase search-index approach (the backend analogue of the frontend
/// optimisation landed in PR #112). This is the in-memory filtering hot path
/// exercised by list pages; the benchmark needs no database or other infrastructure.
/// </summary>
[MemoryDiagnoser]
public class SearchFilteringBenchmarks
{
    private sealed record Row(string Name, string Host, string Description);

    private List<Row> _rows = [];
    private List<(Row Row, string Index)> _indexed = [];
    private string _term = string.Empty;

    [Params(1_000, 10_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _rows = Enumerable.Range(0, RowCount)
            .Select(i => new Row($"Connection {i}", $"host-{i}.internal", $"Environment {i % 7}"))
            .ToList();

        // The index is built once when the dataset loads, then reused for every keystroke.
        _indexed = _rows
            .Select(r => (r, $"{r.Name} {r.Host} {r.Description}".ToLowerInvariant()))
            .ToList();

        // Worst case: a term that only the last row matches, forcing a full scan.
        _term = $"host-{RowCount - 1}";
    }

    [Benchmark(Baseline = true)]
    public int NaiveFilter()
    {
        var term = _term.ToLowerInvariant();
        return _rows.Count(r =>
            r.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
            || r.Host.Contains(term, StringComparison.OrdinalIgnoreCase)
            || r.Description.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    [Benchmark]
    public int PrecomputedIndexFilter()
    {
        var term = _term.ToLowerInvariant();
        return _indexed.Count(x => x.Index.Contains(term, StringComparison.Ordinal));
    }
}
