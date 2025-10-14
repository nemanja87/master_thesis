import { NavLink } from 'react-router-dom';

const links = [
  { to: '/run', label: 'Run Experiments' },
  { to: '/compare', label: 'Compare' },
  { to: '/history', label: 'History' }
];

export function Sidebar() {
  return (
    <aside className="w-64 bg-white shadow-lg h-screen sticky top-0">
      <div className="p-6 border-b border-slate-200">
        <h1 className="text-xl font-semibold text-primary-600">SG Benchmark</h1>
        <p className="text-sm text-slate-500">Observability + Experimentation</p>
      </div>
      <nav className="p-4 flex flex-col space-y-2">
        {links.map((link) => (
          <NavLink
            key={link.to}
            to={link.to}
            className={({ isActive }) =>
              `px-3 py-2 rounded-md text-sm font-medium transition-colors ${
                isActive ? 'bg-primary-50 text-primary-600' : 'text-slate-600 hover:bg-slate-100'
              }`
            }
          >
            {link.label}
          </NavLink>
        ))}
      </nav>
    </aside>
  );
}
