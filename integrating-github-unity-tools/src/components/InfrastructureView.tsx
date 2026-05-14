export default function InfrastructureView() {
  return (
    <div className="space-y-6">
      {/* Architecture diagram */}
      <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
        <h2 className="text-lg font-bold text-slate-800 mb-4">🏗️ Архитектура связи</h2>
        <div className="bg-slate-900 rounded-xl p-6 font-mono text-sm overflow-x-auto">
          <pre className="text-slate-300 leading-relaxed">{`┌──────────────────────────────┐     ┌─────────────────────────────┐
│   User's Windows Desktop     │     │   AI Agent VM (Linux)       │
│                              │     │                             │
│  Unity Editor 6000.3.9f1    │     │  Docker engine              │
│  Project: tps1              │     │  cloudflared                │
│                              │     │  Python 3.12 + PyTorch CPU  │
│  Plugin "AI Game Developer" │◄────►│  TripoSR @ ~/TripoSR       │
│  (com.ivan-murzak.unity-mcp)│     │  mcp_call.sh helper         │
└──────────────────────────────┘     └─────────────────────────────┘
              ▲                                    │
              │    HTTPS + Bearer Token            │
              └──── Cloudflare Quick-Tunnel ───────┘
                    (*.trycloudflare.com)`}</pre>
        </div>
      </div>

      {/* Components */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {[
          {
            icon: '🔌',
            title: 'Unity Plugin (AI Game Developer)',
            author: 'Ivan Murzak, MIT',
            link: 'https://github.com/IvanMurzak/Unity-MCP',
            desc: 'Устанавливается как com.ivan-murzak.unity-mcp. Каждый Unity-проект имеет свой токен.',
            config: [
              'Connection: Custom',
              'Server URL: https://xxxx.trycloudflare.com',
              'Transport: http (streamableHttp)',
              'Authorization Token: required',
            ],
          },
          {
            icon: '🐳',
            title: 'unity-mcp-server (Docker)',
            author: 'Ivan Murzak',
            link: 'ivanmurzakdev/unity-mcp-server:0.72.1',
            desc: 'MCP-сервер в режиме streamableHttp. Слушает TCP 443 внутри контейнера.',
            config: [
              'MCP_PLUGIN_CLIENT_TRANSPORT=streamableHttp',
              'MCP_PLUGIN_PORT=443',
              'MCP_AUTHORIZATION=required',
              'MCP_PLUGIN_TOKEN=<your-token>',
            ],
          },
          {
            icon: '☁️',
            title: 'Cloudflare Quick-Tunnel',
            author: 'cloudflared CLI',
            link: 'https://github.com/cloudflare/cloudflared',
            desc: 'Создаёт эфемерный HTTPS endpoint. URL меняется при перезапуске!',
            config: [
              'Порт проброса: localhost:443',
              'URL: https://xxxx.trycloudflare.com',
              'Аутентификации нет — только Bearer Token',
              'Временный URL (меняется после VM pause)',
            ],
          },
          {
            icon: '🛠️',
            title: 'mcp_call.sh (Agent Helper)',
            author: 'tools/ в репо',
            link: '#',
            desc: '3-шаговый JSON-RPC хендшейк: initialize → notifications/initialized → tools/call',
            config: [
              'MCP_URL из ~/.config/unity-mcp/url',
              'MCP_TOKEN из ~/.config/unity-mcp/token',
              'DNS DoH через Google (*.trycloudflare.com)',
              'Кэш IP в /tmp/.mcp_ip_<host>',
            ],
          },
        ].map((c) => (
          <div key={c.title} className="bg-white rounded-xl border border-slate-200 shadow-sm p-5 space-y-3">
            <div className="flex items-center gap-3">
              <span className="text-2xl">{c.icon}</span>
              <div>
                <h3 className="text-sm font-bold text-slate-800">{c.title}</h3>
                <p className="text-xs text-slate-400">{c.author}</p>
              </div>
            </div>
            <p className="text-xs text-slate-500">{c.desc}</p>
            <div className="bg-slate-50 rounded-lg p-3 space-y-1">
              {c.config.map(l => (
                <code key={l} className="block text-xs font-mono text-slate-600">{l}</code>
              ))}
            </div>
          </div>
        ))}
      </div>

      {/* Failure modes */}
      <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
        <h2 className="text-lg font-bold text-slate-800 mb-4">⚠️ Частые проблемы и решения</h2>
        <div className="space-y-3">
          {[
            {
              symptom: 'plugin token does not match server token',
              cause: 'Пользователь нажал "New" на токен плагина',
              fix: 'Попросить новый токен → перезапустить контейнер с новым токеном',
              severity: 'red',
            },
            {
              symptom: 'Failed to get mcp-session-id',
              cause: 'Tunnel упал или subdomain не резолвится локально',
              fix: 'Перезапустить start_tunnel.sh → новый URL → обновить в плагине',
              severity: 'orange',
            },
            {
              symptom: 'Plugin в Unity красный "Disconnected"',
              cause: 'Сервер не запущен или URL устарел',
              fix: 'Проверить docker ps → убедиться, что URL в плагине совпадает с tunnel URL',
              severity: 'red',
            },
            {
              symptom: 'Version handshake failed: No response from server',
              cause: 'Обычно временное — сервер ожидает long-poll от плагина',
              fix: 'Подождать несколько секунд. Если persistent — Unity Editor не завис ли?',
              severity: 'yellow',
            },
            {
              symptom: 'GLB не импортируется в Unity',
              cause: 'Пакет com.unity.cloud.gltfast не установлен',
              fix: 'Вызвать package-add с packageName="com.unity.cloud.gltfast" ПЕРЕД загрузкой файла',
              severity: 'orange',
            },
          ].map((f) => (
            <div
              key={f.symptom}
              className={`rounded-xl p-4 border ${
                f.severity === 'red' ? 'bg-red-50 border-red-200' :
                f.severity === 'orange' ? 'bg-orange-50 border-orange-200' :
                'bg-yellow-50 border-yellow-200'
              }`}
            >
              <div className="flex items-start gap-3">
                <span className="text-lg flex-shrink-0">
                  {f.severity === 'red' ? '🔴' : f.severity === 'orange' ? '🟠' : '🟡'}
                </span>
                <div className="space-y-1">
                  <code className="block text-xs font-bold font-mono text-slate-800">{f.symptom}</code>
                  <p className="text-xs text-slate-600"><strong>Причина:</strong> {f.cause}</p>
                  <p className="text-xs text-slate-700 font-medium">✅ {f.fix}</p>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Security */}
      <div className="bg-amber-50 border border-amber-200 rounded-xl p-5">
        <h3 className="text-sm font-bold text-amber-800 mb-2">🔐 Безопасность</h3>
        <ul className="space-y-1 text-xs text-amber-700">
          <li>• Bearer token — длинный случайный секрет. Обращаться как с API-ключом.</li>
          <li>• Не коммитить <code className="font-mono bg-amber-100 px-1 rounded">~/.config/unity-mcp/token</code> в репозиторий.</li>
          <li>• URL Cloudflare НЕ является секретом — важен только токен.</li>
          <li>• Любой, кто знает URL + токен, может удалённо управлять Unity Editor. Ротируй при утечке.</li>
        </ul>
      </div>
    </div>
  );
}
