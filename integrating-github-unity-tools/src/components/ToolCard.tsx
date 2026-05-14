import { McpTool } from '../types';
import { CATEGORY_COLORS } from '../data/tools';

interface Props {
  tool: McpTool;
  onCopy: (name: string) => void;
  copied: boolean;
}

export default function ToolCard({ tool, onCopy, copied }: Props) {
  const colorClass = CATEGORY_COLORS[tool.category] || 'bg-gray-100 text-gray-700 border-gray-200';

  return (
    <div className="bg-white rounded-xl border border-slate-200 shadow-sm hover:shadow-md transition-shadow p-4 space-y-3 group">
      <div className="flex items-start justify-between gap-2">
        <div className="flex items-center gap-2 min-w-0">
          <span className={`inline-flex px-2 py-0.5 rounded-md text-xs font-bold border ${colorClass} whitespace-nowrap`}>
            {tool.category}
          </span>
          <code className="text-sm font-bold text-slate-800 truncate">{tool.name}</code>
        </div>
        <button
          onClick={() => onCopy(tool.name)}
          className="flex-shrink-0 opacity-0 group-hover:opacity-100 transition-opacity px-2 py-1 rounded-md bg-slate-100 hover:bg-slate-200 text-xs text-slate-600 font-mono"
        >
          {copied ? '✅' : '📋'}
        </button>
      </div>

      <p className="text-xs text-slate-500 leading-relaxed">{tool.description}</p>

      <div className="flex flex-wrap gap-1">
        {tool.params.map(p => (
          <span
            key={p}
            className={`inline-flex px-2 py-0.5 rounded text-xs font-mono ${
              p.endsWith('?')
                ? 'bg-slate-100 text-slate-400'
                : 'bg-indigo-50 text-indigo-600 font-semibold'
            }`}
          >
            {p}
          </span>
        ))}
      </div>
    </div>
  );
}
