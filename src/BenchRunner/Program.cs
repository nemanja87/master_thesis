using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using BenchRunner.Configuration;
using BenchRunner.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace BenchRunner;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task<int> Main(string[] args)
    {
        var runOptions = BenchRunOptions.Parse(args, message => Console.Error.WriteLine(message));
        if (runOptions is null)
        {
            PrintUsage();
            return 1;
        }

        var configuration = BuildConfiguration();
        var benchConfig = new BenchConfiguration();
        configuration.Bind(benchConfig);

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger("BenchRunner");

        if (!benchConfig.Workloads.TryGetValue(runOptions.Workload, out var workload))
        {
            logger.LogError("Unknown workload '{Workload}'. Available workloads: {Workloads}", runOptions.Workload, string.Join(", ", benchConfig.Workloads.Keys));
            return 1;
        }

        using var httpClient = new HttpClient();
        string token = string.Empty;

        // Only acquire JWT token if security mode is not 'none'
        if (!string.Equals(runOptions.SecurityMode, "none", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                token = await AcquireJwtTokenAsync(httpClient, benchConfig.Security.Jwt, logger, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to acquire JWT access token.");
                return 1;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                logger.LogError("JWT token acquisition returned an empty token.");
                return 1;
            }
        }
        else
        {
            logger.LogInformation("Running in 'none' security mode - skipping JWT authentication.");
        }

        BenchmarkResult? benchmarkResult;
        DateTimeOffset runStart;
        DateTimeOffset runEnd;

        if (string.Equals(runOptions.Protocol, "rest", StringComparison.OrdinalIgnoreCase))
        {
            if (workload.Rest is null)
            {
                logger.LogError("Workload '{Workload}' does not define a REST scenario.", runOptions.Workload);
                return 1;
            }

            var restOutcome = await ExecuteRestBenchmarkAsync(benchConfig, runOptions, workload.Rest, token, logger, CancellationToken.None);
            benchmarkResult = restOutcome.Result;
            runStart = restOutcome.WindowStart;
            runEnd = restOutcome.WindowEnd;
        }
        else
        {
            if (workload.Grpc is null)
            {
                logger.LogError("Workload '{Workload}' does not define a gRPC scenario.", runOptions.Workload);
                return 1;
            }

            var grpcOutcome = await ExecuteGrpcBenchmarkAsync(benchConfig, runOptions, workload.Grpc, token, logger, CancellationToken.None);
            benchmarkResult = grpcOutcome.Result;
            runStart = grpcOutcome.WindowStart;
            runEnd = grpcOutcome.WindowEnd;
        }

        if (benchmarkResult is null || !benchmarkResult.Succeeded)
        {
            logger.LogError("Benchmark execution failed with exit code {ExitCode}.\nstdout: {StdOut}\nstderr: {StdErr}",
                benchmarkResult?.ExitCode,
                benchmarkResult?.StdOut,
                benchmarkResult?.StdErr);
            return benchmarkResult?.ExitCode ?? 1;
        }

        List<MetricSubmission> collectedMetrics = new();
        collectedMetrics.AddRange(ParseToolMetrics(benchmarkResult, logger));

        if (!string.IsNullOrWhiteSpace(benchConfig.Prometheus.BaseUrl) && benchConfig.Prometheus.Queries.Count > 0)
        {
            try
            {
                var promMetrics = await QueryPrometheusAsync(httpClient, benchConfig.Prometheus, runStart, runEnd, logger, CancellationToken.None);
                collectedMetrics.AddRange(promMetrics);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to collect Prometheus metrics; continuing without them.");
            }
        }

        if (collectedMetrics.Count == 0)
        {
            logger.LogWarning("No metrics were collected from benchmarking tools or Prometheus; adding placeholder metric to satisfy result schema.");
            collectedMetrics.Add(new MetricSubmission { Name = "bench_placeholder", Unit = "count", Value = 0 });
        }

        var runSubmission = BuildRunSubmission(benchConfig, runOptions, runStart, runEnd, collectedMetrics, benchmarkResult.OutputPath);

        var resultsEndpoint = CombineUrl(benchConfig.Results.BaseUrl, benchConfig.Results.ApiRoute);
        try
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, resultsEndpoint)
            {
                Content = JsonContent.Create(runSubmission, options: JsonOptions)
            };
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await httpClient.SendAsync(requestMessage);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Results submission failed with status code {Status}: {Body}", (int)response.StatusCode, await response.Content.ReadAsStringAsync());
                return 1;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to submit run results to {Endpoint}.", resultsEndpoint);
            return 1;
        }

        logger.LogInformation("Benchmark run complete and submitted successfully.");
        return 0;
    }

    private static RunSubmission BuildRunSubmission(BenchConfiguration config, BenchRunOptions options, DateTimeOffset windowStart, DateTimeOffset windowEnd, IReadOnlyCollection<MetricSubmission> metrics, string toolOutputPath)
    {
        var configurationSnapshot = new JsonObject
        {
            ["protocol"] = options.Protocol,
            ["security"] = options.SecurityMode,
            ["workload"] = options.Workload,
            ["rps"] = options.RequestsPerSecond,
            ["durationSeconds"] = options.Duration.TotalSeconds,
            ["warmupSeconds"] = options.Warmup.TotalSeconds,
            ["connections"] = options.Connections,
            ["toolOutput"] = toolOutputPath
        };

        return new RunSubmission
        {
            Name = $"{options.Workload}-{options.Protocol}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            Environment = config.Environment,
            StartedAt = windowStart,
            CompletedAt = windowEnd,
            Configuration = configurationSnapshot.ToJsonString(JsonOptions),
            Metrics = metrics.ToList()
        };
    }

    private static async Task<(BenchmarkResult Result, DateTimeOffset WindowStart, DateTimeOffset WindowEnd)> ExecuteRestBenchmarkAsync(
        BenchConfiguration config,
        BenchRunOptions runOptions,
        RestWorkload workload,
        string token,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var restUrl = CombineUrl(config.Target.RestBaseUrl, workload.Path);
        logger.LogInformation("REST URL constructed: {RestUrl} (BaseUrl={BaseUrl}, Path={Path})", restUrl, config.Target.RestBaseUrl, workload.Path);
        var scriptPath = Path.Combine(Path.GetTempPath(), $"bench-{Guid.NewGuid():N}.js");
        var summaryPath = Path.Combine(Path.GetTempPath(), $"k6-summary-{Guid.NewGuid():N}.json");

        var scriptContent = BuildK6Script(restUrl, workload.Method, workload.Body, token, runOptions);
        await File.WriteAllTextAsync(scriptPath, scriptContent, cancellationToken);
        logger.LogInformation("K6 script written to {ScriptPath}", scriptPath);

        var psi = new ProcessStartInfo
        {
            FileName = config.Tools.K6Path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add(scriptPath);
        psi.ArgumentList.Add("--summary-export");
        psi.ArgumentList.Add(summaryPath);

        ApplyTlsEnvironment(config.Security.Tls, runOptions.SecurityMode, psi);

        if (runOptions.SecurityMode.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            psi.ArgumentList.Add("--insecure-skip-tls-verify");
        }

        if (runOptions.Warmup > TimeSpan.Zero)
        {
            logger.LogInformation("Including warmup period of {Warmup} via k6 scenario configuration.", runOptions.Warmup);
        }

        var windowStart = DateTimeOffset.UtcNow;
        var result = await RunProcessAsync(psi, "k6", summaryPath, logger, cancellationToken);
        var windowEnd = DateTimeOffset.UtcNow;

        CleanupTempFile(scriptPath, logger);

        return (result, windowStart, windowEnd);
    }

    private static string BuildK6Script(string url, string method, object? body, string token, BenchRunOptions options)
    {
        var jsonBody = body is null ? "" : JsonSerializer.Serialize(body, JsonOptions);
        var escapedBody = jsonBody.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var mainDuration = ToK6Duration(options.Duration);
        var warmupDuration = options.Warmup > TimeSpan.Zero ? ToK6Duration(options.Warmup) : null;
        var warmupRate = Math.Max(1, options.RequestsPerSecond / 4);

        var sb = new StringBuilder();
        sb.AppendLine("import http from 'k6/http';");
        sb.AppendLine("import { check } from 'k6';");
        sb.AppendLine("export const options = {");
        sb.AppendLine("  scenarios: {");
        if (warmupDuration is not null)
        {
            sb.AppendLine("    warmup: {");
            sb.AppendLine("      executor: 'constant-arrival-rate',");
            sb.AppendLine($"      rate: {warmupRate},");
            sb.AppendLine("      timeUnit: '1s',");
            sb.AppendLine($"      duration: '{warmupDuration}',");
            sb.AppendLine("      preAllocatedVUs: " + options.Connections + ",");
            sb.AppendLine("      maxVUs: " + options.Connections + ",");
            sb.AppendLine("      startTime: '0s',");
            sb.AppendLine("      gracefulStop: '0s'\n    },");
        }
        sb.AppendLine("    main: {");
        sb.AppendLine("      executor: 'constant-arrival-rate',");
        sb.AppendLine($"      rate: {options.RequestsPerSecond},");
        sb.AppendLine("      timeUnit: '1s',");
        sb.AppendLine($"      duration: '{mainDuration}',");
        sb.AppendLine("      preAllocatedVUs: " + options.Connections + ",");
        sb.AppendLine("      maxVUs: " + options.Connections + ",");
        sb.AppendLine(warmupDuration is not null
            ? $"      startTime: '{warmupDuration}',"
            : "      startTime: '0s',");
        sb.AppendLine("      gracefulStop: '0s'\n    }");
        sb.AppendLine("  },\n  thresholds: {\n    http_req_duration: ['p(95)<5000']\n  }\n};");
        sb.AppendLine($"const params = {{ headers: {{ 'Authorization': 'Bearer {token}', 'Content-Type': 'application/json' }} }};");
        sb.AppendLine("export default function () {");
        var upperMethod = method.ToUpperInvariant();
        if (upperMethod == "GET")
        {
            sb.AppendLine($"  const res = http.get('{url}', params);");
        }
        else if (upperMethod == "POST")
        {
            sb.AppendLine($"  const res = http.post('{url}', \"{escapedBody}\", params);");
        }
        else if (upperMethod == "PUT")
        {
            sb.AppendLine($"  const res = http.put('{url}', \"{escapedBody}\", params);");
        }
        else
        {
            sb.AppendLine($"  const res = http.request('{upperMethod}', '{url}', \"{escapedBody}\", params);");
        }
        sb.AppendLine("  check(res, { 'status is 2xx': r => r.status >= 200 && r.status < 300 });");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static async Task<(BenchmarkResult Result, DateTimeOffset WindowStart, DateTimeOffset WindowEnd)> ExecuteGrpcBenchmarkAsync(
        BenchConfiguration config,
        BenchRunOptions runOptions,
        GrpcWorkload workload,
        string token,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var callTarget = workload.Call;
        if (string.IsNullOrWhiteSpace(callTarget))
        {
            throw new InvalidOperationException("gRPC workload call target is required.");
        }

        if (runOptions.Warmup > TimeSpan.Zero)
        {
            _ = await RunGhzAsync(config, runOptions, workload, token, logger, cancellationToken, runOptions.Warmup, warmupOnly: true);
        }

        var windowStart = DateTimeOffset.UtcNow;
        var result = await RunGhzAsync(config, runOptions, workload, token, logger, cancellationToken, runOptions.Duration, warmupOnly: false);
        var windowEnd = DateTimeOffset.UtcNow;

        return (result, windowStart, windowEnd);
    }

    private static async Task<BenchmarkResult> RunGhzAsync(
        BenchConfiguration config,
        BenchRunOptions options,
        GrpcWorkload workload,
        string token,
        ILogger logger,
        CancellationToken cancellationToken,
        TimeSpan duration,
        bool warmupOnly)
    {
        var summaryPath = Path.Combine(Path.GetTempPath(), $"ghz-summary-{Guid.NewGuid():N}.json");

        var psi = new ProcessStartInfo
        {
            FileName = config.Tools.GhzPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        psi.ArgumentList.Add("--call");
        psi.ArgumentList.Add(workload.Call);
        psi.ArgumentList.Add("--rps");
        psi.ArgumentList.Add(options.RequestsPerSecond.ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("--connections");
        psi.ArgumentList.Add(options.Connections.ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("--duration");
        psi.ArgumentList.Add(ToGhzDuration(duration));
        psi.ArgumentList.Add("--format");
        psi.ArgumentList.Add("json");
        psi.ArgumentList.Add("--output");
        psi.ArgumentList.Add(summaryPath);
        var metadataPath = Path.Combine(Path.GetTempPath(), $"ghz-metadata-{Guid.NewGuid():N}.json");
        var metadata = new Dictionary<string, string>
        {
            ["authorization"] = $"Bearer {token}"
        };
        await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(metadata, JsonOptions), cancellationToken);
        psi.ArgumentList.Add("--metadata-file");
        psi.ArgumentList.Add(metadataPath);
        psi.ArgumentList.Add("--data");
        var payload = workload.RequestBody is null ? "{}" : JsonSerializer.Serialize(workload.RequestBody, JsonOptions);
        psi.ArgumentList.Add(payload);

        var protoPath = !string.IsNullOrWhiteSpace(workload.Proto) ? workload.Proto : config.Target.ProtoPath;
        if (!string.IsNullOrWhiteSpace(protoPath))
        {
            psi.ArgumentList.Add("--proto");
            psi.ArgumentList.Add(protoPath);
        }

        ApplyGhzSecurityArguments(config.Security.Tls, options.SecurityMode, psi);

        psi.ArgumentList.Add(config.Target.GrpcAddress);

        var result = await RunProcessAsync(psi, "ghz", summaryPath, logger, cancellationToken);
        CleanupTempFile(metadataPath, logger);
        if (!result.Succeeded)
        {
            if (warmupOnly)
            {
                logger.LogWarning("Warmup command failed but continuing: {StdErr}", result.StdErr);
            }
            else
            {
                throw new InvalidOperationException($"ghz execution failed: {result.StdErr}");
            }
        }

        if (warmupOnly)
        {
            CleanupTempFile(summaryPath, logger);
            return result;
        }

        return result;
    }

    private static void ApplyGhzSecurityArguments(TlsOptions tls, string securityMode, ProcessStartInfo psi)
    {
        if (string.Equals(securityMode, "none", StringComparison.OrdinalIgnoreCase))
        {
            psi.ArgumentList.Add("--insecure");
            return;
        }

        if (!string.IsNullOrWhiteSpace(tls.CaCertificatePath))
        {
            psi.ArgumentList.Add("--cacert");
            psi.ArgumentList.Add(tls.CaCertificatePath);
        }

        if (string.Equals(securityMode, "mtls", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(tls.ClientCertificatePath) && !string.IsNullOrWhiteSpace(tls.ClientCertificateKeyPath))
            {
                psi.ArgumentList.Add("--cert");
                psi.ArgumentList.Add(tls.ClientCertificatePath);
                psi.ArgumentList.Add("--key");
                psi.ArgumentList.Add(tls.ClientCertificateKeyPath);
            }
        }
    }

    private static void ApplyTlsEnvironment(TlsOptions tls, string securityMode, ProcessStartInfo psi)
    {
        if (string.Equals(securityMode, "none", StringComparison.OrdinalIgnoreCase))
        {
            psi.Environment["K6_TLS_INSECURE_SKIP_VERIFY"] = "true";
            return;
        }

        if (!string.IsNullOrWhiteSpace(tls.CaCertificatePath))
        {
            psi.Environment["K6_CA_CERT"] = tls.CaCertificatePath;
        }

        if (string.Equals(securityMode, "mtls", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(tls.ClientCertificatePath))
            {
                psi.Environment["K6_CLIENT_CERT"] = tls.ClientCertificatePath;
            }

            if (!string.IsNullOrWhiteSpace(tls.ClientCertificateKeyPath))
            {
                psi.Environment["K6_CLIENT_KEY"] = tls.ClientCertificateKeyPath;
            }
        }
    }

    private static async Task<BenchmarkResult> RunProcessAsync(ProcessStartInfo psi, string tool, string outputPath, ILogger logger, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting {Tool}: {FileName} {Arguments}", tool, psi.FileName, string.Join(' ', psi.ArgumentList.Select(a => EscapeArgument(a))));

        using var process = new Process { StartInfo = psi };        
        var stdOutBuilder = new StringBuilder();
        var stdErrBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (stdOutBuilder)
            {
                stdOutBuilder.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (stdErrBuilder)
            {
                stdErrBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        var exitCode = process.ExitCode;
        var succeeded = exitCode == 0;

        logger.LogInformation("{Tool} exited with code {ExitCode}.", tool, exitCode);

        return new BenchmarkResult
        {
            Succeeded = succeeded,
            ExitCode = exitCode,
            Tool = tool,
            OutputPath = outputPath,
            StdOut = stdOutBuilder.ToString(),
            StdErr = stdErrBuilder.ToString()
        };
    }

    private static IEnumerable<MetricSubmission> ParseToolMetrics(BenchmarkResult benchmarkResult, ILogger logger)
    {
        if (string.Equals(benchmarkResult.Tool, "k6", StringComparison.OrdinalIgnoreCase))
        {
            return ParseK6Metrics(benchmarkResult.OutputPath, logger);
        }

        if (string.Equals(benchmarkResult.Tool, "ghz", StringComparison.OrdinalIgnoreCase))
        {
            return ParseGhzMetrics(benchmarkResult.OutputPath, logger);
        }

        return Array.Empty<MetricSubmission>();
    }

    private static IEnumerable<MetricSubmission> ParseK6Metrics(string summaryPath, ILogger logger)
    {
        if (!File.Exists(summaryPath))
        {
            logger.LogWarning("k6 summary file not found at {Path}.", summaryPath);
            return Array.Empty<MetricSubmission>();
        }

        var json = JsonDocument.Parse(File.ReadAllText(summaryPath));
        var metrics = json.RootElement.GetProperty("metrics");
        var submissions = new List<MetricSubmission>();

        void AddMetric(string metricName, string unit, string statistic, string? alias = null)
        {
            if (metrics.TryGetProperty(metricName, out var element) && element.TryGetProperty(statistic, out var value) && value.ValueKind == JsonValueKind.Number)
            {
                var suffix = alias ?? statistic;
                suffix = suffix.Replace("(", string.Empty, StringComparison.Ordinal)
                               .Replace(")", string.Empty, StringComparison.Ordinal)
                               .Replace(".", string.Empty, StringComparison.Ordinal);
                submissions.Add(new MetricSubmission
                {
                    Name = $"{metricName}_{suffix}",
                    Unit = unit,
                    Value = value.GetDouble()
                });
            }
        }

        AddMetric("http_reqs", "count", "count");
        AddMetric("http_req_duration", "ms", "avg");
        AddMetric("http_req_duration", "ms", "p(50)", "p50");
        AddMetric("http_req_duration", "ms", "p(75)", "p75");
        AddMetric("http_req_duration", "ms", "p(90)", "p90");
        AddMetric("http_req_duration", "ms", "p(95)", "p95");
        AddMetric("http_req_duration", "ms", "p(99)", "p99");
        AddMetric("http_req_failed", "rate", "rate");

        return submissions;
    }

    private static IEnumerable<MetricSubmission> ParseGhzMetrics(string summaryPath, ILogger logger)
    {
        if (!File.Exists(summaryPath))
        {
            logger.LogWarning("ghz summary file not found at {Path}.", summaryPath);
            return Array.Empty<MetricSubmission>();
        }

        var json = JsonDocument.Parse(File.ReadAllText(summaryPath));
        var root = json.RootElement;
        var metrics = new List<MetricSubmission>();
        var addedLatencyMetrics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddLatencyMetric(string suffix, double value)
        {
            if (double.IsNaN(value))
            {
                return;
            }

            var key = $"ghz_latency_{suffix}";
            if (addedLatencyMetrics.Add(key))
            {
                metrics.Add(new MetricSubmission { Name = key, Unit = "ms", Value = value });
            }
        }

        if (root.TryGetProperty("count", out var count))
        {
            metrics.Add(new MetricSubmission { Name = "ghz_count", Unit = "count", Value = count.GetDouble() });
        }
        if (root.TryGetProperty("rps", out var rps))
        {
            metrics.Add(new MetricSubmission { Name = "ghz_rps", Unit = "ops", Value = rps.GetDouble() });
        }
        if (root.TryGetProperty("average", out var avg))
        {
            AddLatencyMetric("avg", ParseGhzDurationValue(avg));
        }
        if (root.TryGetProperty("p95", out var p95))
        {
            AddLatencyMetric("p95", ParseGhzDurationValue(p95));
        }

        if (root.TryGetProperty("latencyDistribution", out var distribution) && distribution.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in distribution.EnumerateArray())
            {
                if (!item.TryGetProperty("percentage", out var percentageElement) ||
                    !item.TryGetProperty("latency", out var latencyElement))
                {
                    continue;
                }

                var percentage = percentageElement.GetDouble();
                var percentileKey = $"p{Math.Round(percentage, MidpointRounding.AwayFromZero):0}";
                AddLatencyMetric(percentileKey, ParseGhzDurationValue(latencyElement));
            }
        }

        return metrics.Where(m => !double.IsNaN(m.Value));
    }

    private static double ParseGhzDurationValue(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.GetDouble();
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return double.NaN;
            }

            if (value.EndsWith("ms", StringComparison.OrdinalIgnoreCase) && double.TryParse(value[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var ms))
            {
                return ms;
            }

            if (value.EndsWith("s", StringComparison.OrdinalIgnoreCase) && double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            {
                return seconds * 1000;
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric))
            {
                return numeric;
            }
        }

        return double.NaN;
    }

    private static async Task<IEnumerable<MetricSubmission>> QueryPrometheusAsync(HttpClient httpClient, PrometheusOptions options, DateTimeOffset start, DateTimeOffset end, ILogger logger, CancellationToken cancellationToken)
    {
        var metrics = new List<MetricSubmission>();
        var step = Math.Max(1, options.StepSeconds);

        foreach (var query in options.Queries)
        {
            var uri = new UriBuilder(options.BaseUrl)
            {
                Path = "/api/v1/query_range",
                Query = $"query={Uri.EscapeDataString(query.Query)}&start={start.ToUnixTimeSeconds()}&end={end.ToUnixTimeSeconds()}&step={step}"
            }.Uri;

            using var response = await httpClient.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Prometheus query '{Name}' failed with status {Status}", query.Name, (int)response.StatusCode);
                continue;
            }

            var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cancellationToken);
            if (json? ["data"]?.AsObject() is not { } data || data["result"] is not JsonArray resultArray)
            {
                continue;
            }

            foreach (var result in resultArray)
            {
                if (result is not JsonObject obj || obj["values"] is not JsonArray values)
                {
                    continue;
                }

                var sampleValues = values
                    .Select(v => v as JsonArray)
                    .Where(arr => arr is not null && arr.Count >= 2)
                    .Select(arr => double.TryParse(arr![1]?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var val) ? val : double.NaN)
                    .Where(d => !double.IsNaN(d))
                    .ToList();

                if (sampleValues.Count == 0)
                {
                    continue;
                }

                var average = sampleValues.Average();
                var max = sampleValues.Max();

                metrics.Add(new MetricSubmission { Name = $"{query.Name}_avg", Unit = "value", Value = average });
                metrics.Add(new MetricSubmission { Name = $"{query.Name}_max", Unit = "value", Value = max });
            }
        }

        return metrics;
    }

    private static async Task<string> AcquireJwtTokenAsync(HttpClient httpClient, JwtOptions options, ILogger logger, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.TokenEndpoint))
        {
            throw new InvalidOperationException("JWT token endpoint is not configured.");
        }

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret,
            ["scope"] = options.Scopes
        });

        using var response = await httpClient.PostAsync(options.TokenEndpoint, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Token request failed ({Status}): {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException("Failed to acquire access token.");
        }

        using var json = JsonDocument.Parse(body);
        if (json.RootElement.TryGetProperty("access_token", out var tokenElement))
        {
            return tokenElement.GetString() ?? string.Empty;
        }

        throw new InvalidOperationException("Response did not contain an access_token.");
    }

    private static IConfiguration BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

        var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        if (!string.IsNullOrWhiteSpace(environmentName))
        {
            builder.AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false);
        }

        return builder
            .AddEnvironmentVariables(prefix: "BENCH_")
            .Build();
    }

    private static void CleanupTempFile(string path, ILogger logger)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to delete temporary file {Path}.", path);
        }
    }

    private static string CombineUrl(string baseUrl, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
        {
            return baseUrl;
        }

        // Check if relative is truly an absolute URL (has a scheme like http://, https://)
        if (Uri.TryCreate(relative, UriKind.Absolute, out var absolute) && absolute.Scheme != "file")
        {
            return absolute.ToString();
        }

        return baseUrl.TrimEnd('/') + "/" + relative.TrimStart('/');
    }

    private static string ToK6Duration(TimeSpan duration) => duration.TotalSeconds switch
    {
        < 1 => "1s",
        _ => $"{duration.TotalSeconds:F0}s"
    };

    private static string ToGhzDuration(TimeSpan duration)
    {
        if (duration.TotalSeconds < 1)
        {
            return "1s";
        }

        if (duration.TotalSeconds % 60 == 0)
        {
            var minutes = Math.Max(1, (int)(duration.TotalSeconds / 60));
            return $"{minutes}m";
        }

        return $"{Math.Round(duration.TotalSeconds, MidpointRounding.AwayFromZero)}s";
    }

    private static string EscapeArgument(string argument)
    {
        return argument.Contains(' ') ? $"\"{argument}\"" : argument;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("BenchRunner usage:");
        Console.WriteLine("  dotnet BenchRunner.dll --protocol <rest|grpc> --security <none|tls|mtls> --workload <name> --rps <int> --duration <1m> --warmup <10s> --connections <int>");
    }
}
