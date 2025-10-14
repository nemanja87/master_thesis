import { Navigate, Route, Routes } from 'react-router-dom';
import { Sidebar } from './components/layout/Sidebar';
import { RunExperimentsPage } from './pages/RunExperimentsPage';
import { ComparePage } from './pages/ComparePage';
import { HistoryPage } from './pages/HistoryPage';

function App() {
  return (
    <div className="flex min-h-screen bg-slate-100">
      <Sidebar />
      <main className="flex-1 p-6 lg:p-10">
        <Routes>
          <Route path="/run" element={<RunExperimentsPage />} />
          <Route path="/compare" element={<ComparePage />} />
          <Route path="/history" element={<HistoryPage />} />
          <Route path="*" element={<Navigate to="/run" replace />} />
        </Routes>
      </main>
    </div>
  );
}

export default App;
