import {
  Area,
  AreaChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis
} from 'recharts';

interface LatencyPoint {
  percentile: number;
  value: number;
}

interface LatencyChartProps {
  data: LatencyPoint[];
}

export function LatencyChart({ data }: LatencyChartProps) {
  const hasData = data.length > 0;

  return (
    <div className="bg-white border border-slate-200 rounded-xl shadow-sm p-6">
      <h3 className="text-sm font-semibold text-slate-600 mb-4">Latency Percentiles (ms)</h3>
      {hasData ? (
        <ResponsiveContainer width="100%" height={260}>
          <AreaChart data={data}>
            <defs>
              <linearGradient id="latencyGradient" x1="0" y1="0" x2="0" y2="1">
                <stop offset="5%" stopColor="#2563eb" stopOpacity={0.3} />
                <stop offset="95%" stopColor="#2563eb" stopOpacity={0} />
              </linearGradient>
            </defs>
            <CartesianGrid strokeDasharray="4 4" stroke="#e2e8f0" />
            <XAxis dataKey="percentile" tickFormatter={(value) => `P${value}`} stroke="#94a3b8" />
            <YAxis stroke="#94a3b8" domain={[0, 'dataMax + 20']} />
            <Tooltip formatter={(value: number) => `${value.toFixed(1)} ms`} labelFormatter={(label) => `P${label}`} />
            <Area
              type="monotone"
              dataKey="value"
              stroke="#2563eb"
              fill="url(#latencyGradient)"
              strokeWidth={2}
            />
          </AreaChart>
        </ResponsiveContainer>
      ) : (
        <p className="text-sm text-slate-500">Latency metrics will appear once a benchmark run completes.</p>
      )}
    </div>
  );
}
