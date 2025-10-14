import { PageHeader } from '../components/layout/PageHeader';
import { useRunHistory } from '../contexts/RunHistoryContext';

export function HistoryPage() {
  const { runs, isLoading, error, selectedRunIds, toggleSelection, refresh } = useRunHistory();

  const renderBody = () => {
    if (isLoading) {
      return (
        <tr>
          <td colSpan={7} className="px-4 py-6 text-center text-sm text-slate-500">
            Loading recent runs...
          </td>
        </tr>
      );
    }

    if (error) {
      return (
        <tr>
          <td colSpan={7} className="px-4 py-6 text-center text-sm text-red-600">
            {error}.{' '}
            <button
              type="button"
              className="font-medium text-primary-600 hover:text-primary-500"
              onClick={() => {
                void refresh();
              }}
            >
              Retry
            </button>
          </td>
        </tr>
      );
    }

    if (runs.length === 0) {
      return (
        <tr>
          <td colSpan={7} className="px-4 py-6 text-center text-sm text-slate-500">
            No benchmark runs recorded yet. Trigger a run to populate history.
          </td>
        </tr>
      );
    }

    return runs.map((run) => {
      const started = formatDate(run.startedAt);
      const protocol = run.configuration.protocol ?? 'unknown';
      const security = run.configuration.security ?? 'unknown';
      const rps = run.configuration.rps ?? '-';
      const workload = run.configuration.workload ?? 'n/a';

      return (
        <tr key={run.id} className="hover:bg-slate-50">
          <td className="px-4 py-3">
            <input
              type="checkbox"
              className="h-4 w-4 rounded border-slate-300 text-primary-500 focus:ring-primary-200"
              checked={selectedRunIds.includes(run.id)}
              onChange={() => toggleSelection(run.id)}
              aria-label={`Select run ${run.name}`}
            />
          </td>
          <td className="px-4 py-3 text-sm font-medium text-primary-600">{run.name}</td>
          <td className="px-4 py-3 text-xs font-semibold uppercase tracking-wide text-slate-500">
            {protocol}
          </td>
          <td className="px-4 py-3 text-xs font-semibold uppercase tracking-wide text-slate-500">
            {security}
          </td>
          <td className="px-4 py-3 text-sm text-slate-600">{rps}</td>
          <td className="px-4 py-3 text-sm text-slate-600">{workload}</td>
          <td className="px-4 py-3 text-sm text-slate-500">{started}</td>
        </tr>
      );
    });
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title="History"
        description="Browse previous BenchRunner executions and drill into recorded metrics"
      />

      <div className="bg-white border border-slate-200 rounded-xl shadow-sm overflow-hidden">
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200">
            <thead className="bg-slate-50">
              <tr>
                <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wide">
                  Select
                </th>
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
            <tbody className="divide-y divide-slate-100">{renderBody()}</tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

function formatDate(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}
