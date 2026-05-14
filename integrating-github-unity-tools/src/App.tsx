import { useState } from 'react';
import { TabId } from './types';
import ConnectionPanel from './components/ConnectionPanel';
import DashboardView from './components/DashboardView';
import ToolsView from './components/ToolsView';
import SkillsView from './components/SkillsView';
import InfrastructureView from './components/InfrastructureView';
import TerminalView from './components/TerminalView';
import StatusBadge from './components/StatusBadge';

const TABS: { id: TabId; label: string; icon: string }[] = [
  { id: 'dashboard', label: 'Dashboard', icon: '🏠' },
  { id: 'tools', label: 'Tools (82)', icon: '🛠️' },
  { id: 'skills', label: 'Skills', icon: '📖' },
  { id: 'infrastructure', label: 'Infrastructure', icon: '🏗️' },
  { id: 'terminal', label: 'Terminal', icon: '💻' },
];

export default function App() {
  const [tab, setTab] = useState<TabId>('dashboard');
  const [sidebarOpen, setSidebarOpen] = useState(true);
  const [url, setUrl] = useState('');
  const [token, setToken] = useState('');
  const [status, setStatus] = useState<'disconnected' | 'connecting' | 'connected' | 'error'>('disconnected');

  const handleConnect = () => {
    if (!url || !token) return;
    setStatus('connecting');
    // Simulate connection attempt
    setTimeout(() => {
      if (url.includes('trycloudflare') || url.startsWith('https://')) {
        setStatus('connected');
      } else {
        setStatus('error');
      }
    }, 1500);
  };

  const handleDisconnect = () => {
    setStatus('disconnected');
  };

  return (
    <div className="min-h-screen bg-slate-100 flex flex-col">
      {/* Top bar */}
      <header className="bg-slate-900 text-white px-4 py-3 flex items-center gap-4 shadow-lg z-10 flex-shrink-0">
        <button
          onClick={() => setSidebarOpen(v => !v)}
          className="text-slate-400 hover:text-white transition-colors p-1"
          title="Toggle sidebar"
        >
          <svg className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" d="M4 6h16M4 12h16M4 18h16" />
          </svg>
        </button>

        <div className="flex items-center gap-2">
          <span className="text-2xl">🎮</span>
          <div>
            <h1 className="text-sm font-bold leading-none">Unity MCP Control Panel</h1>
            <p className="text-xs text-slate-400 mt-0.5">v0.72.1 · 82 tools · 13.05.2026 snapshot</p>
          </div>
        </div>

        <div className="ml-auto flex items-center gap-3">
          <StatusBadge status={status} />
          <span className="text-xs text-slate-400 hidden sm:block">
            {status === 'connected' ? `→ ${url.slice(0, 35)}...` : 'Not connected to Unity'}
          </span>
        </div>
      </header>

      <div className="flex flex-1 overflow-hidden">
        {/* Sidebar */}
        <aside
          className={`bg-slate-900 flex-shrink-0 transition-all duration-300 overflow-hidden ${sidebarOpen ? 'w-56' : 'w-0'}`}
        >
          <div className="w-56 p-3 space-y-1 pt-4">
            {TABS.map(t => (
              <button
                key={t.id}
                onClick={() => setTab(t.id)}
                className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-colors ${
                  tab === t.id
                    ? 'bg-indigo-600 text-white shadow-lg shadow-indigo-900/30'
                    : 'text-slate-400 hover:text-white hover:bg-slate-800'
                }`}
              >
                <span className="text-base">{t.icon}</span>
                <span>{t.label}</span>
              </button>
            ))}

            <div className="pt-4 border-t border-slate-800">
              <p className="text-xs text-slate-600 px-3 mb-2 font-semibold uppercase tracking-wider">Репо</p>
              <a
                href="https://github.com/aliter230880/unity/tree/main/unity13.05version"
                target="_blank"
                rel="noopener noreferrer"
                className="flex items-center gap-3 px-3 py-2 rounded-xl text-xs text-slate-500 hover:text-white hover:bg-slate-800 transition-colors"
              >
                <span>🐙</span>
                <span>unity13.05version</span>
              </a>
              <a
                href="https://github.com/IvanMurzak/Unity-MCP"
                target="_blank"
                rel="noopener noreferrer"
                className="flex items-center gap-3 px-3 py-2 rounded-xl text-xs text-slate-500 hover:text-white hover:bg-slate-800 transition-colors"
              >
                <span>🔌</span>
                <span>Unity-MCP Plugin</span>
              </a>
            </div>
          </div>
        </aside>

        {/* Main content */}
        <main className="flex-1 overflow-auto">
          <div className="max-w-6xl mx-auto p-5 space-y-5">
            {/* Connection panel — always visible */}
            {tab !== 'dashboard' && (
              <div className="lg:hidden">
                <ConnectionPanel
                  url={url}
                  token={token}
                  status={status}
                  onUrlChange={setUrl}
                  onTokenChange={setToken}
                  onConnect={handleConnect}
                  onDisconnect={handleDisconnect}
                />
              </div>
            )}

            {tab === 'dashboard' && (
              <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
                <div className="lg:col-span-2">
                  <DashboardView />
                </div>
                <div>
                  <ConnectionPanel
                    url={url}
                    token={token}
                    status={status}
                    onUrlChange={setUrl}
                    onTokenChange={setToken}
                    onConnect={handleConnect}
                    onDisconnect={handleDisconnect}
                  />
                </div>
              </div>
            )}

            {tab === 'tools' && <ToolsView />}
            {tab === 'skills' && <SkillsView />}
            {tab === 'infrastructure' && <InfrastructureView />}
            {tab === 'terminal' && <TerminalView />}
          </div>
        </main>

        {/* Right panel — connection on non-dashboard tabs (desktop) */}
        {tab !== 'dashboard' && (
          <aside className="hidden lg:block w-72 flex-shrink-0 bg-slate-50 border-l border-slate-200 p-4 overflow-y-auto">
            <ConnectionPanel
              url={url}
              token={token}
              status={status}
              onUrlChange={setUrl}
              onTokenChange={setToken}
              onConnect={handleConnect}
              onDisconnect={handleDisconnect}
            />

            {/* Shortcut tool reference */}
            <div className="mt-4 bg-white rounded-xl border border-slate-200 p-4 space-y-2">
              <p className="text-xs font-bold text-slate-600">🔥 Часто используемые</p>
              {[
                'scene-list-opened',
                'gameobject-find',
                'script-execute',
                'console-get-logs',
                'assets-find',
                'screenshot-game-view',
              ].map(name => (
                <button
                  key={name}
                  onClick={() => {
                    navigator.clipboard.writeText(`./tools/mcp_call.sh ${name} '{}'`).catch(() => {});
                  }}
                  className="w-full text-left px-2 py-1.5 rounded-lg hover:bg-slate-50 border border-transparent hover:border-slate-200 transition-colors group"
                >
                  <code className="text-xs font-mono text-indigo-600 group-hover:text-indigo-800">{name}</code>
                </button>
              ))}
            </div>

            {/* Notes */}
            <div className="mt-4 bg-amber-50 border border-amber-200 rounded-xl p-4">
              <p className="text-xs font-bold text-amber-700 mb-2">📌 Помни</p>
              <ul className="space-y-1 text-xs text-amber-600">
                <li>• URL меняется после VM-pause</li>
                <li>• Не нажимай "New" на токен без причины</li>
                <li>• script-execute = escape hatch</li>
                <li>• Перед .glb → ставь gltfast</li>
              </ul>
            </div>
          </aside>
        )}
      </div>
    </div>
  );
}
