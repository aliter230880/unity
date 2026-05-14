import { useState } from 'react';
import { Skill } from '../data/skills';

interface Props {
  skill: Skill;
}

const tagColors: Record<string, string> = {
  core: 'bg-indigo-100 text-indigo-700',
  analysis: 'bg-cyan-100 text-cyan-700',
  power: 'bg-orange-100 text-orange-700',
  '3d': 'bg-purple-100 text-purple-700',
};

export default function SkillPanel({ skill }: Props) {
  const [open, setOpen] = useState(false);
  const [copied, setCopied] = useState(false);

  const handleCopy = () => {
    if (skill.command) {
      navigator.clipboard.writeText(skill.command).catch(() => {});
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  return (
    <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
      <button
        onClick={() => setOpen(v => !v)}
        className="w-full text-left p-5 flex items-center gap-4 hover:bg-slate-50 transition-colors"
      >
        <span className="text-3xl">{skill.icon}</span>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 mb-1">
            <h3 className="text-base font-bold text-slate-800">{skill.title}</h3>
            <span className={`px-2 py-0.5 rounded-full text-xs font-semibold ${tagColors[skill.tag] || 'bg-gray-100 text-gray-600'}`}>
              {skill.tag}
            </span>
          </div>
          <p className="text-xs text-slate-500 leading-relaxed line-clamp-2">{skill.description}</p>
        </div>
        <span className="text-slate-400 text-lg flex-shrink-0">{open ? '▲' : '▼'}</span>
      </button>

      {open && (
        <div className="px-5 pb-5 space-y-4 border-t border-slate-100">
          <div className="pt-4">
            <p className="text-xs font-semibold text-slate-600 mb-2">Шаги выполнения:</p>
            <ul className="space-y-1.5">
              {skill.steps.map((step, i) => (
                <li key={i} className="text-xs text-slate-600 flex gap-2">
                  <span className="text-indigo-400 font-bold flex-shrink-0">›</span>
                  <span>{step}</span>
                </li>
              ))}
            </ul>
          </div>

          {skill.command && (
            <div>
              <div className="flex items-center justify-between mb-1">
                <p className="text-xs font-semibold text-slate-600">Команды:</p>
                <button
                  onClick={handleCopy}
                  className="text-xs px-2 py-0.5 rounded bg-slate-100 hover:bg-slate-200 text-slate-600 transition-colors"
                >
                  {copied ? '✅ Скопировано' : '📋 Копировать'}
                </button>
              </div>
              <pre className="bg-slate-900 text-green-400 rounded-xl p-4 text-xs font-mono overflow-x-auto whitespace-pre-wrap leading-relaxed">
                {skill.command}
              </pre>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
