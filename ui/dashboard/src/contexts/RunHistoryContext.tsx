import { createContext, ReactNode, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { fetchRuns } from '../services/resultsService';
import { RunRecord } from '../types/bench';

interface RunHistoryContextValue {
  runs: RunRecord[];
  isLoading: boolean;
  error: string | null;
  selectedRunIds: string[];
  selectedRuns: RunRecord[];
  toggleSelection: (runId: string) => void;
  clearSelection: () => void;
  refresh: () => Promise<void>;
}

const RunHistoryContext = createContext<RunHistoryContextValue | undefined>(undefined);

export function RunHistoryProvider({ children }: { children: ReactNode }) {
  const [runs, setRuns] = useState<RunRecord[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedRunIds, setSelectedRunIds] = useState<string[]>([]);

  const refresh = useCallback(async () => {
    setIsLoading(true);
    try {
      const data = await fetchRuns();
      setRuns(data);
      setError(null);
    } catch (err) {
      console.error('Failed to load runs', err);
      setError(err instanceof Error ? err.message : 'Failed to load benchmark runs.');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  useEffect(() => {
    setSelectedRunIds((previous) => previous.filter((id) => runs.some((run) => run.id === id)));
  }, [runs]);

  const toggleSelection = useCallback((runId: string) => {
    setSelectedRunIds((previous) =>
      previous.includes(runId) ? previous.filter((id) => id !== runId) : [...previous, runId]
    );
  }, []);

  const clearSelection = useCallback(() => setSelectedRunIds([]), []);

  const selectedRuns = useMemo(
    () => runs.filter((run) => selectedRunIds.includes(run.id)),
    [runs, selectedRunIds]
  );

  const value = useMemo(
    () => ({
      runs,
      isLoading,
      error,
      selectedRunIds,
      selectedRuns,
      toggleSelection,
      clearSelection,
      refresh
    }),
    [runs, isLoading, error, selectedRunIds, selectedRuns, toggleSelection, clearSelection, refresh]
  );

  return <RunHistoryContext.Provider value={value}>{children}</RunHistoryContext.Provider>;
}

export function useRunHistory(): RunHistoryContextValue {
  const context = useContext(RunHistoryContext);
  if (!context) {
    throw new Error('useRunHistory must be used within a RunHistoryProvider');
  }

  return context;
}
