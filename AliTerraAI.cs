// ╔═══════════════════════════════════════════════════════════════════╗
// ║   AliTerra AI Coder  v6  —  Scene Editor + Auto-Apply            ║
// ║   Установка: Assets/Editor/AliTerraAI.cs                        ║
// ║   Меню: Window → AliTerra → AI Coder  (Ctrl+Shift+A)           ║
// ╚═══════════════════════════════════════════════════════════════════╝
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
    // ── File categories ──────────────────────────────────────────────
    public enum FileCategory { Script, Scene, Prefab, Material, Shader, Config, Audio, Model, Image, Other }

    public class FileEntry
    {
        public string fullPath  = "";
        public string assetPath = "";
        public string fileName  = "";
        public string ext       = "";
        public FileCategory category;
        public bool isText      = false;
        public long sizeBytes   = 0;
    }

    public class ScriptInfo
    {
        public string path      = "";
        public string className = "";
        public string baseClass = "";
        public int lineCount    = 0;
        public List<string> methods = new List<string>();
        public string content   = "";
    }

    public class ChatMsg
    {
        public bool   isUser    = false;
        public string text      = "";
        public string code      = "";
        public bool   isPending = false;  // показывает спиннер ожидания
        public double startTime = 0;      // EditorApplication.timeSinceStartup при отправке
    }

    // ═══════════════════════════════════════════════════════════════════
    public class AliTerraAICoder : EditorWindow
    {
        // ── Server ────────────────────────────────────────────────────
        private const string SERVER_URL     = "https://44c604d5-cbad-400c-8af7-eb2443eadba0-00-3vtnrupat6ost.riker.replit.dev";
        private const int    MAX_FILE_CHARS = 8000;
        private const int    MAX_REL_CHARS  = 2500;
        private const int    MAX_REL_COUNT  = 5;

        // ── EditorPrefs ───────────────────────────────────────────────
        private const string PREF_GH_TOKEN  = "AliTerra_GH_Token";
        private const string PREF_GH_OWNER  = "AliTerra_GH_Owner";
        private const string PREF_GH_REPO   = "AliTerra_GH_Repo";
        private const string PREF_GH_BRANCH = "AliTerra_GH_Branch";
        private const string PREF_AUTO      = "AliTerra_AutoApply";

        // ── UI ────────────────────────────────────────────────────────
        private int    activeTab  = 0;
        private bool   isBusy    = false;
        private string statusMsg = "";

        // ── Chat ──────────────────────────────────────────────────────
        private List<ChatMsg> history      = new List<ChatMsg>();
        private string        userInput    = "";
        private Vector2       chatScroll;
        private int           pendingIndex = -1;   // индекс пузыря "Думаю..."
        private int           retryCount   = 0;    // авто-повтор при ошибке
        private string        lastJson     = "";   // для повтора

        // ── Auto-apply ────────────────────────────────────────────────
        private bool autoApply = false;  // auto-write & compile AI edits

        // ── Context ───────────────────────────────────────────────────
        private FileEntry selectedFile = null;
        private string    fileContent  = "";
        private string    ctxScene     = "";
        private string    ctxObject    = "";
        private string    sceneHierarchy = "";    // JSON-like hierarchy
        private string    lastScannedScene = "";

        // ── Project scan ──────────────────────────────────────────────
        private List<FileEntry>  allFiles       = new List<FileEntry>();
        private List<ScriptInfo> scriptIndex    = new List<ScriptInfo>();
        private string           projectSummary = "";
        private bool             scanRunning    = false;
        private bool             scanDone       = false;
        private int              scanProgress   = 0;
        private int              scanTotal      = 0;

        // ── File browser ──────────────────────────────────────────────
        private Vector2      browserScroll;
        private string       browserSearch = "";
        private FileCategory browserFilter = FileCategory.Script;
        private bool         showAllTypes  = false;

        // ── GitHub ────────────────────────────────────────────────────
        private string ghToken  = "";
        private string ghOwner  = "aliter230880";
        private string ghRepo   = "unity";
        private string ghBranch = "main";
        private string ghStatus = "";
        private bool   ghBusy   = false;
        private string gitLog   = "";

        private double lastPush = 0;

        // ── Extension tables ──────────────────────────────────────────
        static readonly Dictionary<string, FileCategory> CatMap = new Dictionary<string, FileCategory>
        {
            {".cs",    FileCategory.Script},
            {".js",    FileCategory.Script},
            {".unity", FileCategory.Scene},
            {".prefab",FileCategory.Prefab},
            {".mat",   FileCategory.Material},
            {".physicmaterial", FileCategory.Material},
            {".shader",FileCategory.Shader},
            {".shadergraph",    FileCategory.Shader},
            {".shadersubgraph", FileCategory.Shader},
            {".cginc", FileCategory.Shader},
            {".hlsl",  FileCategory.Shader},
            {".compute",FileCategory.Shader},
            {".asmdef",FileCategory.Config},
            {".asmref",FileCategory.Config},
            {".json",  FileCategory.Config},
            {".xml",   FileCategory.Config},
            {".txt",   FileCategory.Config},
            {".md",    FileCategory.Config},
            {".inputactions", FileCategory.Config},
            {".asset", FileCategory.Config},
            {".anim",  FileCategory.Config},
            {".controller", FileCategory.Config},
            {".overridecontroller", FileCategory.Config},
            {".wav",   FileCategory.Audio},
            {".mp3",   FileCategory.Audio},
            {".ogg",   FileCategory.Audio},
            {".fbx",   FileCategory.Model},
            {".obj",   FileCategory.Model},
            {".glb",   FileCategory.Model},
            {".png",   FileCategory.Image},
            {".jpg",   FileCategory.Image},
            {".jpeg",  FileCategory.Image},
            {".tga",   FileCategory.Image},
            {".psd",   FileCategory.Image},
            {".exr",   FileCategory.Image},
        };

        static readonly HashSet<string> TextExts = new HashSet<string>
        {
            ".cs",".js",".unity",".prefab",".mat",".physicmaterial",
            ".shader",".shadergraph",".shadersubgraph",".cginc",".hlsl",".compute",
            ".asmdef",".asmref",".json",".xml",".txt",".md",".inputactions",
            ".asset",".anim",".controller",".overridecontroller",
            ".lighting",".terrainlayer",".guiskin",".jslib",".uxml",".uss",
        };

        // ── Menu ──────────────────────────────────────────────────────
        [MenuItem("Window/AliTerra/AI Coder %#a")]
        public static void ShowWindow()
        {
            var w = GetWindow<AliTerraAICoder>("🤖 AliTerra AI");
            w.minSize = new Vector2(460, 580);
        }

        // ── Lifecycle ──────────────────────────────────────────────────
        private void OnEnable()
        {
            LoadSettings();
            EditorApplication.update += OnTick;
            EditorCoroutine.Start(ScanProjectRoutine());
            history.Add(new ChatMsg
            {
                isUser = false,
                text = "Привет! Я AliTerra AI Coder v6.\n\n" +
                       "Расскажи мне что нужно изменить в сцене — " +
                       "например: «В MainCity добавь SpawnPoint у входа в казино» " +
                       "или «Убери тени с объекта CityBlocks».\n\n" +
                       "Я напишу Editor-скрипт и, если включён 🤖 Авто-режим, " +
                       "сразу применю его. После компиляции Unity изменения будут в сцене.",
            });
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnTick;
        }

        private void OnTick()
        {
            RefreshContext();
            double now = EditorApplication.timeSinceStartup;
            if (now - lastPush > 3.0)
            {
                lastPush = now;
                PushHeartbeat();
            }
            // Перерисовываем пока ждём ответа (для счётчика секунд)
            if (isBusy) Repaint();
        }

        private void LoadSettings()
        {
            ghToken    = EditorPrefs.GetString(PREF_GH_TOKEN,  "");
            ghOwner    = EditorPrefs.GetString(PREF_GH_OWNER,  "aliter230880");
            ghRepo     = EditorPrefs.GetString(PREF_GH_REPO,   "unity");
            ghBranch   = EditorPrefs.GetString(PREF_GH_BRANCH, "main");
            autoApply  = EditorPrefs.GetBool(PREF_AUTO, false);
        }

        private void SaveSettings()
        {
            EditorPrefs.SetString(PREF_GH_TOKEN,  ghToken);
            EditorPrefs.SetString(PREF_GH_OWNER,  ghOwner);
            EditorPrefs.SetString(PREF_GH_REPO,   ghRepo);
            EditorPrefs.SetString(PREF_GH_BRANCH, ghBranch);
            EditorPrefs.SetBool(PREF_AUTO, autoApply);
        }

        // ── Context ───────────────────────────────────────────────────
        private void RefreshContext()
        {
            try { ctxScene = EditorSceneManager.GetActiveScene().name; } catch { }

            var go = Selection.activeGameObject;
            ctxObject = go != null ? go.name : "";

            var obj = Selection.activeObject;
            if (obj != null)
            {
                string ap = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(ap) && !ap.EndsWith(".meta"))
                    if (selectedFile == null || selectedFile.assetPath != ap)
                        SelectFile(ap);
            }

            // Rescan hierarchy when scene changes
            if (ctxScene != lastScannedScene)
            {
                lastScannedScene = ctxScene;
                sceneHierarchy   = ScanSceneHierarchy();
            }
        }

        private void SelectFile(string assetPath)
        {
            string fullPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", assetPath));
            if (!File.Exists(fullPath)) return;

            var fe = new FileEntry();
            fe.assetPath = assetPath;
            fe.fullPath  = fullPath;
            fe.fileName  = Path.GetFileName(assetPath);
            fe.ext       = Path.GetExtension(assetPath).ToLowerInvariant();
            fe.isText    = TextExts.Contains(fe.ext);
            fe.category  = CatMap.ContainsKey(fe.ext) ? CatMap[fe.ext] : FileCategory.Other;
            try { fe.sizeBytes = new FileInfo(fullPath).Length; } catch { }
            selectedFile = fe;
            fileContent  = "";
            if (fe.isText)
                try { fileContent = File.ReadAllText(fullPath); } catch { fileContent = "(ошибка)"; }
            Repaint();
        }

        // ── Scene hierarchy scanner ────────────────────────────────────
        private string ScanSceneHierarchy()
        {
            try
            {
                var scene = EditorSceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(scene.name)) return "";

                var sb = new StringBuilder();
                sb.AppendLine("[ИЕРАРХИЯ: " + scene.name + " | rootObjects:" + scene.rootCount + "]");

                var roots = scene.GetRootGameObjects();
                int count = 0;
                foreach (var root in roots)
                {
                    AppendGO(sb, root, 0, ref count);
                    if (count > 300 || sb.Length > 12000)
                    {
                        sb.AppendLine("... [обрезано: " + (roots.Length - Array.IndexOf(roots, root)) + " корневых объектов осталось]");
                        break;
                    }
                }
                return sb.ToString();
            }
            catch { return ""; }
        }

        private void AppendGO(StringBuilder sb, GameObject go, int depth, ref int count)
        {
            if (depth > 6 || count > 300 || sb.Length > 12000) return;
            count++;
            string pad = depth == 0 ? "" : new string(' ', depth * 2);
            var comps = go.GetComponents<Component>();
            var compNames = new StringBuilder();
            foreach (var c in comps)
            {
                if (c == null || c is Transform) continue;
                if (compNames.Length > 0) compNames.Append(',');
                compNames.Append(c.GetType().Name);
            }
            Vector3 p = go.transform.position;
            sb.Append(pad);
            sb.Append(go.name);
            sb.Append(" [");
            sb.Append(p.x.ToString("F1"));
            sb.Append(',');
            sb.Append(p.y.ToString("F1"));
            sb.Append(',');
            sb.Append(p.z.ToString("F1"));
            sb.Append(']');
            if (compNames.Length > 0) { sb.Append(" {"); sb.Append(compNames); sb.Append('}'); }
            if (!go.activeSelf) sb.Append(" [DISABLED]");
            sb.AppendLine();

            for (int i = 0; i < go.transform.childCount; i++)
                AppendGO(sb, go.transform.GetChild(i).gameObject, depth + 1, ref count);
        }

        // ── Project scanner ────────────────────────────────────────────
        private IEnumerator ScanProjectRoutine()
        {
            scanRunning = true;
            scanDone    = false;
            allFiles.Clear();
            scriptIndex.Clear();

            string root = Application.dataPath;
            string[] paths;
            try { paths = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories); }
            catch { scanRunning = false; yield break; }

            var filtered = new List<string>();
            foreach (string p in paths) if (!p.EndsWith(".meta")) filtered.Add(p);

            scanTotal    = filtered.Count;
            scanProgress = 0;

            for (int i = 0; i < filtered.Count; i++)
            {
                string full = filtered[i];
                string rel  = "Assets" + full.Substring(root.Length).Replace('\\', '/');
                string ext  = Path.GetExtension(full).ToLowerInvariant();

                var fe = new FileEntry();
                fe.fullPath  = full;
                fe.assetPath = rel;
                fe.fileName  = Path.GetFileName(full);
                fe.ext       = ext;
                fe.isText    = TextExts.Contains(ext);
                fe.category  = CatMap.ContainsKey(ext) ? CatMap[ext] : FileCategory.Other;
                try { fe.sizeBytes = new FileInfo(full).Length; } catch { }
                allFiles.Add(fe);

                if (ext == ".cs")
                {
                    string content = "";
                    try { content = File.ReadAllText(full); } catch { }
                    var si = new ScriptInfo();
                    si.path      = rel;
                    si.className = ExtractClass(content);
                    si.baseClass = ExtractBase(content);
                    si.lineCount = CountLines(content);
                    si.methods   = ExtractMethods(content);
                    si.content   = content;
                    scriptIndex.Add(si);
                }

                scanProgress++;
                if (i % 30 == 0) { Repaint(); yield return null; }
            }
            projectSummary = BuildSummary();
            scanRunning    = false;
            scanDone       = true;
            Repaint();
        }

        private string BuildSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[ПРОЕКТ: " + allFiles.Count + " файлов, " + scriptIndex.Count + " скриптов]");
            var cats = new Dictionary<string, int>();
            foreach (var fe in allFiles)
            {
                string k = fe.category.ToString();
                cats[k] = cats.ContainsKey(k) ? cats[k] + 1 : 1;
            }
            foreach (var kv in cats) sb.AppendLine("  " + kv.Key + ": " + kv.Value);
            sb.AppendLine();
            sb.AppendLine("[КЛЮЧЕВЫЕ СКРИПТЫ]");
            var own = new List<ScriptInfo>();
            foreach (var s in scriptIndex)
                if (!s.path.Contains("/3rd party/") && !s.path.Contains("/Photon/") &&
                    !s.path.Contains("/Convai/") && !s.path.Contains("/Imports/") &&
                    !s.path.Contains("/Thirdweb/") && !s.path.Contains("/BloodEffects") &&
                    !s.path.Contains("/Bakery/") && !s.path.Contains("/RealisticCar"))
                    own.Add(s);
            foreach (var s in own)
            {
                string m = s.methods.Count > 0
                    ? string.Join(",", s.methods.GetRange(0, Math.Min(4, s.methods.Count)).ToArray()) : "—";
                string b = string.IsNullOrEmpty(s.baseClass) ? "" : ":" + s.baseClass;
                sb.AppendLine("  " + s.className + b + " (" + s.lineCount + "л) | " + m);
            }
            return sb.ToString();
        }

        // ── C# helpers ────────────────────────────────────────────────
        static string ExtractClass(string c) { var m = Regex.Match(c, @"\bclass\s+(\w+)"); return m.Success ? m.Groups[1].Value : ""; }
        static string ExtractBase(string c)  { var m = Regex.Match(c, @"\bclass\s+\w+\s*:\s*(\w[\w.]+)"); return m.Success ? m.Groups[1].Value : ""; }
        static int    CountLines(string c)   { if (string.IsNullOrEmpty(c)) return 0; int n = 1; foreach (char ch in c) if (ch == '\n') n++; return n; }
        static List<string> ExtractMethods(string c)
        {
            var list = new List<string>();
            var ms = Regex.Matches(c, @"(?:public|protected)\s+(?:override\s+|static\s+|async\s+)*(?:\w[\w<>\[\]]*\s+)+(\w+)\s*\(");
            foreach (Match m in ms)
            {
                string n = m.Groups[1].Value;
                if (n == "if" || n == "for" || n == "while" || n == "new" || n == "class") continue;
                if (!list.Contains(n)) list.Add(n);
            }
            return list;
        }

        private List<ScriptInfo> FindRelated(string query, int max)
        {
            string q = query.ToLowerInvariant();
            string[] words = q.Split(new char[] { ' ', '\t', '\n', '.', ',', '?', '!' }, StringSplitOptions.RemoveEmptyEntries);
            var scored = new List<KeyValuePair<float, ScriptInfo>>();
            string skip = selectedFile != null ? selectedFile.assetPath : "";
            foreach (var s in scriptIndex)
            {
                if (s.path == skip) continue;
                float score = 0f;
                string tgt = (s.className + " " + s.path).ToLowerInvariant();
                foreach (string w in words)
                {
                    if (w.Length < 3) continue;
                    if (s.className.ToLowerInvariant().Contains(w)) score += 3f;
                    else if (tgt.Contains(w)) score += 1f;
                    if (s.content.ToLowerInvariant().Contains(w)) score += 0.3f;
                }
                if (score > 0f) scored.Add(new KeyValuePair<float, ScriptInfo>(score, s));
            }
            scored.Sort((a, b) => b.Key.CompareTo(a.Key));
            var result = new List<ScriptInfo>();
            for (int i = 0; i < Math.Min(max, scored.Count); i++) result.Add(scored[i].Value);
            return result;
        }

        // ── Heartbeat ─────────────────────────────────────────────────
        private void PushHeartbeat()
        {
            if (string.IsNullOrEmpty(SERVER_URL) || SERVER_URL == "__SERVER_URL__") return;
            var sb = new StringBuilder();
            sb.Append('{');
            AppendKV(sb, "scene",           ctxScene);                              sb.Append(',');
            AppendKV(sb, "selectedObject",  ctxObject);                             sb.Append(',');
            AppendKV(sb, "openScriptName",  selectedFile != null ? selectedFile.fileName : "");  sb.Append(',');
            AppendKV(sb, "openScriptPath",  selectedFile != null ? selectedFile.assetPath : "");
            sb.Append('}');
            EditorCoroutine.Start(HttpPost(SERVER_URL + "/api/unity/push", sb.ToString(), null));
        }

        // ── Auto-apply: write Editor script ───────────────────────────
        private void AutoApplyCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return;
            string ts         = DateTime.Now.ToString("yyyyMMddHHmmss");
            string scriptName = "AliTerra_Edit_" + ts;
            string editorDir  = Application.dataPath + "/Editor";
            if (!Directory.Exists(editorDir))
                Directory.CreateDirectory(editorDir);
            string scriptPath = editorDir + "/" + scriptName + ".cs";
            try
            {
                File.WriteAllText(scriptPath, code, Encoding.UTF8);
                AssetDatabase.Refresh();
                statusMsg = "✅ Скрипт записан: Assets/Editor/" + scriptName + ".cs — Unity компилирует и применяет...";
            }
            catch (Exception ex) { statusMsg = "❌ " + ex.Message; }
            Repaint();
        }

        // ── Manual write file ─────────────────────────────────────────
        private void WriteFile(string assetPath, string content)
        {
            string fullPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", assetPath));
            try
            {
                if (File.Exists(fullPath)) File.Copy(fullPath, fullPath + ".bak", true);
                File.WriteAllText(fullPath, content, Encoding.UTF8);
                AssetDatabase.Refresh();
                if (selectedFile != null && selectedFile.assetPath == assetPath) fileContent = content;
                statusMsg = "✅ Записано: " + assetPath;
            }
            catch (Exception ex) { statusMsg = "❌ " + ex.Message; }
            Repaint();
        }

        // ── Send AI message ────────────────────────────────────────────
        private void SendMessage()
        {
            string text = userInput.Trim();
            if (string.IsNullOrEmpty(text) || isBusy) return;
            userInput    = "";
            isBusy       = true;
            retryCount   = 0;
            statusMsg    = "";

            // Пузырь пользователя
            history.Add(new ChatMsg { isUser = true, text = text });

            // Немедленный пузырь "Думаю..." — пользователь видит реакцию сразу
            double t = EditorApplication.timeSinceStartup;
            history.Add(new ChatMsg { isUser = false, isPending = true, startTime = t });
            pendingIndex = history.Count - 1;
            chatScroll   = new Vector2(0, float.MaxValue);
            Repaint();

            var related = scanDone ? FindRelated(text, MAX_REL_COUNT) : new List<ScriptInfo>();
            lastJson     = BuildChatJson(text, related);
            EditorCoroutine.Start(HttpPost(SERVER_URL + "/api/ai/chat", lastJson, OnAIResponse));
        }

        private void OnAIResponse(string raw)
        {
            // Пустой ответ — авто-повтор один раз
            if (string.IsNullOrEmpty(raw) && retryCount < 1)
            {
                retryCount++;
                // Обновляем текст пузыря на "повтор"
                if (pendingIndex >= 0 && pendingIndex < history.Count)
                    history[pendingIndex].text = "⏳ Нет ответа — повторяю...";
                Repaint();
                EditorCoroutine.Start(HttpPost(SERVER_URL + "/api/ai/chat", lastJson, OnAIResponse));
                return;
            }

            isBusy = false;

            ChatMsg responseMsg;
            if (string.IsNullOrEmpty(raw))
            {
                responseMsg = new ChatMsg { isUser = false, text = "⚠️ Сервер не отвечает. Проверь подключение и попробуй ещё раз." };
            }
            else
            {
                string reply = JsonGet(raw, "reply");
                string code  = JsonGet(raw, "code");
                if (string.IsNullOrEmpty(reply)) reply = raw.StartsWith("{") ? "(нет текста в ответе)" : raw;
                responseMsg = new ChatMsg { isUser = false, text = reply, code = code };
            }

            // Заменяем пузырь "Думаю..." на реальный ответ
            if (pendingIndex >= 0 && pendingIndex < history.Count)
                history[pendingIndex] = responseMsg;
            else
                history.Add(responseMsg);

            pendingIndex = -1;
            chatScroll   = new Vector2(0, float.MaxValue);

            if (!string.IsNullOrEmpty(responseMsg.code) && autoApply)
                AutoApplyCode(responseMsg.code);

            Repaint();
        }

        // ── GitHub ────────────────────────────────────────────────────
        private void GitHubPushFile(string assetPath, string content, string msg)
        {
            if (string.IsNullOrEmpty(ghToken)) { ghStatus = "❌ Нет токена"; return; }
            ghBusy   = true;
            ghStatus = "⏳ Получаю SHA...";
            Repaint();
            EditorCoroutine.Start(GitHubPushRoutine(assetPath, content, msg));
        }

        private IEnumerator GitHubPushRoutine(string assetPath, string content, string message)
        {
            string url = "https://api.github.com/repos/" + ghOwner + "/" + ghRepo + "/contents/" + assetPath;
            string sha = "";
            var getReq = new UnityWebRequest(url + "?ref=" + ghBranch, "GET");
            getReq.downloadHandler = new DownloadHandlerBuffer();
            getReq.SetRequestHeader("Authorization", "token " + ghToken);
            getReq.SetRequestHeader("User-Agent",    "AliTerraAI/5.0");
            getReq.SetRequestHeader("Accept",        "application/vnd.github+json");
            yield return getReq.SendWebRequest();
            if (getReq.result == UnityWebRequest.Result.Success) sha = JsonGet(getReq.downloadHandler.text, "sha");
            getReq.Dispose();

            ghStatus = "⏳ Пушу...";
            Repaint();

            string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(content));
            var body = new StringBuilder();
            body.Append('{');
            AppendKV(body, "message", message); body.Append(',');
            AppendKV(body, "content", b64);     body.Append(',');
            AppendKV(body, "branch",  ghBranch);
            if (!string.IsNullOrEmpty(sha)) { body.Append(','); AppendKV(body, "sha", sha); }
            body.Append('}');

            var putReq = new UnityWebRequest(url, "PUT");
            putReq.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString()));
            putReq.downloadHandler = new DownloadHandlerBuffer();
            putReq.SetRequestHeader("Content-Type",  "application/json");
            putReq.SetRequestHeader("Authorization", "token " + ghToken);
            putReq.SetRequestHeader("User-Agent",    "AliTerraAI/5.0");
            putReq.SetRequestHeader("Accept",        "application/vnd.github+json");
            yield return putReq.SendWebRequest();

            ghStatus = putReq.result == UnityWebRequest.Result.Success
                ? "✅ Запушено! https://github.com/" + ghOwner + "/" + ghRepo
                : "❌ " + putReq.error;
            putReq.Dispose();
            ghBusy = false;
            Repaint();
        }

        private void RunGit(string args)
        {
            string root = Application.dataPath + "/..";
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git", Arguments = args, WorkingDirectory = root,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    UseShellExecute = false, CreateNoWindow = true
                };
                var p = System.Diagnostics.Process.Start(psi);
                p.WaitForExit(10000);
                gitLog = "$ git " + args + "\n" + (p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd()).Trim();
            }
            catch (Exception e) { gitLog = "Ошибка: " + e.Message; }
            Repaint();
        }

        // ── JSON helpers ───────────────────────────────────────────────
        private string BuildChatJson(string userText, List<ScriptInfo> related)
        {
            var sb = new StringBuilder();
            sb.Append('{');

            // messages array (history without current message)
            sb.Append('"'); sb.Append("messages"); sb.Append('"'); sb.Append(':'); sb.Append('[');
            for (int i = 0; i < history.Count - 1; i++)   // -1 to skip the one we just added
            {
                if (i > 0) sb.Append(',');
                sb.Append('{');
                AppendKV(sb, "role",    history[i].isUser ? "user" : "assistant"); sb.Append(',');
                AppendKV(sb, "content", history[i].text);
                sb.Append('}');
            }
            sb.Append(']'); sb.Append(',');

            // context
            sb.Append('"'); sb.Append("context"); sb.Append('"'); sb.Append(':'); sb.Append('{');
            AppendKV(sb, "scene",          ctxScene);  sb.Append(',');
            AppendKV(sb, "selectedObject", ctxObject); sb.Append(',');

            // scene hierarchy (capped)
            string hier = sceneHierarchy.Length > 10000 ? sceneHierarchy.Substring(0, 10000) + "\n...[обрезано]" : sceneHierarchy;
            AppendKV(sb, "sceneHierarchy", hier); sb.Append(',');

            string fname = selectedFile != null ? selectedFile.fileName  : "";
            string fpath = selectedFile != null ? selectedFile.assetPath : "";
            string ftype = selectedFile != null ? selectedFile.category.ToString() + " (" + selectedFile.ext + ")" : "";
            AppendKV(sb, "scriptName",    fname); sb.Append(',');
            AppendKV(sb, "scriptPath",    fpath); sb.Append(',');
            AppendKV(sb, "fileType",      ftype); sb.Append(',');

            string fcontent = "";
            if (selectedFile != null && selectedFile.isText)
                fcontent = fileContent.Length > MAX_FILE_CHARS
                    ? fileContent.Substring(0, MAX_FILE_CHARS) + "\n...[обрезано]"
                    : fileContent;
            else if (selectedFile != null && !selectedFile.isText)
                fcontent = "[Бинарный: " + (selectedFile.sizeBytes / 1024) + " KB]";
            AppendKV(sb, "scriptContent",  fcontent); sb.Append(',');
            AppendKV(sb, "projectSummary", scanDone ? projectSummary.Substring(0, Math.Min(5000, projectSummary.Length)) : ""); sb.Append(',');
            AppendKVBool(sb, "projectScanned",     scanDone); sb.Append(',');
            AppendKVInt(sb,  "projectScriptCount", scriptIndex.Count); sb.Append(',');
            AppendKVBool(sb, "autoApplyMode",      autoApply); sb.Append(',');

            // related
            sb.Append('"'); sb.Append("relatedScripts"); sb.Append('"'); sb.Append(':'); sb.Append('[');
            for (int i = 0; i < related.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var r = related[i];
                sb.Append('{');
                AppendKV(sb, "path",      r.path);      sb.Append(',');
                AppendKV(sb, "className", r.className); sb.Append(',');
                string rc = r.content.Length > MAX_REL_CHARS ? r.content.Substring(0, MAX_REL_CHARS) + "\n..." : r.content;
                AppendKV(sb, "content", rc);
                sb.Append('}');
            }
            sb.Append(']');
            sb.Append('}'); sb.Append(',');

            sb.Append('"'); sb.Append("message"); sb.Append('"'); sb.Append(':'); sb.Append('"');
            AppendEsc(sb, userText);
            sb.Append('"');
            sb.Append('}');
            return sb.ToString();
        }

        static void AppendKV(StringBuilder sb, string k, string v)
        { sb.Append('"'); sb.Append(k); sb.Append('"'); sb.Append(':'); sb.Append('"'); AppendEsc(sb, v ?? ""); sb.Append('"'); }
        static void AppendKVBool(StringBuilder sb, string k, bool v)
        { sb.Append('"'); sb.Append(k); sb.Append('"'); sb.Append(':'); sb.Append(v ? "true" : "false"); }
        static void AppendKVInt(StringBuilder sb, string k, int v)
        { sb.Append('"'); sb.Append(k); sb.Append('"'); sb.Append(':'); sb.Append(v.ToString()); }

        static void AppendEsc(StringBuilder sb, string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            foreach (char c in s)
            {
                if      (c == '"')  { sb.Append('\\'); sb.Append('"'); }
                else if (c == '\\') { sb.Append('\\'); sb.Append('\\'); }
                else if (c == '\n') { sb.Append('\\'); sb.Append('n'); }
                else if (c == '\r') { sb.Append('\\'); sb.Append('r'); }
                else if (c == '\t') { sb.Append('\\'); sb.Append('t'); }
                else sb.Append(c);
            }
        }

        static string JsonGet(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return "";
            string needle = "\"" + key + "\"";
            int pos = json.IndexOf(needle);
            if (pos < 0) return "";
            int colon = json.IndexOf(':', pos + needle.Length);
            if (colon < 0) return "";
            int i = colon + 1;
            while (i < json.Length && (json[i] == ' ' || json[i] == '\n' || json[i] == '\r')) i++;
            if (i >= json.Length || json[i] != '"') return "";
            i++;
            var sb = new StringBuilder();
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < json.Length)
                {
                    i++; char e = json[i];
                    if      (e == '"')  sb.Append('"');
                    else if (e == '\\') sb.Append('\\');
                    else if (e == 'n')  sb.Append('\n');
                    else if (e == 'r')  sb.Append('\r');
                    else if (e == 't')  sb.Append('\t');
                    else sb.Append(e);
                }
                else if (c == '"') break;
                else sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        // ── HTTP ───────────────────────────────────────────────────────
        private IEnumerator HttpPost(string url, string json, Action<string> cb)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            var req = new UnityWebRequest(url, "POST");
            req.uploadHandler   = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 120;
            yield return req.SendWebRequest();
            string result = "";
            if (req.result == UnityWebRequest.Result.Success)
            {
                result = req.downloadHandler.text;
            }
            else
            {
                // Возвращаем JSON с описанием ошибки — OnAIResponse покажет его пользователю
                string errDetail = req.error ?? "Нет соединения";
                long   code      = req.responseCode;
                if (code > 0) errDetail = "HTTP " + code + ": " + errDetail;
                Debug.LogWarning("[AliTerra] " + errDetail);
                // Для повтора возвращаем пустую строку; статус ошибки сохраняем в statusMsg
                statusMsg = "⚠️ " + errDetail;
                result    = "";
            }
            req.Dispose();
            if (cb != null) cb(result);
        }

        // ═══════════════════════════════════════════════════════════════
        // ── GUI ────────────────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════
        private void OnGUI()
        {
            DrawHeader();
            string[] tabs = new string[] { "💬 Чат", "📁 Файлы", "🐙 GitHub", "🔧 Debug" };
            activeTab = GUILayout.Toolbar(activeTab, tabs);
            GUILayout.Space(4);
            if      (activeTab == 0) DrawChatTab();
            else if (activeTab == 1) DrawFilesTab();
            else if (activeTab == 2) DrawGitHubTab();
            else                     DrawDebugTab();
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("AliTerra AI v6", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            // Auto-apply toggle in header
            bool newAuto = GUILayout.Toggle(autoApply, " 🤖 Авто", EditorStyles.miniButton, GUILayout.Width(70));
            if (newAuto != autoApply) { autoApply = newAuto; SaveSettings(); }

            if (scanRunning)
            {
                GUI.color = Color.yellow;
                GUILayout.Label("⏳ " + scanProgress + "/" + scanTotal, EditorStyles.miniLabel);
                GUI.color = Color.white;
                Repaint();
            }
            else if (scanDone)
            {
                GUI.color = Color.green;
                GUILayout.Label("✅ " + allFiles.Count + " файлов", EditorStyles.miniLabel);
                GUI.color = Color.white;
            }
            GUILayout.EndHorizontal();

            // Scene bar
            if (!string.IsNullOrEmpty(ctxScene))
            {
                GUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label("🌍 Сцена: " + ctxScene, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("🔄 Скан", EditorStyles.miniButton, GUILayout.Width(50)))
                    sceneHierarchy = ScanSceneHierarchy();
                GUILayout.Label(sceneHierarchy.Length > 0 ? "✓ иерархия" : "нет", EditorStyles.miniLabel);
                GUILayout.EndHorizontal();
            }

            if (autoApply)
            {
                GUI.color = new Color(1f, 0.8f, 0.3f);
                GUILayout.Label("⚡ АВТО-РЕЖИМ: AI сразу записывает и применяет изменения", EditorStyles.boldLabel);
                GUI.color = Color.white;
            }
        }

        // ── Chat tab ───────────────────────────────────────────────────
        private void DrawChatTab()
        {
            // Scene quick-actions
            if (!string.IsNullOrEmpty(ctxScene))
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("📋 Что в сцене?", GUILayout.Height(26)))
                    QuickSend("Опиши всё что есть в сцене " + ctxScene + " — объекты, системы, что работает");
                if (GUILayout.Button("🐛 Проблемы", GUILayout.Height(26)))
                    QuickSend("Найди потенциальные проблемы и баги в сцене " + ctxScene);
                if (GUILayout.Button("🏗 Добавь", GUILayout.Height(26)))
                    QuickSend("В сцене " + ctxScene + " добавь: ");
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("🎯 Оптимизация", GUILayout.Height(26)))
                    QuickSend("Как оптимизировать производительность в сцене " + ctxScene + "? Напиши Editor скрипт с изменениями");
                if (GUILayout.Button("🔄 Обнови иерархию", GUILayout.Height(26)))
                { sceneHierarchy = ScanSceneHierarchy(); statusMsg = "Иерархия обновлена: " + ctxScene; }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(2);

            float chatH = position.height - (autoApply ? 230 : 210);
            chatScroll = EditorGUILayout.BeginScrollView(chatScroll, GUILayout.Height(Math.Max(100, chatH)));
            foreach (var msg in history) DrawBubble(msg);
            EditorGUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(statusMsg))
            {
                var ss = new GUIStyle(EditorStyles.helpBox); ss.wordWrap = true;
                GUILayout.Label(statusMsg, ss);
            }

            GUILayout.Space(2);
            GUILayout.BeginHorizontal();
            GUI.enabled = !isBusy;
            userInput = GUILayout.TextField(userInput, GUILayout.Height(40));
            bool send = GUILayout.Button(isBusy ? "⏳" : "➤", GUILayout.Width(42), GUILayout.Height(40));
            GUI.enabled = true;
            if (send || (Event.current.type == EventType.KeyDown &&
                         Event.current.keyCode == KeyCode.Return &&
                         !string.IsNullOrEmpty(userInput) && !isBusy))
            { SendMessage(); Event.current.Use(); }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🗑 Чат", GUILayout.Height(20)))
            { history.Clear(); statusMsg = ""; }
            if (!scanRunning && GUILayout.Button("🔄 Скан проекта", GUILayout.Height(20)))
            { scanDone = false; EditorCoroutine.Start(ScanProjectRoutine()); }
            GUILayout.EndHorizontal();
        }

        private void DrawBubble(ChatMsg msg)
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);

            // ── Пузырь ожидания ───────────────────────────────────────
            if (msg.isPending)
            {
                double elapsed = EditorApplication.timeSinceStartup - msg.startTime;
                string[] dots  = new string[] { "●○○", "○●○", "○○●" };
                string   anim  = dots[(int)(elapsed * 2) % 3];
                GUI.color = new Color(0.5f, 0.9f, 1f);
                GUILayout.Label("🤖 AI:", EditorStyles.boldLabel);
                GUI.color = Color.white;
                string elapsedStr = elapsed < 60
                    ? ((int)elapsed).ToString() + "с"
                    : ((int)(elapsed / 60)).ToString() + "м " + ((int)(elapsed % 60)).ToString() + "с";
                GUILayout.Label(anim + "  Думаю...  (" + elapsedStr + ")", EditorStyles.wordWrappedLabel);
                GUILayout.EndVertical();
                return;
            }

            GUILayout.Label(msg.isUser ? "👤 Ты:" : "🤖 AI:", EditorStyles.boldLabel);
            var ws = new GUIStyle(EditorStyles.wordWrappedLabel); ws.wordWrap = true;
            GUILayout.Label(msg.text, ws);

            if (!string.IsNullOrEmpty(msg.code))
            {
                GUILayout.Space(3);
                GUILayout.Label("📋 Код для применения:", EditorStyles.boldLabel);
                var cs = new GUIStyle(EditorStyles.textArea); cs.wordWrap = false; cs.fontSize = 10;
                GUILayout.TextArea(msg.code, cs, GUILayout.MaxHeight(150));
                GUILayout.BeginHorizontal();

                // Auto-apply button
                if (GUILayout.Button("⚡ Применить к сцене", GUILayout.Height(28)))
                {
                    if (!autoApply)
                    {
                        if (EditorUtility.DisplayDialog("Применить изменения?",
                            "AI создаст Editor-скрипт который будет выполнен при компиляции Unity.\n\nЭто изменит текущую сцену " + ctxScene + ".",
                            "Применить", "Отмена"))
                            AutoApplyCode(msg.code);
                    }
                    else AutoApplyCode(msg.code);
                }

                // Write to selected file
                if (selectedFile != null && selectedFile.isText &&
                    GUILayout.Button("💾 В файл", GUILayout.Height(28)))
                {
                    if (EditorUtility.DisplayDialog("Записать?", selectedFile.assetPath, "Да", "Нет"))
                        WriteFile(selectedFile.assetPath, msg.code);
                }
                // Push to GitHub
                if (!ghBusy && GUILayout.Button("🐙 Push", GUILayout.Height(28)))
                {
                    string name = "AliTerra_Edit_" + DateTime.Now.ToString("yyyyMMdd");
                    GitHubPushFile("Assets/Editor/" + name + ".cs", msg.code, "AI: " + name);
                    activeTab = 2;
                }
                if (GUILayout.Button("📋 Копия", GUILayout.Height(28)))
                    EditorGUIUtility.systemCopyBuffer = msg.code;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            GUILayout.Space(2);
        }

        private void QuickSend(string q) { userInput = q; SendMessage(); }

        // ── Files tab ──────────────────────────────────────────────────
        private void DrawFilesTab()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Файлов: " + allFiles.Count, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("🔄", GUILayout.Width(26), GUILayout.Height(18)))
            { scanDone = false; EditorCoroutine.Start(ScanProjectRoutine()); }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            bool prev = showAllTypes;
            showAllTypes = GUILayout.Toggle(showAllTypes, "Все", EditorStyles.miniButton, GUILayout.Width(36));
            if (showAllTypes != prev) browserFilter = FileCategory.Other;
            foreach (FileCategory cat in Enum.GetValues(typeof(FileCategory)))
            {
                bool sel = !showAllTypes && browserFilter == cat;
                bool ns  = GUILayout.Toggle(sel, cat.ToString(), EditorStyles.miniButton);
                if (ns && !sel) { browserFilter = cat; showAllTypes = false; }
            }
            GUILayout.EndHorizontal();
            browserSearch = EditorGUILayout.TextField("🔍", browserSearch);
            GUILayout.Space(2);

            browserScroll = EditorGUILayout.BeginScrollView(browserScroll);
            string search = browserSearch.ToLowerInvariant();
            foreach (var fe in allFiles)
            {
                if (!showAllTypes && fe.category != browserFilter) continue;
                if (!string.IsNullOrEmpty(search) && !fe.fileName.ToLowerInvariant().Contains(search)) continue;
                if (fe.assetPath.Contains("/.") || fe.assetPath.Contains("\\.")) continue;
                bool isSelected = selectedFile != null && selectedFile.assetPath == fe.assetPath;
                GUILayout.BeginHorizontal();
                GUI.color = GetCatColor(fe.category);
                if (GUILayout.Button((fe.isText ? "📄 " : "🔒 ") + fe.assetPath,
                    isSelected ? EditorStyles.boldLabel : EditorStyles.miniLabel, GUILayout.ExpandWidth(true)))
                { SelectFile(fe.assetPath); activeTab = 0; }
                GUI.color = Color.white;
                if (!ghBusy && fe.isText && GUILayout.Button("🐙", EditorStyles.miniButton, GUILayout.Width(22)))
                {
                    string fc = ""; try { fc = File.ReadAllText(fe.fullPath); } catch { }
                    GitHubPushFile(fe.assetPath, fc, "push: " + fe.fileName);
                    activeTab = 2;
                }
                GUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            if (selectedFile != null && selectedFile.isText)
            {
                GUILayout.Space(4);
                GUILayout.Label(selectedFile.fileName, EditorStyles.boldLabel);
                var cs = new GUIStyle(EditorStyles.textArea); cs.wordWrap = false; cs.fontSize = 10;
                string pv = fileContent.Length > 2000 ? fileContent.Substring(0, 2000) + "\n..." : fileContent;
                GUILayout.TextArea(pv, cs, GUILayout.Height(120));
            }
        }

        private Color GetCatColor(FileCategory cat)
        {
            switch (cat)
            {
                case FileCategory.Script:   return new Color(0.6f, 1f, 0.6f);
                case FileCategory.Scene:    return new Color(1f, 0.9f, 0.4f);
                case FileCategory.Prefab:   return new Color(0.5f, 0.8f, 1f);
                case FileCategory.Material: return new Color(1f, 0.6f, 0.4f);
                case FileCategory.Shader:   return new Color(0.9f, 0.5f, 1f);
                default:                    return Color.white;
            }
        }

        // ── GitHub tab ─────────────────────────────────────────────────
        private Vector2 ghScroll;
        private void DrawGitHubTab()
        {
            ghScroll = EditorGUILayout.BeginScrollView(ghScroll);
            GUILayout.Label("GitHub настройки", EditorStyles.boldLabel);
            GUILayout.BeginVertical(EditorStyles.helpBox);
            ghOwner  = EditorGUILayout.TextField("Owner",  ghOwner);
            ghRepo   = EditorGUILayout.TextField("Repo",   ghRepo);
            ghBranch = EditorGUILayout.TextField("Branch", ghBranch);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Token", GUILayout.Width(50));
            ghToken  = EditorGUILayout.PasswordField(ghToken);
            GUILayout.EndHorizontal();
            GUILayout.Label("Токен хранится только в EditorPrefs — не передаётся на сервер", EditorStyles.miniLabel);
            if (GUILayout.Button("💾 Сохранить", GUILayout.Height(26))) { SaveSettings(); ghStatus = "✅ Сохранено"; }
            GUILayout.EndVertical();

            if (!string.IsNullOrEmpty(ghStatus))
                GUILayout.Label(ghStatus, EditorStyles.helpBox);

            GUILayout.Space(6);
            GUILayout.Label("Действия с файлами", EditorStyles.boldLabel);
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.enabled = !ghBusy && !string.IsNullOrEmpty(ghToken);
            if (selectedFile != null && selectedFile.isText)
                if (GUILayout.Button("🐙 Push: " + selectedFile.fileName, GUILayout.Height(28)))
                    GitHubPushFile(selectedFile.assetPath, fileContent, "edit: " + selectedFile.fileName);
            GUI.enabled = true;
            GUILayout.Space(4);
            GUILayout.Label("Git CLI", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("status", GUILayout.Height(24)))  RunGit("status");
            if (GUILayout.Button("add -A", GUILayout.Height(24)))  RunGit("add -A");
            if (GUILayout.Button("pull",   GUILayout.Height(24)))  RunGit("pull");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("commit -m \"AI\"", GUILayout.Height(24))) RunGit("commit -m \"AliTerra AI changes\"");
            if (GUILayout.Button("push",             GUILayout.Height(24))) RunGit("push");
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            if (!string.IsNullOrEmpty(gitLog))
            {
                var cs = new GUIStyle(EditorStyles.textArea); cs.wordWrap = false; cs.fontSize = 10;
                GUILayout.TextArea(gitLog, cs, GUILayout.Height(80));
            }
            EditorGUILayout.EndScrollView();
        }

        // ── Debug tab ──────────────────────────────────────────────────
        private void DrawDebugTab()
        {
            GUILayout.Label("Сцена: " + ctxScene + "  |  Объект: " + (ctxObject.Length > 0 ? ctxObject : "—"), EditorStyles.miniLabel);
            GUILayout.Label("Авто-режим: " + autoApply, EditorStyles.miniLabel);
            GUILayout.Label("Скриптов: " + scriptIndex.Count + "  |  Файлов: " + allFiles.Count, EditorStyles.miniLabel);
            GUILayout.Space(4);
            GUILayout.Label("Иерархия сцены:", EditorStyles.boldLabel);
            var cs = new GUIStyle(EditorStyles.textArea); cs.wordWrap = false; cs.fontSize = 9;
            string h = sceneHierarchy.Length > 0 ? sceneHierarchy.Substring(0, Math.Min(3000, sceneHierarchy.Length)) : "(нет — откройте сцену и нажмите 🔄)";
            GUILayout.TextArea(h, cs, GUILayout.Height(200));
            GUILayout.Space(4);
            GUILayout.Label("Индекс проекта:", EditorStyles.boldLabel);
            string sm = string.IsNullOrEmpty(projectSummary) ? "(сканирование...)" : projectSummary.Substring(0, Math.Min(2000, projectSummary.Length));
            GUILayout.TextArea(sm, cs, GUILayout.Height(140));
        }
    }

    // ── EditorCoroutine ───────────────────────────────────────────────
    public class EditorCoroutine
    {
        private IEnumerator routine;
        private object       current;

        private EditorCoroutine(IEnumerator r) { routine = r; }

        public static EditorCoroutine Start(IEnumerator routine)
        {
            var c = new EditorCoroutine(routine);
            if (!c.routine.MoveNext()) return c;
            c.current = c.routine.Current;
            EditorApplication.update += c.Tick;
            return c;
        }

        private void Tick()
        {
            var op = current as AsyncOperation;
            if (op != null && !op.isDone) return;
            if (!routine.MoveNext()) { EditorApplication.update -= Tick; return; }
            current = routine.Current;
        }
    }
}
