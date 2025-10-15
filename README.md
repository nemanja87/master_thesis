# Secure Gateway Benchmark Suite

## Overview

This repository contains the reference implementation used to compare REST (HTTP) and gRPC transports under multiple security postures (S0–S5). It includes a full .NET microservice environment (Gateway, Order, Inventory, Results, Auth), a BenchRunner CLI that drives synthetic workloads, supporting infrastructure (Postgres, Prometheus, Grafana), and a dashboard UI for triggering runs and reviewing metrics.

## Repository Layout

- `src/` – application services, shared contracts, security helpers, and the BenchRunner CLI.
- `deploy/` – Docker Compose stack, service Dockerfiles, Prometheus and Grafana configuration, helper Make targets.
- `tests/` – unit, integration, load, and security test projects (all target `net9.0`).
- `ui/` – React + Vite dashboard for launching BenchRunner runs and visualising results.
- `docs/` – methodology, protocol matrix, runbook, and threat model notes.

## Prerequisites

- Docker Desktop or Docker Engine + Docker Compose v2
- .NET 9 SDK (`dotnet --version` ≥ 9.0.100-preview or later)
- Node.js 18+ (only if you plan to run the dashboard locally)
- Make (optional, simplifies compose commands)

## Getting Started

```bash
git clone <repo-url>
cd thesis-sg-compare
dotnet restore
```

### Build & Run the Compose Stack

```bash
cd deploy
make compose-up            # build images and start all services
make compose-logs          # tail combined logs
make compose-down          # stop stack and remove volumes
```

Services exposed locally:

- Gateway REST `http://localhost:8080`
- Gateway gRPC `http://localhost:9090`
- AuthServer `https://localhost:5001`
- ResultsService API `http://localhost:8000`
- Grafana `http://localhost:5000` (admin/admin)
- Prometheus `http://localhost:9091` (container 9090 → host 9091)

### Running BenchRunner

BenchRunner lives in `src/BenchRunner`. Configuration is driven by `appsettings.json` (paths, workloads, Prometheus queries, results endpoint).

Trigger a run from your host:

```bash
dotnet run --project src/BenchRunner/BenchRunner.csproj -- \
  --protocol rest \
  --security tls \
  --workload orders-create \
  --rps 25 \
  --duration 60s \
  --warmup 10s \
  --connections 20
```

For gRPC workloads, ensure the workload definition includes the `Call`, optional `Metadata`, and (if needed) override `Proto`. Note: Gateway's gRPC endpoint on `http://localhost:9090` is HTTP/2 only; visiting it in a browser will show “An HTTP/1.x request was sent to an HTTP/2 only endpoint”. This is expected.

### Using the Dashboard UI

```bash
cd ui/dashboard
npm install
npm run dev  # http://localhost:5173 by default
```

Set `VITE_RESULTS_SERVICE_URL` if the ResultsService is not on `http://localhost:8000`. The “Run Experiments” panel lets you trigger BenchRunner inside the compose stack and view captured metrics (latency percentiles, CPU queries, configuration snapshot).

### Collecting Observability Data

- Prometheus queries configured in `BenchRunner/appsettings.json` are executed per run, and the averages are persisted alongside tool outputs.
- Grafana dashboards under `deploy/grafana/dashboards` provide latency and resource visualisations; export PNG/CSV for thesis figures.
- Adjust Prometheus expressions to target container metrics or process-level counters depending on environment.

## Test Suite

All test projects target `net9.0` and can be executed from the repository root:

```bash
dotnet test thesis-sg-compare.sln          # entire test matrix
dotnet test tests/UnitTests/UnitTests.csproj
dotnet test tests/SecurityTests/SecurityTests.csproj
dotnet test tests/IntegrationTests/IntegrationTests.csproj
dotnet test tests/LoadTests/LoadTests.csproj
```

Integration and load suites contain infrastructure-dependent tests that are currently skipped unless external services are running.

## Typical Workflow

1. Start the compose stack (`make compose-up`).
2. Run representative workloads via BenchRunner (REST & gRPC, varying security profiles).
3. Inspect Grafana dashboards and Prometheus results to understand performance/security trade-offs.
4. Export run data from the ResultsService (`GET /api/runs`) for further analysis in notebooks or reports.

## Troubleshooting

- **JWT acquisition failures** – confirm AuthServer is reachable from BenchRunner and the `bench-runner` client scopes include the workload’s requirements.
- **gRPC metadata issues** – update workload `Metadata` (e.g., `order-id`) in BenchRunner config.
- **Prometheus metrics empty** – verify queries match available metrics (e.g., switch to `process_cpu_seconds_total` in local compose setups).
- **TLS/mTLS errors** – set appropriate certificate paths via environment variables or update the security profile to S0/S1 for baseline testing.
