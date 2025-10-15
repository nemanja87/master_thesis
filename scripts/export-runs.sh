#!/usr/bin/env bash
set -euo pipefail

# Export all runs from ResultsService to a single JSON file and a CSV summary (if jq is available).
#
# Usage:
#   scripts/export-runs.sh [outfile-prefix]
#
# Env:
#   RESULTS_URL  ResultsService base URL (default http://localhost:8000)

RESULTS_URL=${RESULTS_URL:-http://localhost:8000}
PREFIX=${1:-runs}

mkdir -p results

ALL_JSON="results/${PREFIX}-all.json"
curl -sS "${RESULTS_URL%/}/api/runs" | jq '.' >"${ALL_JSON}"
echo "[export-runs] Wrote ${ALL_JSON}"

if command -v jq >/dev/null 2>&1; then
  CSV="results/${PREFIX}-summary.csv"
  # Basic summary: id, startedAt, protocol/security/workload/rps, metric name/value
  jq -r '
    .[] | . as $r | (.metrics[]? // []) | 
    [$r.id, $r.startedAt, ($r.configuration | fromjson | .protocol), ($r.configuration | fromjson | .security), ($r.configuration | fromjson | .workload), ($r.configuration | fromjson | .rps), .name, .value] 
    | @csv' "${ALL_JSON}" >"${CSV}"
  echo "[export-runs] Wrote ${CSV}"
fi

