using System.Collections.Generic;

namespace BenchRunner.Configuration;

internal sealed class BenchConfiguration
{
    public string Environment { get; set; } = "local";
    public ToolOptions Tools { get; set; } = new();
    public TargetOptions Target { get; set; } = new();
    public Dictionary<string, WorkloadDefinition> Workloads { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public SecurityOptions Security { get; set; } = new();
    public PrometheusOptions Prometheus { get; set; } = new();
    public ResultsOptions Results { get; set; } = new();
}

internal sealed class ToolOptions
{
    public string K6Path { get; set; } = "k6";
    public string GhzPath { get; set; } = "ghz";
}

internal sealed class TargetOptions
{
    public string RestBaseUrl { get; set; } = "https://localhost:8080";
    public string GrpcAddress { get; set; } = "localhost:9090";
    public string ProtoPath { get; set; } = string.Empty;
}

internal sealed class SecurityOptions
{
    public JwtOptions Jwt { get; set; } = new();
    public TlsOptions Tls { get; set; } = new();
}

internal sealed class JwtOptions
{
    public string TokenEndpoint { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
}

internal sealed class TlsOptions
{
    public string CaCertificatePath { get; set; } = string.Empty;
    public string ClientCertificatePath { get; set; } = string.Empty;
    public string ClientCertificateKeyPath { get; set; } = string.Empty;
}

internal sealed class PrometheusOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public int StepSeconds { get; set; } = 15;
    public List<PrometheusQuery> Queries { get; set; } = new();
}

internal sealed class PrometheusQuery
{
    public string Name { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
}

internal sealed class ResultsOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiRoute { get; set; } = "/api/runs";
}

internal sealed class WorkloadDefinition
{
    public RestWorkload? Rest { get; set; }
    public GrpcWorkload? Grpc { get; set; }
}

internal sealed class RestWorkload
{
    public string Method { get; set; } = "GET";
    public string Path { get; set; } = "/";
    public object? Body { get; set; }
}

internal sealed class GrpcWorkload
{
    public string Call { get; set; } = string.Empty;
    public string? Proto { get; set; }
    public object? RequestBody { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
