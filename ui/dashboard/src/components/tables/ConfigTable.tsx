interface ConfigRow {
  key: string;
  value: string;
}

interface ConfigTableProps {
  rows: ConfigRow[];
}

export function ConfigTable({ rows }: ConfigTableProps) {
  if (rows.length === 0) {
    return (
      <div className="p-6 text-sm text-slate-500">
        Trigger a run to see captured configuration and metadata.
      </div>
    );
  }

  return (
    <table className="min-w-full divide-y divide-slate-200">
      <thead className="bg-slate-50">
        <tr>
          <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wide">Key</th>
          <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wide">Value</th>
        </tr>
      </thead>
      <tbody className="divide-y divide-slate-100">
        {rows.map((row) => (
          <tr key={row.key} className="hover:bg-slate-50">
            <td className="px-4 py-3 text-sm font-medium text-slate-700">{row.key}</td>
            <td className="px-4 py-3 text-sm text-slate-600 break-all">{row.value}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
