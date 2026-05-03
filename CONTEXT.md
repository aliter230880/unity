# AliTerra AI Coder — Полный контекст проекта

> Последнее обновление: 2026-05-03  
> Версия плагина: v6  
> Статус: в работе

---

## Что это такое

AliTerra — метавселенная / P2E игра на Unity. Этот репозиторий содержит:
- **Unity Editor Plugin** (`AliTerraAI.cs`) — встраивается в Unity Editor, позволяет разработчику общаться с AI прямо внутри редактора
- **API Server** (Node.js/Express) — принимает запросы от плагина, шлёт контекст в Claude AI (Anthropic), возвращает C# код
- **Web Inspector** (React/Vite) — веб-панель с визуализацией состояния Unity и кнопкой скачивания плагина

---

## Архитектура системы

```
Unity Editor
  └─ AliTerraAI.cs (EditorWindow)
       ├─ Сканирует проект (все файлы + C# индекс методов)
       ├─ Сканирует иерархию сцены (все GameObject с позициями)
       ├─ Чат-интерфейс (история, пузыри, авто-применение)
       ├─ File browser (все форматы с цветовой кодировкой)
       ├─ GitHub push (REST API, PAT в EditorPrefs)
       └─ HTTP → API Server
            │
            ▼
API Server (Express, порт 8080)
  ├─ POST /api/ai/chat      ← получает контекст + сообщение, возвращает reply+code
  ├─ GET  /api/unity/plugin ← отдаёт AliTerraAI.cs с подставленным SERVER_URL
  ├─ POST /api/unity/push   ← heartbeat от Unity (сцена, объект, файл)
  └─ GET  /api/unity/state  ← текущее состояние для Web Inspector
       │
       ▼
Anthropic Claude API (через Replit AI proxy)
  └─ claude-sonnet-4-6, max_tokens: 8192

Web Inspector (React/Vite, порт 18976)
  └─ Показывает: статус подключения Unity, активную сцену, открытый файл
  └─ Кнопка скачивания плагина
```

---

## Стек технологий

| Слой | Технология |
|---|---|
| Unity плагин | C# 7.3, UnityEditor API, UnityWebRequest, EditorCoroutine |
| API сервер | Node.js, Express 5, TypeScript, esbuild, pino (логи) |
| AI | Anthropic Claude claude-sonnet-4-6 (через Replit proxy) |
| Frontend | React 18, Vite 7, TypeScript |
| Монорепо | pnpm workspaces |
| Деплой | Replit (shared proxy на порт 80) |

### Игровой стек Unity (для AI-контекста)
- **Photon Fusion** — мультиплеер
- **Thirdweb SDK** — Web3 / NFT / LUX токены
- **Ready Player Me** — аватары игроков
- **Convai** — AI NPC диалоги
- **PHP + Node.js** — backend

---

## Ключевые файлы

```
artifacts/
  api-server/
    plugin/
      AliTerraAI.cs          ← МАСТЕР-файл плагина (редактировать здесь)
    src/
      plugin/
        AliTerraAI.cs         ← КОПИЯ (синхронизируется через cp)
      routes/
        ai.ts                 ← системный промпт + обработка запросов к Claude
        unity.ts              ← /api/unity/* эндпоинты, сервит плагин
        index.ts              ← монтирует все роуты
      index.ts                ← Express сервер (порт из $PORT, дефолт 8080)
    build.mjs                 ← esbuild конфиг

  aliterra-inspector/
    src/
      components/
        PluginDownload.tsx    ← UI панель: статус Unity + кнопка скачать

CONTEXT.md                    ← этот файл
```

---

## Эволюция плагина (v1 → v6)

### v1 — Базовый чат
- EditorWindow с полем ввода и чатом
- HTTP POST к API серверу
- Базовый контекст (активная сцена, выбранный объект)

### v2 — Сканирование проекта
- `ScanProjectRoutine()` — итерирует все файлы в `Assets/`
- Строит индекс C# классов: имя, базовый класс, количество строк, методы
- `FindRelated()` — поиск связанных скриптов по ключевым словам запроса
- `BuildSummary()` — краткий текстовый индекс 1500+ скриптов

