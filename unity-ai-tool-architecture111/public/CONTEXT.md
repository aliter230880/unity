# AliTerra AI — Unity Fullstack Developer
## Architecture & Context Guide

---

## 🏗️ System Architecture

```
Unity Editor (AliTerraAI.cs)
         │
         │  POST /api/unity/sync    — sends ALL project files
         │  GET  /api/unity/commands — polls for pending commands (every 3s)
         │  POST /api/unity/logs    — flushes console logs (every 5s)
         ▼
Next.js API Server (Orchestrator)
         │
         │  Stores project state in PostgreSQL
         │  Calls OpenAI GPT-4o with Tool Use
         ▼
OpenAI GPT-4o (The Brain)
         │
         │  Uses 11 tools to read/write files, inspect scene, etc.
         ▼
PostgreSQL Database (The Memory)
```

---

## 🔧 Available AI Tools (Function Calling)

| Tool | Description |
|------|-------------|
| `list_project_files` | Lists all synced Unity project files by type |
| `read_file` | Reads any file content (C#, YAML, JSON, etc.) |
| `write_file` | Creates or overwrites any file → queued for Unity |
| `delete_file` | Deletes a file from the project |
| `create_gameobject` | Creates a GameObject in the active scene |
| `add_component` | Adds a component to an existing GameObject |
| `read_console_logs` | Reads Unity console errors/warnings — for auto-fix |
| `read_scene_hierarchy` | Gets the current scene object tree |
| `execute_editor_command` | play/stop/save/refresh/open_scene |
| `create_scriptable_object` | Creates a ScriptableObject asset |
| `search_in_files` | Searches across all project files for a pattern |

---

## 📡 API Endpoints

### Unity Plugin Endpoints
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/unity/sync` | Receives all files from Unity plugin |
| GET | `/api/unity/commands?apiKey=...` | Plugin polls for pending commands |
| POST | `/api/unity/commands` | Plugin reports command completion |
| POST | `/api/unity/logs` | Receives Unity console logs |
| GET | `/api/unity/logs?apiKey=...` | UI reads logs |
| GET | `/api/unity/files?projectId=...` | UI reads file list |
| POST | `/api/unity/files` | UI reads file content |
| GET | `/api/unity/status?projectId=...` | Dashboard status |

### Chat Endpoints
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/chat` | Main AI chat (SSE streaming) |
| GET | `/api/projects` | List projects |
| POST | `/api/projects` | Create project |
| GET | `/api/sessions?projectId=...` | List sessions |
| POST | `/api/sessions` | Create session |
| GET | `/api/messages?sessionId=...` | Load chat history |
| GET | `/api/plugin/download?projectId=...` | Download AliTerraAI.cs |

---

## 🗄️ Database Schema

### `projects`
- `id` UUID PK
- `name` — project display name
- `unity_version` — e.g. "2022.3.20f1"
- `api_key` — unique key sent by Unity plugin in every request

### `project_files`
- `id` SERIAL PK
- `project_id` → projects
- `path` — relative path: "Assets/Scripts/PlayerController.cs"
- `type` — script | scene | prefab | material | shader | config | other
- `size_bytes` — file size
- `content` — full text content (up to 12KB per file)

### `sessions`
- `id` UUID PK
- `project_id` → projects
- `title` — auto-set from first message

### `messages`
- `id` UUID PK
- `session_id` → sessions
- `role` — user | assistant | tool
- `content` — message text
- `tool_calls` — JSONB: OpenAI tool call objects
- `tool_call_id` — for tool response messages
- `tool_name` — name of tool that was called

### `pending_commands`
- `id` UUID PK
- `project_id` → projects
- `type` — write_file | create_gameobject | add_component | execute_editor_command | delete_file | create_scriptable_object
- `payload` — JSONB: command parameters
- `status` — pending | executing | done | error
- `result` — execution result from Unity

### `console_logs`
- `id` SERIAL PK
- `project_id` → projects
- `log_type` — log | warning | error | compiler_error | exception
- `message` — log text
- `stack_trace` — optional stack trace

### `scene_snapshots`
- `id` SERIAL PK
- `project_id` → projects
- `scene_name` — active scene name
- `hierarchy` — text tree of all GameObjects + components

---

## 🔄 Data Flow: "Make enemies chase the player"

```
1. User types: "Make enemies chase player when they get close, turn red"

2. AI calls list_project_files(filter_type="script")
   → Sees: EnemyAI.cs, PlayerController.cs, etc.

3. AI calls read_file("Assets/Scripts/EnemyAI.cs")
   → Reads existing EnemyAI code

4. AI calls read_file("Assets/Scripts/PlayerController.cs")
   → Understands player structure

5. AI calls write_file("Assets/Scripts/EnemyAI.cs", <new code>)
   → Server stores command in pending_commands table

6. Unity plugin polls GET /api/unity/commands every 3s
   → Receives the write_file command
   → Writes EnemyAI.cs to disk
   → Unity recompiles automatically

7. Unity console picks up compilation result
   → Plugin flushes logs to POST /api/unity/logs

8. AI calls read_console_logs(log_type="error")
   → Checks for compilation errors

9. If error found → AI calls write_file again with fix
   → Loop continues until clean compilation

10. AI responds: "Done! Enemies now chase and turn red."
```

---

## 🔌 Unity Plugin (AliTerraAI.cs)

### File Location
```
YourUnityProject/Assets/Editor/AliTerraAI.cs
```

### Menu Entry
```
Window → AliTerra → AI Coder (Ctrl+Shift+A)
```

### Plugin Capabilities
- **File Sync**: Scans entire project (Assets, Packages, ProjectSettings) and uploads all text files
- **Command Polling**: Every 3 seconds checks for pending commands from AI
- **Console Capture**: Intercepts Unity's `Application.logMessageReceived` and buffers logs
- **Command Execution**:
  - `write_file` — writes any file to disk, creates directories, makes .bak backup
  - `delete_file` — deletes file + .meta
  - `create_gameobject` — creates primitives or empty GOs with position/color/components
  - `add_component` — finds type by name across all assemblies
  - `execute_editor_command` — play, stop, save_scene, refresh_assets, open_scene
  - `create_scriptable_object` — creates SO asset via ScriptableObject.CreateInstance

### File Size Limits
- Text files: up to 12,000 characters are synced
- Binary files (images, audio, etc.): skipped, only path/size recorded
- Sync batch size: 100 files per HTTP request

---

## ⚙️ Environment Variables

```env
DATABASE_URL=postgresql://postgres:postgres@127.0.0.1:5432/app_db
OPENAI_API_KEY=sk-...
```

---

## 🚀 Extending the System

### Adding a New Tool

1. Add to `UNITY_TOOLS` in `src/lib/tools.ts`
2. Add handler in `executeTool()` in `src/lib/orchestrator.ts`
3. If it requires Unity action: insert into `pending_commands` table
4. Add handler in Unity plugin's `ExecuteCommand()` switch statement

### Adding a New Unity Command Type

1. Add new case in `ExecuteCommand()` in the plugin C#
2. Add new method `ExecMyCommand()`
3. Report completion via `ReportCommandDone()`

---

## 🛡️ Security Notes

- API keys are per-project UUIDs (format: `ak_<32 hex chars>`)
- All Unity plugin requests must include valid `apiKey`
- OPENAI_API_KEY is only accessed server-side (never exposed to browser)
- File writes are sandboxed to Unity project directory (relative paths only)
- Automatic .bak file creation before overwriting

---

## 📊 Performance Characteristics

- File sync: ~100 files/second, batched in 100-file chunks
- Command polling: every 3 seconds (configurable in plugin)
- Log flush: every 5 seconds (or when buffer reaches 200 entries)
- Max context per chat: 30 messages from history
- Max AI iterations per request: 10 tool-use loops
- Max file content in AI context: 12,000 chars per file

---

*Generated by AliTerra AI System*
