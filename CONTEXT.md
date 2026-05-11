# AliTerra AI — Fullstack Unity Developer v7

## Архитектура системы

```
Unity Editor Plugin (AliTerraAI.cs)
        │
        ├─► POST /api/unity/sync ──────► Сервер хранит ВСЕ файлы проекта
        ├─► POST /api/unity/push ──────► Состояние сцены (выбранный объект, иерархия)
        ├─► POST /api/unity/logs ──────► Консольные логи Unity (errors/warnings)
        ├─► GET  /api/unity/commands ──► Команды от AI (polling каждые 3 сек)
        └─► POST /api/unity/commands ──► Отчёт о выполнении команд
                │
        POST /api/ai/chat ─────────────► Claude claude-sonnet-4-6 с Anthropic Tools
```

## AI Tools (Function Calling)

AI использует инструменты вместо генерации кода в чат:

| Инструмент | Действие |
|------------|----------|
| `list_project_files` | Просматривает все синхронизированные файлы |
| `read_script(path)` | Читает содержимое файла |
| `create_script(path, content)` | Создаёт файл → команда → плагин записывает |
| `modify_script(path, content)` | Изменяет файл → команда → плагин записывает |
| `create_game_object(name, primitive, ...)` | Создаёт объект в сцене |
| `execute_editor_command(cmd)` | refresh, save_scene, compile, log_message |
| `read_console_logs(type, limit)` | Читает логи Unity для отладки |

## Поток данных (v7)

```
Пользователь: "Создай систему инвентаря"
        ↓
AI (claude-sonnet-4-6):
  1. list_project_files → видит структуру проекта
  2. read_script("Assets/Scripts/Player/PlayerController.cs") → читает код
  3. create_script("Assets/Scripts/Inventory/InventorySystem.cs", fullCode)
  4. create_script("Assets/Scripts/Inventory/Item.cs", fullCode)
  5. execute_editor_command("refresh")
  6. read_console_logs("error") → проверяет ошибки
        ↓
Плагин (polling):
  - GET /api/unity/commands → получает команды write_file
  - File.WriteAllText(path, content) → записывает файлы
  - AssetDatabase.Refresh() → Unity видит файлы
  - POST /api/unity/commands {success:true} → отчёт
        ↓
AI: "✅ Создал InventorySystem и Item. Ошибок нет."
```

## Компоненты

### API Server (`artifacts/api-server/`)
- **Framework**: Express 5, TypeScript, esbuild
- **AI**: Anthropic SDK (@anthropic-ai/sdk), claude-sonnet-4-6
- **Port**: 8080 (proxy → /api)
- **Store**: In-memory (store.ts) — файлы, команды, логи

#### Роуты
- `POST /api/ai/chat` — чат с AI + tools, multi-turn tool loop
- `POST /api/unity/sync` — получить ВСЕ файлы проекта от плагина
- `GET  /api/unity/commands` — команды для плагина (pending → sent)
- `POST /api/unity/commands` — отчёт о выполнении команды
- `POST /api/unity/logs` — консольные логи Unity
- `GET  /api/unity/state` — текущее состояние подключения
- `POST /api/unity/push` — legacy push состояния сцены
- `GET  /api/unity/plugin` — скачать плагин с подставленным SERVER_URL

### Plugin (`plugin/AliTerraAI.cs`)
- **Version**: v7
- **Unity**: 2019+ / C# 7.3 (NO new() shorthand, NO target-typed new)
- **Tabs**: 💬 Чат | 🔄 Fullstack | 📁 Файлы | 🔧 Debug
- **Installation**: Assets/Editor/AliTerraAI.cs
- **Menu**: Window → AliTerra → AI Coder (Ctrl+Shift+A)

#### Новые функции v7
- `StartFullSync()` — сканирует Assets/Packages/ProjectSettings, отправляет все текстовые файлы (≤350KB) на сервер
- `PollCommandsRoutine()` — каждые 3 сек запрашивает команды, исполняет: write_file, create_gameobject, add_component, execute_editor_command
- `FlushLogs()` — каждые 5 сек отправляет консольные логи Unity на сервер
- `OnLogMessage()` — перехватчик Application.logMessageReceived
- Вкладка "🔄 Fullstack" — кнопка синхронизации, toggle polling, лог команд

### Web Inspector (`artifacts/aliterra-inspector/`)
- React + Vite, путь: /

### DB (`lib/db/`)
- PostgreSQL + Drizzle ORM (для будущего использования)

## Стек AliTerra (Unity проект)
- Unity 2022.3 LTS
- C# 7.3+ (Unity 2019+ совместимость)
- Photon Fusion (мультиплеер)
- Thirdweb SDK (Web3/NFT/блокчейн)
- Ready Player Me (аватары)
- Convai (AI NPC)
- PHP/Node.js backend

## Replit домен
https://44c604d5-cbad-400c-8af7-eb2443eadba0-00-3vtnrupat6ost.riker.replit.dev

## GitHub репозиторий
https://github.com/aliter230880/unity
- `plugin/AliTerraAI.cs` — master plugin
- `CONTEXT.md` — эта документация