### v3 — Работа с файлами
- `FileEntry` + `FileCategory` (Script/Scene/Prefab/Material/Shader/Config/Audio/Model/Image/Other)
- File browser с фильтрами и поиском
- Чтение содержимого текстовых файлов (`.cs`, `.unity`, `.prefab`, `.mat`, `.shader`, `.json`, и т.д.)
- Отображение бинарных файлов с размером

### v4 — GitHub интеграция
- Прямые REST API вызовы к GitHub (без git CLI)
- PAT токен хранится в `EditorPrefs` — не передаётся на сервер
- `GitHubPushRoutine()`: GET SHA → PUT content (Base64)
- Поддержка push из file browser и chat bubble

### v5 — Редактирование сцены
- `ScanSceneHierarchy()` — сканирует все GameObject с позициями и компонентами
- Передаёт иерархию в AI-контекст (до 10,000 символов)
- `AutoApplyCode()` — записывает `[InitializeOnLoad]` Editor-скрипт в `Assets/Editor/`
- `AssetDatabase.Refresh()` — Unity видит новый файл, компилирует, скрипт запускается
- Шаблон InitializeOnLoad с `EditorPrefs` защитой от повторного запуска и самоудалением

### v6 — Стабильность и UX (текущая)
- **Немедленный отклик**: пузырь `🤖 AI: ●○○  Думаю...  (Nс)` появляется сразу после отправки
- **Живая анимация**: точка бегает по 3 позициям каждые 0.5с, счётчик секунд
- **Авто-повтор**: при пустом ответе — один автоматический повтор с уведомлением
- **Понятные ошибки**: HTTP-код + текст ошибки показывается в статусе
- **Таймаут увеличен** с 90 до 120 секунд
- `isPending` поле в `ChatMsg`, `pendingIndex` + `lastJson` + `retryCount` в окне

---

## AI промпт (системный)

Файл: `artifacts/api-server/src/routes/ai.ts`

Промпт разделён на блоки:
1. **EDITOR_SCRIPT_EXAMPLE** — шаблон `[InitializeOnLoad]` скрипта с объяснением паттерна
2. **SYSTEM_PROMPT** — основной промпт:
   - Роль: Unity-разработчик / CTO AliTerra метавселенной
   - Режим работы со сценами (5 шагов: читай иерархию → найди объект → сгенерируй скрипт)
   - Правила контекста (что делать с каждым блоком данных)
   - Формат ответов (всегда русский, всегда полный файл)

### Контекст передаваемый AI:
```
[КОНТЕКСТ UNITY]
  Активная сцена: MainCity
  Выбранный объект: Casino1
  ⚡ АВТО-РЕЖИМ ВКЛЮЧЁН (если autoApplyMode=true)
  
  [ИЕРАРХИЯ: MainCity | rootObjects:47]
  Casino1 [10.0,0.0,20.0] {BoxCollider,MeshRenderer}
  ...

  [ПРОЕКТ: 1847 файлов, 312 скриптов]
  PlayerController:MonoBehaviour (245л) | Start,Update,Move,Jump
  ...

  [ВЫБРАННЫЙ ФАЙЛ: PlayerController.cs] (Script (.cs))
  ```csharp
  ... содержимое файла (до 8000 символов) ...
  ```

  [СВЯЗАННЫЕ СКРИПТЫ]
  // Assets/Scripts/Network/FusionPlayer.cs — FusionPlayer
  ...

[ЗАПРОС]
  <сообщение пользователя>
```

### Извлечение кода из ответа (`extractCode`):
Ищет блоки по приоритету: `csharp` → `hlsl` → `yaml` → `json` → `xml` → любой

---

## Flow: от запроса до изменения сцены

```
Пользователь: "В MainCity добавь SpawnPoint у Casino"
    │
    ▼
SendMessage() → добавляет пузырь "Думаю..." → HTTP POST /api/ai/chat
    │
    ▼ (10-30 секунд, AI думает)
    │
    ▼
Claude AI получает:
  - иерархию сцены (знает Casino1 [10,0,20])
  - проектный индекс (знает все классы)
  - запрос пользователя
  │
  ▼
Claude генерирует InitializeOnLoad C# скрипт:
  class AliTerra_Edit_AddSpawnPoint {
    static AliTerra_Edit_AddSpawnPoint() {
      if (EditorPrefs.GetBool("done_key")) return;
      EditorPrefs.SetBool("done_key", true);
      EditorApplication.delayCall += Apply;
    }
    static void Apply() {
      var go = new GameObject("SpawnPoint");
      go.transform.position = new Vector3(12, 0, 20); // рядом с Casino [10,0,20]
      EditorSceneManager.MarkSceneDirty(...);
      EditorSceneManager.SaveScene(...);
    }
  }
    │
    ▼
OnAIResponse() → заменяет пузырь "Думаю..." на реальный ответ
    │
    ├─ autoApply=true → AutoApplyCode() → File.WriteAllText("Assets/Editor/AliTerra_Edit_xxxxxxxx.cs")
    │                                   → AssetDatabase.Refresh() → Unity компилирует
    │                                   → [InitializeOnLoad] запускается при компиляции
    │                                   → SpawnPoint создан, сцена сохранена
    │
    └─ autoApply=false → показывает кнопку "⚡ Применить к сцене"
```

