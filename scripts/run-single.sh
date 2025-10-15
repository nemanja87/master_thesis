#!/usr/bin/env bash
set -euo pipefail

# Trigger a single BenchRunner run via ResultsService API and save the latest run JSON.
#
# Usage:
#   scripts/run-single.sh <SEC_PROFILE> <protocol:rest|grpc> <workload> <rps> [duration] [warmup] [connections]
#
# Env:
#   RESULTS_URL  ResultsService base URL (default http://localhost:8000)
#

RESULTS_URL=${RESULTS_URL:-http://localhost:8000}

if [[ $# -lt 4 ]]; then
  echo "Usage: $0 <SEC_PROFILE> <protocol:rest|grpc> <workload> <rps> [duration] [warmup] [connections]" >&2
  exit 1
fi

SEC_PROFILE=$1
PROTOCOL=$2
WORKLOAD=$3
RPS=$4
DURATION=${5:-60}
WARMUP=${6:-10}
CONNECTIONS=${7:-10}

echo "[run-single] Setting SEC_PROFILE=${SEC_PROFILE} (compose stack must read this)."
export SEC_PROFILE

echo "[run-single] Triggering BenchRunner: protocol=${PROTOCOL} workload=${WORKLOAD} rps=${RPS} duration=${DURATION}s warmup=${WARMUP}s connections=${CONNECTIONS}"
curl -sS -X POST "${RESULTS_URL%/}/api/benchrunner/run" \
  -H 'Content-Type: application/json' \
  -d "{\"protocol\":\"${PROTOCOL}\",\"security\":\"${SEC_PROFILE}\",\"workload\":\"${WORKLOAD}\",\"rps\":${RPS},\"duration\":${DURATION},\"warmup\":${WARMUP},\"connections\":${CONNECTIONS}}" >/dev/null

echo "[run-single] Fetching the most recent run and saving to ./results ..."
mkdir -p results
STAMP=$(date +%Y%m%d-%H%M%S)
FILENAME="results/${STAMP}-${SEC_PROFILE}-${PROTOCOL}-${WORKLOAD}-rps${RPS}.json"
curl -sS "${RESULTS_URL%/}/api/runs" | jq '.[0]' >"${FILENAME}"
echo "[run-single] Saved ${FILENAME}"

