using System.Collections.Generic;
using System.Globalization;

namespace BenchRunner.Configuration;

internal sealed class BenchRunOptions
{
    public string Protocol { get; init; } = "rest";
    public string SecurityMode { get; init; } = "tls";
    public string Workload { get; init; } = "orders-create";
    public int RequestsPerSecond { get; init; } = 25;
    public TimeSpan Duration { get; init; } = TimeSpan.FromSeconds(60);
    public TimeSpan Warmup { get; init; } = TimeSpan.FromSeconds(10);
    public int Connections { get; init; } = 25;

    public static BenchRunOptions? Parse(string[] args, Action<string>? errorWriter = null)
    {
        var writer = errorWriter ?? Console.Error.WriteLine;
        var optionMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                writer($"Unexpected argument '{token}'.");
                return null;
            }

            var key = token[2..];
            if (i + 1 >= args.Length)
            {
                writer($"Missing value for option '{key}'.");
                return null;
            }

            optionMap[key] = args[++i];
        }

        try
        {
            var protocol = optionMap.TryGetValue("protocol", out var p) ? p : "rest";
            if (!string.Equals(protocol, "rest", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(protocol, "grpc", StringComparison.OrdinalIgnoreCase))
            {
                writer($"Unsupported protocol '{protocol}'. Expected 'rest' or 'grpc'.");
                return null;
            }

            var security = optionMap.TryGetValue("security", out var s) ? s : "tls";
            if (!string.Equals(security, "none", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(security, "tls", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(security, "mtls", StringComparison.OrdinalIgnoreCase))
            {
                writer($"Unsupported security mode '{security}'. Expected 'none', 'tls', or 'mtls'.");
                return null;
            }

            var workload = optionMap.TryGetValue("workload", out var w) ? w : "orders-create";
            var rps = optionMap.TryGetValue("rps", out var rpsString) ? int.Parse(rpsString, CultureInfo.InvariantCulture) : 25;
            var duration = optionMap.TryGetValue("duration", out var durationString) ? ParseDuration(durationString) : TimeSpan.FromSeconds(60);
            var warmup = optionMap.TryGetValue("warmup", out var warmupString) ? ParseDuration(warmupString) : TimeSpan.FromSeconds(10);
            var connections = optionMap.TryGetValue("connections", out var connString) ? int.Parse(connString, CultureInfo.InvariantCulture) : 25;

            return new BenchRunOptions
            {
                Protocol = protocol.ToLowerInvariant(),
                SecurityMode = security.ToLowerInvariant(),
                Workload = workload,
                RequestsPerSecond = rps,
                Duration = duration,
                Warmup = warmup,
                Connections = connections
            };
        }
        catch (FormatException ex)
        {
            writer($"Invalid numeric or time span value: {ex.Message}");
            return null;
        }
    }

    private static TimeSpan ParseDuration(string value)
    {
        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var ts))
        {
            return ts;
        }

        if (value.EndsWith("s", StringComparison.OrdinalIgnoreCase) && double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        if (value.EndsWith("m", StringComparison.OrdinalIgnoreCase) && double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes))
        {
            return TimeSpan.FromMinutes(minutes);
        }

        throw new FormatException($"Unable to parse duration value '{value}'.");
    }
}
