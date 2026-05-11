import { NextRequest } from "next/server";
import { db } from "@/db";
import { projects } from "@/db/schema";
import { eq } from "drizzle-orm";

export const dynamic = "force-dynamic";

export async function GET(req: NextRequest) {
  const projectId = req.nextUrl.searchParams.get("projectId");
  const serverUrl =
    req.nextUrl.origin ||
    process.env.NEXT_PUBLIC_APP_URL ||
    "http://localhost:3000";

  let apiKey = "YOUR_API_KEY_HERE";
  let projectName = "MyUnityProject";

  if (projectId) {
    const [project] = await db
      .select()
      .from(projects)
      .where(eq(projects.id, projectId))
      .limit(1);

    if (project) {
      apiKey = project.apiKey;
      projectName = project.name;
    }
  }

  const pluginCode = generatePluginCode(serverUrl, apiKey, projectName);

  return new Response(pluginCode, {
    headers: {
      "Content-Type": "text/plain; charset=utf-8",
      "Content-Disposition": `attachment; filename="AliTerraAI.cs"`,
    },
  });
}

function generatePluginCode(
  serverUrl: string,
  apiKey: string,
  projectName: string
): string {
  return `// ╔═══════════════════════════════════════════════════════════════════════╗
// ║  AliTerra AI Coder v11 — Fullstack Unity Developer Plugin            ║
// ║  Installation: Assets/Editor/AliTerraAI.cs                          ║
// ║  Menu: Window → AliTerra → AI Coder (Ctrl+Shift+A)                  ║
// ║                                                                       ║
// ║  Server: ${serverUrl.padEnd(55)} ║
// ╚═══════════════════════════════════════════════════════════════════════╝
//
// This plugin connects Unity to AliTerra AI server.
// The AI can see ALL project files, write scripts, create GameObjects,
// read console logs and fix errors — automatically.
//
// SETUP:
// 1. Place this file in Assets/Editor/AliTerraAI.cs
// 2. Open Window → AliTerra → AI Coder
// 3. Click "Sync Project" to send files to server
// 4. Enable Polling to allow AI to execute commands
// 5. Chat with AI in the web browser at: ${serverUrl}

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
    // ── Data structs ──────────────────────────────────────────────────────
    [Serializable]
    public class SyncFile
    {
        public string path = "";
        public string type = "other";
        public long size = 0;
        public string content = "";
    }

    [Serializable]
    public class CommandResponse
    {
        public CommandItem[] commands = new CommandItem[0];
    }

    [Serializable]
    public class CommandItem
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
    }

    [Serializable]
    public class LogBatch
    {
        public LogItem[] logs = new LogItem[0];
    }

    [Serializable]
    public class LogItem
    {
        public string logType = "log";
        public string message = "";
        public string stackTrace = "";
    }

    // ── File categories ───────────────────────────────────────────────────
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

    public class ChatMsg
    {
        public bool isUser = false;
        public string text = "";
        public bool isPending = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Main EditorWindow
    // ═══════════════════════════════════════════════════════════════════════
    public class AliTerraAICoder : EditorWindow
    {
        // ── Config ────────────────────────────────────────────────────────
        private const string SERVER_URL = "${serverUrl}";
        private const string API_KEY = "${apiKey}";
        private const string PROJECT_NAME = "${projectName}";
        private const int MAX_FILE_CONTENT = 80000;    // 80KB per file max
        private const int MAX_SYNC_FILES = 1000;
        private const double POLL_INTERVAL = 3.0;
        private const double LOG_FLUSH_INTERVAL = 5.0;
        private const double STATE_PUSH_INTERVAL = 30.0;

        // ── Prefs keys ────────────────────────────────────────────────────
        private const string PREF_POLLING = "AT_Polling";
        private const string PREF_AUTO = "AT_Auto";
        private const string PREF_TAB = "AT_Tab";

        // ── State ─────────────────────────────────────────────────────────
        private bool polling = false;
        private bool syncBusy = false;
        private string syncStatus = "Not synced";
        private int syncFileCount = 0;
        private double lastSyncTime = -1;
        private double lastPollTime = -1;
        private double lastLogFlush = -1;
        private double lastStatePush = -1;
        private int pendingCmdCount = 0;

        // ── Chat (local log) ──────────────────────────────────────────────
        private List<ChatMsg> chatLog = new List<ChatMsg>();
        private Vector2 chatScroll;
        private List<string> commandLog = new List<string>();
        private Vector2 cmdLogScroll;

        // ── Files ─────────────────────────────────────────────────────────
        private List<FileEntry> allFiles = new List<FileEntry>();
        private bool scanDone = false;
        private bool scanRunning = false;
        private int scanProgress = 0;
        private int scanTotal = 0;
        private Vector2 fileScroll;
        private string fileFilter = "";
        private int fileTypeIdx = 0;
        private string[] fileTypeLabels = new string[] { "All", "Scripts", "Scenes", "Prefabs", "Shaders", "Other" };
        private FileEntry viewFile = null;
        private string viewContent = "";
        private Vector2 viewScroll;

        // ── Console logs buffer ───────────────────────────────────────────
        private List<LogItem> pendingLogs = new List<LogItem>();
        private bool logListening = false;

        // ── Scene ─────────────────────────────────────────────────────────
        private string ctxScene = "";
        private string sceneHierarchy = "";
        private string ctxObject = "";

        // ── UI ────────────────────────────────────────────────────────────
        private int activeTab = 0;
        private GUIStyle sLog, sCmd, sBg, sHeader, sCode;
        private bool stylesInit = false;

        // ── Excluded dirs ─────────────────────────────────────────────────
        private static readonly HashSet<string> ExcludedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git", ".vs", ".idea", "Library", "Temp", "Logs", "obj", "Build",
            "Builds", "UserSettings", "MemoryCaptures", "node_modules", "__pycache__"
        };

        private static readonly HashSet<string> TextExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".asmdef", ".asmref", ".json", ".txt", ".md", ".xml", ".yaml", ".yml",
            ".unity", ".prefab", ".mat", ".asset", ".controller", ".anim", ".shader",
            ".compute", ".hlsl", ".cginc", ".uss", ".uxml", ".inputactions", ".csproj", ".sln",
            ".glsl", ".hlsl", ".ini", ".cfg"
        };

        // ── Menu ──────────────────────────────────────────────────────────
        [MenuItem("Window/AliTerra/AI Coder %#a")]
        public static void Open()
        {
            AliTerraAICoder w = GetWindow<AliTerraAICoder>("🤖 AliTerra AI");
            w.minSize = new Vector2(440, 560);
        }

        // ── Lifecycle ─────────────────────────────────────────────────────
        void OnEnable()
        {
            polling = EditorPrefs.GetBool(PREF_POLLING, false);
            activeTab = EditorPrefs.GetInt(PREF_TAB, 0);

            if (!logListening)
            {
                Application.logMessageReceived += OnLog;
                logListening = true;
            }

            EditorApplication.update += OnUpdate;
            CaptureContext();
            AddLog("✅ AliTerra AI v11 connected to " + SERVER_URL);
        }

        void OnDisable()
        {
            EditorApplication.update -= OnUpdate;
            if (logListening)
            {
                Application.logMessageReceived -= OnLog;
                logListening = false;
            }
        }

        void OnLog(string msg, string stack, LogType type)
        {
            string lt = type == LogType.Error ? "error"
                       : type == LogType.Warning ? "warning"
                       : type == LogType.Exception ? "exception"
                       : "log";

            if (pendingLogs.Count < 500)
            {
                pendingLogs.Add(new LogItem
                {
                    logType = lt,
                    message = msg.Length > 1000 ? msg.Substring(0, 1000) : msg,
                    stackTrace = (stack ?? "").Length > 800 ? stack.Substring(0, 800) : (stack ?? "")
                });
            }
        }

        // ── Update loop ───────────────────────────────────────────────────
        void OnUpdate()
        {
            double t = EditorApplication.timeSinceStartup;
            CaptureContext();

            if (!polling) return;

            // Poll commands
            if (t - lastPollTime > POLL_INTERVAL)
            {
                lastPollTime = t;
                EditorCoroutine.Start(PollCommandsRoutine());
            }

            // Flush logs
            if (pendingLogs.Count > 0 && t - lastLogFlush > LOG_FLUSH_INTERVAL)
            {
                lastLogFlush = t;
                FlushLogs();
            }

            // Periodic state push
            if (t - lastStatePush > STATE_PUSH_INTERVAL)
            {
                lastStatePush = t;
                EditorCoroutine.Start(PushStateRoutine());
            }
        }

        // ── Context capture ───────────────────────────────────────────────
        void CaptureContext()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.name != ctxScene)
            {
                ctxScene = scene.name;
                BuildSceneHierarchy();
            }

            GameObject go = Selection.activeGameObject;
            ctxObject = go != null ? go.name : "";
        }

        void BuildSceneHierarchy()
        {
            try
            {
                Scene scene = EditorSceneManager.GetActiveScene();
                if (!scene.IsValid()) return;
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("[SCENE HIERARCHY: " + scene.name + "]");
                int count = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                    AppendHierarchy(sb, root, 0, ref count);
                sceneHierarchy = sb.ToString();
            }
            catch { }
        }

        void AppendHierarchy(StringBuilder sb, GameObject go, int depth, ref int count)
        {
            if (count > 500) return;
            count++;
            sb.Append(new string(' ', depth * 2)).Append("- ").Append(go.name);
            Component[] comps = go.GetComponents<Component>();
            List<string> cnames = new List<string>();
            foreach (Component c in comps)
                if (c != null && !(c is Transform))
                    cnames.Add(c.GetType().Name);
            if (cnames.Count > 0)
                sb.Append(" [").Append(string.Join(", ", cnames.ToArray())).Append("]");
            sb.AppendLine();
            for (int i = 0; i < go.transform.childCount && count < 500; i++)
                AppendHierarchy(sb, go.transform.GetChild(i).gameObject, depth + 1, ref count);
        }

        // ── Log helpers ───────────────────────────────────────────────────
        void AddLog(string msg) { commandLog.Add(msg); if (commandLog.Count > 200) commandLog.RemoveAt(0); }

        void FlushLogs()
        {
            if (pendingLogs.Count == 0) return;
            List<LogItem> batch = new List<LogItem>(pendingLogs);
            pendingLogs.Clear();
            StringBuilder sb = new StringBuilder("{\\\"logs\\\":[");
            for (int i = 0; i < batch.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append("{\\\"logType\\\":\\\"").Append(EscapeJson(batch[i].logType))
                  .Append("\\\",\\\"message\\\":\\\"").Append(EscapeJson(batch[i].message))
                  .Append("\\\",\\\"stackTrace\\\":\\\"").Append(EscapeJson(batch[i].stackTrace))
                  .Append("\\\"}");
            }
            sb.Append("]}");
            SendPost(SERVER_URL + "/api/unity/logs", sb.ToString(), null);
        }

        // ── Full project sync ─────────────────────────────────────────────
        void StartFullSync()
        {
            if (syncBusy) return;
            syncBusy = true;
            syncStatus = "Scanning...";
            Repaint();
            EditorCoroutine.Start(SyncRoutine());
        }

        IEnumerator SyncRoutine()
        {
            string root = GetProjectRoot();
            List<SyncFile> files = new List<SyncFile>();
            string[] scanDirs = new string[] { "Assets", "Packages", "ProjectSettings" };

            foreach (string dirName in scanDirs)
            {
                string dirPath = Path.Combine(root, dirName);
                if (!Directory.Exists(dirPath)) continue;

                foreach (string file in WalkDirectory(dirPath))
                {
                    if (files.Count >= MAX_SYNC_FILES) break;
                    string rel = ToRelative(root, file);
                    if (string.IsNullOrEmpty(rel)) continue;

                    FileInfo fi;
                    try { fi = new FileInfo(file); }
                    catch { continue; }

                    string ext = fi.Extension.ToLowerInvariant();
                    bool isText = TextExtensions.Contains(ext);
                    string content = "";

                    if (isText && fi.Length < MAX_FILE_CONTENT)
                    {
                        try { content = File.ReadAllText(file, Encoding.UTF8); }
                        catch { }
                    }

                    string fileType = ClassifyExt(ext);
                    files.Add(new SyncFile
                    {
                        path = rel.Replace("\\\\", "/"),
                        type = fileType,
                        size = fi.Length,
                        content = content
                    });
                }
            }

            syncFileCount = files.Count;
            syncStatus = "Uploading " + files.Count + " files...";
            Repaint();

            string json = BuildSyncJson(files);
            yield return PostRoutine(SERVER_URL + "/api/unity/sync", json, (ok, text) =>
            {
                syncBusy = false;
                if (ok)
                {
                    syncStatus = "✅ Synced " + files.Count + " files";
                    lastSyncTime = EditorApplication.timeSinceStartup;
                    AddLog("✅ Sync: " + files.Count + " files uploaded");
                }
                else
                {
                    syncStatus = "❌ Sync error: " + text;
                    AddLog("❌ Sync error: " + text);
                }
                Repaint();
            });
        }

        string BuildSyncJson(List<SyncFile> files)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            sb.Append("\\\"projectName\\\":\\\"").Append(EscapeJson(PROJECT_NAME)).Append("\\\",");
            sb.Append("\\\"unityVersion\\\":\\\"").Append(EscapeJson(Application.unityVersion)).Append("\\\",");
            sb.Append("\\\"scene\\\":\\\"").Append(EscapeJson(ctxScene)).Append("\\\",");
            string hier = sceneHierarchy.Length > 8000 ? sceneHierarchy.Substring(0, 8000) : sceneHierarchy;
            sb.Append("\\\"hierarchy\\\":\\\"").Append(EscapeJson(hier)).Append("\\\",");
            sb.Append("\\\"files\\\":[");
            for (int i = 0; i < files.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append("{");
                sb.Append("\\\"path\\\":\\\"").Append(EscapeJson(files[i].path)).Append("\\\",");
                sb.Append("\\\"type\\\":\\\"").Append(EscapeJson(files[i].type)).Append("\\\",");
                sb.Append("\\\"size\\\":").Append(files[i].size).Append(",");
                sb.Append("\\\"content\\\":\\\"").Append(EscapeJson(files[i].content)).Append("\\\"");
                sb.Append("}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        // ── Push state (hierarchy etc) ────────────────────────────────────
        IEnumerator PushStateRoutine()
        {
            string hier = sceneHierarchy.Length > 6000 ? sceneHierarchy.Substring(0, 6000) : sceneHierarchy;
            string json = "{\\\"scene\\\":\\\"" + EscapeJson(ctxScene) + "\\\",\\\"hierarchy\\\":\\\"" + EscapeJson(hier) + "\\\"}";
            yield return PostRoutine(SERVER_URL + "/api/unity/sync", json, null);
        }

        // ── Command polling ────────────────────────────────────────────────
        IEnumerator PollCommandsRoutine()
        {
            yield return GetRoutine(SERVER_URL + "/api/unity/commands", (ok, text) =>
            {
                if (!ok || string.IsNullOrEmpty(text)) return;
                ProcessCommands(text);
            });
        }

        void ProcessCommands(string json)
        {
            CommandResponse resp;
            try { resp = JsonUtility.FromJson<CommandResponse>(json); }
            catch { return; }

            if (resp == null || resp.commands == null || resp.commands.Length == 0)
            {
                pendingCmdCount = 0;
                return;
            }

            pendingCmdCount = resp.commands.Length;

            foreach (CommandItem cmd in resp.commands)
            {
                bool ok = false;
                string result = "";
                try { ok = ExecuteCommand(cmd, out result); }
                catch (Exception ex) { result = ex.Message; }

                string label = (ok ? "✅ " : "❌ ") + cmd.type;
                if (!string.IsNullOrEmpty(result))
                    label += ": " + result.Substring(0, Math.Min(80, result.Length));
                AddLog(label);

                ReportCommandDone(cmd.id, ok, result);
            }

            if (resp.commands.Length > 0)
            {
                AssetDatabase.Refresh();
                pendingCmdCount = 0;
                Repaint();
            }
        }

        bool ExecuteCommand(CommandItem cmd, out string result)
        {
            result = "";
            switch (cmd.type)
            {
                case "write_file":      return CmdWriteFile(cmd.path, cmd.content, out result);
                case "delete_file":     return CmdDeleteFile(cmd.path, out result);
                case "create_gameobject": return CmdCreateGameObject(cmd, out result);
                case "add_component":   return CmdAddComponent(cmd.name, cmd.components, out result);
                case "execute_editor_command": return CmdEditorCommand(cmd.command, cmd.message, out result);
                default:
                    result = "Unknown command: " + cmd.type;
                    return false;
            }
        }

        // ── Command implementations ────────────────────────────────────────
        bool CmdWriteFile(string path, string content, out string result)
        {
            result = "";
            if (string.IsNullOrEmpty(path)) { result = "path empty"; return false; }
            if (content == null) { result = "content null"; return false; }

            string fullPath = Path.Combine(GetProjectRoot(), path.Replace("/", Path.DirectorySeparatorChar.ToString()));
            string dir = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Create backup
            if (File.Exists(fullPath))
            {
                string backupDir = Path.Combine(GetProjectRoot(), ".vibe-backups");
                if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);
                string backupName = Path.GetFileNameWithoutExtension(path) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + Path.GetExtension(path);
                try { File.Copy(fullPath, Path.Combine(backupDir, backupName)); } catch { }
            }

            File.WriteAllText(fullPath, content, Encoding.UTF8);
            result = "Written: " + path;
            return true;
        }

        bool CmdDeleteFile(string path, out string result)
        {
            result = "";
            string fullPath = Path.Combine(GetProjectRoot(), path.Replace("/", Path.DirectorySeparatorChar.ToString()));
            if (!File.Exists(fullPath)) { result = "File not found: " + path; return false; }
            File.Delete(fullPath);
            result = "Deleted: " + path;
            return true;
        }

        bool CmdCreateGameObject(CommandItem cmd, out string result)
        {
            result = "";
            GameObject go;

            if (cmd.primitive == "Empty" || string.IsNullOrEmpty(cmd.primitive))
                go = new GameObject(cmd.name);
            else
            {
                PrimitiveType pt = PrimitiveType.Cube;
                if (Enum.TryParse(cmd.primitive, out PrimitiveType parsed)) pt = parsed;
                go = GameObject.CreatePrimitive(pt);
                go.name = cmd.name;
            }

            // Position
            if (!string.IsNullOrEmpty(cmd.position))
            {
                string[] parts = cmd.position.Split(',');
                if (parts.Length >= 3)
                {
                    float x, y, z;
                    if (float.TryParse(parts[0], out x) && float.TryParse(parts[1], out y) && float.TryParse(parts[2], out z))
                        go.transform.position = new Vector3(x, y, z);
                }
            }

            // Color
            if (!string.IsNullOrEmpty(cmd.color))
            {
                string[] parts = cmd.color.Split(',');
                if (parts.Length >= 3)
                {
                    float r, g, b;
                    if (float.TryParse(parts[0], out r) && float.TryParse(parts[1], out g) && float.TryParse(parts[2], out b))
                    {
                        Renderer rend = go.GetComponent<Renderer>();
                        if (rend != null)
                        {
                            Material mat = new Material(Shader.Find("Standard"));
                            mat.color = new Color(r, g, b);
                            rend.material = mat;
                        }
                    }
                }
            }

            // Parent
            if (!string.IsNullOrEmpty(cmd.parent))
            {
                GameObject parentGo = GameObject.Find(cmd.parent);
                if (parentGo != null) go.transform.SetParent(parentGo.transform, true);
            }

            // Components
            if (!string.IsNullOrEmpty(cmd.components))
            {
                foreach (string compName in cmd.components.Split(','))
                {
                    string cn = compName.Trim();
                    if (string.IsNullOrEmpty(cn)) continue;
                    Type t = GetTypeByName(cn);
                    if (t != null) go.AddComponent(t);
                }
            }

            Undo.RegisterCreatedObjectUndo(go, "AliTerra: Create " + go.name);
            result = "Created: " + go.name;
            return true;
        }

        bool CmdAddComponent(string goName, string components, out string result)
        {
            result = "";
            GameObject go = GameObject.Find(goName);
            if (go == null) { result = "GameObject not found: " + goName; return false; }

            List<string> added = new List<string>();
            foreach (string compName in components.Split(','))
            {
                string cn = compName.Trim();
                if (string.IsNullOrEmpty(cn)) continue;
                Type t = GetTypeByName(cn);
                if (t != null) { go.AddComponent(t); added.Add(cn); }
                else added.Add("?" + cn);
            }

            result = "Added to " + goName + ": " + string.Join(", ", added.ToArray());
            return true;
        }

        bool CmdEditorCommand(string command, string message, out string result)
        {
            result = "";
            switch (command.ToLower())
            {
                case "play":    EditorApplication.isPlaying = true;  result = "Play mode"; return true;
                case "stop":    EditorApplication.isPlaying = false; result = "Stop mode"; return true;
                case "pause":   EditorApplication.isPaused = !EditorApplication.isPaused; result = "Toggle pause"; return true;
                case "save":    EditorSceneManager.SaveOpenScenes(); result = "Scenes saved"; return true;
                case "refresh": AssetDatabase.Refresh(); result = "Assets refreshed"; return true;
                case "message": EditorUtility.DisplayDialog("AliTerra AI", message ?? "", "OK"); result = "Message shown"; return true;
                default:        result = "Unknown editor command: " + command; return false;
            }
        }

        void ReportCommandDone(string id, bool ok, string resultText)
        {
            if (string.IsNullOrEmpty(id)) return;
            string json = "{\\\"commandId\\\":\\\"" + id + "\\\",\\\"success\\\":" + (ok ? "true" : "false")
                        + ",\\\"result\\\":\\\"" + EscapeJson(resultText) + "\\\"}";
            SendPost(SERVER_URL + "/api/unity/commands", json, null);
        }

        // ── Project scanning ──────────────────────────────────────────────
        void StartProjectScan()
        {
            if (scanRunning) return;
            scanRunning = true;
            scanDone = false;
            allFiles.Clear();
            Repaint();
            EditorCoroutine.Start(ScanRoutine());
        }

        IEnumerator ScanRoutine()
        {
            string root = GetProjectRoot();
            string[] scanDirs = new string[] { "Assets", "Packages" };
            List<FileEntry> found = new List<FileEntry>();
            scanTotal = 0;
            scanProgress = 0;

            foreach (string dirName in scanDirs)
            {
                string dirPath = Path.Combine(root, dirName);
                if (!Directory.Exists(dirPath)) continue;
                string[] allFileArr;
                try { allFileArr = Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories); }
                catch { continue; }
                scanTotal += allFileArr.Length;
            }

            foreach (string dirName in scanDirs)
            {
                string dirPath = Path.Combine(root, dirName);
                if (!Directory.Exists(dirPath)) continue;

                foreach (string file in WalkDirectory(dirPath))
                {
                    scanProgress++;
                    if (scanProgress % 50 == 0) { Repaint(); yield return null; }

                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    bool isText = TextExtensions.Contains(ext);
                    FileInfo fi;
                    try { fi = new FileInfo(file); }
                    catch { continue; }

                    FileEntry fe = new FileEntry
                    {
                        fullPath = file,
                        assetPath = ToRelative(root, file).Replace("\\\\", "/"),
                        fileName = fi.Name,
                        ext = ext,
                        category = ClassifyCat(ext),
                        isText = isText,
                        sizeBytes = fi.Length
                    };
                    found.Add(fe);
                }
            }

            allFiles = found;
            scanDone = true;
            scanRunning = false;
            Repaint();
        }

        // ── UI ─────────────────────────────────────────────────────────────
        void InitStyles()
        {
            if (stylesInit) return;
            stylesInit = true;

            sLog = new GUIStyle(EditorStyles.helpBox) { wordWrap = true, fontSize = 11 };
            sLog.normal.textColor = new Color(0.85f, 0.92f, 0.98f);

            sCmd = new GUIStyle(EditorStyles.miniLabel);
            sCmd.normal.textColor = new Color(0.6f, 0.9f, 0.6f);

            sBg = new GUIStyle(GUI.skin.box);
            sBg.normal.background = MakeTex(new Color(0.08f, 0.10f, 0.13f));

            sHeader = new GUIStyle(EditorStyles.boldLabel);
            sHeader.fontSize = 13;

            sCode = new GUIStyle(EditorStyles.helpBox) { wordWrap = false, fontSize = 10 };
            sCode.normal.textColor = new Color(0.7f, 0.95f, 0.7f);
        }

        void OnGUI()
        {
            InitStyles();

            // ── Header ────────────────────────────────────────────────────
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("🤖 AliTerra AI v11", EditorStyles.boldLabel, GUILayout.Width(140));
            GUILayout.FlexibleSpace();

            // Sync indicator
            Color old = GUI.color;
            bool synced = lastSyncTime > 0;
            GUI.color = synced ? Color.cyan : Color.gray;
            GUILayout.Label(synced ? "⚡ " + syncFileCount + " files" : "⚡ no sync", EditorStyles.miniLabel);
            GUI.color = old;

            // Polling toggle
            GUI.color = polling ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button(polling ? "● POLL ON" : "○ POLL OFF", EditorStyles.toolbarButton, GUILayout.Width(88)))
            {
                polling = !polling;
                EditorPrefs.SetBool(PREF_POLLING, polling);
                AddLog(polling ? "▶ Polling ON" : "■ Polling OFF");
            }
            GUI.color = old;

            // Pending badge
            if (pendingCmdCount > 0)
            {
                GUI.color = Color.yellow;
                GUILayout.Label("⏳ " + pendingCmdCount, EditorStyles.miniLabel, GUILayout.Width(40));
                GUI.color = old;
            }
            EditorGUILayout.EndHorizontal();

            // ── Tabs ──────────────────────────────────────────────────────
            int newTab = GUILayout.Toolbar(activeTab, new string[] { "🔄 Sync", "📁 Files", "📋 Log", "ℹ️ Info" });
            if (newTab != activeTab) { activeTab = newTab; EditorPrefs.SetInt(PREF_TAB, newTab); }

            switch (activeTab)
            {
                case 0: DrawSyncTab(); break;
                case 1: DrawFilesTab(); break;
                case 2: DrawLogTab(); break;
                case 3: DrawInfoTab(); break;
            }
        }

        void DrawSyncTab()
        {
            EditorGUILayout.Space(4);

            // Status box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📡 Connection", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Server: " + SERVER_URL, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Project: " + PROJECT_NAME, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Scene: " + (string.IsNullOrEmpty(ctxScene) ? "none" : ctxScene), EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Status: " + syncStatus, EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Sync buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔄 Sync Project", GUILayout.Height(36)))
                StartFullSync();
            if (GUILayout.Button("🧪 Test Connection", GUILayout.Height(36)))
                EditorCoroutine.Start(TestConnectionRoutine());
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            // Polling section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("⚡ Command Polling", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("When ON, plugin polls server every 3s and executes AI commands (write files, create objects, etc.)", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            bool newPoll = EditorGUILayout.ToggleLeft("", polling, GUILayout.Width(20));
            if (newPoll != polling)
            {
                polling = newPoll;
                EditorPrefs.SetBool(PREF_POLLING, polling);
            }
            Color old2 = GUI.color;
            GUI.color = polling ? Color.green : Color.gray;
            GUILayout.Label(polling ? "● ON — AI can write files & create objects" : "○ OFF", EditorStyles.miniLabel);
            GUI.color = old2;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Open browser button
            if (GUILayout.Button("🌐 Open AliTerra in Browser", GUILayout.Height(32)))
                Application.OpenURL(SERVER_URL);
        }

        void DrawFilesTab()
        {
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            if (!scanRunning)
            {
                if (GUILayout.Button(scanDone ? "🔄 Rescan" : "🔍 Scan Project", GUILayout.Height(30)))
                    StartProjectScan();
            }
            else
            {
                EditorGUILayout.HelpBox("Scanning: " + scanProgress + "/" + scanTotal, MessageType.Info);
            }
            EditorGUILayout.EndHorizontal();

            if (!scanDone) return;

            EditorGUILayout.LabelField("Files: " + allFiles.Count, EditorStyles.miniLabel);
            fileFilter = EditorGUILayout.TextField("🔍", fileFilter);
            fileTypeIdx = GUILayout.Toolbar(fileTypeIdx, fileTypeLabels);

            FileCategory? filterCat = null;
            if (fileTypeIdx == 1) filterCat = FileCategory.Script;
            if (fileTypeIdx == 2) filterCat = FileCategory.Scene;
            if (fileTypeIdx == 3) filterCat = FileCategory.Prefab;
            if (fileTypeIdx == 4) filterCat = FileCategory.Shader;
            if (fileTypeIdx == 5) filterCat = FileCategory.Other;

            fileScroll = EditorGUILayout.BeginScrollView(fileScroll, GUILayout.Height(200));
            foreach (FileEntry fe in allFiles)
            {
                if (filterCat.HasValue && fe.category != filterCat.Value) continue;
                if (!string.IsNullOrEmpty(fileFilter) && !fe.fileName.ToLowerInvariant().Contains(fileFilter.ToLowerInvariant())) continue;

                EditorGUILayout.BeginHorizontal();
                string icon = fe.category == FileCategory.Script ? "📜"
                            : fe.category == FileCategory.Scene ? "🎬"
                            : fe.category == FileCategory.Prefab ? "🧊"
                            : fe.category == FileCategory.Shader ? "✨" : "📄";
                GUILayout.Label(icon + " " + fe.fileName, GUILayout.ExpandWidth(true));
                if (fe.isText && GUILayout.Button("👁", EditorStyles.miniButton, GUILayout.Width(24)))
                {
                    viewFile = fe;
                    try { viewContent = File.ReadAllText(fe.fullPath, Encoding.UTF8); } catch { viewContent = "Error reading file"; }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            if (viewFile != null)
            {
                EditorGUILayout.LabelField("📄 " + viewFile.fileName, EditorStyles.boldLabel);
                viewScroll = EditorGUILayout.BeginScrollView(viewScroll, GUILayout.ExpandHeight(true));
                string disp = viewContent.Length > 8000 ? viewContent.Substring(0, 8000) + "\\n...(truncated)" : viewContent;
                GUILayout.Label(disp, sCode);
                EditorGUILayout.EndScrollView();
            }
        }

        void DrawLogTab()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("📋 Command Log (" + commandLog.Count + ")", EditorStyles.boldLabel);
            if (GUILayout.Button("🗑", EditorStyles.miniButton, GUILayout.Width(24))) commandLog.Clear();
            EditorGUILayout.EndHorizontal();

            cmdLogScroll = EditorGUILayout.BeginScrollView(cmdLogScroll, GUILayout.ExpandHeight(true));
            for (int i = commandLog.Count - 1; i >= 0; i--)
                GUILayout.Label(commandLog[i], sCmd);
            EditorGUILayout.EndScrollView();
        }

        void DrawInfoTab()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🤖 AliTerra AI v11", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("This plugin connects your Unity project to the AliTerra AI server. The AI can:", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("• See ALL project files (scripts, scenes, prefabs, shaders)", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Read and write any file automatically", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Create GameObjects and add components", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Read console logs and fix errors automatically", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Execute editor commands (Play, Save, Refresh)", EditorStyles.miniLabel);
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("HOW TO USE:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("1. Click 'Sync Project' on the Sync tab", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("2. Enable 'POLL ON' button", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("3. Open browser: " + SERVER_URL, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("4. Chat with AI — it will control your Unity!", EditorStyles.miniLabel);
            EditorGUILayout.Space(8);
            if (GUILayout.Button("🌐 Open in Browser"))
                Application.OpenURL(SERVER_URL);
            EditorGUILayout.EndVertical();
        }

        // ── HTTP helpers ──────────────────────────────────────────────────
        IEnumerator TestConnectionRoutine()
        {
            syncStatus = "⏳ Testing...";
            Repaint();
            yield return GetRoutine(SERVER_URL + "/api/health", (ok, text) =>
            {
                if (ok && text.Contains("ok"))
                {
                    syncStatus = "✅ Connected!";
                    AddLog("✅ Server OK: " + SERVER_URL);
                }
                else
                {
                    syncStatus = "❌ Cannot connect";
                    AddLog("❌ Connection failed: " + text);
                }
                Repaint();
            });
        }

        void SendPost(string url, string json, Action<bool, string> callback)
        {
            EditorCoroutine.Start(PostRoutine(url, json, callback));
        }

        IEnumerator PostRoutine(string url, string json, Action<bool, string> callback)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            UnityWebRequest req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("X-Api-Key", API_KEY);
            req.timeout = 60;
            yield return req.SendWebRequest();
            bool ok = req.result == UnityWebRequest.Result.Success;
            string text = ok ? req.downloadHandler.text : req.error;
            callback?.Invoke(ok, text);
        }

        IEnumerator GetRoutine(string url, Action<bool, string> callback)
        {
            UnityWebRequest req = UnityWebRequest.Get(url);
            req.SetRequestHeader("X-Api-Key", API_KEY);
            req.timeout = 30;
            yield return req.SendWebRequest();
            bool ok = req.result == UnityWebRequest.Result.Success;
            string text = ok ? req.downloadHandler.text : req.error;
            callback?.Invoke(ok, text);
        }

        // ── Utilities ─────────────────────────────────────────────────────
        string GetProjectRoot() => Path.GetDirectoryName(Application.dataPath) ?? "";

        string ToRelative(string root, string full)
        {
            if (full.StartsWith(root))
                return full.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, '/');
            return full;
        }

        IEnumerable<string> WalkDirectory(string dir)
        {
            foreach (string d in Directory.GetDirectories(dir))
            {
                if (ExcludedDirs.Contains(Path.GetFileName(d))) continue;
                foreach (string f in WalkDirectory(d)) yield return f;
            }
            foreach (string f in Directory.GetFiles(dir))
                yield return f;
        }

        string ClassifyExt(string ext)
        {
            switch (ext)
            {
                case ".cs": return "script";
                case ".unity": return "scene";
                case ".prefab": return "prefab";
                case ".mat": return "material";
                case ".shader": case ".compute": case ".hlsl": case ".cginc": return "shader";
                case ".json": case ".yaml": case ".yml": case ".xml": case ".txt": return "config";
                case ".anim": return "animation";
                case ".controller": return "animator";
                case ".asset": return "asset";
                default: return "other";
            }
        }

        FileCategory ClassifyCat(string ext)
        {
            switch (ext)
            {
                case ".cs": return FileCategory.Script;
                case ".unity": return FileCategory.Scene;
                case ".prefab": return FileCategory.Prefab;
                case ".shader": case ".compute": return FileCategory.Shader;
                case ".mat": return FileCategory.Material;
                default: return FileCategory.Other;
            }
        }

        Type GetTypeByName(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = assembly.GetType(name);
                if (t != null) return t;
                t = assembly.GetType("UnityEngine." + name);
                if (t != null) return t;
                t = assembly.GetType("UnityEngine.AI." + name);
                if (t != null) return t;
            }
            return null;
        }

        string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\\\", "\\\\\\\\")
                    .Replace("\\"", "\\\\\\"")
                    .Replace("\\n", "\\\\n")
                    .Replace("\\r", "\\\\r")
                    .Replace("\\t", "\\\\t")
                    .Replace("\\0", "");
        }

        static Texture2D MakeTex(Color c)
        {
            Texture2D t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EditorCoroutine — lightweight coroutine for Editor scripts
    // ═══════════════════════════════════════════════════════════════════════
    public static class EditorCoroutine
    {
        public static void Start(IEnumerator routine)
        {
            new CoroutineRunner(routine);
        }

        private class CoroutineRunner
        {
            private IEnumerator _routine;

            public CoroutineRunner(IEnumerator routine)
            {
                _routine = routine;
                EditorApplication.update += Update;
            }

            void Update()
            {
                try
                {
                    if (!Step(_routine))
                        EditorApplication.update -= Update;
                }
                catch (Exception ex)
                {
                    Debug.LogError("[AliTerra] Coroutine error: " + ex.Message);
                    EditorApplication.update -= Update;
                }
            }

            bool Step(IEnumerator e)
            {
                if (e.Current is UnityWebRequestAsyncOperation op)
                    if (!op.isDone) return true;

                if (!e.MoveNext()) return false;

                if (e.Current is IEnumerator nested)
                    return StepNested(e, nested);

                return true;
            }

            bool StepNested(IEnumerator outer, IEnumerator inner)
            {
                if (Step(inner)) return true;
                return outer.MoveNext();
            }
        }
    }
}`;
}
