#!/usr/bin/env python3
"""
Aggregate ResultsService runs into median latency/throughput/CPU by (protocol, security, workload, rps)
and generate simple CSV files and plots.

Usage:
  python analysis/analyze_runs.py [--url http://localhost:8000] [--out analysis/out]

Requires: pandas, matplotlib (install via `pip install pandas matplotlib`).
"""
import argparse
import json
import os
from collections import defaultdict

try:
    import pandas as pd
    import matplotlib.pyplot as plt
except ImportError as e:
    pd = None
    plt = None

import urllib.request


def fetch_runs(url: str):
    with urllib.request.urlopen(url.rstrip('/') + '/api/runs') as resp:
        data = resp.read()
    return json.loads(data)


def extract_row(run):
    cfg = json.loads(run.get('configuration', '{}'))
    return {
        'id': run['id'],
        'startedAt': run['startedAt'],
        'protocol': cfg.get('protocol'),
        'security': cfg.get('security'),
        'workload': cfg.get('workload'),
        'rps': cfg.get('rps'),
        'metrics': run.get('metrics', []),
    }


def pick_metric(metrics, names):
    names_lower = [n.lower() for n in names]
    for m in metrics:
        if m['name'].lower() in names_lower:
            return float(m['value'])
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--url', default='http://localhost:8000')
    ap.add_argument('--out', default='analysis/out')
    args = ap.parse_args()

    runs = fetch_runs(args.url)
    os.makedirs(args.out, exist_ok=True)

    rows = []
    for run in runs:
        row = extract_row(run)
        m = row['metrics']
        # latency (ms)
        row['latency_p95'] = pick_metric(m, ['http_req_duration_p95', 'ghz_latency_p95'])
        row['latency_p50'] = pick_metric(m, ['http_req_duration_p50', 'ghz_latency_p50', 'ghz_latency_avg'])
        # throughput
        row['throughput_rps'] = pick_metric(m, ['ghz_rps'])
        if row['throughput_rps'] is None:
            # approximate for k6: http_reqs_count / durationSeconds — duration is in config, but not here; skip precise calc
            pass
        # cpu
        row['cpu_avg'] = None
        for mm in m:
            if mm['name'].lower().endswith('_cpu_avg'):
                row['cpu_avg'] = float(mm['value'])
                break
        rows.append(row)

    if pd is None:
        out_json = os.path.join(args.out, 'flattened.json')
        with open(out_json, 'w') as f:
            json.dump(rows, f, indent=2)
        print(f"Wrote {out_json}. Install pandas/matplotlib for CSVs and plots.")
        return

    df = pd.DataFrame(rows)
    df.to_csv(os.path.join(args.out, 'runs_flat.csv'), index=False)

    group_cols = ['workload', 'protocol', 'security', 'rps']
    agg = df.groupby(group_cols).agg(
        latency_p95_median=('latency_p95', 'median'),
        latency_p50_median=('latency_p50', 'median'),
        throughput_rps_median=('throughput_rps', 'median'),
        cpu_avg_median=('cpu_avg', 'median'),
        count=('id', 'count'),
    ).reset_index()
    agg.to_csv(os.path.join(args.out, 'summary_by_group.csv'), index=False)
    print(f"Wrote {os.path.join(args.out, 'summary_by_group.csv')}")

    # Simple plots: latency p95 vs RPS per security profile for REST & gRPC
    if plt is not None:
        for workload in agg['workload'].unique():
            for security in agg['security'].unique():
                subset = agg[(agg['workload'] == workload) & (agg['security'] == security)]
                if subset.empty:
                    continue
                fig, ax = plt.subplots(figsize=(7, 4))
                for proto in ['rest', 'grpc']:
                    d = subset[subset['protocol'] == proto].sort_values('rps')
                    if d.empty:
                        continue
                    ax.plot(d['rps'], d['latency_p95_median'], marker='o', label=proto.upper())
                ax.set_title(f"{workload} – p95 Latency vs RPS ({security})")
                ax.set_xlabel('RPS')
                ax.set_ylabel('Latency p95 (ms)')
                ax.grid(True, linestyle='--', alpha=0.4)
                ax.legend()
                out_png = os.path.join(args.out, f"{workload}-{security}-latency_p95.png")
                plt.tight_layout()
                plt.savefig(out_png)
                plt.close(fig)
                print(f"Wrote {out_png}")


if __name__ == '__main__':
    main()

