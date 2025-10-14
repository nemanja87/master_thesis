import { PageHeader } from '../components/layout/PageHeader';
import { useRunHistory } from '../contexts/RunHistoryContext';

export function ComparePage() {
  const { selectedRuns, clearSelection } = useRunHistory();
  const hasEnoughRuns = selectedRuns.length >= 2;

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

          <div className="bg-white border border-slate-200 rounded-xl p-6 shadow-sm">
            <p className="text-sm text-slate-600">
              Detailed comparison charts and statistical deltas will appear here. For now you can verify that the
              selected runs are ready for deeper analysis. Capture additional metrics in BenchRunner to enrich this view.
            </p>
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

function formatDate(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}
