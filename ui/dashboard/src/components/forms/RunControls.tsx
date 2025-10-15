import { useEffect, useMemo, useState } from 'react';
import { BenchRunRequest, Protocol, SecurityMode } from '../../types/bench';

interface RunControlsProps {
  isRunning?: boolean;
  runStartedAt: number | null;
  onSubmit: (request: BenchRunRequest) => Promise<void> | void;
}

const protocolOptions: Protocol[] = ['rest', 'grpc'];
const securityOptions: SecurityMode[] = ['none', 'tls', 'mtls'];

const workloadOptions = [
  { value: 'orders-create', label: 'Orders - Create' },
  { value: 'inventory-reserve', label: 'Inventory - Reserve' }
];

export function RunControls({ isRunning = false, runStartedAt, onSubmit }: RunControlsProps) {
  const [protocol, setProtocol] = useState<Protocol>('rest');
  const [security, setSecurity] = useState<SecurityMode>('tls');
  const [workload, setWorkload] = useState<string>(workloadOptions[0]?.value ?? 'orders-create');
  const [rps, setRps] = useState<number>(50);
  const [duration, setDuration] = useState<number>(60);
  const [warmup, setWarmup] = useState<number>(10);
  const [connections, setConnections] = useState<number>(10);

  const derivedRequest = useMemo<BenchRunRequest>(
    () => ({ protocol, security, workload, rps, duration, warmup, connections }),
    [protocol, security, workload, rps, duration, warmup, connections]
  );

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    await onSubmit(derivedRequest);
  };

  const elapsed = useElapsedTime(isRunning, runStartedAt);
  const buttonLabel = isRunning ? `Running… ${formatElapsed(elapsed)}` : 'Trigger BenchRunner';

  return (
    <form
      onSubmit={handleSubmit}
      className="bg-white border border-slate-200 rounded-xl shadow-sm p-6 grid gap-6 lg:grid-cols-3"
    >
      <div className="flex flex-col gap-2">
        <label className="text-sm font-medium text-slate-600" htmlFor="protocol">
          Protocol
        </label>
        <select
          id="protocol"
          className="select"
          value={protocol}
          onChange={(event) => setProtocol(event.target.value as Protocol)}
        >
          {protocolOptions.map((option) => (
            <option key={option} value={option}>
              {option.toUpperCase()}
            </option>
          ))}
        </select>
      </div>

      <div className="flex flex-col gap-2">
        <label className="text-sm font-medium text-slate-600" htmlFor="security">
          Security Mode
        </label>
        <select
          id="security"
          className="select"
          value={security}
          onChange={(event) => setSecurity(event.target.value as SecurityMode)}
        >
          {securityOptions.map((option) => (
            <option key={option} value={option}>
              {option.toUpperCase()}
            </option>
          ))}
        </select>
      </div>

      <div className="flex flex-col gap-2">
        <label className="text-sm font-medium text-slate-600" htmlFor="workload">
          Workload
        </label>
        <select
          id="workload"
          className="select"
          value={workload}
          onChange={(event) => setWorkload(event.target.value)}
        >
          {workloadOptions.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
      </div>

      <div className="flex flex-col gap-2">
        <label className="text-sm font-medium text-slate-600" htmlFor="rps">
          Requests per Second
        </label>
        <input
          id="rps"
          type="number"
          min={1}
          className="input"
          value={rps}
          onChange={(event) => setRps(Number(event.currentTarget.value))}
        />
      </div>

      <div className="flex flex-col gap-2">
        <label className="text-sm font-medium text-slate-600" htmlFor="duration">
          Duration (seconds)
        </label>
        <input
          id="duration"
          type="number"
          min={5}
          className="input"
          value={duration}
          onChange={(event) => setDuration(Number(event.currentTarget.value))}
        />
      </div>

      <div className="flex flex-col gap-2">
        <label className="text-sm font-medium text-slate-600" htmlFor="warmup">
          Warmup (seconds)
        </label>
        <input
          id="warmup"
          type="number"
          min={0}
          className="input"
          value={warmup}
          onChange={(event) => setWarmup(Number(event.currentTarget.value))}
        />
      </div>

      <div className="flex flex-col gap-2">
        <label className="text-sm font-medium text-slate-600" htmlFor="connections">
          Connections
        </label>
        <input
          id="connections"
          type="number"
          min={1}
          className="input"
          value={connections}
          onChange={(event) => setConnections(Number(event.currentTarget.value))}
        />
      </div>

      <div className="flex items-end">
        <button type="submit" className="btn btn-primary w-full" disabled={isRunning}>
          {buttonLabel}
        </button>
      </div>
    </form>
  );
}

function useElapsedTime(isRunning: boolean, startedAt: number | null) {
  const [elapsed, setElapsed] = useState(0);

  useEffect(() => {
    if (!isRunning || startedAt === null) {
      setElapsed(0);
      return;
    }

    setElapsed(Math.floor((Date.now() - startedAt) / 1000));

    const interval = window.setInterval(() => {
      setElapsed(Math.floor((Date.now() - startedAt) / 1000));
    }, 1000);

    return () => window.clearInterval(interval);
  }, [isRunning, startedAt]);

  return elapsed;
}

function formatElapsed(totalSeconds: number) {
  const minutes = Math.floor(totalSeconds / 60)
    .toString()
    .padStart(2, '0');
  const seconds = (totalSeconds % 60).toString().padStart(2, '0');
  return `${minutes}:${seconds}`;
}
