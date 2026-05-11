import { db } from "@/db";
import { projects } from "@/db/schema";
import { eq } from "drizzle-orm";

export const dynamic = "force-dynamic";

// GET /api/plugin/download?projectId=...
export async function GET(req: Request) {
  try {
    const { searchParams } = new URL(req.url);
    const projectId = searchParams.get("projectId");

    const host = req.headers.get("host") || "localhost:3000";
    const proto = req.headers.get("x-forwarded-proto") || "http";
    const serverUrl = `${proto}://${host}`;

    let apiKey = "YOUR_API_KEY";
    let projectName = "My Unity Project";

    if (projectId) {
      const rows = await db
        .select()
        .from(projects)
        .where(eq(projects.id, projectId));
      if (rows.length > 0) {
        apiKey = rows[0].apiKey;
        projectName = rows[0].name;
      }
    }

    const pluginCode = generatePluginCode(serverUrl, apiKey, projectName);

    return new Response(pluginCode, {
      headers: {
        "Content-Type": "text/plain; charset=utf-8",
        "Content-Disposition": `attachment; filename="AliTerraAI.cs"`,
      },
    });
  } catch (e) {
    return Response.json({ error: String(e) }, { status: 500 });
  }
}

function generatePluginCode(serverUrl: string, apiKey: string, projectName: string): string {
  return `// ╔═══════════════════════════════════════════════════════════════════════════╗
// ║  AliTerra AI v8 — Fullstack Unity Developer                              ║
// ║  Server: ${serverUrl.padEnd(62)}║
// ║  Install: Assets/Editor/AliTerraAI.cs                                    ║
// ║  Menu: Window → AliTerra → AI Coder (Ctrl+Shift+A)                       ║
// ╚═══════════════════════════════════════════════════════════════════════════╝
//
// HOW IT WORKS:
// 1. Plugin syncs ALL project files to the server on demand
// 2. AI reads any file, writes any file, creates GameObjects — all via Tools
// 3. Plugin polls every 3s for pending commands and executes them
// 4. Console logs are flushed to server so AI can self-heal compilation errors
//
// Project: ${projectName}
// API Key: ${apiKey}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace AliTerra
{
    // ─── Data Classes ───────────────────────────────────────────────────────────
    [Serializable] public class SyncFile
    {
        public string path = "";
        public string type = "other";
        public long size = 0;
        public string content = "";
    }

    [Serializable] public class CommandResponse
    {
        public CommandItem[] commands = new CommandItem[0];
    }

    [Serializable] public class CommandItem
    {
        public string id = "";
        public string type = "";
        public string path = "";
        public string content = "";
        public string name = "";
        public string primitive = "";
        public string components = "";
        public string position = "";
        public string color = "";
        public string parent = "";
        public string command = "";
        public string message = "";
        public string script_class = "";
        public string asset_path = "";
    }

    [Serializable] public class LogBatch
    {
        public string apiKey = "";
        public LogEntry[] logs = new LogEntry[0];
    }

    [Serializable] public class LogEntry
    {
        public string logType = "log";
        public string message = "";
        public string stackTrace = "";
    }

    // ─── File Categories ────────────────────────────────────────────────────────
    public enum FileCategory { Script, Scene, Prefab, Material, Shader, Config, Audio, Model, Image, Other }

    public class FileEntry
    {
        public string fullPath = "";
        public string assetPath = "";
        public string fileName = "";
        public string ext = "";
        public FileCategory category;
        public bool isText = false;
        public long sizeBytes = 0;
    }

    public class ScriptInfo
    {
        public string path = "";
        public string className = "";
        public string baseClass = "";
        public int lineCount = 0;
        public List<string> methods = new List<string>();
        public string content = "";
    }

    public class ChatMsg
    {
        public bool isUser = false;
        public string text = "";
        public string toolCalls = "";
        public bool isPending = false;
        public double startTime = 0;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Main EditorWindow
    // ═══════════════════════════════════════════════════════════════════════════
    public class AliTerraAICoder : EditorWindow
    {
        // ── Constants ──────────────────────────────────────────────────────────
        private const string SERVER_URL = "${serverUrl}";
        private const string API_KEY    = "${apiKey}";
        private const string PROJECT_NAME = "${projectName}";

        private const int    MAX_FILE_CHARS  = 12000;
        private const int    MAX_SYNC_BYTES  = 512 * 1024;  // 512 KB per file
        private const double POLL_INTERVAL   = 3.0;
        private const double LOG_INTERVAL    = 5.0;

        // ── Prefs Keys ─────────────────────────────────────────────────────────
        private const string PREF_AUTO     = "AliTerra_AutoApply";
        private const string PREF_POLLING  = "AliTerra_Polling";
        private const string PREF_SESSION  = "AliTerra_SessionId";
        private const string PREF_PROJECT  = "AliTerra_ProjectId";

        // ── State ──────────────────────────────────────────────────────────────
        private int    activeTab     = 0;
        private bool   isBusy        = false;
        private string statusMsg     = "";
        private string projectId     = "";
        private string sessionId     = "";

        // ── Chat ───────────────────────────────────────────────────────────────
        private List<ChatMsg> history = new List<ChatMsg>();
        private string userInput     = "";
        private Vector2 chatScroll;
        private int  pendingIndex    = -1;

        // ── Sync ───────────────────────────────────────────────────────────────
        private bool   syncBusy      = false;
        private string syncStatus    = "Not synced";
        private int    syncFileCount = 0;
        private double lastSyncTime  = -1;

        // ── Polling ────────────────────────────────────────────────────────────
        private bool   polling       = false;
        private double lastPollTime  = -1;
        private double lastLogTime   = -1;
        private int    pendingCmds   = 0;

        // ── Console ────────────────────────────────────────────────────────────
        private List<LogEntry> pendingLogs = new List<LogEntry>();
        private List<string>   commandLog  = new List<string>();
        private Vector2        cmdLogScroll;
        private bool           logListening = false;

        // ── Context ────────────────────────────────────────────────────────────
        private string ctxScene     = "";
        private string ctxObject    = "";
        private string sceneHierarchy = "";
        private string lastScene    = "";

        // ── Files Tab ──────────────────────────────────────────────────────────
        private List<FileEntry> allFiles  = new List<FileEntry>();
        private bool            scanDone  = false;
        private bool            scanRunning = false;
        private int             scanProgress = 0;
        private int             scanTotal    = 0;
        private string          fileFilter   = "";
        private int             fileTypeIdx  = 0;
        private Vector2         fileScroll;
        private FileEntry       viewFile    = null;
        private string          viewContent = "";
        private Vector2         viewScroll;
        private string[]        fileTypeOpts = new[] { "All", "Scripts", "Shaders", "Scenes", "Prefabs", "Other" };

        // ── Styles ─────────────────────────────────────────────────────────────
        private GUIStyle sUser, sAI, sCode, sCmd;
        private bool     stylesOk = false;

        private static readonly HashSet<string> ExcludedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git", ".vs", ".idea", "Library", "Temp", "Logs", "Obj", "obj",
            "Build", "Builds", "UserSettings", "MemoryCaptures", "Recordings",
            "node_modules", ".vibe-backups", "__pycache__"
        };

        private static readonly HashSet<string> TextExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".asmdef", ".asmref", ".json", ".txt", ".md", ".xml",
            ".yaml", ".yml", ".unity", ".prefab", ".mat", ".asset",
            ".controller", ".overridecontroller", ".anim", ".shader",
            ".compute", ".hlsl", ".cginc", ".uss", ".uxml", ".inputactions",
            ".csproj", ".sln"
        };

        // ─────────────────────────────────────────────────────────────────────
        [MenuItem("Window/AliTerra/AI Coder %#a")]
        public static void Open()
        {
            var w = GetWindow<AliTerraAICoder>("🤖 AliTerra AI");
            w.minSize = new Vector2(440, 600);
        }

        // ─────────────────────────────────────────────────────────────────────
        void OnEnable()
        {
            polling    = EditorPrefs.GetBool(PREF_POLLING, false);
            projectId  = EditorPrefs.GetString(PREF_PROJECT, "");
            sessionId  = EditorPrefs.GetString(PREF_SESSION, "");

            EditorApplication.update += Tick;

            if (!logListening)
            {
                Application.logMessageReceived += OnLog;
                logListening = true;
            }

            if (history.Count == 0)
                AddWelcome();

            // Bootstrap: register with server if we don't have a projectId/sessionId
            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(sessionId))
                EditorCoroutine.Start(BootstrapRoutine());
        }

        void OnDisable()
        {
            EditorPrefs.SetBool(PREF_POLLING, polling);
            EditorPrefs.SetString(PREF_PROJECT, projectId);
            EditorPrefs.SetString(PREF_SESSION, sessionId);
            EditorApplication.update -= Tick;
            if (logListening) { Application.logMessageReceived -= OnLog; logListening = false; }
        }

        void AddWelcome()
        {
            history.Add(new ChatMsg
            {
                text = "👋 AliTerra AI v8 — Fullstack Unity Developer\\n\\n" +
                       "I can see and edit ALL files in your project.\\n\\n" +
                       "🔄 Click 'Sync All Files' in the Fullstack tab first\\n" +
                       "⚡ Enable Polling so I can write files automatically\\n\\n" +
                       "Then just describe what you want to build!"
            });
        }

        // ─── Tick ───────────────────────────────────────────────────────────────
        void Tick()
        {
            CaptureContext();
            double t = EditorApplication.timeSinceStartup;

            if (polling && !syncBusy && t - lastPollTime > POLL_INTERVAL)
            {
                lastPollTime = t;
                EditorCoroutine.Start(PollCommandsRoutine());
            }

            if (pendingLogs.Count > 0 && t - lastLogTime > LOG_INTERVAL)
            {
                lastLogTime = t;
                FlushLogs();
            }
        }

        // ─── Console Capture ────────────────────────────────────────────────────
        void OnLog(string msg, string stack, LogType type)
        {
            string lt = type == LogType.Error || type == LogType.Exception ? "error" :
                        type == LogType.Warning ? "warning" : "log";
            if (pendingLogs.Count < 200)
                pendingLogs.Add(new LogEntry { logType = lt, message = msg, stackTrace = stack ?? "" });
        }

        void FlushLogs()
        {
            if (pendingLogs.Count == 0 || string.IsNullOrEmpty(API_KEY)) return;
            var batch = new List<LogEntry>(pendingLogs);
            pendingLogs.Clear();

            var sb = new StringBuilder();
            sb.Append("{\\"apiKey\\":\\""); sb.Append(EscapeJson(API_KEY)); sb.Append("\\",\\"logs\\":[");
            for (int i = 0; i < batch.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append("{\\"logType\\":\\""); sb.Append(EscapeJson(batch[i].logType));
                sb.Append("\\",\\"message\\":\\""); sb.Append(EscapeJson(batch[i].message));
                sb.Append("\\",\\"stackTrace\\":\\""); sb.Append(EscapeJson(batch[i].stackTrace ?? ""));
                sb.Append("\\"}");
            }
            sb.Append("]}");
            SendPost(SERVER_URL + "/api/unity/logs", sb.ToString(), null);
        }

        // ─── Context Capture ────────────────────────────────────────────────────
        void CaptureContext()
        {
            ctxScene = SceneManager.GetActiveScene().name;
            if (ctxScene != lastScene && !string.IsNullOrEmpty(ctxScene))
            {
                lastScene = ctxScene;
                BuildHierarchy();
            }
            var go = Selection.activeGameObject;
            ctxObject = go != null ? go.name : "";
        }

        void BuildHierarchy()
        {
            try
            {
                var scene = EditorSceneManager.GetActiveScene();
                if (!scene.IsValid()) return;
                var sb = new StringBuilder();
                sb.AppendLine("[SCENE: " + scene.name + "]");
                int count = 0;
                foreach (var root in scene.GetRootGameObjects())
                    AppendHierarchy(sb, root, 0, ref count);
                sceneHierarchy = sb.ToString();
            }
            catch { }
        }

        void AppendHierarchy(StringBuilder sb, GameObject go, int depth, ref int count)
        {
            if (count > 500) return; count++;
            sb.Append(new string(' ', depth * 2)).Append("- ").Append(go.name);
            var comps = go.GetComponents<Component>();
            var names = new List<string>();
            foreach (var c in comps) if (c != null && !(c is Transform)) names.Add(c.GetType().Name);
            if (names.Count > 0) sb.Append(" [").Append(string.Join(", ", names.ToArray())).Append("]");
            sb.AppendLine();
            for (int i = 0; i < go.transform.childCount; i++)
                AppendHierarchy(sb, go.transform.GetChild(i).gameObject, depth + 1, ref count);
        }

        // ─── Bootstrap ──────────────────────────────────────────────────────────
        IEnumerator BootstrapRoutine()
        {
            // Try to get or create project
            string createBody = "{\\"name\\":\\"" + EscapeJson(PROJECT_NAME) + "\\"}";
            yield return PostRoutine(SERVER_URL + "/api/projects", createBody, (ok, text) =>
            {
                if (ok && !string.IsNullOrEmpty(text))
                {
                    // Try to parse id
                    var idMatch = Regex.Match(text, "\\"id\\":\\"([^\\"]+)\\"");
                    if (idMatch.Success)
                    {
                        projectId = idMatch.Groups[1].Value;
                        EditorPrefs.SetString(PREF_PROJECT, projectId);
                        // Create session
                        EditorCoroutine.Start(CreateSessionRoutine());
                    }
                }
            });
        }

        IEnumerator CreateSessionRoutine()
        {
            string body = "{\\"projectId\\":\\"" + EscapeJson(projectId) + "\\",\\"title\\":\\"Main Session\\"}";
            yield return PostRoutine(SERVER_URL + "/api/sessions", body, (ok, text) =>
            {
                if (ok && !string.IsNullOrEmpty(text))
                {
                    var idMatch = Regex.Match(text, "\\"id\\":\\"([^\\"]+)\\"");
                    if (idMatch.Success)
                    {
                        sessionId = idMatch.Groups[1].Value;
                        EditorPrefs.SetString(PREF_SESSION, sessionId);
                        AddCommandLog("✅ Connected to server. ProjectId: " + projectId.Substring(0, 8) + "...");
                    }
                }
            });
        }

        // ─── Full File Sync ──────────────────────────────────────────────────────
        void StartFullSync()
        {
            if (syncBusy) return;
            syncBusy   = true;
            syncStatus = "Scanning files...";
            Repaint();
            EditorCoroutine.Start(SyncRoutine());
        }

        IEnumerator SyncRoutine()
        {
            string root      = Application.dataPath.Replace("/Assets", "");
            var    files     = new List<SyncFile>();
            var    scanDirs  = new[] { "Assets", "Packages", "ProjectSettings" };

            foreach (var dirName in scanDirs)
            {
                string dirPath = Path.Combine(root, dirName);
                if (!Directory.Exists(dirPath)) continue;

                foreach (var file in WalkDirectory(dirPath))
                {
                    string rel = ToRelative(root, file);
                    if (string.IsNullOrEmpty(rel)) continue;
                    FileInfo fi;
                    try { fi = new FileInfo(file); } catch { continue; }

                    string ext     = fi.Extension.ToLowerInvariant();
                    bool   isText  = TextExtensions.Contains(ext);
                    string content = "";

                    if (isText && fi.Length < MAX_SYNC_BYTES)
                    {
                        try { content = File.ReadAllText(file, Encoding.UTF8); }
                        catch { content = ""; }
                        if (content.Length > MAX_FILE_CHARS)
                            content = content.Substring(0, MAX_FILE_CHARS) + "\\n// [TRUNCATED]";
                    }

                    files.Add(new SyncFile
                    {
                        path    = rel.Replace("\\\\", "/"),
                        type    = ClassifyExt(ext),
                        size    = fi.Length,
                        content = content
                    });
                }
                yield return null;
            }

            syncStatus    = "Uploading " + files.Count + " files...";
            syncFileCount = files.Count;
            Repaint();

            // Send in batches of 100
            int batchSize   = 100;
            int totalBatches = Mathf.CeilToInt(files.Count / (float)batchSize);

            for (int b = 0; b < totalBatches; b++)
            {
                var batch = files.GetRange(b * batchSize, Mathf.Min(batchSize, files.Count - b * batchSize));
                string json  = BuildSyncJson(batch, b == 0);
                bool   batchOk = false;
                yield return PostRoutine(SERVER_URL + "/api/unity/sync", json, (ok, text) => { batchOk = ok; });
                if (!batchOk) { syncStatus = "❌ Upload failed at batch " + b; syncBusy = false; Repaint(); yield break; }
                syncStatus = "Uploading... " + Mathf.Min((b + 1) * batchSize, files.Count) + "/" + files.Count;
                Repaint();
            }

            syncBusy      = false;
            syncStatus    = "✅ Synced " + files.Count + " files";
            lastSyncTime  = EditorApplication.timeSinceStartup;
            AddCommandLog("Synced " + files.Count + " files");
            Repaint();
        }

        string BuildSyncJson(List<SyncFile> files, bool isFirst)
        {
            var sb = new StringBuilder();
            sb.Append("{\\"apiKey\\":\\""); sb.Append(EscapeJson(API_KEY));
            sb.Append("\\",\\"projectName\\":\\""); sb.Append(EscapeJson(PROJECT_NAME));
            sb.Append("\\",\\"unityVersion\\":\\""); sb.Append(EscapeJson(Application.unityVersion));
            if (isFirst)
            {
                sb.Append("\\",\\"scene\\":\\""); sb.Append(EscapeJson(ctxScene));
                sb.Append("\\",\\"hierarchy\\":\\""); sb.Append(EscapeJson(sceneHierarchy.Length > 6000 ? sceneHierarchy.Substring(0, 6000) : sceneHierarchy));
            }
            sb.Append("\\",\\"files\\":[");
            for (int i = 0; i < files.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append("{\\"path\\":\\""); sb.Append(EscapeJson(files[i].path));
                sb.Append("\\",\\"type\\":\\""); sb.Append(EscapeJson(files[i].type));
                sb.Append("\\",\\"size\\":"); sb.Append(files[i].size);
                sb.Append(",\\"content\\":\\""); sb.Append(EscapeJson(files[i].content));
                sb.Append("\\"}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        // ─── Command Polling ────────────────────────────────────────────────────
        IEnumerator PollCommandsRoutine()
        {
            string url = SERVER_URL + "/api/unity/commands?apiKey=" + UnityWebRequest.EscapeURL(API_KEY);
            yield return GetRoutine(url, (ok, text) =>
            {
                if (!ok || string.IsNullOrEmpty(text)) return;
                CommandResponse resp;
                try { resp = JsonUtility.FromJson<CommandResponse>(text); }
                catch { return; }
                if (resp == null || resp.commands == null || resp.commands.Length == 0) return;

                pendingCmds = resp.commands.Length;
                foreach (var cmd in resp.commands)
                {
                    bool   cmdOk  = false;
                    string result = "";
                    try   { cmdOk = ExecuteCommand(cmd, out result); }
                    catch (Exception ex) { cmdOk = false; result = ex.Message; }

                    AddCommandLog((cmdOk ? "✅ " : "❌ ") + cmd.type + (result.Length > 0 ? ": " + result.Substring(0, Math.Min(80, result.Length)) : ""));
                    ReportCommandDone(cmd.id, cmdOk, result);
                }
                AssetDatabase.Refresh();
                pendingCmds = 0;
                Repaint();
            });
        }

        void ReportCommandDone(string cmdId, bool success, string result)
        {
            string body = "{\\"apiKey\\":\\"" + EscapeJson(API_KEY) +
                          "\\",\\"commandId\\":\\"" + EscapeJson(cmdId) +
                          "\\",\\"success\\":" + (success ? "true" : "false") +
                          ",\\"result\\":\\"" + EscapeJson(result.Length > 500 ? result.Substring(0, 500) : result) + "\\"}";
            SendPost(SERVER_URL + "/api/unity/commands", body, null);
        }

        bool ExecuteCommand(CommandItem cmd, out string result)
        {
            result = "";
            switch (cmd.type)
            {
                case "write_file":                return ExecWriteFile(cmd.path, cmd.content, out result);
                case "delete_file":               return ExecDeleteFile(cmd.path, out result);
                case "create_gameobject":         return ExecCreateGO(cmd, out result);
                case "add_component":             return ExecAddComponent(cmd.name, cmd.components, out result);
                case "execute_editor_command":    return ExecEditorCmd(cmd.command, cmd.message, out result);
                case "create_scriptable_object":  return ExecCreateSO(cmd.script_class, cmd.asset_path, out result);
                default: result = "Unknown command: " + cmd.type; return false;
            }
        }

        bool ExecWriteFile(string path, string content, out string result)
        {
            result = "";
            if (string.IsNullOrEmpty(path)) { result = "path empty"; return false; }
            if (content == null) { result = "content null"; return false; }

            string root     = Application.dataPath.Replace("/Assets", "");
            string fullPath = Path.Combine(root, path.Replace("/", Path.DirectorySeparatorChar.ToString()));
            string dir      = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            // Auto-backup
            if (File.Exists(fullPath)) File.Copy(fullPath, fullPath + ".bak", true);

            File.WriteAllText(fullPath, content, Encoding.UTF8);
            string assetPath = path.StartsWith("Assets/") ? path : "Assets/" + path;
            AssetDatabase.ImportAsset(assetPath.Replace("\\\\", "/"));
            result = "Written: " + path;
            return true;
        }

        bool ExecDeleteFile(string path, out string result)
        {
            result = "";
            string root     = Application.dataPath.Replace("/Assets", "");
            string fullPath = Path.Combine(root, path.Replace("/", Path.DirectorySeparatorChar.ToString()));
            if (!File.Exists(fullPath)) { result = "File not found: " + path; return false; }
            File.Delete(fullPath);
            string metaPath = fullPath + ".meta";
            if (File.Exists(metaPath)) File.Delete(metaPath);
            AssetDatabase.Refresh();
            result = "Deleted: " + path;
            return true;
        }

        bool ExecCreateGO(CommandItem cmd, out string result)
        {
            result = "";
            if (string.IsNullOrEmpty(cmd.name)) { result = "name empty"; return false; }
            GameObject go;
            switch ((cmd.primitive ?? "empty").ToLowerInvariant())
            {
                case "cube":     go = GameObject.CreatePrimitive(PrimitiveType.Cube);     break;
                case "sphere":   go = GameObject.CreatePrimitive(PrimitiveType.Sphere);   break;
                case "capsule":  go = GameObject.CreatePrimitive(PrimitiveType.Capsule);  break;
                case "cylinder": go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); break;
                case "plane":    go = GameObject.CreatePrimitive(PrimitiveType.Plane);    break;
                case "quad":     go = GameObject.CreatePrimitive(PrimitiveType.Quad);     break;
                default:         go = new GameObject();                                    break;
            }
            go.name = cmd.name;

            if (!string.IsNullOrEmpty(cmd.position))
            {
                var parts = cmd.position.Split(',');
                if (parts.Length == 3 && float.TryParse(parts[0], out float x) && float.TryParse(parts[1], out float y) && float.TryParse(parts[2], out float z))
                    go.transform.position = new Vector3(x, y, z);
            }

            if (!string.IsNullOrEmpty(cmd.color))
            {
                var rend = go.GetComponent<Renderer>();
                if (rend != null)
                {
                    var parts = cmd.color.Split(',');
                    if (parts.Length >= 3 && float.TryParse(parts[0], out float r) && float.TryParse(parts[1], out float g) && float.TryParse(parts[2], out float b))
                    {
                        rend.material = new Material(Shader.Find("Standard"));
                        rend.material.color = new Color(r, g, b);
                    }
                }
            }

            if (!string.IsNullOrEmpty(cmd.parent))
            {
                var parentGo = GameObject.Find(cmd.parent);
                if (parentGo != null) go.transform.SetParent(parentGo.transform);
            }

            if (!string.IsNullOrEmpty(cmd.components))
                ExecAddComponent(cmd.name, cmd.components, out _);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            result = "Created: " + cmd.name;
            return true;
        }

        bool ExecAddComponent(string goName, string comps, out string result)
        {
            result = "";
            var go = GameObject.Find(goName);
            if (go == null) { result = "GameObject not found: " + goName; return false; }
            foreach (var compName in comps.Split(','))
            {
                string cn = compName.Trim();
                if (string.IsNullOrEmpty(cn)) continue;
                var type = GetTypeByName(cn);
                if (type != null) go.AddComponent(type);
                else result += "Unknown: " + cn + " ";
            }
            result = string.IsNullOrEmpty(result) ? "Components added to " + goName : result.Trim();
            return true;
        }

        bool ExecEditorCmd(string cmd, string arg, out string result)
        {
            result = "";
            switch (cmd)
            {
                case "play":           EditorApplication.isPlaying = true;                    result = "Play started";      break;
                case "stop":           EditorApplication.isPlaying = false;                   result = "Play stopped";      break;
                case "save_scene":     EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene()); result = "Scene saved"; break;
                case "refresh_assets": AssetDatabase.Refresh();                               result = "Assets refreshed";  break;
                case "open_scene":
                    if (!string.IsNullOrEmpty(arg)) { EditorSceneManager.OpenScene(arg); result = "Opened: " + arg; }
                    else { result = "No scene path provided"; return false; }
                    break;
                default: result = "Unknown editor command: " + cmd; return false;
            }
            return true;
        }

        bool ExecCreateSO(string scriptClass, string assetPath, out string result)
        {
            result = "";
            try
            {
                var type = GetTypeByName(scriptClass);
                if (type == null) { result = "Script class not found: " + scriptClass; return false; }
                var so = ScriptableObject.CreateInstance(type);
                string dir = Path.GetDirectoryName(assetPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(Path.Combine(Application.dataPath.Replace("/Assets", ""), dir)))
                    Directory.CreateDirectory(Path.Combine(Application.dataPath.Replace("/Assets", ""), dir));
                AssetDatabase.CreateAsset(so, assetPath);
                AssetDatabase.SaveAssets();
                result = "Created SO: " + assetPath;
                return true;
            }
            catch (Exception ex) { result = ex.Message; return false; }
        }

        System.Type GetTypeByName(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(name);
                if (t != null) return t;
                t = asm.GetType("UnityEngine." + name);
                if (t != null) return t;
            }
            return null;
        }

        // ─── File Scanning (for Files tab) ──────────────────────────────────────
        void StartScan()
        {
            if (scanRunning) return;
            scanRunning = true; scanDone = false; scanProgress = 0; scanTotal = 0;
            allFiles.Clear();
            EditorCoroutine.Start(ScanRoutine());
        }

        IEnumerator ScanRoutine()
        {
            string assets = Application.dataPath;
            var    paths  = new List<string>();
            foreach (var f in WalkDirectory(assets)) paths.Add(f);
            scanTotal = paths.Count;
            string root = Application.dataPath.Replace("/Assets", "");
            foreach (var path in paths)
            {
                scanProgress++;
                FileInfo fi;
                try { fi = new FileInfo(path); } catch { continue; }
                string rel = ToRelative(root, path);
                string ext = fi.Extension.ToLowerInvariant();
                allFiles.Add(new FileEntry
                {
                    fullPath  = path,
                    assetPath = rel.Replace("\\\\", "/"),
                    fileName  = fi.Name,
                    ext       = ext,
                    sizeBytes = fi.Length,
                    isText    = TextExtensions.Contains(ext),
                    category  = ClassifyCategory(ext)
                });
                if (scanProgress % 50 == 0) { Repaint(); yield return null; }
            }
            scanRunning = false; scanDone = true;
            Repaint();
        }

        // ─── Helpers ────────────────────────────────────────────────────────────
        IEnumerable<string> WalkDirectory(string dir)
        {
            string dirName = Path.GetFileName(dir);
            if (ExcludedDirs.Contains(dirName)) yield break;
            string[] files;
            try { files = Directory.GetFiles(dir); } catch { yield break; }
            foreach (var f in files) yield return f;
            string[] dirs;
            try { dirs = Directory.GetDirectories(dir); } catch { yield break; }
            foreach (var sub in dirs)
                foreach (var f in WalkDirectory(sub)) yield return f;
        }

        string ToRelative(string root, string fullPath)
        {
            string norm = fullPath.Replace("\\\\", "/");
            string r    = root.Replace("\\\\", "/");
            if (norm.StartsWith(r + "/")) return norm.Substring(r.Length + 1);
            return "";
        }

        string ClassifyExt(string ext)
        {
            switch (ext)
            {
                case ".cs":   return "script";
                case ".unity": return "scene";
                case ".prefab": return "prefab";
                case ".mat":   return "material";
                case ".shader": case ".compute": case ".hlsl": case ".cginc": return "shader";
                case ".json": case ".xml": case ".yaml": case ".yml": case ".asset": case ".asmdef": return "config";
                default:      return "other";
            }
        }

        FileCategory ClassifyCategory(string ext)
        {
            switch (ext)
            {
                case ".cs":   return FileCategory.Script;
                case ".unity": return FileCategory.Scene;
                case ".prefab": return FileCategory.Prefab;
                case ".mat":   return FileCategory.Material;
                case ".shader": case ".compute": return FileCategory.Shader;
                default:      return FileCategory.Other;
            }
        }

        string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\\\", "\\\\\\\\").Replace("\\"", "\\\\\\"").Replace("\\n", "\\\\n")
                    .Replace("\\r", "\\\\r").Replace("\\t", "\\\\t");
        }

        void AddCommandLog(string entry)
        {
            commandLog.Add(DateTime.Now.ToString("HH:mm:ss") + " " + entry);
            if (commandLog.Count > 200) commandLog.RemoveAt(0);
        }

        // ─── HTTP Helpers ────────────────────────────────────────────────────────
        void SendPost(string url, string json, Action<bool, string> callback)
        {
            EditorCoroutine.Start(PostRoutine(url, json, callback));
        }

        IEnumerator GetRoutine(string url, Action<bool, string> callback)
        {
            var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            bool ok = req.result == UnityWebRequest.Result.Success;
            callback?.Invoke(ok, ok ? req.downloadHandler.text : req.error);
        }

        IEnumerator PostRoutine(string url, string json, Action<bool, string> callback)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            var req = new UnityWebRequest(url, "POST");
            req.uploadHandler   = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            bool ok = req.result == UnityWebRequest.Result.Success;
            callback?.Invoke(ok, ok ? req.downloadHandler.text : req.error);
        }

        // ─── SEND CHAT MESSAGE ───────────────────────────────────────────────────
        void SendMessage()
        {
            string msg = userInput.Trim();
            if (string.IsNullOrEmpty(msg) || isBusy) return;
            userInput = "";

            history.Add(new ChatMsg { isUser = true, text = msg });
            var pending = new ChatMsg { text = "⏳ Thinking...", isPending = true, startTime = EditorApplication.timeSinceStartup };
            history.Add(pending);
            pendingIndex = history.Count - 1;

            isBusy    = true;
            statusMsg = "Sending to AI...";
            Repaint();

            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(sessionId))
            {
                pending.text     = "❌ Not connected to server. Check Fullstack tab.";
                pending.isPending = false;
                isBusy = false; statusMsg = ""; Repaint();
                return;
            }

            string body = "{\\"message\\":\\"" + EscapeJson(msg) +
                          "\\",\\"sessionId\\":\\"" + EscapeJson(sessionId) +
                          "\\",\\"projectId\\":\\"" + EscapeJson(projectId) + "\\"}";

            EditorCoroutine.Start(ChatRoutine(body, pending));
        }

        IEnumerator ChatRoutine(string body, ChatMsg pendingMsg)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            var req = new UnityWebRequest(SERVER_URL + "/api/chat", "POST");
            req.uploadHandler   = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 120;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                pendingMsg.text      = "❌ Error: " + req.error;
                pendingMsg.isPending = false;
                isBusy = false; statusMsg = ""; Repaint();
                yield break;
            }

            string responseText = req.downloadHandler.text;
            string finalContent = "";
            string toolLog      = "";

            // Parse SSE response
            var lines = responseText.Split('\\n');
            foreach (var line in lines)
            {
                if (!line.StartsWith("data: ")) continue;
                string data = line.Substring(6).Trim();
                if (data == "[DONE]") break;
                try
                {
                    var typeMatch    = Regex.Match(data, "\\"type\\":\\"([^\\"]+)\\"");
                    var contentMatch = Regex.Match(data, "\\"content\\":\\"((?:[^\\"\\\\\\\\]|\\\\\\\\.)*)\\"\\"");
                    if (!contentMatch.Success)
                        contentMatch = Regex.Match(data, "\\"content\\":(\\"[\\\\s\\\\S]*?\\")(?:,|})");

                    string type    = typeMatch.Success ? typeMatch.Groups[1].Value : "";
                    string content = contentMatch.Success ? Regex.Unescape(contentMatch.Groups[1].Value.Trim('"')) : "";

                    if (type == "tool")     toolLog      += content;
                    if (type == "final")    finalContent  = content;
                }
                catch { }
            }

            if (string.IsNullOrEmpty(finalContent))
            {
                // Try to extract from raw JSON
                var match = Regex.Match(responseText, "\\"type\\":\\"final\\",\\"content\\":\\"((?:[^\\"\\\\\\\\]|\\\\\\\\.)*)\\"");
                if (match.Success)
                    finalContent = Regex.Unescape(match.Groups[1].Value);
            }

            pendingMsg.text      = string.IsNullOrEmpty(finalContent) ? "✅ Done. Check the Fullstack tab for executed commands." : finalContent;
            pendingMsg.toolCalls = toolLog;
            pendingMsg.isPending = false;
            isBusy     = false;
            statusMsg  = "";
            Repaint();
        }

        // ─── GUI ────────────────────────────────────────────────────────────────
        void InitStyles()
        {
            if (stylesOk) return;
            stylesOk = true;

            sUser = new GUIStyle(GUI.skin.box) { wordWrap = true, alignment = TextAnchor.UpperLeft, padding = new RectOffset(8, 8, 6, 6), fontSize = 12 };
            sUser.normal.background = MakeTex(new Color(0.17f, 0.35f, 0.6f, 0.95f));
            sUser.normal.textColor  = Color.white;

            sAI = new GUIStyle(sUser);
            sAI.normal.background = MakeTex(new Color(0.10f, 0.13f, 0.16f, 1f));
            sAI.normal.textColor  = new Color(0.88f, 0.92f, 0.96f);

            sCode = new GUIStyle(EditorStyles.helpBox) { wordWrap = false, fontSize = 10 };
            sCode.normal.textColor = new Color(0.6f, 0.9f, 0.6f);

            sCmd = new GUIStyle(EditorStyles.miniLabel);
            sCmd.normal.textColor = new Color(0.5f, 0.8f, 0.5f);
        }

        static Texture2D MakeTex(Color c) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t; }

        void OnGUI()
        {
            InitStyles();

            // Header
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("🤖 AliTerra AI v8", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            Color old = GUI.color;
            bool synced = lastSyncTime > 0;
            GUI.color = synced ? Color.cyan : Color.gray;
            GUILayout.Label(synced ? "⚡ " + syncFileCount + " files" : "⚡ not synced", EditorStyles.miniLabel);
            GUI.color = polling ? Color.green : Color.gray;
            GUILayout.Label(polling ? "● poll" : "○ poll", EditorStyles.miniLabel);
            GUI.color = old;
            if (pendingCmds > 0) { GUI.color = Color.yellow; GUILayout.Label("⏳" + pendingCmds, EditorStyles.miniLabel); GUI.color = old; }
            EditorGUILayout.EndHorizontal();

            activeTab = GUILayout.Toolbar(activeTab, new[] { "💬 Chat", "🔄 Fullstack", "📁 Files", "🔧 Debug" });
            switch (activeTab)
            {
                case 0: DrawChat(); break;
                case 1: DrawFullstack(); break;
                case 2: DrawFiles(); break;
                case 3: DrawDebug(); break;
            }
        }

        // ─── Chat Tab ───────────────────────────────────────────────────────────
        void DrawChat()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Scene: " + (string.IsNullOrEmpty(ctxScene) ? "none" : ctxScene) +
                            (string.IsNullOrEmpty(ctxObject) ? "" : " | Selected: " + ctxObject), EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            chatScroll = EditorGUILayout.BeginScrollView(chatScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < history.Count; i++)
            {
                var m = history[i];
                GUILayout.BeginHorizontal();
                if (m.isUser) GUILayout.Space(40);
                GUILayout.BeginVertical(m.isUser ? sUser : sAI);
                GUILayout.Label(m.text, m.isUser ? sUser : sAI);
                if (!string.IsNullOrEmpty(m.toolCalls))
                {
                    GUILayout.Label("🔧 Tools used:", EditorStyles.miniLabel);
                    EditorGUILayout.BeginScrollView(Vector2.zero, GUILayout.MaxHeight(60));
                    GUILayout.Label(m.toolCalls, sCmd);
                    EditorGUILayout.EndScrollView();
                }
                GUILayout.EndVertical();
                if (!m.isUser) GUILayout.Space(40);
                GUILayout.EndHorizontal();
                GUILayout.Space(3);
            }
            EditorGUILayout.EndScrollView();

            if (Event.current.type == EventType.Repaint) chatScroll.y = float.MaxValue;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.SetNextControlName("chatInput");
            userInput = EditorGUILayout.TextArea(userInput, GUILayout.MinHeight(56), GUILayout.ExpandWidth(true));
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.enabled = !isBusy && !string.IsNullOrEmpty(userInput.Trim());
            if (GUILayout.Button(isBusy ? "⏳ Thinking..." : "▶ Send", GUILayout.Width(100))) SendMessage();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            if (Event.current.type == EventType.KeyDown && Event.current.shift && Event.current.keyCode == KeyCode.Return)
            {
                if (!isBusy && !string.IsNullOrEmpty(userInput.Trim())) { SendMessage(); Event.current.Use(); }
            }
        }

        // ─── Fullstack Tab ──────────────────────────────────────────────────────
        void DrawFullstack()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Server: " + SERVER_URL, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Project ID: " + (projectId.Length > 8 ? projectId.Substring(0, 8) + "..." : projectId), EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Session ID: " + (sessionId.Length > 8 ? sessionId.Substring(0, 8) + "..." : sessionId), EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            // Sync
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📂 File Sync", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Uploads ALL project files so AI can read them.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(2);
            GUI.enabled = !syncBusy;
            if (GUILayout.Button(syncBusy ? "⏳ Syncing..." : "🔄 Sync All Files", GUILayout.Height(28))) StartFullSync();
            GUI.enabled = true;
            Color old = GUI.color;
            GUI.color = syncStatus.StartsWith("✅") ? Color.green : syncStatus.StartsWith("❌") ? Color.red : Color.yellow;
            EditorGUILayout.LabelField(syncStatus, EditorStyles.wordWrappedMiniLabel);
            GUI.color = old;
            if (lastSyncTime > 0)
                EditorGUILayout.LabelField("Last sync: " + (int)(EditorApplication.timeSinceStartup - lastSyncTime) + "s ago · " + syncFileCount + " files", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Polling
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("⚡ Command Polling", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Plugin polls every 3s and executes AI commands (write files, create objects).", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.BeginHorizontal();
            bool newPolling = EditorGUILayout.ToggleLeft("", polling, GUILayout.Width(20));
            if (newPolling != polling) { polling = newPolling; AddCommandLog(polling ? "Polling ON" : "Polling OFF"); }
            GUI.color = polling ? Color.green : Color.gray;
            GUILayout.Label(polling ? "● ACTIVE — AI writes files automatically" : "○ INACTIVE", EditorStyles.miniLabel);
            GUI.color = old;
            EditorGUILayout.EndHorizontal();
            if (pendingCmds > 0) { GUI.color = Color.yellow; EditorGUILayout.LabelField("⏳ Executing " + pendingCmds + " commands..."); GUI.color = old; }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Command log
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("📋 Command Log (" + commandLog.Count + ")", EditorStyles.boldLabel);
            if (GUILayout.Button("🗑", EditorStyles.miniButton, GUILayout.Width(24))) commandLog.Clear();
            EditorGUILayout.EndHorizontal();
            cmdLogScroll = EditorGUILayout.BeginScrollView(cmdLogScroll, GUILayout.Height(140));
            foreach (var entry in commandLog) GUILayout.Label(entry, sCmd);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // ─── Files Tab ──────────────────────────────────────────────────────────
        void DrawFiles()
        {
            EditorGUILayout.Space(4);
            if (!scanDone)
            {
                if (!scanRunning)
                {
                    if (GUILayout.Button("🔍 Scan Project")) StartScan();
                }
                else
                {
                    EditorGUILayout.HelpBox("Scanning: " + scanProgress + "/" + scanTotal, MessageType.Info);
                }
                return;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Files: " + allFiles.Count, EditorStyles.miniLabel);
            if (GUILayout.Button("🔄", EditorStyles.miniButton, GUILayout.Width(24))) StartScan();
            EditorGUILayout.EndHorizontal();

            fileFilter  = EditorGUILayout.TextField("🔍 Search:", fileFilter);
            fileTypeIdx = GUILayout.Toolbar(fileTypeIdx, fileTypeOpts);

            FileCategory? cat = null;
            if (fileTypeIdx == 1) cat = FileCategory.Script;
            if (fileTypeIdx == 2) cat = FileCategory.Shader;
            if (fileTypeIdx == 3) cat = FileCategory.Scene;
            if (fileTypeIdx == 4) cat = FileCategory.Prefab;

            fileScroll = EditorGUILayout.BeginScrollView(fileScroll, GUILayout.Height(200));
            foreach (var fe in allFiles)
            {
                if (cat.HasValue && fe.category != cat.Value) continue;
                if (!string.IsNullOrEmpty(fileFilter) && !fe.fileName.ToLowerInvariant().Contains(fileFilter.ToLowerInvariant())) continue;

                EditorGUILayout.BeginHorizontal();
                string icon = fe.category == FileCategory.Script ? "📜" : fe.category == FileCategory.Scene ? "🎬" :
                              fe.category == FileCategory.Prefab ? "🧊" : fe.category == FileCategory.Shader ? "✨" : "📄";
                GUILayout.Label(icon + " " + fe.fileName, GUILayout.ExpandWidth(true));
                if (fe.isText && GUILayout.Button("👁", EditorStyles.miniButton, GUILayout.Width(24)))
                {
                    viewFile = fe;
                    viewContent = "";
                    try { viewContent = File.ReadAllText(fe.fullPath, Encoding.UTF8); } catch { }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            if (viewFile != null && !string.IsNullOrEmpty(viewContent))
            {
                EditorGUILayout.LabelField("📄 " + viewFile.fileName, EditorStyles.boldLabel);
                viewScroll = EditorGUILayout.BeginScrollView(viewScroll, GUILayout.ExpandHeight(true));
                GUILayout.Label(viewContent.Length > 8000 ? viewContent.Substring(0, 8000) + "\\n...[truncated]" : viewContent, sCode);
                EditorGUILayout.EndScrollView();
            }
        }

        // ─── Debug Tab ──────────────────────────────────────────────────────────
        void DrawDebug()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Connection", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Server: " + SERVER_URL);
            EditorGUILayout.LabelField("API Key: " + API_KEY.Substring(0, Math.Min(20, API_KEY.Length)) + "...");
            EditorGUILayout.LabelField("Project ID: " + projectId);
            EditorGUILayout.LabelField("Session ID: " + sessionId);
            EditorGUILayout.LabelField("Scene: " + ctxScene + " | Object: " + ctxObject);
            EditorGUILayout.LabelField("Files synced: " + syncFileCount);
            EditorGUILayout.LabelField("Pending logs: " + pendingLogs.Count);
            EditorGUILayout.EndVertical();

            if (GUILayout.Button("Force Re-connect")) EditorCoroutine.Start(BootstrapRoutine());
            if (GUILayout.Button("Flush Logs Now")) FlushLogs();
            if (GUILayout.Button("Send Test Message"))
            {
                userInput = "List all scripts in the project";
                SendMessage();
                activeTab = 0;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  EditorCoroutine — lightweight coroutine for Editor code
    // ═══════════════════════════════════════════════════════════════════════════
    public class EditorCoroutine
    {
        private IEnumerator routine;
        public static EditorCoroutine Start(IEnumerator r) { var c = new EditorCoroutine(r); c.Register(); return c; }
        private EditorCoroutine(IEnumerator r) { routine = r; }
        private void Register()   { EditorApplication.update += Update; }
        private void Unregister() { EditorApplication.update -= Update; }
        void Update() { if (!MoveNext()) Unregister(); }
        bool MoveNext()
        {
            var cur = routine.Current;
            if (cur is UnityWebRequestAsyncOperation op) { if (!op.isDone) return true; }
            return routine.MoveNext();
        }
    }
}
`;
}
