import { useMemo } from 'react';
import { PageHeader } from '../components/layout/PageHeader';
import { RunControls } from '../components/forms/RunControls';
import { LatencyChart } from '../components/charts/LatencyChart';
import { CpuChart } from '../components/charts/CpuChart';
import { ConfigTable } from '../components/tables/ConfigTable';
import { useBenchRunner } from '../hooks/useBenchRunner';

export function RunExperimentsPage() {
  const { latestRun, triggerRun, isRunning, error } = useBenchRunner();

  const configRows = useMemo(() => {
    if (!latestRun) {
      return [];
    }

    return Object.entries(latestRun.configuration ?? {}).map(([key, value]) => ({
      key,
      value: typeof value === 'object' ? JSON.stringify(value) : String(value)
    }));
  }, [latestRun]);

  return (
    <div className="space-y-10">
      <PageHeader
        title="Run Experiments"
        description="Configure and trigger new synthetic load tests via BenchRunner"
      />

      <RunControls isRunning={isRunning} onSubmit={triggerRun} />

      {error ? (
        <div className="bg-red-50 border border-red-200 text-red-600 text-sm rounded-lg px-4 py-3">
          {error}
        </div>
      ) : null}

      <section className="grid gap-6 lg:grid-cols-2">
        <LatencyChart data={latestRun?.latency ?? []} />
        <CpuChart data={latestRun?.cpu ?? []} />
      </section>

      <section className="bg-white rounded-xl shadow-sm border border-slate-200">
        <ConfigTable rows={configRows} />
      </section>
    </div>
  );
}
