import { useState, useRef, useEffect } from 'react';

interface LogLine {
  id: number;
  type: 'input' | 'output' | 'error' | 'system';
  text: string;
  ts: string;
}

const QUICK_COMMANDS = [
  { label: 'scene-list-opened', cmd: './tools/mcp_call.sh scene-list-opened \'{}\'', desc: 'Список открытых сцен' },
  { label: 'editor-state', cmd: './tools/mcp_call.sh editor-application-get-state \'{}\'', desc: 'Состояние редактора' },
  { label: 'console-get-logs', cmd: './tools/mcp_call.sh console-get-logs \'{"maxEntries":20}\'', desc: 'Последние логи' },
  { label: 'assets-find all', cmd: './tools/mcp_call.sh assets-find \'{}\'', desc: 'Поиск ассетов' },
  { label: 'script-execute hello', cmd: './tools/mcp_call.sh script-execute \'{"csharpCode":"Debug.Log(\\"Hello from AI!\\");","isMethodBody":true}\'', desc: 'Тест Roslyn' },
  { label: 'screenshot-game-view', cmd: './tools/mcp_call.sh screenshot-game-view \'{}\'', desc: 'Скриншот Game View' },
  { label: 'start tunnel', cmd: './tools/start_tunnel.sh 443', desc: 'Запустить Cloudflare tunnel' },
  { label: 'start server', cmd: './tools/start_unity_mcp_server.sh <YOUR_TOKEN>', desc: 'Запустить MCP сервер' },
];

let idCounter = 10;

export default function TerminalView() {
  const [lines, setLines] = useState<LogLine[]>([
    { id: 1, type: 'system', text: '═══ Unity MCP Terminal — Справочник команд ═══', ts: now() },
    { id: 2, type: 'system', text: 'Это имитация терминала для быстрого копирования команд.', ts: now() },
    { id: 3, type: 'system', text: 'Реальное выполнение происходит на машине агента (Linux VM).', ts: now() },
    { id: 4, type: 'system', text: '─'.repeat(60), ts: now() },
  ]);
  const [input, setInput] = useState('');
  const bottomRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [lines]);

  function now() {
    return new Date().toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
  }

  function addLine(type: LogLine['type'], text: string) {
    setLines(prev => [...prev, { id: idCounter++, type, text, ts: now() }]);
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!input.trim()) return;
    addLine('input', `$ ${input.trim()}`);
    simulateOutput(input.trim());
    setInput('');
  }

  function simulateOutput(cmd: string) {
    const c = cmd.toLowerCase();
    if (c.includes('scene-list-opened')) {
      addLine('output', '{"scenes":[{"name":"SampleScene","path":"Assets/Scenes/SampleScene.unity","isActive":true}]}');
    } else if (c.includes('editor-application-get-state')) {
      addLine('output', '{"isPlaying":false,"isPaused":false,"isCompiling":false,"unityVersion":"6000.3.9f1"}');
    } else if (c.includes('console-get-logs')) {
      addLine('output', '{"logs":[{"type":"Log","message":"[MCP] Connected successfully","stackTrace":""}]}');
    } else if (c.includes('debug.log') || c.includes('script-execute')) {
      addLine('output', '{"success":true,"result":"Hello from AI!"}');
    } else if (c.includes('help')) {
      addLine('output', 'Доступные команды: см. вкладку Tools (82 инструмента)');
      addLine('output', 'Формат: ./tools/mcp_call.sh <tool-name> \'<json-args>\'');
    } else if (c.includes('start_unity_mcp_server')) {
      addLine('output', '[start_unity_mcp_server] running on 0.0.0.0:443');
      addLine('output', '[server] MCP Server listening on 443 (streamableHttp)');
    } else if (c.includes('start_tunnel')) {
      addLine('output', '[start_tunnel] launched cloudflared, waiting for URL...');
      setTimeout(() => addLine('output', '[start_tunnel] URL: https://demo-xxx.trycloudflare.com'), 800);
    } else {
      addLine('output', `[Симуляция] Команда принята: ${cmd}`);
      addLine('output', 'Для реального выполнения используйте shell на машине агента.');
    }
  }

  function copyCmd(cmd: string) {
    navigator.clipboard.writeText(cmd).catch(() => {});
    addLine('system', `📋 Скопировано: ${cmd.slice(0, 60)}${cmd.length > 60 ? '...' : ''}`);
  }

  return (
    <div className="space-y-4">
      {/* Quick commands */}
      <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-5">
        <h2 className="text-sm font-bold text-slate-700 mb-3">⚡ Быстрые команды</h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
          {QUICK_COMMANDS.map(qc => (
            <button
              key={qc.label}
              onClick={() => copyCmd(qc.cmd)}
              className="flex items-start gap-3 p-3 rounded-xl bg-slate-50 hover:bg-indigo-50 border border-slate-200 hover:border-indigo-200 text-left transition-colors group"
            >
              <span className="text-indigo-400 font-mono text-xs mt-0.5">$</span>
              <div className="min-w-0">
                <span className="block text-xs font-bold text-slate-700 group-hover:text-indigo-700">{qc.label}</span>
                <span className="block text-xs text-slate-400">{qc.desc}</span>
              </div>
              <span className="ml-auto text-xs text-slate-300 group-hover:text-indigo-400 flex-shrink-0">📋</span>
            </button>
          ))}
        </div>
      </div>

      {/* Terminal */}
      <div className="bg-slate-900 rounded-2xl overflow-hidden shadow-xl">
        <div className="flex items-center gap-2 px-4 py-3 bg-slate-800 border-b border-slate-700">
          <span className="w-3 h-3 rounded-full bg-red-500" />
          <span className="w-3 h-3 rounded-full bg-yellow-500" />
          <span className="w-3 h-3 rounded-full bg-green-500" />
          <span className="ml-3 text-xs font-mono text-slate-400">unity-mcp-terminal</span>
        </div>

        <div className="h-80 overflow-y-auto p-4 font-mono text-xs space-y-1">
          {lines.map(l => (
            <div key={l.id} className="flex gap-3 leading-relaxed">
              <span className="text-slate-600 flex-shrink-0">{l.ts}</span>
              <span className={
                l.type === 'input' ? 'text-green-400' :
                l.type === 'error' ? 'text-red-400' :
                l.type === 'system' ? 'text-yellow-400' :
                'text-slate-300'
              }>
                {l.text}
              </span>
            </div>
          ))}
          <div ref={bottomRef} />
        </div>

        <form onSubmit={handleSubmit} className="flex items-center gap-2 px-4 py-3 bg-slate-800 border-t border-slate-700">
          <span className="text-green-400 font-mono text-xs flex-shrink-0">$</span>
          <input
            type="text"
            value={input}
            onChange={e => setInput(e.target.value)}
            placeholder="./tools/mcp_call.sh scene-list-opened '{}'"
            className="flex-1 bg-transparent text-green-300 font-mono text-xs focus:outline-none placeholder-slate-600"
          />
          <button type="submit" className="px-3 py-1 rounded bg-green-800 text-green-300 text-xs font-mono hover:bg-green-700 transition-colors">
            ↵
          </button>
        </form>
      </div>
    </div>
  );
}
