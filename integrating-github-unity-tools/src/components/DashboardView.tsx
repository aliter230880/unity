import { MCP_TOOLS, CATEGORIES } from '../data/tools';
import { SKILLS } from '../data/skills';

const SETUP_STEPS = [
  { n: 1, text: 'Прочитать context/infrastructure.md — схема связи', done: true },
  { n: 2, text: 'Прочитать skills/unity-mcp-bridge/SKILL.md — пошаговый запуск', done: true },
  { n: 3, text: 'Запустить start_unity_mcp_server.sh + start_tunnel.sh', done: false },
  { n: 4, text: 'Пользователь: открыть Unity → Window → AI Game Developer → вставить URL', done: false },
  { n: 5, text: 'Пользователь: прислать JSON с Authorization: Bearer <token>', done: false },
  { n: 6, text: 'Перезапустить контейнер с токеном. Плагин зелёный — можно работать!', done: false },
];

export default function DashboardView() {
  const toolsByCategory = CATEGORIES.slice(1).map(c => ({
    ...c,
    tools: MCP_TOOLS.filter(t => t.category === c.id),
  }));

  return (
    <div className="space-y-6">
      {/* Hero */}
      <div className="bg-gradient-to-br from-indigo-600 via-purple-600 to-violet-700 rounded-2xl p-8 text-white shadow-xl">
        <div className="flex items-start gap-5">
          <div className="text-5xl">🎮</div>
          <div className="space-y-2">
            <h1 className="text-2xl font-bold">Unity MCP Control Panel</h1>
            <p className="text-indigo-200 text-sm leading-relaxed max-w-lg">
              AI-агент подключён к Unity Editor через MCP-мост. Полный каталог из <strong className="text-white">82 инструментов</strong> для управления
              сценой, ассетами, скриптами, ProBuilder-мешами, анимациями и многим другим — прямо в живом редакторе.
            </p>
            <div className="flex flex-wrap gap-2 pt-1">
              <span className="bg-white/20 text-white text-xs px-3 py-1 rounded-full">Unity 6.3 / 6000.3.9f1</span>
              <span className="bg-white/20 text-white text-xs px-3 py-1 rounded-full">unity-mcp-server v0.72.1</span>
              <span className="bg-white/20 text-white text-xs px-3 py-1 rounded-full">Snapshot: 13.05.2026</span>
              <span className="bg-white/20 text-white text-xs px-3 py-1 rounded-full">TripoSR CPU</span>
            </div>
          </div>
        </div>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
        {[
          { label: 'MCP Инструментов', value: '82', icon: '🛠️', color: 'from-blue-500 to-indigo-600' },
          { label: 'Категорий', value: '15', icon: '📂', color: 'from-purple-500 to-violet-600' },
          { label: 'Skills (playbooks)', value: String(SKILLS.length), icon: '📖', color: 'from-emerald-500 to-teal-600' },
          { label: 'Shell-скриптов', value: '7', icon: '⚙️', color: 'from-orange-500 to-amber-600' },
        ].map(s => (
          <div key={s.label} className={`bg-gradient-to-br ${s.color} rounded-xl p-5 text-white shadow-sm`}>
            <div className="text-2xl mb-1">{s.icon}</div>
            <div className="text-3xl font-bold">{s.value}</div>
            <div className="text-xs opacity-80 mt-1">{s.label}</div>
          </div>
        ))}
      </div>

      {/* Quick start */}
      <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
        <h2 className="text-lg font-bold text-slate-800 mb-4">🚀 Быстрый старт (для нового AI-агента)</h2>
        <div className="space-y-2">
          {SETUP_STEPS.map(step => (
            <div
              key={step.n}
              className={`flex items-start gap-3 p-3 rounded-xl ${step.done ? 'bg-green-50 border border-green-200' : 'bg-slate-50 border border-slate-200'}`}
            >
              <span className={`flex-shrink-0 w-6 h-6 rounded-full text-xs font-bold flex items-center justify-center ${
                step.done ? 'bg-green-500 text-white' : 'bg-slate-300 text-slate-600'
              }`}>
                {step.done ? '✓' : step.n}
              </span>
              <p className={`text-sm ${step.done ? 'text-green-700' : 'text-slate-600'}`}>{step.text}</p>
            </div>
          ))}
        </div>
      </div>

      {/* Category grid */}
      <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
        <h2 className="text-lg font-bold text-slate-800 mb-4">📊 Инструменты по категориям</h2>
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
          {toolsByCategory.map(c => (
            <div key={c.id} className="rounded-xl border border-slate-200 bg-slate-50 p-3 text-center hover:border-indigo-300 transition-colors">
              <div className={`inline-flex items-center justify-center w-8 h-8 rounded-lg ${c.color} text-white text-xs font-bold mb-2`}>
                {c.count}
              </div>
              <div className="text-xs font-bold text-slate-700">{c.label}</div>
            </div>
          ))}
        </div>
      </div>

      {/* Key principles */}
      <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
        <h2 className="text-lg font-bold text-slate-800 mb-4">⚡ Ключевые принципы</h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          {[
            {
              icon: '🚫📁',
              title: 'Никаких локальных клонов проекта',
              desc: 'Все изменения идут через MCP в живой редактор. AI правит сцену прямо здесь и сейчас.',
            },
            {
              icon: '🆓🎲',
              title: 'Бесплатный Image-to-3D через TripoSR',
              desc: 'TripoSR на CPU за ~45 сек. Платные сервисы — только по явной просьбе пользователя.',
            },
            {
              icon: '⚡🔧',
              title: 'script-execute — это escape hatch',
              desc: 'Когда MCP-инструмент не подходит, пишем C# и запускаем через Roslyn.',
            },
            {
              icon: '🔄☁️',
              title: 'Cloudflare URL временный',
              desc: 'После VM-сна URL меняется. Восстановление: start_unity_mcp_server.sh + start_tunnel.sh.',
            },
          ].map(p => (
            <div key={p.title} className="bg-slate-50 rounded-xl border border-slate-200 p-4 space-y-1">
              <div className="flex items-center gap-2">
                <span className="text-xl">{p.icon}</span>
                <h3 className="text-sm font-bold text-slate-800">{p.title}</h3>
              </div>
              <p className="text-xs text-slate-500 leading-relaxed">{p.desc}</p>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
