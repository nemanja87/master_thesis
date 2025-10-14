export type Protocol = 'rest' | 'grpc';
export type SecurityMode = 'none' | 'tls' | 'mtls';

export interface BenchRunRequest {
  protocol: Protocol;
  security: SecurityMode;
  workload: string;
  rps: number;
  duration: number;
  warmup: number;
  connections: number;
}

export interface BenchRunResult {
  id: string;
  configuration?: Record<string, unknown>;
  latency?: Array<{ percentile: number; value: number }>;
  cpu?: Array<{ timestamp: string; value: number }>;
}

export interface MetricSummary {
  id: string;
  name: string;
  unit: string;
  value: number;
}

export interface RunConfigurationSnapshot {
  protocol?: Protocol;
  security?: SecurityMode;
  workload?: string;
  rps?: number;
  durationSeconds?: number;
  warmupSeconds?: number;
  connections?: number;
  [key: string]: unknown;
}

export interface RunRecord {
  id: string;
  name: string;
  environment: string;
  startedAt: string;
  completedAt?: string;
  configurationJson: string;
  configuration: RunConfigurationSnapshot;
  metrics: MetricSummary[];
}
