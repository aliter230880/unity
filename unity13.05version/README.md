# unity13.05version — фундамент AI-агента для разработки Unity

Это снимок инструментов, скилов и контекста, собранных во время сессии **13 мая 2026** (Devin → Unity Editor проекта `tps1`, Brackeys Bundle Multiplayer Platformer на Unity 6.3 / 6000.3.9f1).

Цель папки — **не просто архив текущей сессии**, а зародыш переносимого AI-инструмента для Unity, который сможет:

- Подключаться к **запущенному Unity Editor** на машине пользователя через MCP-мост (никаких локальных копий проекта в репозитории не нужно — все правки выполняются прямо в живом редакторе).
- Выполнять **широкий спектр операций**: создавать/редактировать GameObject'ы, ассеты, скрипты, материалы, шейдеры, префабы, анимации, ProBuilder-меши, читать логи, делать скриншоты, добавлять/удалять пакеты, исполнять произвольный C# через Roslyn (`script-execute`).
- **Локально генерировать 3D-меши из 2D-изображений** (TripoSR на CPU) без API-ключей и подписок.
- **Загружать сгенерированные ассеты в Unity** через временный HTTPS-туннель + `script-execute`, без файлового доступа к машине пользователя.

> Этот фундамент рассчитан на повторное использование любым AI-агентом (Devin, Claude Code, Cursor, и т.д.) который умеет звать локальные shell-команды и MCP-инструменты. Никакой привязки к конкретному агенту здесь нет.

## Структура папки

```
unity13.05version/
├── README.md                      ← вы тут
├── tools/                         ← рабочие shell-скрипты, готовые к запуску
│   ├── mcp_call.sh                ← единичный MCP tools/call через streamableHttp
│   ├── mcp_tools_list.sh          ← /tools/list (получить каталог инструментов)
│   ├── img_to_3d.sh               ← локальная генерация 3D-меша из картинки (TripoSR)
│   ├── start_unity_mcp_server.sh  ← поднятие Docker `unity-mcp-server`
│   ├── start_tunnel.sh            ← Cloudflare quick-tunnel
│   ├── serve_file.sh              ← быстрый HTTP-сервер для отдачи бинарников Unity'ю
│   └── patch_triposr_cpu.sh       ← патч TripoSR под CPU-only окружение
│
├── skills/                        ← переиспользуемые playbook'и в формате SKILL.md
│   ├── unity-mcp-bridge/          ← как поднять связь Devin ↔ Unity Editor
│   ├── unity-scene-analysis/      ← как собрать снимок проекта/сцены через MCP
│   ├── unity-script-execute/      ← как корректно вызывать C# через Roslyn
│   ├── unity-mcp-tool-reference/  ← каталог 82 MCP-инструментов (как искать нужный)
│   ├── img-to-3d-triposr/         ← локальная генерация 3D из 2D-изображения
│   ├── glb-import-to-unity/       ← как доставить бинарный ассет в Assets/ без файлового доступа
│   └── mesh-orient-scale/         ← как автоматически развернуть сгенерированный меш «ногами вниз»
│
├── context/                       ← фактическая память сессии (не агентское руководство)
│   ├── infrastructure.md          ← Docker + Cloudflare tunnel + plugin token: схема связи
│   ├── tps1-project-snapshot.md   ← состояние проекта пользователя на 13.05.2026
│   ├── mcp-tool-catalog.md        ← полный каталог всех 82 MCP-инструментов с описаниями
│   └── session-log-2026-05-13.md  ← хронология сессии: что попробовали, что заработало, тупики
│
└── examples/
    └── generated-character/       ← пример: TripoSR на фото → GLB → импорт на сцену
        └── README.md
```

## Быстрый старт (для будущего AI-агента в новой сессии)

1. Прочитать **`context/infrastructure.md`** — понять, как Devin'овский Linux-контейнер связывается с Unity Editor на Windows-машине пользователя.
2. Прочитать **`skills/unity-mcp-bridge/SKILL.md`** — пошаговое поднятие моста (Docker + Cloudflare tunnel + plugin token loop).
3. Запустить **`tools/start_unity_mcp_server.sh`** + **`tools/start_tunnel.sh`** → получить публичный URL.
4. Попросить пользователя:
   - Открыть Unity-проект.
   - Установить `.unitypackage` плагина (если не установлен).
   - Открыть `Window → AI Game Developer`, поставить URL из шага 3.
   - Прислать JSON с `Authorization: Bearer <token>`.
