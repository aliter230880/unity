// ╔═══════════════════════════════════════════════════════════════════════╗
// ║   AliTerra AI Coder  v7  —  Fullstack Unity Developer                ║
// ║   Установка: Assets/Editor/AliTerraAI.cs                            ║
// ║   Меню: Window → AliTerra → AI Coder  (Ctrl+Shift+A)               ║
// ╚═══════════════════════════════════════════════════════════════════════╝
// v7: AI видит ВСЕ файлы проекта + исполняет команды напрямую через polling
// Плагин → синхронизирует файлы → AI читает через tools → команды в очередь → плагин исполняет

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
    // ── Data classes (C# 7.3 compatible) ────────────────────────────────

    [Serializable]
    public class SyncFile
    {
        public string path    = "";
        public string type    = "other";
        public long   size    = 0;
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
        public string id         = "";
        public string type       = "";
        public string path       = "";
        public string content    = "";
        public string name       = "";
        public string primitive  = "";
        public string components = "";
        public string position   = "";
        public string color      = "";
        public string parent     = "";
        public string command    = "";
        public string message    = "";
    }

    [Serializable]
    public class LogBuffer
    {
        public string logType   = "log";
        public string message   = "";
        public string stackTrace = "";
    }

    // ── File categories ──────────────────────────────────────────────────

    public enum FileCategory { Script, Scene, Prefab, Material, Shader, Config, Audio, Model, Image, Other }

    public class FileEntry
    {
        public string       fullPath  = "";
        public string       assetPath = "";
        public string       fileName  = "";
        public string       ext       = "";
        public FileCategory category;
        public bool         isText    = false;
        public long         sizeBytes = 0;
    }

    public class ScriptInfo
    {
        public string       path      = "";
        public string       className = "";
        public string       baseClass = "";
        public int          lineCount = 0;
        public List<string> methods   = new List<string>();
        public string       content   = "";
    }

    public class ChatMsg
    {
        public bool   isUser    = false;
        public string text      = "";
        public string code      = "";
        public bool   isPending = false;
        public double startTime = 0;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //   Main EditorWindow
    // ═══════════════════════════════════════════════════════════════════════

    public class AliTerraAICoder : EditorWindow
    {
        // ── Server ──────────────────────────────────────────────────────
        private const string DEFAULT_URL   = "__SERVER_URL__";
        private string       serverUrl     = "__SERVER_URL__";
        private const int    MAX_FILE_CHARS = 8000;
        private const int    MAX_REL_CHARS  = 2500;
        private const int    MAX_REL_COUNT  = 5;
        private const int    MAX_SYNC_BYTES = 350 * 1024; // 350 KB per file

        // ── EditorPrefs ─────────────────────────────────────────────────
        private const string PREF_SERVER_URL = "AliTerra_ServerUrl";
        private const string PREF_GH_TOKEN  = "AliTerra_GH_Token";
        private const string PREF_GH_OWNER  = "AliTerra_GH_Owner";
        private const string PREF_GH_REPO   = "AliTerra_GH_Repo";
        private const string PREF_GH_BRANCH = "AliTerra_GH_Branch";
        private const string PREF_AUTO      = "AliTerra_AutoApply";
        private const string PREF_POLLING   = "AliTerra_Polling";

        // ── UI ──────────────────────────────────────────────────────────
        private int    activeTab  = 0;
        private bool   isBusy    = false;
        private string statusMsg = "";

        // ── Chat ────────────────────────────────────────────────────────
        private List<ChatMsg> history      = new List<ChatMsg>();
        private string        userInput    = "";
        private Vector2       chatScroll;
        private int           pendingIndex = -1;
        private int           retryCount   = 0;
        private string        lastJson     = "";

        // ── Auto-apply ──────────────────────────────────────────────────
        private bool autoApply = false;

        // ── Context ─────────────────────────────────────────────────────
        private FileEntry selectedFile    = null;
        private string    fileContent     = "";
        private string    ctxScene        = "";
        private string    ctxObject       = "";
        private string    sceneHierarchy  = "";
        private string    lastScannedScene = "";

        // ── Project scan ────────────────────────────────────────────────
        private List<FileEntry>  allFiles       = new List<FileEntry>();
        private List<ScriptInfo> scriptIndex    = new List<ScriptInfo>();
        private string           projectSummary = "";
        private bool             scanRunning    = false;
        private bool             scanDone       = false;
        private int              scanProgress   = 0;
        private int              scanTotal      = 0;

        // ── GitHub ──────────────────────────────────────────────────────
        private string ghToken  = "";
        private string ghOwner  = "";
        private string ghRepo   = "";
        private string ghBranch = "main";
        private bool   showGh   = false;
        private string ghStatus = "";

        // ── File browser ────────────────────────────────────────────────
        private Vector2  fileScroll;
        private string   fileFilter   = "";
        private FileEntry viewFile    = null;
        private Vector2  viewScroll;
        private string   viewContent  = "";
        private string[] fileTypeOpts = new string[] { "All", "Scripts", "Shaders", "Scenes", "Prefabs", "Other" };
        private int      fileTypeIdx  = 0;

        // ── Debug ───────────────────────────────────────────────────────
        private string  lastJsonSent     = "";
        private string  lastJsonReceived = "";
        private Vector2 debugScroll;
        private Vector2 codeScroll;

        // ── v7: Fullstack polling & sync ────────────────────────────────
        private bool   polling        = false;
        private bool   syncBusy       = false;
        private string syncStatus     = "Не синхронизировано";
        private int    syncFileCount  = 0;
        private double lastSyncTime   = -1;
        private double lastPollTime   = -1;
        private double lastLogSend    = -1;
        private int    pendingCmds    = 0;
        private const double POLL_INTERVAL    = 3.0;
        private const double LOG_SEND_INTERVAL = 5.0;

        private List<string>    commandLog    = new List<string>();
        private Vector2         commandLogScroll;

        private List<LogBuffer> pendingLogs   = new List<LogBuffer>();
        private bool            logListening  = false;

        // ── Styles ──────────────────────────────────────────────────────
        private GUIStyle sUser, sAI, sBg, sCode, sCmd;
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
            ".compute", ".hlsl", ".cginc", ".uss", ".uxml",
            ".inputactions", ".csproj", ".sln"
        };

        // ── Menu ────────────────────────────────────────────────────────
        [MenuItem("Window/AliTerra/AI Coder %#a")]
        public static void Open()
        {
            AliTerraAICoder w = GetWindow<AliTerraAICoder>("🤖 AliTerra AI");
            w.minSize = new Vector2(420, 560);
        }

        // ── Lifecycle ───────────────────────────────────────────────────
        void OnEnable()
        {
            // Load server URL — prefer saved value, fall back to baked-in default
            string saved = EditorPrefs.GetString(PREF_SERVER_URL, "");
            if (!string.IsNullOrEmpty(saved))
                serverUrl = saved;
            else if (DEFAULT_URL != "__SERVER_URL__")
                serverUrl = DEFAULT_URL;
            // else: user needs to type it in the Fullstack tab

            autoApply = EditorPrefs.GetBool(PREF_AUTO, false);
            polling   = EditorPrefs.GetBool(PREF_POLLING, false);
            ghToken   = EditorPrefs.GetString(PREF_GH_TOKEN, "");
            ghOwner   = EditorPrefs.GetString(PREF_GH_OWNER, "");
            ghRepo    = EditorPrefs.GetString(PREF_GH_REPO, "");
            ghBranch  = EditorPrefs.GetString(PREF_GH_BRANCH, "main");

            EditorApplication.update += Tick;

            if (!logListening)
            {
                Application.logMessageReceived += OnLogMessage;
                logListening = true;
            }

            if (history.Count == 0)
            {
                history.Add(new ChatMsg
                {
                    text = "Привет! Я AliTerra AI v7 — fullstack Unity-разработчик.\n\n" +
                           "🔄 Нажми «Синхронизировать» во вкладке Fullstack чтобы я увидел ВСЕ файлы проекта.\n\n" +
                           "После синхронизации я смогу:\n" +
                           "• Читать любой файл проекта\n" +
                           "• Создавать и редактировать скрипты\n" +
                           "• Создавать объекты в сцене\n" +
                           "• Проверять ошибки компиляции\n\n" +
                           "Выбери .cs файл в Project или GameObject в Hierarchy для контекста."
                });
            }

            EditorCoroutine.Start(PushStateRoutine());
        }

        void OnDisable()
        {
            EditorPrefs.SetString(PREF_SERVER_URL, serverUrl);
            EditorPrefs.SetBool(PREF_AUTO, autoApply);
            EditorPrefs.SetBool(PREF_POLLING, polling);
            EditorPrefs.SetString(PREF_GH_TOKEN, ghToken);
            EditorPrefs.SetString(PREF_GH_OWNER, ghOwner);
            EditorPrefs.SetString(PREF_GH_REPO, ghRepo);
            EditorPrefs.SetString(PREF_GH_BRANCH, ghBranch);

            EditorApplication.update -= Tick;

            if (logListening)
            {
                Application.logMessageReceived -= OnLogMessage;
                logListening = false;
            }
        }

        // ── Tick ────────────────────────────────────────────────────────
        void Tick()
        {
            CaptureContext();

            double t = EditorApplication.timeSinceStartup;

            // Command polling
            if (polling && !syncBusy && t - lastPollTime > POLL_INTERVAL)
            {
                lastPollTime = t;
                EditorCoroutine.Start(PollCommandsRoutine());
            }

            // Log flushing
            if (pendingLogs.Count > 0 && t - lastLogSend > LOG_SEND_INTERVAL)
            {
                lastLogSend = t;
                FlushLogs();
            }

            // Push state every 3s
            if (t - lastPollTime > 3.0 || lastPollTime < 0)
            {
                // already handled above or first run
            }
        }

        // ── Console log capture ─────────────────────────────────────────
        void OnLogMessage(string msg, string stackTrace, LogType type)
        {
            string logTypeStr;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                logTypeStr = "error";
            else if (type == LogType.Warning)
                logTypeStr = "warning";
            else
                logTypeStr = "log";

            LogBuffer lb = new LogBuffer();
            lb.logType    = logTypeStr;
            lb.message    = msg;
            lb.stackTrace = stackTrace;
            pendingLogs.Add(lb);

            if (pendingLogs.Count > 200)
                pendingLogs.RemoveAt(0);
        }

        void FlushLogs()
        {
            if (pendingLogs.Count == 0) return;

            List<LogBuffer> batch = new List<LogBuffer>(pendingLogs);
            pendingLogs.Clear();

            StringBuilder sb = new StringBuilder();
            sb.Append("{\"logs\":[");
            for (int i = 0; i < batch.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append("{\"logType\":\"");
                sb.Append(EscapeJson(batch[i].logType));
                sb.Append("\",\"message\":\"");
                sb.Append(EscapeJson(batch[i].message));
                sb.Append("\",\"stackTrace\":\"");
                sb.Append(EscapeJson(batch[i].stackTrace ?? ""));
                sb.Append("\"}");
            }
            sb.Append("]}");

            string json = sb.ToString();
            SendPost(serverUrl + "/api/unity/logs", json, null);
        }

        // ── Context capture ─────────────────────────────────────────────
        void CaptureContext()
        {
            ctxScene = SceneManager.GetActiveScene().name;

            if (ctxScene != lastScannedScene && !string.IsNullOrEmpty(ctxScene))
            {
                lastScannedScene = ctxScene;
                BuildSceneHierarchy();
            }

            GameObject go = Selection.activeGameObject;
            if (go != null)
            {
                ctxObject = go.name;
                if (selectedFile == null)
                {
                    MonoBehaviour mb = go.GetComponent<MonoBehaviour>();
                    if (mb != null)
                    {
                        MonoScript ms2 = MonoScript.FromMonoBehaviour(mb);
                        if (ms2 != null) ReadSelectedScript(AssetDatabase.GetAssetPath(ms2));
                    }
                }
            }
            else
            {
                ctxObject = "";
            }

            if (Selection.activeObject is MonoScript ms)
            {
                string path = AssetDatabase.GetAssetPath(ms);
                if (selectedFile == null || selectedFile.assetPath != path)
                    ReadSelectedScript(path);
            }
        }

        void ReadSelectedScript(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (selectedFile != null && selectedFile.assetPath == path) return;

            FileInfo fi = new FileInfo(path);
            if (!fi.Exists) return;

            selectedFile = new FileEntry();
            selectedFile.assetPath  = path;
            selectedFile.fullPath   = fi.FullName;
            selectedFile.fileName   = fi.Name;
            selectedFile.ext        = fi.Extension.ToLowerInvariant();
            selectedFile.sizeBytes  = fi.Length;
            selectedFile.isText     = true;
            selectedFile.category   = ClassifyExt(selectedFile.ext);

            try { fileContent = File.ReadAllText(path, Encoding.UTF8); }
            catch { fileContent = ""; }
        }

        // ── Scene hierarchy ─────────────────────────────────────────────
        void BuildSceneHierarchy()
        {
            try
            {
                Scene scene = EditorSceneManager.GetActiveScene();
                if (!scene.IsValid()) return;
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("[ИЕРАРХИЯ СЦЕНЫ: " + scene.name + "]");
                int count = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                    AppendHierarchy(sb, root, 0, ref count);
                sceneHierarchy = sb.ToString();
                EditorCoroutine.Start(PushStateRoutine());
            }
            catch { }
        }

        void AppendHierarchy(StringBuilder sb, GameObject go, int depth, ref int count)
        {
            if (count > 300) return;
            count++;
            sb.Append(new string(' ', depth * 2));
            sb.Append("- ");
            sb.Append(go.name);
            Component[] comps = go.GetComponents<Component>();
            List<string> names = new List<string>();
            foreach (Component c in comps)
                if (c != null && !(c is Transform)) names.Add(c.GetType().Name);
            if (names.Count > 0) { sb.Append(" ["); sb.Append(string.Join(", ", names.ToArray())); sb.Append("]"); }
            sb.AppendLine();
            for (int i = 0; i < go.transform.childCount; i++)
                AppendHierarchy(sb, go.transform.GetChild(i).gameObject, depth + 1, ref count);
        }

        // ── Push state (legacy v6 compat) ───────────────────────────────
        IEnumerator PushStateRoutine()
        {
            if (string.IsNullOrEmpty(serverUrl) || serverUrl == "__SERVER_URL__") yield break;
            string json = BuildStateJson();
            yield return SendPostRoutine(serverUrl + "/api/unity/push", json, null);
        }

        string BuildStateJson()
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            d["scene"]           = ctxScene;
            d["selectedObject"]  = ctxObject;
            d["sceneHierarchy"]  = sceneHierarchy.Length > 6000 ? sceneHierarchy.Substring(0, 6000) : sceneHierarchy;
            return BuildFlatJson(d);
        }

        // ── v7: Full file sync ──────────────────────────────────────────
        void StartFullSync()
        {
            if (syncBusy) return;
            syncBusy   = true;
            syncStatus = "Сканирование файлов...";
            Repaint();
            EditorCoroutine.Start(SyncRoutine());
        }

        IEnumerator SyncRoutine()
        {
            string root = GetProjectRoot();
            List<SyncFile> files = new List<SyncFile>();

            string[] scanDirs = new string[] { "Assets", "Packages", "ProjectSettings" };
            int scanned = 0;

            foreach (string dirName in scanDirs)
            {
                string dirPath = Path.Combine(root, dirName);
                if (!Directory.Exists(dirPath)) continue;

                foreach (string file in WalkDirectory(dirPath))
                {
                    string rel = ToRelative(root, file);
                    if (string.IsNullOrEmpty(rel)) continue;

                    FileInfo fi;
                    try { fi = new FileInfo(file); }
                    catch { continue; }

                    string ext     = fi.Extension.ToLowerInvariant();
                    bool   isText  = TextExtensions.Contains(ext);
                    string content = "";

                    if (isText && fi.Length <= MAX_SYNC_BYTES)
                    {
                        try { content = File.ReadAllText(file, Encoding.UTF8); }
                        catch { content = ""; }
                    }

                    SyncFile sf = new SyncFile();
                    sf.path    = rel.Replace("\\", "/");
                    sf.type    = ClassifyPath(rel);
                    sf.size    = fi.Length;
                    sf.content = content;
                    files.Add(sf);
                    scanned++;

                    if (scanned % 50 == 0)
                    {
                        syncStatus = "Сканирование: " + scanned + " файлов...";
                        Repaint();
                        yield return null;
                    }
                }
            }

            syncStatus     = "Отправка " + files.Count + " файлов на сервер...";
            syncFileCount  = files.Count;
            Repaint();

            // Build JSON in chunks (avoid huge single string)
            string payload = BuildSyncJson(files);
            syncStatus = "Загрузка " + files.Count + " файлов (" + (payload.Length / 1024) + " KB)...";
            Repaint();

            yield return SendPostRoutine(serverUrl + "/api/unity/sync", payload, (ok, text) =>
            {
                syncBusy  = false;
                if (ok)
                {
                    syncStatus  = "✅ Синхронизировано " + files.Count + " файлов";
                    lastSyncTime = EditorApplication.timeSinceStartup;
                    AddCommandLog("Синхронизировано " + files.Count + " файлов");
                }
                else
                {
                    syncStatus = "❌ Ошибка: " + text;
                }
                Repaint();
            });
        }

        string BuildSyncJson(List<SyncFile> files)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"projectName\":\"");
            sb.Append(EscapeJson(Path.GetFileName(GetProjectRoot())));
            sb.Append("\",\"unityVersion\":\"");
            sb.Append(EscapeJson(Application.unityVersion));
            sb.Append("\",\"scene\":\"");
            sb.Append(EscapeJson(ctxScene));
            sb.Append("\",\"hierarchy\":\"");
            sb.Append(EscapeJson(sceneHierarchy.Length > 4000 ? sceneHierarchy.Substring(0, 4000) : sceneHierarchy));
            sb.Append("\",\"files\":[");
            for (int i = 0; i < files.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append("{\"path\":\"");
                sb.Append(EscapeJson(files[i].path));
                sb.Append("\",\"type\":\"");
                sb.Append(EscapeJson(files[i].type));
                sb.Append("\",\"size\":");
                sb.Append(files[i].size);
                sb.Append(",\"content\":\"");
                sb.Append(EscapeJson(files[i].content));
                sb.Append("\"}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        // ── Connection test ─────────────────────────────────────────────
        IEnumerator TestConnectionRoutine()
        {
            syncStatus = "⏳ Проверка соединения...";
            Repaint();
            yield return GetRoutine(serverUrl + "/api/unity/state", (ok, text) =>
            {
                if (ok)
                {
                    syncStatus = "✅ Соединение успешно!";
                    AddCommandLog("✅ Тест: " + serverUrl + " — OK");
                }
                else
                {
                    syncStatus = "❌ Нет соединения — проверь URL";
                    AddCommandLog("❌ Тест соединения провалился: " + serverUrl);
                }
                Repaint();
            });
        }

        // ── v7: Command polling ─────────────────────────────────────────
        IEnumerator PollCommandsRoutine()
        {
            yield return GetRoutine(serverUrl + "/api/unity/commands", (ok, text) =>
            {
                if (!ok || string.IsNullOrEmpty(text)) return;
                ExecuteCommandJson(text);
                Repaint();
            });
        }

        void ExecuteCommandJson(string json)
        {
            CommandResponse response;
            try { response = JsonUtility.FromJson<CommandResponse>(json); }
            catch { return; }

            if (response == null || response.commands == null || response.commands.Length == 0) return;

            foreach (CommandItem cmd in response.commands)
            {
                bool   ok     = false;
                string result = "";
                try
                {
                    ok = ExecuteCommand(cmd, out result);
                }
                catch (Exception ex)
                {
                    ok     = false;
                    result = ex.Message;
                }

                string label = (ok ? "✅ " : "❌ ") + cmd.type;
                if (!string.IsNullOrEmpty(result)) label += ": " + result.Substring(0, Math.Min(60, result.Length));
                AddCommandLog(label);

                ReportCommandComplete(cmd.id, ok, result);
            }

            AssetDatabase.Refresh();
        }

        bool ExecuteCommand(CommandItem cmd, out string result)
        {
            result = "";

            switch (cmd.type)
            {
                case "write_file":
                    return ExecuteWriteFile(cmd.path, cmd.content, out result);

                case "create_gameobject":
                    return ExecuteCreateGameObject(cmd, out result);

                case "add_component":
                    return ExecuteAddComponent(cmd.name, cmd.components, out result);

                case "execute_editor_command":
                    return ExecuteEditorCommand(cmd.command, cmd.message, out result);

                default:
                    result = "Неизвестная команда: " + cmd.type;
                    return false;
            }
        }

        bool ExecuteWriteFile(string path, string content, out string result)
        {
            result = "";
            if (string.IsNullOrEmpty(path)) { result = "path пустой"; return false; }
            if (content == null)            { result = "content null"; return false; }

            string fullPath = Path.Combine(GetProjectRoot(), path.Replace("/", Path.DirectorySeparatorChar.ToString()));
            string dir      = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Backup existing file
            if (File.Exists(fullPath))
            {
                string bak = fullPath + ".bak";
                File.Copy(fullPath, bak, true);
            }

            File.WriteAllText(fullPath, content, Encoding.UTF8);

            string assetPath = path.StartsWith("Assets/") ? path : "Assets/" + path;
            AssetDatabase.ImportAsset(assetPath.Replace("\\", "/"));

            result = "Записан: " + path;
            return true;
        }

        bool ExecuteCreateGameObject(CommandItem cmd, out string result)
        {
            result = "";
            string goName = cmd.name;
            if (string.IsNullOrEmpty(goName)) { result = "name пустой"; return false; }

            GameObject go;

            switch (cmd.primitive.ToLowerInvariant())
            {
                case "cube":     go = GameObject.CreatePrimitive(PrimitiveType.Cube);     break;
                case "sphere":   go = GameObject.CreatePrimitive(PrimitiveType.Sphere);   break;
                case "capsule":  go = GameObject.CreatePrimitive(PrimitiveType.Capsule);  break;
                case "cylinder": go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); break;
                case "plane":    go = GameObject.CreatePrimitive(PrimitiveType.Plane);    break;
                case "quad":     go = GameObject.CreatePrimitive(PrimitiveType.Quad);     break;
                default:         go = new GameObject();                                    break;
            }

            go.name = goName;

            // Position
            if (!string.IsNullOrEmpty(cmd.position))
            {
                float x = ParseVecField(cmd.position, "x");
                float y = ParseVecField(cmd.position, "y");
                float z = ParseVecField(cmd.position, "z");
                go.transform.position = new Vector3(x, y, z);
            }

            // Color
            if (!string.IsNullOrEmpty(cmd.color))
                ApplyColor(go, cmd.color);

            // Parent
            if (!string.IsNullOrEmpty(cmd.parent))
            {
                GameObject parent = GameObject.Find(cmd.parent);
                if (parent != null) go.transform.SetParent(parent.transform);
            }

            // Components
            if (!string.IsNullOrEmpty(cmd.components))
            {
                foreach (string compName in cmd.components.Split(','))
                {
                    string trimmed = compName.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;
                    Type compType = FindType(trimmed);
                    if (compType != null && typeof(Component).IsAssignableFrom(compType))
                        go.AddComponent(compType);
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            result = "Создан: " + goName;
            return true;
        }

        bool ExecuteAddComponent(string targetName, string components, out string result)
        {
            result = "";
            GameObject go = GameObject.Find(targetName);
            if (go == null) { result = "Объект не найден: " + targetName; return false; }

            foreach (string compName in components.Split(','))
            {
                string trimmed = compName.Trim();
                Type compType  = FindType(trimmed);
                if (compType != null && typeof(Component).IsAssignableFrom(compType))
                    go.AddComponent(compType);
            }

            result = "Компоненты добавлены к: " + targetName;
            return true;
        }

        bool ExecuteEditorCommand(string command, string message, out string result)
        {
            result = "";
            switch (command)
            {
                case "refresh":
                case "compile":
                    AssetDatabase.Refresh();
                    result = "AssetDatabase.Refresh() выполнен";
                    return true;

                case "save_scene":
                    EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                    result = "Сцена сохранена";
                    return true;

                case "log_message":
                    Debug.Log("[AliTerra AI] " + (message ?? ""));
                    result = "Log отправлен";
                    return true;

                default:
                    result = "Неизвестная команда editor: " + command;
                    return false;
            }
        }

        void ReportCommandComplete(string id, bool success, string res)
        {
            if (string.IsNullOrEmpty(id)) return;
            string json = "{\"commandId\":\"" + EscapeJson(id) +
                          "\",\"success\":" + (success ? "true" : "false") +
                          ",\"result\":\"" + EscapeJson(res) + "\"}";
            SendPost(serverUrl + "/api/unity/commands", json, null);
        }

        void AddCommandLog(string entry)
        {
            string ts = DateTime.Now.ToString("HH:mm:ss");
            commandLog.Insert(0, "[" + ts + "] " + entry);
            if (commandLog.Count > 100) commandLog.RemoveAt(commandLog.Count - 1);
        }

        // ── Helper: color ───────────────────────────────────────────────
        void ApplyColor(GameObject go, string color)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r == null) return;

            Color c = Color.white;
            string lower = color.ToLowerInvariant();

            if      (lower == "red")    c = Color.red;
            else if (lower == "green")  c = Color.green;
            else if (lower == "blue")   c = Color.blue;
            else if (lower == "yellow") c = Color.yellow;
            else if (lower == "black")  c = Color.black;
            else if (lower == "white")  c = Color.white;
            else if (lower == "cyan")   c = Color.cyan;
            else if (lower == "magenta") c = Color.magenta;
            else if (lower == "orange") c = new Color(1f, 0.5f, 0f);
            else if (lower == "purple") c = new Color(0.5f, 0f, 1f);
            else ColorUtility.TryParseHtmlString(color, out c);

            Material mat = new Material(r.sharedMaterial != null ? r.sharedMaterial : Shader.Find("Standard") != null ? new Material(Shader.Find("Standard")) : r.sharedMaterial);
            mat.color = c;
            r.sharedMaterial = mat;
        }

        float ParseVecField(string json, string field)
        {
            Match m = Regex.Match(json, "\"" + field + "\"\\s*:\\s*([\\-0-9\\.]+)");
            if (!m.Success) return 0f;
            float val;
            if (float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out val))
                return val;
            return 0f;
        }

        Type FindType(string name)
        {
            foreach (System.Reflection.Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = asm.GetType(name);
                if (t != null) return t;
                try
                {
                    foreach (Type tt in asm.GetTypes())
                        if (tt.Name == name) return tt;
                }
                catch { }
            }
            return null;
        }

        // ── File walking ────────────────────────────────────────────────
        static string GetProjectRoot()
        {
            string assetsPath = Application.dataPath;
            return Directory.GetParent(assetsPath).FullName;
        }

        IEnumerable<string> WalkDirectory(string dir)
        {
            string name = Path.GetFileName(dir);
            if (ExcludedDirs.Contains(name)) yield break;

            string[] files = new string[0];
            try { files = Directory.GetFiles(dir); } catch { }
            foreach (string file in files)
            {
                string fn = Path.GetFileName(file);
                if (fn.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                yield return file;
            }

            string[] dirs = new string[0];
            try { dirs = Directory.GetDirectories(dir); } catch { }
            foreach (string child in dirs)
                foreach (string f in WalkDirectory(child))
                    yield return f;
        }

        static string ToRelative(string root, string full)
        {
            if (full.StartsWith(root))
            {
                string rel = full.Substring(root.Length);
                if (rel.StartsWith(Path.DirectorySeparatorChar.ToString()) || rel.StartsWith(Path.AltDirectorySeparatorChar.ToString()))
                    rel = rel.Substring(1);
                return rel;
            }
            return full;
        }

        static string ClassifyPath(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".cs")         return "script";
            if (ext == ".shader" || ext == ".hlsl" || ext == ".cginc" || ext == ".compute") return "shader";
            if (ext == ".unity")      return "scene";
            if (ext == ".prefab")     return "prefab";
            if (ext == ".mat" || ext == ".asset" || ext == ".anim" || ext == ".controller") return "material";
            if (ext == ".json" || ext == ".xml" || ext == ".yaml" || ext == ".yml" || ext == ".asmdef") return "config";
            return "other";
        }

        static FileCategory ClassifyExt(string ext)
        {
            if (ext == ".cs")    return FileCategory.Script;
            if (ext == ".unity") return FileCategory.Scene;
            if (ext == ".prefab") return FileCategory.Prefab;
            if (ext == ".mat")   return FileCategory.Material;
            if (ext == ".shader" || ext == ".hlsl" || ext == ".cginc") return FileCategory.Shader;
            if (ext == ".json" || ext == ".xml" || ext == ".asmdef") return FileCategory.Config;
            return FileCategory.Other;
        }

        // ── Chat ────────────────────────────────────────────────────────
        void SendMessage()
        {
            if (string.IsNullOrEmpty(userInput.Trim()) || isBusy) return;
            isBusy = true;

            string input = userInput.Trim();
            userInput = "";
            GUI.FocusControl("");

            history.Add(new ChatMsg { isUser = true, text = input });

            ChatMsg pending = new ChatMsg { isPending = true, text = "Думаю", startTime = EditorApplication.timeSinceStartup };
            history.Add(pending);
            pendingIndex = history.Count - 1;
            retryCount   = 0;

            lastJson = BuildChatJson(input);
            Repaint();

            EditorCoroutine.Start(SendChatRoutine(lastJson));
        }

        string BuildChatJson(string lastUserMsg)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"messages\":[");

            List<ChatMsg> toSend = new List<ChatMsg>();
            for (int i = 0; i < history.Count - 1; i++) // skip pending
            {
                ChatMsg m = history[i];
                if (!m.isPending) toSend.Add(m);
            }
            if (toSend.Count > 20) toSend = toSend.GetRange(toSend.Count - 20, 20);

            bool first = true;
            foreach (ChatMsg m in toSend)
            {
                if (!first) sb.Append(",");
                sb.Append("{\"role\":\"");
                sb.Append(m.isUser ? "user" : "assistant");
                sb.Append("\",\"content\":\"");
                sb.Append(EscapeJson(m.text));
                sb.Append("\"}");
                first = false;
            }

            sb.Append("],\"context\":{");
            sb.Append("\"scene\":\"");       sb.Append(EscapeJson(ctxScene));
            sb.Append("\",\"selectedObject\":\""); sb.Append(EscapeJson(ctxObject));
            sb.Append("\",\"autoApplyMode\":"); sb.Append(autoApply ? "true" : "false");

            if (!string.IsNullOrEmpty(sceneHierarchy))
            {
                sb.Append(",\"sceneHierarchy\":\"");
                string h = sceneHierarchy.Length > 8000 ? sceneHierarchy.Substring(0, 8000) : sceneHierarchy;
                sb.Append(EscapeJson(h));
                sb.Append("\"");
            }

            if (selectedFile != null && !string.IsNullOrEmpty(fileContent))
            {
                sb.Append(",\"scriptName\":\"");  sb.Append(EscapeJson(selectedFile.fileName));
                sb.Append("\",\"scriptPath\":\""); sb.Append(EscapeJson(selectedFile.assetPath));
                sb.Append("\",\"fileType\":\"");   sb.Append(EscapeJson(selectedFile.ext));
                sb.Append("\",\"scriptContent\":\"");
                string fc = fileContent.Length > MAX_FILE_CHARS ? fileContent.Substring(0, MAX_FILE_CHARS) + "...(обрезано)" : fileContent;
                sb.Append(EscapeJson(fc));
                sb.Append("\"");
            }

            sb.Append("}}");
            return sb.ToString();
        }

        IEnumerator SendChatRoutine(string json)
        {
            bool success = false;
            string responseText = "";

            yield return SendPostRoutine(serverUrl + "/api/ai/chat", json, (ok, text) =>
            {
                success      = ok;
                responseText = text;
            });

            isBusy = false;

            if (!success)
            {
                if (retryCount < 2)
                {
                    retryCount++;
                    statusMsg = "Повтор " + retryCount + "/2...";
                    isBusy    = true;
                    yield return new WaitForSeconds(2f);
                    EditorCoroutine.Start(SendChatRoutine(lastJson));
                    yield break;
                }
                if (pendingIndex >= 0 && pendingIndex < history.Count)
                    history[pendingIndex] = new ChatMsg { text = "❌ Ошибка: " + responseText };
                pendingIndex = -1;
                statusMsg    = "";
                Repaint();
                yield break;
            }

            ParseChatResponse(responseText);
            pendingIndex = -1;
            retryCount   = 0;
            statusMsg    = "";
            Repaint();
        }

        void ParseChatResponse(string json)
        {
            string reply    = ExtractJsonStr(json, "reply");
            string code     = ExtractJsonStr(json, "code");
            string codeLang = ExtractJsonStr(json, "codeLang");
            bool   hasCode  = json.Contains("\"hasCode\":true");
            string sPath    = ExtractJsonStr(json, "scriptPath");
            int    toolCount = 0;
            Match  tcm      = Regex.Match(json, "\"toolCallCount\"\\s*:\\s*(\\d+)");
            if (tcm.Success) int.TryParse(tcm.Groups[1].Value, out toolCount);
            int    pending  = 0;
            Match  pcm      = Regex.Match(json, "\"pendingCommands\"\\s*:\\s*(\\d+)");
            if (pcm.Success) int.TryParse(pcm.Groups[1].Value, out pending);
            pendingCmds = pending;

            string display = reply;
            if (hasCode && !string.IsNullOrEmpty(code))
                display = Regex.Replace(display, @"```[\s\S]*?```", "[📜 код ниже]").Trim();

            string suffix = "";
            if (toolCount > 0)   suffix += "\n\n🔧 Использовано инструментов: " + toolCount;
            if (pending > 0)     suffix += "\n⏳ Команд в очереди: " + pending + " (включи polling)";

            if (pendingIndex >= 0 && pendingIndex < history.Count)
            {
                history[pendingIndex] = new ChatMsg
                {
                    text       = display + suffix,
                    code       = hasCode ? code : "",
                    isPending  = false,
                };
                if (hasCode && !string.IsNullOrEmpty(code) && autoApply && selectedFile != null)
                {
                    string applyPath = !string.IsNullOrEmpty(sPath) ? sPath : selectedFile.assetPath;
                    if (!string.IsNullOrEmpty(applyPath))
                    {
                        ApplyCode(code, applyPath);
                        history[pendingIndex].text += "\n\n⚡ Авто-применено к: " + applyPath;
                    }
                }
            }
        }

        void ApplyCode(string code, string path)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(code)) return;
            try
            {
                if (File.Exists(path)) File.Copy(path, path + ".bak", true);
                File.WriteAllText(path, code, Encoding.UTF8);
                AssetDatabase.ImportAsset(path);
                AssetDatabase.Refresh();
                fileContent = code;
                statusMsg   = "✅ Применено: " + Path.GetFileName(path);
                Debug.Log("[AliTerra AI] Applied: " + path);
            }
            catch (Exception ex)
            {
                statusMsg = "❌ " + ex.Message;
                Debug.LogError("[AliTerra AI] " + ex.Message);
            }
        }

        // ── HTTP helpers ────────────────────────────────────────────────
        IEnumerator SendPostRoutine(string url, string json, Action<bool, string> callback)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            UnityWebRequest req = new UnityWebRequest(url, "POST");
            req.uploadHandler   = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout         = 180;
            yield return req.SendWebRequest();
            bool ok = req.result == UnityWebRequest.Result.Success;
            callback?.Invoke(ok, ok ? req.downloadHandler.text : req.error);
        }

        IEnumerator GetRoutine(string url, Action<bool, string> callback)
        {
            UnityWebRequest req = new UnityWebRequest(url, "GET");
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout         = 30;
            yield return req.SendWebRequest();
            bool ok = req.result == UnityWebRequest.Result.Success;
            callback?.Invoke(ok, ok ? req.downloadHandler.text : req.error);
        }

        void SendPost(string url, string json, Action<bool, string> callback)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            UnityWebRequest req = new UnityWebRequest(url, "POST");
            req.uploadHandler   = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout         = 30;
            UnityWebRequestAsyncOperation op = req.SendWebRequest();
            if (callback != null)
                op.completed += _ => { bool ok = req.result == UnityWebRequest.Result.Success; callback(ok, ok ? req.downloadHandler.text : req.error); };
        }

        // ── JSON helpers ────────────────────────────────────────────────
        static string BuildFlatJson(Dictionary<string, string> d)
        {
            StringBuilder sb = new StringBuilder("{");
            bool first = true;
            foreach (KeyValuePair<string, string> kv in d)
            {
                if (!first) sb.Append(",");
                sb.Append("\""); sb.Append(EscapeJson(kv.Key)); sb.Append("\":\"");
                sb.Append(EscapeJson(kv.Value ?? "")); sb.Append("\"");
                first = false;
            }
            sb.Append("}");
            return sb.ToString();
        }

        static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        static string ExtractJsonStr(string json, string key)
        {
            Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:[^\\\\\"]|\\\\.)*)\"");
            if (!m.Success) return "";
            return m.Groups[1].Value
                .Replace("\\n", "\n").Replace("\\r", "\r")
                .Replace("\\t", "\t").Replace("\\\\", "\\")
                .Replace("\\\"", "\"");
        }

        // ── Project scan (existing, for file browser) ───────────────────
        void StartProjectScan()
        {
            if (scanRunning) return;
            scanRunning  = true;
            scanDone     = false;
            scanProgress = 0;
            scanTotal    = 0;
            allFiles.Clear();
            scriptIndex.Clear();
            projectSummary = "";
            EditorCoroutine.Start(ProjectScanRoutine());
        }

        IEnumerator ProjectScanRoutine()
        {
            string root   = GetProjectRoot();
            string assets = Path.Combine(root, "Assets");
            if (!Directory.Exists(assets)) { scanRunning = false; scanDone = true; yield break; }

            List<string> allPaths = new List<string>();
            foreach (string f in WalkDirectory(assets)) allPaths.Add(f);

            scanTotal = allPaths.Count;
            foreach (string path in allPaths)
            {
                scanProgress++;
                FileInfo fi = new FileInfo(path);
                string   rel = ToRelative(root, path);
                string   ext = fi.Extension.ToLowerInvariant();

                FileEntry fe = new FileEntry();
                fe.fullPath  = path;
                fe.assetPath = rel.Replace("\\", "/");
                fe.fileName  = fi.Name;
                fe.ext       = ext;
                fe.sizeBytes = fi.Length;
                fe.isText    = TextExtensions.Contains(ext);
                fe.category  = ClassifyExt(ext);
                allFiles.Add(fe);

                if (ext == ".cs" && fi.Length < 200 * 1024)
                {
                    ScriptInfo si = ParseScript(path);
                    if (si != null) scriptIndex.Add(si);
                }

                if (scanProgress % 100 == 0) { Repaint(); yield return null; }
            }

            BuildProjectSummary();
            scanRunning = false;
            scanDone    = true;
            Repaint();
        }

        ScriptInfo ParseScript(string path)
        {
            try
            {
                string text = File.ReadAllText(path, Encoding.UTF8);
                ScriptInfo si = new ScriptInfo();
                si.path       = path;
                si.lineCount  = text.Split('\n').Length;

                Match clsM = Regex.Match(text, @"class\s+(\w+)(?:\s*:\s*(\w[\w\.\,\s<>]*))?");
                si.className  = clsM.Success ? clsM.Groups[1].Value : Path.GetFileNameWithoutExtension(path);
                si.baseClass  = clsM.Success ? clsM.Groups[2].Value.Trim() : "";

                MatchCollection mm = Regex.Matches(text, @"(?:public|private|protected|internal|override|virtual|static)\s+[\w<>\[\]]+\s+(\w+)\s*\(");
                foreach (Match m in mm)
                    si.methods.Add(m.Groups[1].Value);

                si.content = text.Length > 2000 ? text.Substring(0, 2000) : text;
                return si;
            }
            catch { return null; }
        }

        void BuildProjectSummary()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[ИНДЕКС ПРОЕКТА: " + allFiles.Count + " файлов, " + scriptIndex.Count + " скриптов]");
            foreach (ScriptInfo si in scriptIndex)
            {
                sb.Append(si.className);
                if (!string.IsNullOrEmpty(si.baseClass)) { sb.Append(" : "); sb.Append(si.baseClass); }
                if (si.methods.Count > 0) { sb.Append(" ["); sb.Append(string.Join(", ", si.methods.ToArray())); sb.Append("]"); }
                sb.AppendLine();
            }
            projectSummary = sb.ToString();
        }

        // ── GitHub push ──────────────────────────────────────────────────
        void PushToGitHub(string filePath, string content)
        {
            if (string.IsNullOrEmpty(ghToken) || string.IsNullOrEmpty(ghOwner) || string.IsNullOrEmpty(ghRepo))
            {
                ghStatus = "❌ Заполни GitHub настройки";
                return;
            }
            EditorCoroutine.Start(GitHubPushRoutine(filePath, content));
        }

        IEnumerator GitHubPushRoutine(string filePath, string content)
        {
            ghStatus = "Получаю SHA...";
            Repaint();

            string apiBase = "https://api.github.com/repos/" + ghOwner + "/" + ghRepo + "/contents/";
            string getUrl  = apiBase + filePath;
            string sha     = "";

            UnityWebRequest getReq = UnityWebRequest.Get(getUrl);
            getReq.SetRequestHeader("Authorization", "token " + ghToken);
            getReq.SetRequestHeader("Accept", "application/vnd.github+json");
            getReq.SetRequestHeader("User-Agent", "AliTerraAI");
            yield return getReq.SendWebRequest();

            if (getReq.result == UnityWebRequest.Result.Success)
            {
                Match shaM = Regex.Match(getReq.downloadHandler.text, "\"sha\"\\s*:\\s*\"([^\"]+)\"");
                if (shaM.Success) sha = shaM.Groups[1].Value;
            }

            ghStatus = "Загружаю...";
            Repaint();

            string b64  = Convert.ToBase64String(Encoding.UTF8.GetBytes(content));
            string body = "{\"message\":\"Update " + filePath + " via AliTerra AI\",\"content\":\"" + b64 + "\"";
            if (!string.IsNullOrEmpty(sha)) body += ",\"sha\":\"" + sha + "\"";
            body += ",\"branch\":\"" + ghBranch + "\"}";

            byte[]          putBytes = Encoding.UTF8.GetBytes(body);
            UnityWebRequest putReq   = new UnityWebRequest(getUrl, "PUT");
            putReq.uploadHandler     = new UploadHandlerRaw(putBytes);
            putReq.downloadHandler   = new DownloadHandlerBuffer();
            putReq.SetRequestHeader("Content-Type",  "application/json");
            putReq.SetRequestHeader("Authorization", "token " + ghToken);
            putReq.SetRequestHeader("Accept",        "application/vnd.github+json");
            putReq.SetRequestHeader("User-Agent",    "AliTerraAI");
            yield return putReq.SendWebRequest();

            ghStatus = putReq.result == UnityWebRequest.Result.Success
                ? "✅ " + filePath + " → GitHub"
                : "❌ " + putReq.error;
            Repaint();
        }

        // ── Styles ──────────────────────────────────────────────────────
        void InitStyles()
        {
            if (stylesOk) return;
            stylesOk = true;

            sUser = new GUIStyle(GUI.skin.box) { wordWrap = true, alignment = TextAnchor.UpperLeft, padding = new RectOffset(8, 8, 6, 6), fontSize = 12 };
            sUser.normal.background = MakeTex(new Color(0.17f, 0.35f, 0.6f, 0.95f));
            sUser.normal.textColor  = Color.white;

            sAI = new GUIStyle(sUser);
            sAI.normal.background = MakeTex(new Color(0.12f, 0.15f, 0.18f, 1f));
            sAI.normal.textColor  = new Color(0.88f, 0.92f, 0.96f);

            sBg = new GUIStyle(GUI.skin.box);
            sBg.normal.background = MakeTex(new Color(0.08f, 0.10f, 0.13f, 1f));

            sCode = new GUIStyle(EditorStyles.helpBox) { wordWrap = false, fontSize = 10 };
            sCode.normal.textColor = new Color(0.7f, 0.9f, 0.7f);

            sCmd = new GUIStyle(EditorStyles.miniLabel);
            sCmd.normal.textColor = new Color(0.6f, 0.8f, 0.6f);
        }

        static Texture2D MakeTex(Color c) { Texture2D t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t; }

        // ── OnGUI ────────────────────────────────────────────────────────
        void OnGUI()
        {
            InitStyles();

            // ── Header ─────────────────────────────────────────────────
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("🤖 AliTerra AI v7", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            // Sync indicator
            Color old = GUI.color;
            bool  syncOk = lastSyncTime > 0 && syncFileCount > 0;
            GUI.color = syncOk ? Color.cyan : Color.gray;
            GUILayout.Label(syncOk ? "⚡ " + syncFileCount + " файлов" : "⚡ нет синхр.", EditorStyles.miniLabel);
            GUI.color = old;

            // Polling indicator
            GUI.color = polling ? Color.green : Color.gray;
            GUILayout.Label(polling ? "● polling" : "○ polling", EditorStyles.miniLabel);
            GUI.color = old;

            // Pending commands
            if (pendingCmds > 0)
            {
                GUI.color = Color.yellow;
                GUILayout.Label("⏳ " + pendingCmds, EditorStyles.miniLabel);
                GUI.color = old;
            }
            EditorGUILayout.EndHorizontal();

            // ── Tabs ───────────────────────────────────────────────────
            activeTab = GUILayout.Toolbar(activeTab, new string[] { "💬 Чат", "🔄 Fullstack", "📁 Файлы", "🔧 Debug" });

            switch (activeTab)
            {
                case 0: DrawChatTab();      break;
                case 1: DrawFullstackTab(); break;
                case 2: DrawFilesTab();     break;
                case 3: DrawDebugTab();     break;
            }
        }

        // ── Tab 0: Chat ─────────────────────────────────────────────────
        void DrawChatTab()
        {
            // Context mini bar
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (selectedFile != null)
            {
                EditorGUILayout.LabelField("📜 " + selectedFile.fileName + "  (" + fileContent.Length + " симв.)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(selectedFile.assetPath, EditorStyles.miniLabel);
            }
            else if (!string.IsNullOrEmpty(ctxObject))
            {
                EditorGUILayout.LabelField("🎮 " + ctxObject + " · " + ctxScene, EditorStyles.boldLabel);
            }
            else
            {
                EditorGUILayout.LabelField("⚠️ Выбери .cs файл в Project или GameObject в Hierarchy", EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.BeginHorizontal();
            autoApply = EditorGUILayout.ToggleLeft("⚡ Авто-применить", autoApply, GUILayout.Width(140));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("🗑 Очистить чат", EditorStyles.miniButton, GUILayout.Width(110)))
            {
                history.Clear();
                pendingIndex = -1;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            // Chat history
            chatScroll = EditorGUILayout.BeginScrollView(chatScroll, GUILayout.ExpandHeight(true));

            for (int i = 0; i < history.Count; i++)
            {
                ChatMsg m = history[i];
                EditorGUILayout.BeginHorizontal();
                if (m.isUser) GUILayout.Space(40);

                EditorGUILayout.BeginVertical();
                GUILayout.Label(m.isUser ? "Ты" : "🤖 AI", EditorStyles.miniLabel);

                if (m.isPending)
                {
                    double elapsed = EditorApplication.timeSinceStartup - m.startTime;
                    int    dots    = ((int)(elapsed * 2)) % 4;
                    string dotStr  = new string('.', dots);
                    string spinner = "⏳ Думаю" + dotStr + " (" + (int)elapsed + "s)";
                    EditorGUILayout.HelpBox(spinner, MessageType.Info);
                    Repaint();
                }
                else
                {
                    GUILayout.Box(m.text, m.isUser ? sUser : sAI, GUILayout.ExpandWidth(true));

                    if (!string.IsNullOrEmpty(m.code))
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label("📜 Код", EditorStyles.miniLabel);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("📋 Копировать", EditorStyles.miniButton, GUILayout.Width(90)))
                            EditorGUIUtility.systemCopyBuffer = m.code;
                        if (selectedFile != null && GUILayout.Button("✅ Записать в файл", EditorStyles.miniButton, GUILayout.Width(120)))
                            ApplyCode(m.code, selectedFile.assetPath);
                        EditorGUILayout.EndHorizontal();

                        codeScroll = EditorGUILayout.BeginScrollView(codeScroll, GUILayout.MaxHeight(120));
                        GUILayout.Label(m.code.Length > 1500 ? m.code.Substring(0, 1500) + "..." : m.code, sCode);
                        EditorGUILayout.EndScrollView();

                        if (!string.IsNullOrEmpty(ghToken) && selectedFile != null)
                        {
                            if (GUILayout.Button("📤 Push to GitHub", EditorStyles.miniButton, GUILayout.Width(130)))
                            {
                                string rel = selectedFile.assetPath.Replace("\\", "/");
                                PushToGitHub(rel, m.code);
                            }
                        }
                    }
                }
                EditorGUILayout.EndVertical();
                if (!m.isUser) GUILayout.Space(40);
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(3);
            }

            EditorGUILayout.EndScrollView();
            if (Event.current.type == EventType.Repaint) chatScroll.y = float.MaxValue;

            if (!string.IsNullOrEmpty(statusMsg))
                EditorGUILayout.HelpBox(statusMsg, isBusy ? MessageType.Info : MessageType.None);

            // Input area
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.SetNextControlName("chatInput");
            userInput = EditorGUILayout.TextArea(userInput, GUILayout.MinHeight(54), GUILayout.ExpandWidth(true));

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (selectedFile != null)
            {
                if (GUILayout.Button("🔧 Исправь ошибки", GUILayout.Width(130))) { userInput = "Найди и исправь все ошибки в этом скрипте"; SendMessage(); }
                if (GUILayout.Button("📖 Объясни", GUILayout.Width(75))) { userInput = "Объясни что делает этот код"; SendMessage(); }
            }
            GUI.enabled = !isBusy && !string.IsNullOrEmpty(userInput.Trim());
            if (GUILayout.Button(isBusy ? "⏳..." : "▶ Send", GUILayout.Width(70))) SendMessage();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            if (Event.current.type == EventType.KeyDown && Event.current.shift && Event.current.keyCode == KeyCode.Return)
            {
                if (!isBusy && !string.IsNullOrEmpty(userInput.Trim()))
                {
                    SendMessage();
                    Event.current.Use();
                }
            }
        }

        // ── Tab 1: Fullstack ────────────────────────────────────────────
        void DrawFullstackTab()
        {
            EditorGUILayout.Space(4);

            // ── Server URL ─────────────────────────────────────────────
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🌐 URL сервера", EditorStyles.boldLabel);

            bool urlEmpty = string.IsNullOrEmpty(serverUrl) || serverUrl == "__SERVER_URL__";
            Color old = GUI.color;
            if (urlEmpty) GUI.color = Color.red;
            string newUrl = EditorGUILayout.TextField(serverUrl ?? "");
            GUI.color = old;

            if (newUrl != serverUrl)
            {
                serverUrl = newUrl.Trim().TrimEnd('/');
                EditorPrefs.SetString(PREF_SERVER_URL, serverUrl);
            }

            if (urlEmpty)
            {
                EditorGUILayout.HelpBox(
                    "⚠️ Вставь URL сервера!\n" +
                    "Скопируй из веб-браузера (кнопка «Скачать плагин» → адрес сайта)\n" +
                    "Формат: https://XXXX.riker.replit.dev",
                    MessageType.Error);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("🔗 Тест соединения", EditorStyles.miniButton, GUILayout.Width(140)))
                    EditorCoroutine.Start(TestConnectionRoutine());
                if (GUILayout.Button("📋 Копировать URL", EditorStyles.miniButton, GUILayout.Width(130)))
                    EditorGUIUtility.systemCopyBuffer = serverUrl;
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Sync section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📂 Синхронизация файлов", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Отправляет ВСЕ файлы Unity проекта на сервер — AI сможет их читать.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = !syncBusy;
            if (GUILayout.Button(syncBusy ? "⏳ Синхронизация..." : "🔄 Синхронизировать ВСЕ файлы", GUILayout.Height(28)))
                StartFullSync();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            Color old = GUI.color;
            GUI.color = syncStatus.StartsWith("✅") ? Color.green : syncStatus.StartsWith("❌") ? Color.red : Color.yellow;
            EditorGUILayout.LabelField(syncStatus, EditorStyles.wordWrappedMiniLabel);
            GUI.color = old;

            if (lastSyncTime > 0)
            {
                double ago = EditorApplication.timeSinceStartup - lastSyncTime;
                EditorGUILayout.LabelField("Последняя синхронизация: " + (int)ago + "s назад · " + syncFileCount + " файлов", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Polling section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("⚡ Command Polling", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Плагин опрашивает сервер каждые 3 сек и исполняет команды AI (запись файлов, создание объектов).", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            bool newPolling = EditorGUILayout.ToggleLeft("", polling, GUILayout.Width(20));
            if (newPolling != polling)
            {
                polling = newPolling;
                EditorPrefs.SetBool(PREF_POLLING, polling);
                if (polling) AddCommandLog("Polling включён");
                else         AddCommandLog("Polling выключён");
            }
            GUI.color = polling ? Color.green : Color.gray;
            GUILayout.Label(polling ? "● ВКЛЮЧЁН — AI может писать файлы и создавать объекты" : "○ ВЫКЛЮЧЕН", EditorStyles.miniLabel);
            GUI.color = old;
            EditorGUILayout.EndHorizontal();

            if (pendingCmds > 0)
            {
                GUI.color = Color.yellow;
                EditorGUILayout.LabelField("⏳ Команд ожидает исполнения: " + pendingCmds, EditorStyles.boldLabel);
                GUI.color = old;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Command log
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("📋 Лог команд (" + commandLog.Count + ")", EditorStyles.boldLabel);
            if (GUILayout.Button("🗑", EditorStyles.miniButton, GUILayout.Width(25)))
                commandLog.Clear();
            EditorGUILayout.EndHorizontal();

            commandLogScroll = EditorGUILayout.BeginScrollView(commandLogScroll, GUILayout.Height(150));
            foreach (string entry in commandLog)
                GUILayout.Label(entry, sCmd);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // GitHub section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            showGh = EditorGUILayout.Foldout(showGh, "📤 GitHub настройки");
            if (showGh)
            {
                ghToken  = EditorGUILayout.TextField("Token:",  ghToken);
                ghOwner  = EditorGUILayout.TextField("Owner:",  ghOwner);
                ghRepo   = EditorGUILayout.TextField("Repo:",   ghRepo);
                ghBranch = EditorGUILayout.TextField("Branch:", ghBranch);
                if (!string.IsNullOrEmpty(ghStatus))
                    EditorGUILayout.HelpBox(ghStatus, MessageType.Info);
            }
            EditorGUILayout.EndVertical();
        }

        // ── Tab 2: Files ────────────────────────────────────────────────
        void DrawFilesTab()
        {
            EditorGUILayout.Space(4);

            if (!scanDone)
            {
                EditorGUILayout.BeginHorizontal();
                if (!scanRunning)
                {
                    if (GUILayout.Button("🔍 Сканировать проект")) StartProjectScan();
                }
                else
                {
                    EditorGUILayout.HelpBox("Сканирование: " + scanProgress + "/" + scanTotal, MessageType.Info);
                }
                EditorGUILayout.EndHorizontal();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Файлов: " + allFiles.Count + " · Скриптов: " + scriptIndex.Count, EditorStyles.miniLabel);
            if (GUILayout.Button("🔄", EditorStyles.miniButton, GUILayout.Width(24))) StartProjectScan();
            EditorGUILayout.EndHorizontal();

            fileFilter  = EditorGUILayout.TextField("🔍 Поиск:", fileFilter);
            fileTypeIdx = GUILayout.Toolbar(fileTypeIdx, fileTypeOpts);

            FileCategory? filterCat = null;
            if (fileTypeIdx == 1) filterCat = FileCategory.Script;
            if (fileTypeIdx == 2) filterCat = FileCategory.Shader;
            if (fileTypeIdx == 3) filterCat = FileCategory.Scene;
            if (fileTypeIdx == 4) filterCat = FileCategory.Prefab;
            if (fileTypeIdx == 5) filterCat = FileCategory.Other;

            fileScroll = EditorGUILayout.BeginScrollView(fileScroll, GUILayout.Height(200));
            foreach (FileEntry fe in allFiles)
            {
                if (filterCat.HasValue && fe.category != filterCat.Value) continue;
                if (!string.IsNullOrEmpty(fileFilter) && !fe.fileName.ToLowerInvariant().Contains(fileFilter.ToLowerInvariant())) continue;

                EditorGUILayout.BeginHorizontal();
                string icon = fe.category == FileCategory.Script ? "📜"
                            : fe.category == FileCategory.Scene  ? "🎬"
                            : fe.category == FileCategory.Prefab ? "🧊"
                            : fe.category == FileCategory.Shader ? "✨"
                            : "📄";
                GUILayout.Label(icon + " " + fe.fileName, GUILayout.ExpandWidth(true));
                if (fe.isText && GUILayout.Button("👁", EditorStyles.miniButton, GUILayout.Width(24)))
                {
                    viewFile    = fe;
                    viewContent = "";
                    try { viewContent = File.ReadAllText(fe.fullPath, Encoding.UTF8); } catch { }
                }
                if (fe.category == FileCategory.Script && GUILayout.Button("💬", EditorStyles.miniButton, GUILayout.Width(24)))
                {
                    selectedFile = fe;
                    fileContent  = "";
                    try { fileContent = File.ReadAllText(fe.fullPath, Encoding.UTF8); } catch { }
                    activeTab    = 0;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            if (viewFile != null && !string.IsNullOrEmpty(viewContent))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("📄 " + viewFile.fileName, EditorStyles.boldLabel);
                viewScroll = EditorGUILayout.BeginScrollView(viewScroll, GUILayout.ExpandHeight(true));
                GUILayout.Label(viewContent.Length > 5000 ? viewContent.Substring(0, 5000) + "...(обрезано)" : viewContent, sCode);
                EditorGUILayout.EndScrollView();
            }
        }

        // ── Tab 3: Debug ────────────────────────────────────────────────
        void DrawDebugTab()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Состояние подключения:", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Сервер: " + serverUrl);
            EditorGUILayout.LabelField("Сцена: " + ctxScene);
            EditorGUILayout.LabelField("Объект: " + ctxObject);
            EditorGUILayout.LabelField("Выбранный файл: " + (selectedFile != null ? selectedFile.fileName : "нет"));
            EditorGUILayout.LabelField("Файлов в памяти: " + allFiles.Count);
            EditorGUILayout.LabelField("Pending logs: " + pendingLogs.Count);
            EditorGUILayout.LabelField("Polling: " + (polling ? "ON" : "OFF"));
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Последний запрос:", EditorStyles.boldLabel);
            debugScroll = EditorGUILayout.BeginScrollView(debugScroll, GUILayout.ExpandHeight(true));
            GUILayout.Label(lastJsonSent.Length > 3000 ? lastJsonSent.Substring(0, 3000) + "..." : lastJsonSent, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //   EditorCoroutine — лёгкая корутина для Editor
    // ═══════════════════════════════════════════════════════════════════════

    public class EditorCoroutine
    {
        private IEnumerator routine;

        private EditorCoroutine(IEnumerator r) { routine = r; }

        public static EditorCoroutine Start(IEnumerator r)
        {
            EditorCoroutine c = new EditorCoroutine(r);
            c.Register();
            return c;
        }

        private void Register()   { EditorApplication.update += Update; }
        private void Unregister() { EditorApplication.update -= Update; }

        void Update()
        {
            if (!MoveNext()) Unregister();
        }

        bool MoveNext()
        {
            object current = routine.Current;

            if (current is UnityWebRequestAsyncOperation op)
            {
                if (!op.isDone) return true;
            }
            else if (current is WaitForSeconds wait)
            {
                // simple busy-wait for editor (not production-grade)
                return routine.MoveNext();
            }

            if (routine.MoveNext())
            {
                // Handle nested IEnumerator
                if (routine.Current is IEnumerator nested)
                {
                    routine = Flatten(routine, nested);
                }
                return true;
            }
            return false;
        }

        IEnumerator Flatten(IEnumerator outer, IEnumerator inner)
        {
            while (inner.MoveNext()) yield return inner.Current;
            while (outer.MoveNext()) yield return outer.Current;
        }
    }

    // ── WaitForSeconds stub ──────────────────────────────────────────────
    public class WaitForSeconds
    {
        public float seconds;
        public WaitForSeconds(float s) { seconds = s; }
    }
}
