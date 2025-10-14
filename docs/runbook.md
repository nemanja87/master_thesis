# Runbook

## Prerequisites

- Docker + Docker Compose (or Docker Desktop) installed
- .NET 8 SDK for building/testing services and BenchRunner
- Node.js 18+ (if running the dashboard UI)
- Environment variables configured for certificates and security profile (`SEC_PROFILE`)

## 1. Local Development Environment

1. **Clone repository & install dependencies**
   ```bash
   git clone <repo>
   cd thesis-sg-compare
   dotnet restore
   ```
2. **Install Node dependencies for dashboard (optional)**
   ```bash
   cd ui/dashboard
   npm install
   ```
3. **Run unit tests**
   ```bash
   dotnet test tests/UnitTests/UnitTests.csproj
   ```

## 2. Compose Stack

1. **Build & start services**
   ```bash
   cd deploy
   make compose-up
   ```
   This launches:
   - Postgres (port 5432)
   - AuthServer (5001)
   - Gateway (8080/9090)
   - OrderService (8081/9091)
   - InventoryService (8082/9092)
   - ResultsService (8000)
   - Prometheus (9091 internal) and Grafana (5000 public)

2. **View logs**
   ```bash
   make compose-logs
   ```

3. **Tear down**
   ```bash
   make compose-down
   ```

## 3. Running Benchmarks

1. **Set security profile**
   ```bash
   export SEC_PROFILE=S2
   ```
2. **Trigger sample run**
   ```bash
   cd deploy
   make bench-run-sample
   ```
   Adjust arguments to explore different protocols (`--protocol grpc`) and profiles (`SEC_PROFILE=S5`).

3. **Inspect results**
   - Visit Grafana: <http://localhost:5000> (admin/admin)
   - Query ResultsService API: `GET http://localhost:8000/api/runs`

## 4. Reproducing Figures

1. Collect data for each profile:
   ```bash
   for p in S0 S1 S2 S3 S4 S5; do
     export SEC_PROFILE=$p
     make bench-run-sample
   done
   ```
2. Export Prometheus snapshots or Grafana panel CSV/PNG for latency and CPU:
   - Grafana sidebar → Dashboards → Thesis → Bench Overview → Share → Export

3. Retrieve structured metrics from ResultsService to feed into analysis notebook:
   ```bash
   curl http://localhost:8000/api/runs | jq '.' > runs.json
   ```

## 5. Troubleshooting

- **JWT errors**: Ensure AuthServer reachable; regenerate dev certs (`dotnet dev-certs https --trust`).
- **mTLS failures**: Provide valid cert paths via `Security:CertificatePath` env vars; confirm `SEC_PROFILE` is S3+.
- **Compose build errors**: Run `dotnet publish` manually to identify compilation issues.
- **BenchRunner tool missing**: Install `k6` and `ghz` (or adjust to mock fallback in UI).

## 6. Cleaning Up

- Remove Docker volumes for fresh Postgres state: `docker volume rm thesis-postgres-data`.
- Clear BenchRunner temp scripts: `rm /tmp/bench-*.js /tmp/k6-summary-*.json /tmp/ghz-summary-*.json`.
- Reset environment variables: `unset SEC_PROFILE`.
