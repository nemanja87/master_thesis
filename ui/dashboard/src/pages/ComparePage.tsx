import { useMemo } from 'react';
import {
  CartesianGrid,
  Legend,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis
} from 'recharts';
import { PageHeader } from '../components/layout/PageHeader';
import { useRunHistory } from '../contexts/RunHistoryContext';
import type { MetricSummary, RunRecord } from '../types/bench';

const PERCENTILES = [50, 75, 90, 95, 99] as const;
type Percentile = (typeof PERCENTILES)[number];

const LINE_COLORS = ['#2563eb', '#0f766e', '#7c3aed', '#dc2626', '#049669', '#9333ea'];

interface NormalizedRun {
  run: RunRecord;
  label: string;
  protocol: string;
  latency: Partial<Record<Percentile, number>>;
  averageLatency?: number;
  throughput?: number;
  errorRate?: number;
}

export function ComparePage() {
  const { selectedRuns, clearSelection } = useRunHistory();
  const hasEnoughRuns = selectedRuns.length >= 2;

  const normalizedRuns = useMemo(
    () => selectedRuns.map(normalizeRun),
    [selectedRuns]
  );

  const latencySeries = useMemo(() => buildLatencyChartData(normalizedRuns), [normalizedRuns]);
  const comparisonRows = useMemo(() => buildComparisonRows(normalizedRuns), [normalizedRuns]);
  const insights = useMemo(() => buildInsights(normalizedRuns), [normalizedRuns]);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Compare"
        description="Visualize differences between benchmark runs and highlight regressions"
      />
      {hasEnoughRuns ? (
        <section className="space-y-6">
          <div className="flex items-center justify-between">
            <h2 className="text-lg font-semibold text-slate-800">Selected Runs</h2>
            <button
              type="button"
              onClick={clearSelection}
              className="text-sm font-medium text-primary-600 hover:text-primary-500"
            >
              Clear selection
            </button>
          </div>

          <div className="bg-white border border-slate-200 rounded-xl shadow-sm overflow-hidden">
            <table className="min-w-full divide-y divide-slate-200">
              <thead className="bg-slate-50">
                <tr>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wide">
                    Run
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wide">
                    Protocol
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wide">
                    Security
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wide">
                    RPS
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wide">
                    Workload
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wide">
                    Started
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {selectedRuns.map((run) => (
                  <tr key={run.id} className="hover:bg-slate-50">
                    <td className="px-4 py-3 text-sm font-medium text-primary-600">{run.name}</td>
                    <td className="px-4 py-3 text-xs font-semibold uppercase tracking-wide text-slate-500">
                      {run.configuration.protocol ?? 'unknown'}
                    </td>
                    <td className="px-4 py-3 text-xs font-semibold uppercase tracking-wide text-slate-500">
                      {run.configuration.security ?? 'unknown'}
                    </td>
                    <td className="px-4 py-3 text-sm text-slate-600">{run.configuration.rps ?? '-'}</td>
                    <td className="px-4 py-3 text-sm text-slate-600">{run.configuration.workload ?? 'n/a'}</td>
                    <td className="px-4 py-3 text-sm text-slate-500">{formatDate(run.startedAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {latencySeries.length > 0 ? (
            <div className="bg-white border border-slate-200 rounded-xl shadow-sm p-6 space-y-4">
              <h3 className="text-sm font-semibold text-slate-600">Latency percentiles</h3>
              <ResponsiveContainer width="100%" height={320}>
                <LineChart data={latencySeries}>
                  <CartesianGrid strokeDasharray="4 4" stroke="#e2e8f0" />
                  <XAxis dataKey="percentile" stroke="#94a3b8" />
                  <YAxis unit=" ms" stroke="#94a3b8" />
                  <Tooltip formatter={(value: number) => `${value.toFixed(2)} ms`} />
                  <Legend />
                  {normalizedRuns.map((run, index) => (
                    <Line
                      key={run.label}
                      dataKey={run.label}
                      type="monotone"
                      stroke={LINE_COLORS[index % LINE_COLORS.length]}
                      strokeWidth={2}
                      dot={false}
                    />
                  ))}
                </LineChart>
              </ResponsiveContainer>
            </div>
          ) : (
            <div className="bg-white border border-slate-200 rounded-xl shadow-sm p-6">
              <p className="text-sm text-slate-500">
                Percentile metrics (P50, P95, P99) were not available on the selected runs. Trigger new runs to
                populate latency charts.
              </p>
            </div>
          )}

          <div className="bg-white border border-slate-200 rounded-xl shadow-sm overflow-hidden">
            <table className="min-w-full divide-y divide-slate-200">
              <thead className="bg-slate-50">
                <tr>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wide">
                    Metric
                  </th>
                  {normalizedRuns.map((run) => (
                    <th
                      key={run.label}
                      className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wide"
                    >
                      {run.label}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {comparisonRows.map((row) => (
                  <tr key={row.metric}>
                    <td className="px-4 py-3 text-sm font-medium text-slate-600">{row.metric}</td>
                    {normalizedRuns.map((run) => (
                      <td key={run.label} className="px-4 py-3 text-sm text-slate-600">
                        {row.values[run.label]}
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="bg-white border border-slate-200 rounded-xl shadow-sm p-6 space-y-3">
            <h3 className="text-sm font-semibold text-slate-600">Key takeaways</h3>
            <ul className="list-disc list-inside space-y-2 text-sm text-slate-600">
              {insights.map((insight, index) => (
                <li key={index}>{insight}</li>
              ))}
            </ul>
          </div>
        </section>
      ) : (
        <div className="bg-white border border-slate-200 rounded-xl p-6 shadow-sm">
          <p className="text-sm text-slate-500">
            Select two or more runs from the history to compare latency percentiles, error rates, and resource
            utilization. Use the checkboxes in the{' '}
            <span className="font-semibold text-slate-600">History</span> tab to build a comparison set.
          </p>
        </div>
      )}
    </div>
  );
}

function normalizeRun(run: RunRecord): NormalizedRun {
  const protocol = run.configuration.protocol ?? 'unknown';
  const metrics = run.metrics;

  const latency: Partial<Record<Percentile, number>> = {};
  PERCENTILES.forEach((percentile) => {
    latency[percentile] = findMetricValue(metrics, [
      `http_req_duration_p${percentile}`,
      `http_req_duration_p(${percentile})`,
      `ghz_latency_p${percentile}`
    ]);
  });

  const averageLatency = findMetricValue(metrics, [
    'http_req_duration_avg',
    'ghz_latency_avg'
  ]);

  let throughput = findMetricValue(metrics, ['ghz_rps']);
  if (throughput === undefined) {
    const count = findMetricValue(metrics, ['http_reqs_count']);
    const duration = toNumber(run.configuration.durationSeconds ?? run.configuration.duration);
    if (count !== undefined && duration && duration > 0) {
      throughput = count / duration;
    }
  }

  const errorRate = findMetricValue(metrics, ['http_req_failed_rate', 'error_rate']);

  return {
    run,
    label: run.name,
    protocol,
    latency,
    averageLatency,
    throughput,
    errorRate
  };
}

function buildLatencyChartData(runs: NormalizedRun[]) {
  return PERCENTILES.map((percentile) => {
    const row: Record<string, unknown> = { percentile: `P${percentile}` };
    runs.forEach((run) => {
      const value = run.latency[percentile];
      if (value !== undefined && !Number.isNaN(value)) {
        row[run.label] = value;
      }
    });
    return row;
  }).filter((row) => Object.keys(row).length > 1);
}

function buildComparisonRows(runs: NormalizedRun[]) {
  const descriptors: Array<{
    metric: string;
    extractor: (run: NormalizedRun) => number | undefined;
    formatter?: (value: number | undefined) => string;
  }> = [
    {
      metric: 'Latency P50 (ms)',
      extractor: (run) => run.latency[50],
      formatter: (value) => formatNumber(value, ' ms')
    },
    {
      metric: 'Latency P95 (ms)',
      extractor: (run) => run.latency[95],
      formatter: (value) => formatNumber(value, ' ms')
    },
    {
      metric: 'Average latency (ms)',
      extractor: (run) => run.averageLatency,
      formatter: (value) => formatNumber(value, ' ms')
    },
    {
      metric: 'Throughput (requests/sec)',
      extractor: (run) => run.throughput,
      formatter: (value) => formatNumber(value, ' req/s')
    },
    {
      metric: 'Error rate (%)',
      extractor: (run) => (run.errorRate !== undefined ? run.errorRate * 100 : undefined),
      formatter: (value) => formatNumber(value, '%')
    }
  ];

  return descriptors.map(({ metric, extractor, formatter }) => {
    const values: Record<string, string> = {};
    runs.forEach((run) => {
      const value = extractor(run);
      values[run.label] = formatter ? formatter(value) : formatNumber(value);
    });

    return { metric, values };
  });
}

function buildInsights(runs: NormalizedRun[]) {
  if (runs.length < 2) {
    return ['Select at least two runs to generate comparison insights.'];
  }

  const insights: string[] = [];

  const runsWithLatency = runs
    .map((run) => ({ run, value: run.latency[95] ?? run.averageLatency }))
    .filter((entry): entry is { run: NormalizedRun; value: number } => entry.value !== undefined);

  if (runsWithLatency.length >= 2) {
    const sorted = [...runsWithLatency].sort((a, b) => a.value - b.value);
    const fastest = sorted[0];
    const runnerUp = sorted[1];
    insights.push(
      `${fastest.run.label} delivered the lowest high-percentile latency (${formatNumber(
        fastest.value,
        ' ms'
      )} at P95), ${formatNumber(runnerUp.value - fastest.value, ' ms')} faster than ${runnerUp.run.label}.`
    );
  }

  const runsWithThroughput = runs
    .map((run) => ({ run, value: run.throughput }))
    .filter((entry): entry is { run: NormalizedRun; value: number } => entry.value !== undefined);

  if (runsWithThroughput.length >= 2) {
    const sorted = [...runsWithThroughput].sort((a, b) => b.value - a.value);
    const top = sorted[0];
    const competitor = sorted[1];
    insights.push(
      `${top.run.label} achieved the highest throughput (${formatNumber(top.value, ' req/s')}), outpacing ${
        competitor.run.label
      } by ${formatNumber(top.value - competitor.value, ' req/s')}.`
    );
  }

  const runsWithErrors = runs
    .map((run) => ({ run, value: run.errorRate }))
    .filter((entry): entry is { run: NormalizedRun; value: number } => entry.value !== undefined);

  runsWithErrors.forEach((entry) => {
    if (entry.value === 0) {
      insights.push(`${entry.run.label} completed without recorded errors.`);
    } else {
      insights.push(
        `${entry.run.label} recorded an error rate of ${formatNumber(entry.value * 100, ' %')}. Review logs for failures.`
      );
    }
  });

  if (insights.length === 0) {
    insights.push('No overlapping metrics could be compared. Trigger runs with latency and throughput metrics.');
  }

  return insights;
}

function findMetricValue(metrics: MetricSummary[], candidates: string[]): number | undefined {
  for (const candidate of candidates) {
    const match = metrics.find((metric) => metric.name.toLowerCase() === candidate.toLowerCase());
    if (match && Number.isFinite(match.value)) {
      return match.value;
    }
  }
  return undefined;
}

function toNumber(value: unknown): number | undefined {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value;
  }
  if (typeof value === 'string') {
    const parsed = Number(value);
    if (Number.isFinite(parsed)) {
      return parsed;
    }
  }
  return undefined;
}

function formatNumber(value: number | undefined, suffix = '', fractionDigits = 2) {
  if (value === undefined || Number.isNaN(value)) {
    return '—';
  }
  return `${value.toFixed(fractionDigits)}${suffix}`;
}

function formatDate(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}
