# Benchmark Methodology

## Overview

This thesis evaluates secure gateways (SG) under varied security postures (S0–S5) and transport protocols (REST vs gRPC). The methodology captures workload behaviour, resource usage, and resilience to authentication failures while maintaining reproducibility across environments.

## Security Profile Matrix

| Profile | Transport | Auth | Mutual TLS | Policy / RBAC | Notes |
|---------|-----------|------|------------|----------------|-------|
| S0      | HTTP      | None | No         | No             | Baseline; useful for raw performance ceiling. |
| S1      | HTTPS     | None | No         | No             | TLS overhead without token validation. |
| S2      | HTTPS     | JWT  | No         | No             | Common cloud default; tokens validated at gateway and services. |
| S3      | HTTPS     | None | Yes        | No             | Device / service identity via certificates. |
| S4      | HTTPS     | JWT  | Yes        | No             | Dual-mode identity (client cert + token). |
| S5      | HTTPS     | JWT  | Optional   | Yes (REST policies & gRPC RBAC) | Maximum defence; includes per-method scope enforcement. |

## Workloads

| Workload ID | Description | Protocol(s) | Payload Characteristics | Dependencies |
|-------------|-------------|-------------|--------------------------|--------------|
| `orders-create` | Create orders via REST or gRPC, including inventory reservation. | REST & gRPC | JSON/gRPC payload ~1 KB, concurrency scaled via RPS. | OrderService, InventoryService |
| `inventory-reserve` | Reserve inventory stock (write-heavy). | REST & gRPC | Smaller payloads, high contention on same SKU. | InventoryService |
| `results-write` | Persist benchmark results into PostgreSQL. | REST | JSON payload ~4–6 KB depending on metrics. | ResultsService, Postgres |

Parameters for BenchRunner: `--protocol`, `--security`, `--workload`, `--rps`, `--duration`, `--warmup`, `--connections`. Each run stores configuration snapshot in ResultsService for auditing.

## Metrics

Primary metrics captured per run:

- **Latency percentiles (p50/p95/p99)** via service histograms and BenchRunner summaries.
- **Throughput (requests/sec)** aggregated across gateway and downstream services.
- **Error rate** (HTTP and gRPC status codes).
- **CPU utilisation** per container (Prometheus `process_cpu_seconds_total`).
- **Memory footprint** (Prometheus working set gauges).
- **Benchmark metadata** (security profile, workload, client config) stored alongside results.

Secondary metrics:

- TLS handshake counts (gateway handshake tracker).
- JWT validation errors (AuthServer / downstream logs).
- Postgres transaction rate (ResultsService EF Core counters).

## Test Matrix Execution

For each security profile S0–S5:

1. Run `orders-create` REST with RPS {10, 50, 100}.
2. Run `orders-create` gRPC with same RPS settings.
3. Optional: `inventory-reserve` REST/gRPC at RPS 25.
4. Capture 60s duration with 15s warmup to stabilise caches.
5. Record Prometheus snapshots + Grafana PNG exports for relevant panels.

Repeat measurements at least 3 times, report median with MAD (median absolute deviation).

## Hardware / Environment

- **Local development**: macOS / Linux host with Docker (4 CPU cores, 16 GB RAM recommended).
- **Containers**: Compose stack (Postgres 16, Prometheus 2.49, Grafana 10, custom .NET 8 services).
- **Networking**: All services on Docker bridge network; gateway mapped to localhost 8080/9090; Grafana on 5000.
- **Certificates**: Development TLS certs via ASP.NET development certificates; optional production certificates mounted through environment variables.

## Observability Tooling

- Prometheus scrapes every 15s; Grafana dashboard `bench-overview` preloads CPU/latency charts.
- BenchRunner command logs and summary artefacts stored under `/tmp` (configurable).
- For reproducible figures, export Grafana panels (PNG/CSV) and cross-reference with ResultsService payloads.
