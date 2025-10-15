#!/usr/bin/env bash
set -euo pipefail

# Run the experiment matrix across S0–S5 and REST/gRPC for given workload and RPS list.
# Each run is triggered via ResultsService /api/benchrunner/run and the latest run JSON is archived.
#
# Usage:
#   scripts/run-matrix.sh <workload> "rps_csv" [duration] [warmup] [connections]
# Example:
#   scripts/run-matrix.sh orders-create "10,50,100" 60 10 20
#
# Env:
#   RESULTS_URL   ResultsService base URL (default http://localhost:8000)
#   PROFILES      Space-separated list of profiles (default: "S0 S1 S2 S3 S4 S5")
#   PROTOCOLS     Space-separated list of protocols (default: "rest grpc")

RESULTS_URL=${RESULTS_URL:-http://localhost:8000}
PROFILES=${PROFILES:-"S0 S1 S2 S3 S4 S5"}
PROTOCOLS=${PROTOCOLS:-"rest grpc"}

if [[ $# -lt 2 ]]; then
  echo "Usage: $0 <workload> \"rps_csv\" [duration] [warmup] [connections]" >&2
  exit 1
fi

WORKLOAD=$1
IFS=',' read -r -a RPS_LIST <<< "$2"
DURATION=${3:-60}
WARMUP=${4:-10}
CONNECTIONS=${5:-20}

mkdir -p results

for PROFILE in ${PROFILES}; do
  export SEC_PROFILE=${PROFILE}
  for PROTOCOL in ${PROTOCOLS}; do
    for RPS in "${RPS_LIST[@]}"; do
      echo "\n[run-matrix] Profile=${PROFILE} Protocol=${PROTOCOL} Workload=${WORKLOAD} RPS=${RPS}"
      curl -sS -X POST "${RESULTS_URL%/}/api/benchrunner/run" \
        -H 'Content-Type: application/json' \
        -d "{\"protocol\":\"${PROTOCOL}\",\"security\":\"${PROFILE}\",\"workload\":\"${WORKLOAD}\",\"rps\":${RPS},\"duration\":${DURATION},\"warmup\":${WARMUP},\"connections\":${CONNECTIONS}}" >/dev/null

      # Archive the latest run entry for traceability.
      STAMP=$(date +%Y%m%d-%H%M%S)
      OUT="results/${STAMP}-${PROFILE}-${PROTOCOL}-${WORKLOAD}-rps${RPS}.json"
      curl -sS "${RESULTS_URL%/}/api/runs" | jq '.[0]' >"${OUT}"
      echo "[run-matrix] Saved ${OUT}"
    done
  done
done

echo "\n[run-matrix] Complete. You can now export all runs or analyze them."

