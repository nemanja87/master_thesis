import { Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis, CartesianGrid } from 'recharts';

interface CpuPoint {
  timestamp: string;
  value: number;
}

interface CpuChartProps {
  data: CpuPoint[];
}

export function CpuChart({ data }: CpuChartProps) {
  const hasData = data.length > 0;

  return (
    <div className="bg-white border border-slate-200 rounded-xl shadow-sm p-6">
      <h3 className="text-sm font-semibold text-slate-600 mb-4">CPU Utilization (avg)</h3>
      {hasData ? (
        <ResponsiveContainer width="100%" height={260}>
          <LineChart data={data}>
            <CartesianGrid strokeDasharray="4 4" stroke="#e2e8f0" />
            <XAxis
              dataKey="timestamp"
              tickFormatter={(value) => new Date(value).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
              stroke="#94a3b8"
            />
            <YAxis unit="%" stroke="#94a3b8" domain={[0, 100]} />
            <Tooltip
              formatter={(value: number) => `${value.toFixed(1)} %`}
              labelFormatter={(label) => new Date(label).toLocaleString()}
            />
            <Line type="monotone" dataKey="value" stroke="#1d4ed8" strokeWidth={2} dot={false} />
          </LineChart>
        </ResponsiveContainer>
      ) : (
        <p className="text-sm text-slate-500">CPU metrics will appear when Prometheus queries are configured.</p>
      )}
    </div>
  );
}
