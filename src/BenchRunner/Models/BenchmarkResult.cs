namespace BenchRunner.Models;

internal sealed class BenchmarkResult
{
    public bool Succeeded { get; init; }
    public int ExitCode { get; init; }
    public string Tool { get; init; } = string.Empty;
    public string OutputPath { get; init; } = string.Empty;
    public string StdOut { get; init; } = string.Empty;
    public string StdErr { get; init; } = string.Empty;
}
