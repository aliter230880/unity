import { useState } from 'react';
import StatusBadge from './StatusBadge';

interface Props {
  url: string;
  token: string;
  status: 'disconnected' | 'connecting' | 'connected' | 'error';
  onUrlChange: (v: string) => void;
  onTokenChange: (v: string) => void;
  onConnect: () => void;
  onDisconnect: () => void;
}

export default function ConnectionPanel({ url, token, status, onUrlChange, onTokenChange, onConnect, onDisconnect }: Props) {
  const [showToken, setShowToken] = useState(false);

  return (
    <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6 space-y-5">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-indigo-500 to-purple-600 flex items-center justify-center text-white text-lg">
            🔌
          </div>
          <div>
            <h2 className="text-base font-bold text-slate-800">MCP Connection</h2>
            <p className="text-xs text-slate-400">Unity Editor via Cloudflare tunnel</p>
          </div>
        </div>
        <StatusBadge status={status} />
      </div>

      <div className="space-y-3">
        <div>
          <label className="block text-xs font-semibold text-slate-600 mb-1">Server URL (*.trycloudflare.com)</label>
          <input
            type="url"
            value={url}
            onChange={e => onUrlChange(e.target.value)}
            placeholder="https://xxxx.trycloudflare.com/"
            className="w-full px-3 py-2 rounded-lg border border-slate-200 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-indigo-400 bg-slate-50"
          />
        </div>
        <div>
          <label className="block text-xs font-semibold text-slate-600 mb-1">Bearer Token</label>
          <div className="relative">
            <input
              type={showToken ? 'text' : 'password'}
              value={token}
              onChange={e => onTokenChange(e.target.value)}
              placeholder="Authorization: Bearer xxxxxxx"
              className="w-full px-3 py-2 pr-10 rounded-lg border border-slate-200 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-indigo-400 bg-slate-50"
            />
            <button
              onClick={() => setShowToken(v => !v)}
              className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 text-xs"
            >
              {showToken ? '🙈' : '👁️'}
            </button>
          </div>
        </div>
      </div>

      <div className="flex gap-3">
        <button
          onClick={onConnect}
          disabled={!url || !token || status === 'connecting'}
          className="flex-1 py-2 rounded-lg bg-indigo-600 hover:bg-indigo-700 disabled:opacity-40 text-white text-sm font-semibold transition-colors"
        >
          {status === 'connecting' ? 'Connecting...' : 'Connect'}
        </button>
        {status === 'connected' && (
          <button
            onClick={onDisconnect}
            className="flex-1 py-2 rounded-lg bg-red-100 hover:bg-red-200 text-red-700 text-sm font-semibold transition-colors"
          >
            Disconnect
          </button>
        )}
      </div>

      {status === 'connected' && (
        <div className="bg-green-50 border border-green-200 rounded-lg p-3 text-xs text-green-700">
          ✅ Подключено! Можно вызывать MCP-инструменты напрямую. Используй вкладку <strong>Tools</strong> для просмотра 82 инструментов.
        </div>
      )}

      <div className="bg-slate-50 rounded-lg p-3 space-y-1">
        <p className="text-xs font-semibold text-slate-600">Quick setup:</p>
        <code className="block text-xs text-slate-500 font-mono">./tools/start_unity_mcp_server.sh &lt;TOKEN&gt;</code>
        <code className="block text-xs text-slate-500 font-mono">./tools/start_tunnel.sh 443</code>
      </div>
    </div>
  );
}
