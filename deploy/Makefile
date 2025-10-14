COMPOSE = docker compose -f docker-compose.yml
BENCH_RUNNER = dotnet run --project ../src/BenchRunner/BenchRunner.csproj --

.PHONY: compose-up
compose-up:
	$(COMPOSE) up -d --build

.PHONY: compose-down
compose-down:
	$(COMPOSE) down -v

.PHONY: compose-logs
compose-logs:
	$(COMPOSE) logs -f

.PHONY: bench-run-sample
bench-run-sample:
	$(BENCH_RUNNER) --protocol rest --security tls --workload orders-create --rps 5 --duration 30s --warmup 5s --connections 5
