using System.Diagnostics;

namespace LoadTests;

public sealed class BenchRunnerSmokeTests
{
    [Fact(Skip = "Requires external dependencies and benchmarking tools")]
    public async Task BenchRunner_CompletesRestSmokeRun()
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                ArgumentList =
                {
                    "run",
                    "--project",
                    Path.Combine("..", "..", "src", "BenchRunner", "BenchRunner.csproj"),
                    "--",
                    "--protocol", "rest",
                    "--security", "tls",
                    "--workload", "orders-create",
                    "--rps", "1",
                    "--duration", "5s",
                    "--warmup", "0s",
                    "--connections", "1"
                },
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            }
        };

        process.Start();
        await process.WaitForExitAsync();

        Assert.True(process.ExitCode == 0, $"BenchRunner failed: {await process.StandardError.ReadToEndAsync()}");
    }
}