---

## Известные проблемы и исправленные баги

### ИСПРАВЛЕНО: esbuild ломался на тройных обратных кавычках
**Проблема**: `EDITOR_SCRIPT_EXAMPLE` — template literal содержал ` ```csharp ` и ` ``` ` внутри
строки. esbuild парсил это как конец template literal.  
**Файл**: `artifacts/api-server/src/routes/ai.ts`, строки ~38 и ~92  
**Решение**: Изменили ` ```csharp ` на текст "блок csharp" в строке 148. Внутри `EDITOR_SCRIPT_EXAMPLE`
оставили escaped `\`\`\`` (работает в Node.js runtime, только esbuild иногда глючит).

### ИСПРАВЛЕНО: ExtCat dictionary в плагине
**Проблема**: Неверный синтаксис в `Dictionary` инициализаторе (лишняя запятая / неверный тип)  
**Решение**: Удалили `ExtCat`, оставили только `CatMap` с правильным синтаксисом C# 7.3

### ИСПРАВЛЕНО: Порты заняты при рестарте
**Проблема**: После сбоя процессы держат порты 8080 и 18976  
**Решение**: `fuser -k 8080/tcp 18976/tcp` перед рестартом

### ИСПРАВЛЕНО: Нет обратной связи при ожидании
**Проблема**: После отправки сообщения — тишина до 30 секунд, непонятно работает ли  
**Решение**: v6 — мгновенный пузырь "Думаю..." с анимацией и счётчиком

### ИЗВЕСТНО: Плагин надо синхронизировать вручную
**Проблема**: Два места плагина: `plugin/AliTerraAI.cs` (master) и `src/plugin/AliTerraAI.cs` (копия)  
**Решение**: После каждого изменения выполнять `cp artifacts/api-server/plugin/AliTerraAI.cs artifacts/api-server/src/plugin/AliTerraAI.cs`

### ИЗВЕСТНО: InitializeOnLoad скрипт не запускается в некоторых случаях
**Проблема**: Если у Unity включён "Auto Refresh = false" — скрипт не компилируется автоматически  
**Решение**: Пользователю нужно вручную нажать Ctrl+R или включить Auto Refresh

---

## Ограничения C# в плагине (Unity 2019+, C# 7.3)

- **НЕТ** `new()` shorthand (target-typed new) — только `new ClassName()`
- **НЕТ** multi-character char literals — только `char c = 'x'`
- **НЕТ** record types, pattern matching switch expressions
- **ЕСТЬ** `List<T>`, `Dictionary<K,V>`, `StringBuilder`, LINQ (только System.Linq)
- **ЕСТЬ** `Regex`, `File`, `Directory`, `Path` из System.IO
- `UnityWebRequest` для HTTP (не HttpClient — он блокирует Editor thread)
- `EditorCoroutine` — кастомная реализация coroutines для Editor (встроена в плагин)

---

## API Endpoints

### POST /api/ai/chat
```json
// Запрос:
{
  "messages": [{"role": "user", "content": "..."}],
  "context": {
    "scene": "MainCity",
    "selectedObject": "Casino1",
    "sceneHierarchy": "[ИЕРАРХИЯ: MainCity | rootObjects:47]\n...",
    "scriptName": "PlayerController.cs",
    "scriptPath": "Assets/Scripts/PlayerController.cs",
    "fileType": "Script (.cs)",
    "scriptContent": "using UnityEngine;\n...",
    "projectSummary": "[ПРОЕКТ: 1847 файлов...]\n...",
    "projectScanned": true,
    "projectScriptCount": 312,
    "relatedScripts": [{"path": "...", "className": "...", "content": "..."}],
    "autoApplyMode": true
  },
  "message": "добавь SpawnPoint у входа в казино"
}

// Ответ:
{
  "reply": "Создаю Editor-скрипт...",
  "code": "using UnityEngine;\n...",
  "codeLang": "csharp",
  "hasCode": true,
  "autoApplyMode": true,
  "contextReceived": {...}
}
```

