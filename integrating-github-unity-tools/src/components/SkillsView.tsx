import { SKILLS } from '../data/skills';
import SkillPanel from './SkillPanel';

export default function SkillsView() {
  return (
    <div className="space-y-4">
      <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-5">
        <h2 className="text-lg font-bold text-slate-800 mb-1">📖 Skills — переиспользуемые playbook'и</h2>
        <p className="text-sm text-slate-500">
          Каждый скил описывает конкретную задачу с пошаговой инструкцией и командами для копирования.
          Нажми на карточку чтобы раскрыть.
        </p>
        <div className="mt-3 flex flex-wrap gap-2">
          {['core', 'analysis', 'power', '3d'].map(tag => (
            <span key={tag} className={`px-2 py-0.5 rounded-full text-xs font-semibold ${
              tag === 'core' ? 'bg-indigo-100 text-indigo-700' :
              tag === 'analysis' ? 'bg-cyan-100 text-cyan-700' :
              tag === 'power' ? 'bg-orange-100 text-orange-700' :
              'bg-purple-100 text-purple-700'
            }`}>
              {tag}
            </span>
          ))}
        </div>
      </div>

      <div className="space-y-3">
        {SKILLS.map(skill => (
          <SkillPanel key={skill.id} skill={skill} />
        ))}
      </div>

      {/* Known issues */}
      <div className="bg-slate-800 text-slate-200 rounded-2xl p-6 space-y-3">
        <h3 className="text-base font-bold text-white">❌ Известные ограничения (из сессии 13.05.2026)</h3>
        <ul className="space-y-2 text-sm">
          {[
            { issue: 'Unity AI Assistant (com.unity.ai.assistant)', detail: 'Только для Unity Muse-подписчиков. Без подписки — пусто. Не для локального запуска.' },
            { issue: 'HF Spaces image-to-3D анонимно', detail: 'В 2026 году все пространства (Hunyuan3D-2, TripoSG, TRELLIS...) закрыты GPU-лимитами. Нужен HF-токен.' },
            { issue: 'TripoSR --bake-texture на headless VM', detail: 'Падает на moderngl libGL.so / EGL not found. Решение: запускать без --bake-texture (vertex colors).' },
            { issue: 'Unity нативно не импортирует .glb', detail: 'Нужен com.unity.cloud.gltfast. Установить через package-add ДО загрузки файла в Assets/.' },
          ].map(item => (
            <li key={item.issue} className="flex gap-3">
              <span className="text-red-400 flex-shrink-0 font-mono">✗</span>
              <div>
                <span className="font-semibold text-slate-200">{item.issue}</span>
                <span className="text-slate-400"> — {item.detail}</span>
              </div>
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}
