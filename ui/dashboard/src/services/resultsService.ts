import axios from 'axios';
import {
  BenchRunRequest,
  BenchRunResult,
  MetricSummary,
  Protocol,
  RunConfigurationSnapshot,
  RunRecord,
  SecurityMode
} from '../types/bench';

const RESULTS_BASE_URL = import.meta.env.VITE_RESULTS_SERVICE_URL ?? 'http://localhost:8000';

export async function triggerBenchRun(payload: BenchRunRequest): Promise<BenchRunResult | null> {
  const endpoint = `${RESULTS_BASE_URL.replace(/\/$/, '')}/api/benchrunner/run`;

  await axios.post(endpoint, payload, {
    headers: {
      'Content-Type': 'application/json'
    }
  });

  const runs = await fetchRuns();
  if (runs.length === 0) {
    return null;
  }

  return mapRunRecordToBenchResult(runs[0]);
}

interface RunResponseDto {
  id: string;
  name: string;
  environment: string;
  startedAt: string;
  completedAt?: string | null;
  configuration: string;
  metrics: Array<MetricResponseDto>;
}

interface MetricResponseDto {
  id: string;
  name: string;
  unit: string;
  value: number;
}

export async function fetchRuns(): Promise<RunRecord[]> {
  const endpoint = `${RESULTS_BASE_URL.replace(/\/$/, '')}/api/runs`;
  const response = await axios.get<RunResponseDto[]>(endpoint);
  return response.data.map(mapRunResponse);
}

function mapRunRecordToBenchResult(run: RunRecord): BenchRunResult {
  return {
    id: run.id,
    configuration: run.configuration,
    latency: extractLatencyPoints(run.metrics),
    cpu: extractCpuSamples(run)
  };
}

function mapRunResponse(dto: RunResponseDto): RunRecord {
  return {
    id: dto.id,
    name: dto.name,
    environment: dto.environment,
    startedAt: dto.startedAt,
    completedAt: dto.completedAt ?? undefined,
    configurationJson: dto.configuration,
    configuration: parseConfiguration(dto.configuration),
    metrics: dto.metrics.map(mapMetricResponse)
  };
}

function mapMetricResponse(dto: MetricResponseDto): MetricSummary {
  return {
    id: dto.id,
    name: dto.name,
    unit: dto.unit,
    value: dto.value
  };
}

function extractLatencyPoints(metrics: MetricSummary[]): Array<{ percentile: number; value: number }> {
  const points = new Map<number, number>();

  for (const metric of metrics) {
    const normalizedName = metric.name.toLowerCase();
    const percentileMatch = normalizedName.match(/(http_req_duration|ghz_latency)_p(\d+)/);
    if (percentileMatch) {
      const percentile = Number(percentileMatch[2]);
      if (!Number.isNaN(percentile)) {
        points.set(percentile, metric.value);
      }
      continue;
    }

    if (normalizedName === 'http_req_duration_avg' || normalizedName === 'ghz_latency_avg') {
      points.set(50, metric.value);
    }
  }

  return Array.from(points.entries())
    .sort((a, b) => a[0] - b[0])
    .map(([percentile, value]) => ({ percentile, value }));
}

function extractCpuSamples(run: RunRecord): Array<{ timestamp: string; value: number }> {
  const avgMetric = run.metrics.find((metric) => metric.name.toLowerCase().endsWith('_cpu_avg'));
  if (!avgMetric) {
    return [];
  }

  const start = new Date(run.startedAt).getTime();
  const end = run.completedAt ? new Date(run.completedAt).getTime() : start + 60_000;
  const interval = Math.max(1, Math.floor((end - start) / 5));

  return Array.from({ length: 5 }).map((_, index) => ({
    timestamp: new Date(start + index * interval).toISOString(),
    value: avgMetric.value
  }));
}

function parseConfiguration(json: string): RunConfigurationSnapshot {
  try {
    const parsed = JSON.parse(json) as Record<string, unknown>;
    const snapshot: RunConfigurationSnapshot = {};

    for (const [key, value] of Object.entries(parsed)) {
      snapshot[key] = value;
    }

    const protocol = toProtocol(parsed['protocol']);
    if (protocol) {
      snapshot.protocol = protocol;
    } else {
      delete snapshot.protocol;
    }

    const security = toSecurityMode(parsed['security']);
    if (security) {
      snapshot.security = security;
    } else {
      delete snapshot.security;
    }

    const workload = typeof parsed['workload'] === 'string' ? parsed['workload'] : undefined;
    if (workload) {
      snapshot.workload = workload;
    } else {
      delete snapshot.workload;
    }

    const rps = toNumber(parsed['rps']);
    if (typeof rps === 'number') {
      snapshot.rps = rps;
    } else {
      delete snapshot.rps;
    }

    const duration = toNumber(parsed['durationSeconds']);
    if (typeof duration === 'number') {
      snapshot.durationSeconds = duration;
    } else {
      delete snapshot.durationSeconds;
    }

    const warmup = toNumber(parsed['warmupSeconds']);
    if (typeof warmup === 'number') {
      snapshot.warmupSeconds = warmup;
    } else {
      delete snapshot.warmupSeconds;
    }

    const connections = toNumber(parsed['connections']);
    if (typeof connections === 'number') {
      snapshot.connections = connections;
    } else {
      delete snapshot.connections;
    }

    return snapshot;
  } catch (error) {
    console.warn('Failed to parse run configuration JSON', error);
    return {};
  }
}

function toNumber(value: unknown): number | undefined {
  if (typeof value === 'number') {
    return Number.isFinite(value) ? value : undefined;
  }

  if (typeof value === 'string') {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : undefined;
  }

  return undefined;
}

function toProtocol(value: unknown): Protocol | undefined {
  if (value === 'rest' || value === 'grpc') {
    return value;
  }
  return undefined;
}

function toSecurityMode(value: unknown): SecurityMode | undefined {
  if (value === 'none' || value === 'tls' || value === 'mtls') {
    return value;
  }
  return undefined;
}