### GET /api/unity/plugin
Отдаёт `AliTerraAI.cs` с заменой `__SERVER_URL__` на реальный URL сервера.  
Content-Disposition: attachment; filename="AliTerraAI.cs"

### POST /api/unity/push
```json
{
  "scene": "MainCity",
  "selectedObject": "Casino1",
  "openScriptName": "PlayerController.cs",
  "openScriptPath": "Assets/Scripts/PlayerController.cs"
}
```
Heartbeat каждые 3 секунды от Unity плагина.

### GET /api/unity/state
```json
{
  "connected": true,
  "scene": "MainCity",
  "selectedObject": "Casino1",
  "tag": "",
  "openScriptName": "PlayerController.cs",
  "openScriptPath": "Assets/Scripts/PlayerController.cs"
}
```

---

## Переменные окружения

| Переменная | Описание | Где |
|---|---|---|
| `AI_INTEGRATIONS_ANTHROPIC_BASE_URL` | URL прокси Anthropic (Replit) | Secret |
| `AI_INTEGRATIONS_ANTHROPIC_API_KEY` | API ключ Anthropic (Replit proxy) | Secret |
| `SESSION_SECRET` | Секрет сессий Express | Secret |
| `PORT` | Порт API сервера (назначает Replit) | Runtime |
| `GITHUB_TOKEN` | PAT для push в GitHub из сервера | Secret (добавить) |

---

## Планы и следующие шаги

### Приоритет 1 — Стабильность
- [ ] **Streaming ответов**: переключить `/api/ai/chat` на SSE/streaming — пользователь видит текст как он печатается
- [ ] **Авто-синхронизация плагина**: скрипт который копирует `plugin/` → `src/plugin/` автоматически
- [ ] **Health check**: плагин пингует сервер при старте и показывает статус подключения

### Приоритет 2 — Функционал сцены
- [ ] **Undo поддержка**: `Undo.RegisterCreatedObjectUndo()` в генерируемых скриптах
- [ ] **Preview режим**: AI показывает что изменится ДО применения (список объектов)
- [ ] **Batch операции**: один запрос → несколько изменений в сцене
- [ ] **Prefab поддержка**: создавать/применять Prefab из чата

### Приоритет 3 — GitHub
- [ ] **Auto-push**: после AutoApplyCode — опционально пушить скрипт в GitHub
- [ ] **Diff view**: показывать diff изменённого файла перед push
- [ ] **Branch management**: создавать feature-ветки для AI изменений

### Приоритет 4 — Мультиплеер / Игровая логика
- [ ] **Photon Fusion helpers**: AI знает NetworkObject, NetworkBehaviour, RPC паттерны
- [ ] **Thirdweb integration**: AI может генерировать код для NFT минтинга и LUX токенов
- [ ] **Convai NPC**: AI помогает настраивать диалоги NPC

---

## Установка плагина в Unity

1. Скачай `AliTerraAI.cs` с `https://[replit-domain]/api/unity/plugin`
2. Положи в `Assets/Editor/AliTerraAI.cs`
3. Unity скомпилирует автоматически
4. Открой: `Window → AliTerra → AI Coder` (или Ctrl+Shift+A)
5. В табе `🐙 GitHub` введи Personal Access Token (с правами `repo`)
6. Включи `🤖 Авто` для автоматического применения изменений

### Требования
- Unity 2019.4 LTS или новее
- C# 7.3+
- Интернет соединение (для API вызовов)

---

## Replit домен

```
https://44c604d5-cbad-400c-8af7-eb2443eadba0-00-3vtnrupat6ost.riker.replit.dev
```

API базовый URL: `https://[домен]/api`  
Web Inspector: `https://[домен]/`

---

## Структура репозитория GitHub

```
unity/
  CONTEXT.md                 ← этот файл
  AliTerraAI.cs              ← актуальная версия плагина для скачивания
  README.md                  ← краткое описание для GitHub
```
