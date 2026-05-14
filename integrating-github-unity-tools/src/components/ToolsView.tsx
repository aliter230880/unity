import { useState, useMemo } from 'react';
import { MCP_TOOLS, CATEGORIES, CATEGORY_COLORS } from '../data/tools';
import ToolCard from './ToolCard';

export default function ToolsView() {
  const [search, setSearch] = useState('');
  const [activeCategory, setActiveCategory] = useState('all');
  const [copiedTool, setCopiedTool] = useState<string | null>(null);

  const filtered = useMemo(() => {
    return MCP_TOOLS.filter(t => {
      const matchCat = activeCategory === 'all' || t.category === activeCategory;
      const matchSearch = !search || t.name.toLowerCase().includes(search.toLowerCase()) || t.description.toLowerCase().includes(search.toLowerCase());
      return matchCat && matchSearch;
    });
  }, [search, activeCategory]);

  const handleCopy = (name: string) => {
    navigator.clipboard.writeText(`./tools/mcp_call.sh ${name} '{}'`).catch(() => {});
    setCopiedTool(name);
    setTimeout(() => setCopiedTool(null), 2000);
  };

  return (
    <div className="space-y-5">
      {/* Search */}
      <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-4">
        <div className="relative">
          <span className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400 text-sm">🔍</span>
          <input
            type="text"
            value={search}
            onChange={e => setSearch(e.target.value)}
            placeholder="Поиск по 82 инструментам... (name, description)"
            className="w-full pl-9 pr-4 py-2.5 rounded-xl border border-slate-200 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-400 bg-slate-50"
          />
          {search && (
            <button
              onClick={() => setSearch('')}
              className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 text-xs"
            >
              ✕
            </button>
          )}
        </div>
        <p className="mt-2 text-xs text-slate-400">
          Найдено: <strong className="text-slate-600">{filtered.length}</strong> из {MCP_TOOLS.length} инструментов
        </p>
      </div>

      {/* Category filter */}
      <div className="flex flex-wrap gap-2">
        {CATEGORIES.map(cat => {
          const colorDef = CATEGORY_COLORS[cat.id];
          const isActive = activeCategory === cat.id;
          return (
            <button
              key={cat.id}
              onClick={() => setActiveCategory(cat.id)}
              className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-semibold border transition-all ${
                isActive
                  ? `${cat.color} text-white border-transparent shadow-sm`
                  : colorDef
                  ? `${colorDef} hover:opacity-80`
                  : 'bg-slate-100 text-slate-600 border-slate-200 hover:bg-slate-200'
              }`}
            >
              {cat.label}
              <span className={`${isActive ? 'bg-white/30' : 'bg-current/10'} px-1.5 py-0.5 rounded-full text-xs leading-none`}>
                {cat.count}
              </span>
            </button>
          );
        })}
      </div>

      {/* Tools grid */}
      {filtered.length === 0 ? (
        <div className="text-center py-16 text-slate-400">
          <div className="text-4xl mb-3">🔎</div>
          <p className="font-medium">Ничего не найдено</p>
          <p className="text-xs mt-1">Попробуй другой запрос или категорию</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-3">
          {filtered.map(tool => (
            <ToolCard
              key={tool.name}
              tool={tool}
              onCopy={handleCopy}
              copied={copiedTool === tool.name}
            />
          ))}
        </div>
      )}
    </div>
  );
}