5. Перезапустить контейнер с этим токеном (`MCP_PLUGIN_TOKEN=...`).
6. Когда плагин в Unity зелёный — пользоваться **`tools/mcp_call.sh <tool> <json>`** для всего.

Дальше выбирайте конкретный скил под задачу:

| Задача | Скил |
|---|---|
| Понять, что у пользователя в проекте | `skills/unity-scene-analysis/SKILL.md` |
| Выполнить произвольный C# в редакторе | `skills/unity-script-execute/SKILL.md` |
| Найти нужный MCP-инструмент | `skills/unity-mcp-tool-reference/SKILL.md` + `context/mcp-tool-catalog.md` |
| 3D-меш из картинки локально, бесплатно | `skills/img-to-3d-triposr/SKILL.md` |
| Доставить .glb / .png / любой бинарник в Assets/ | `skills/glb-import-to-unity/SKILL.md` |
| Поставить сгенерированный меш стоя на ноги | `skills/mesh-orient-scale/SKILL.md` |

## Ключевые принципы

1. **Никаких локальных клонов Unity-проекта.** Все изменения идут через MCP в живой редактор. Это означает: AI не «пишет код в файл и делает PR» — AI **прямо здесь и сейчас правит сцену пользователя**. Перед изменениями всегда сообщай пользователю «сейчас сделаю X», после — «сделал X, можешь сохранить».

2. **Никаких платных image-to-3D сервисов как первого выбора.** TripoSR на CPU выдаёт нормальный результат за ~45 секунд. Платные (Tripo3D / Meshy / Rodin / Unity AI) — только если пользователь явно попросит или нужна AAA-детализация.

3. **HF Spaces image-to-3D в 2026 анонимно НЕ работают.** Все приличные Spaces (Hunyuan3D-2, TripoSG, stable-fast-3d, TRELLIS, Unique3D, InstantMesh, CharacterGen) либо требуют HF-токен из-за GPU-duration лимитов, либо находятся в `RUNTIME_ERROR / PAUSED`. Локальный TripoSR — единственный надёжный бесплатный путь.

4. **`script-execute` — это «escape hatch».** Когда нужный MCP-инструмент отсутствует или не поддерживает параметры, всегда можно написать произвольный C# и выполнить через Roslyn (`isMethodBody=true`). Это позволяет работать с любым Unity-API.

5. **Cloudflare quick-tunnel временный.** Если VM засыпает / пользователь возвращается через сутки — URL изменится. Восстановление: `start_unity_mcp_server.sh` + `start_tunnel.sh`, сообщить пользователю новый URL, попросить обновить в плагине.

## Что НЕ работает / тупики

- **Unity AI Assistant package** (`com.unity.ai.assistant`) — клиент, ходит на cloud-серверы Unity AI Gateway. Без подписки Unity Muse / без аккаунта Unity AI — пусто. Локально не запустишь. Контент-фильтр строгий.
- **Hugging Face Spaces image-to-3D** анонимный доступ — закрыт лимитами GPU duration. Нужен HF-токен (бесплатный), иначе всё валится с `GPU duration is larger than the maximum allowed`.
- **TripoSR `--bake-texture`** на headless VM — падает на `moderngl libGL.so / EGL not found` если в системе нет работающего GL-контекста. Решение: либо ставить `libgl1 libegl1 libegl1-mesa libgles2-mesa`, либо запускать без `--bake-texture` (vertex colors).
- **Unity не импортирует `.glb` нативно** — нужен пакет `com.unity.cloud.gltfast`. Ставим его через `package-add` ДО загрузки файла в `Assets/`.

## Дальше

Эта папка — стартовый камень. На её базе можно строить более общий супер-инструмент:

- Универсальный CLI / Python-обёртка над `mcp_call.sh` со схема-валидацией параметров.
- Локальный кеш скриншотов / снимков сцены, чтобы AI имел «зрение» о проекте между сессиями.
- Pipeline image → 3D → auto-rig (Mixamo / Anything World) → анимация → импорт в Unity.
- Pipeline text → код-генерация → `script-execute` → авто-тестирование на сцене.
- Замыкание петли: AI правит сцену → делает screenshot → анализирует визуально → правит снова.

Файлы в этой папке — стартовая точка для любого из этих направлений.
