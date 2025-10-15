import { useCallback, useState } from 'react';
import { BenchRunRequest, BenchRunResult } from '../types/bench';
import { triggerBenchRun } from '../services/resultsService';
import { useRunHistory } from '../contexts/RunHistoryContext';

interface BenchRunnerState {
  latestRun: BenchRunResult | null;
  isRunning: boolean;
  runStartedAt: number | null;
  error: string | null;
  triggerRun: (request: BenchRunRequest) => Promise<void>;
}

export function useBenchRunner(): BenchRunnerState {
  const [latestRun, setLatestRun] = useState<BenchRunResult | null>(null);
  const [isRunning, setIsRunning] = useState(false);
  const [runStartedAt, setRunStartedAt] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const { refresh } = useRunHistory();

  const triggerRun = useCallback(async (request: BenchRunRequest) => {
    try {
      setIsRunning(true);
      setError(null);
      setRunStartedAt(Date.now());
      const result = await triggerBenchRun(request);
      setLatestRun(result ?? null);
      await refresh();
    } catch (err) {
      console.error(err);
      setError('Failed to trigger BenchRunner. See console for details.');
    } finally {
      setIsRunning(false);
      setRunStartedAt(null);
    }
  }, [refresh]);

  return {
    latestRun,
    isRunning,
    runStartedAt,
    error,
    triggerRun
  };
}
