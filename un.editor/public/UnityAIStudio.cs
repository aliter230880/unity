// ╔══════════════════════════════════════════════════════════════════════════╗
// ║  Unity AI Studio Pro v1.1                                              ║
// ║  Menu: Window -> AI Studio Pro                                         ║
// ╚══════════════════════════════════════════════════════════════════════════╝
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace UnityAIStudio
{
    public enum AIProvider { Gemini, DeepSeek, Groq }
    public enum FileCategory { Script, Scene, Prefab, Material, Shader, Texture, Audio, Model, Animation, Config, Font, Video, Other }
    
    [Serializable]
    public class FileEntry
    {
        public string fullPath = "";
        public string assetPath = "";
        public string fileName = "";
        public string ext = "";
        public FileCategory category;
        public bool isText = false;
        public long sizeBytes = 0;
        public string guid = "";
        public List<string> dependencies = new List<string>();
    }
    
    [Serializable]
    public class ScriptInfo
    {
        public string path = "";
        public string className = "";
        public string baseClass = "";
        public string nameSpace = "";
        public int lineCount = 0;
        public List<string> methods = new List<string>();
        public string content = "";
        public bool isMonoBehaviour = false;
        public bool isEditor = false;
    }
    
    [Serializable]
    public class ChatMsg
    {
        public bool isUser = false;
        public string text = "";
        public string code = "";
        public bool isPending = false;
        public double startTime = 0;
        public AIProvider provider;
    }
    
    [Serializable]
    public class SceneObject
    {
        public string name = "";
        public string path = "";
        public Vector3 position;
        public List<string> components = new List<string>();
        public bool isActive = true;
        public int childCount = 0;
    }
    
    public class UnityAIStudioWindow : EditorWindow
    {
        // ═══════════════════════════════════════════════════════════════
        // API KEYS
        // ═══════════════════════════════════════════════════════════════
        private const string GEMINI_API_KEY = "ВСТАВЬ_КЛЮЧ_GEMINI";
        private const string GEMINI_MODEL = "gemini-2.5-flash";
        private const string GEMINI_ENDPOINT = "https://generativelanguage.googleapis.com/v1beta/models";
        
        private const string DEEPSEEK_API_KEY = "ВСТАВЬ_КЛЮЧ_DEEPSEEK";
        private const string DEEPSEEK_MODEL = "deepseek-chat";
        private const string DEEPSEEK_ENDPOINT = "https://api.deepseek.com/chat/completions";
        
        private const string GROQ_API_KEY = "ВСТАВЬ_КЛЮЧ_GROQ";
        private const string GROQ_MODEL = "llama-3.3-70b-versatile";
        private const string GROQ_ENDPOINT = "https://api.groq.com/openai/v1/chat/completions";
        // ═══════════════════════════════════════════════════════════════
        
        private AIProvider currentProvider = AIProvider.Groq;
        private int maxTokens = 4096;
        
        private const string PREF_PROVIDER = "UnityAI_Provider";
        private const string PREF_AUTO = "UnityAI_AutoApply";
        private const string PREF_GH_TOKEN = "UnityAI_GH_Token";
        private const string PREF_GH_OWNER = "UnityAI_GH_Owner";
        private const string PREF_GH_REPO = "UnityAI_GH_Repo";
        private const string PREF_GH_BRANCH = "UnityAI_GH_Branch";
        
        private int mainTab = 0;
        private bool isBusy = false;
        private string statusMsg = "";
        private Vector2 mainScroll;
        
        private List<ChatMsg> history = new List<ChatMsg>();
        private string userInput = "";
        private Vector2 chatScroll;
        private int pendingIndex = -1;
        private int retryCount = 0;
        
        private bool autoApply = false;
        
        private FileEntry selectedFile = null;
        private string fileContent = "";
        private string ctxScene = "";
        private string ctxObject = "";
        private string sceneHierarchy = "";
        private string lastScannedScene = "";
        
        private List<FileEntry> allFiles = new List<FileEntry>();
        private List<ScriptInfo> scriptIndex = new List<ScriptInfo>();
        private string projectSummary = "";
        private bool scanRunning = false;
        private bool scanDone = false;
        private int scanProgress = 0;
        private int scanTotal = 0;
        
        private Vector2 browserScroll;
        private string browserSearch = "";
        private FileCategory browserFilter = FileCategory.Script;
        private bool showAllTypes = false;
        
        private string codeEditor = "";
        private string codeEditorPath = "";
        private Vector2 codeScroll;
        private bool codeModified = false;
        
        private string globalSearch = "";
        private List<string> searchResults = new List<string>();
        
        private string ghToken = "";
        private string ghOwner = "";
        private string ghRepo = "";
        private string ghBranch = "main";
        private string ghStatus = "";
        private bool ghBusy = false;
        private double lastPush = 0;
        
        private List<SceneObject> sceneObjects = new List<SceneObject>();
        
        private static readonly Dictionary<string, FileCategory> CatMap = new Dictionary<string, FileCategory>
        {
            {".cs", FileCategory.Script}, {".js", FileCategory.Script},
            {".unity", FileCategory.Scene}, {".prefab", FileCategory.Prefab},
            {".mat", FileCategory.Material}, {".shader", FileCategory.Shader},
            {".png", FileCategory.Texture}, {".jpg", FileCategory.Texture}, {".jpeg", FileCategory.Texture},
            {".wav", FileCategory.Audio}, {".mp3", FileCategory.Audio}, {".ogg", FileCategory.Audio},
            {".fbx", FileCategory.Model}, {".obj", FileCategory.Model}, {".glb", FileCategory.Model},
            {".anim", FileCategory.Animation}, {".controller", FileCategory.Animation},
            {".json", FileCategory.Config}, {".xml", FileCategory.Config}
        };
        
        private static readonly HashSet<string> TextExts = new HashSet<string>
        {
            ".cs", ".js", ".unity", ".prefab", ".mat", ".shader", ".hlsl",
            ".json", ".xml", ".yaml", ".yml", ".txt", ".md", ".asmdef"
        };

        [MenuItem("Window/AI Studio Pro")]
        public static void ShowWindow()
        {
            var w = GetWindow<UnityAIStudioWindow>("AI Studio Pro");
            w.minSize = new Vector2(520, 650);
        }
        
        private void OnEnable()
        {
            LoadSettings();
            EditorApplication.update += OnTick;
            EditorCoroutine.Start(ScanProjectRoutine());
            
            history.Add(new ChatMsg
            {
                isUser = false,
                text = "Привет! Я Unity AI ассистент.\n\n" +
                       "Просто напишите что нужно сделать, например:\n" +
                       "• \"Добавь SpawnPoint в сцену\"\n" +
                       "• \"Скрипт для движения игрока\"\n" +
                       "• \"Как сделать прыжок?\"\n\n" +
                       "Статус провайдеров:\n" +
                       "• Gemini: " + (HasValidGeminiKey() ? "✅" : "❌") + "\n" +
                       "• DeepSeek: " + (HasValidDeepSeekKey() ? "✅" : "❌") + "\n" +
                       "• Groq: " + (HasValidGroqKey() ? "✅" : "❌"),
                provider = currentProvider
            });
        }
        
        private void OnDisable() { EditorApplication.update -= OnTick; }
        
        private void OnTick()
        {
            RefreshContext();
            if (isBusy) Repaint();
        }
        
        private void LoadSettings()
        {
            ghToken = EditorPrefs.GetString(PREF_GH_TOKEN, "");
            ghOwner = EditorPrefs.GetString(PREF_GH_OWNER, "");
            ghRepo = EditorPrefs.GetString(PREF_GH_REPO, "");
            ghBranch = EditorPrefs.GetString(PREF_GH_BRANCH, "main");
            autoApply = EditorPrefs.GetBool(PREF_AUTO, false);
            currentProvider = (AIProvider)EditorPrefs.GetInt(PREF_PROVIDER, 2);
        }
        
        private void SaveSettings()
        {
            EditorPrefs.SetString(PREF_GH_TOKEN, ghToken);
            EditorPrefs.SetString(PREF_GH_OWNER, ghOwner);
            EditorPrefs.SetString(PREF_GH_REPO, ghRepo);
            EditorPrefs.SetString(PREF_GH_BRANCH, ghBranch);
            EditorPrefs.SetBool(PREF_AUTO, autoApply);
            EditorPrefs.SetInt(PREF_PROVIDER, (int)currentProvider);
        }
        
        private bool HasValidGeminiKey() { return !string.IsNullOrEmpty(GEMINI_API_KEY) && GEMINI_API_KEY.Length > 10; }
        private bool HasValidDeepSeekKey() { return !string.IsNullOrEmpty(DEEPSEEK_API_KEY) && DEEPSEEK_API_KEY.Length > 10; }
        private bool HasValidGroqKey() { return !string.IsNullOrEmpty(GROQ_API_KEY) && GROQ_API_KEY.Length > 10; }
        
        private bool HasValidKey()
        {
            switch (currentProvider)
            {
                case AIProvider.Gemini: return HasValidGeminiKey();
                case AIProvider.DeepSeek: return HasValidDeepSeekKey();
                case AIProvider.Groq: return HasValidGroqKey();
                default: return false;
            }
        }
        
        private string GetApiKey()
        {
            switch (currentProvider)
            {
                case AIProvider.Gemini: return GEMINI_API_KEY;
                case AIProvider.DeepSeek: return DEEPSEEK_API_KEY;
                case AIProvider.Groq: return GROQ_API_KEY;
                default: return "";
            }
        }
        
        private string GetModel()
        {
            switch (currentProvider)
            {
                case AIProvider.Gemini: return GEMINI_MODEL;
                case AIProvider.DeepSeek: return DEEPSEEK_MODEL;
                case AIProvider.Groq: return GROQ_MODEL;
                default: return "";
            }
        }
        
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
            
            if (ctxScene != lastScannedScene)
            {
                lastScannedScene = ctxScene;
                sceneHierarchy = ScanSceneHierarchy();
                ScanSceneObjects();
            }
        }
        
        private void SelectFile(string assetPath)
        {
            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            if (!File.Exists(fullPath)) return;
            
            var fe = new FileEntry();
            fe.assetPath = assetPath;
            fe.fullPath = fullPath;
            fe.fileName = Path.GetFileName(assetPath);
            fe.ext = Path.GetExtension(assetPath).ToLowerInvariant();
            fe.isText = TextExts.Contains(fe.ext);
            fe.category = CatMap.ContainsKey(fe.ext) ? CatMap[fe.ext] : FileCategory.Other;
            fe.guid = AssetDatabase.AssetPathToGUID(assetPath);
            try { fe.sizeBytes = new FileInfo(fullPath).Length; } catch { }
            try { fe.dependencies = new List<string>(AssetDatabase.GetDependencies(assetPath, false)); } catch { }
            
            selectedFile = fe;
            fileContent = "";
            if (fe.isText) try { fileContent = File.ReadAllText(fullPath); } catch { }
            
            if (fe.isText && fe.category == FileCategory.Script)
            {
                codeEditor = fileContent;
                codeEditorPath = assetPath;
                codeModified = false;
            }
            Repaint();
        }
        
        private string ScanSceneHierarchy()
        {
            try
            {
                var scene = EditorSceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(scene.name)) return "";
                
                var sb = new StringBuilder();
                sb.AppendLine("[HIERARCHY: " + scene.name + " | objects: " + scene.rootCount + "]");
                
                var roots = scene.GetRootGameObjects();
                int count = 0;
                foreach (var root in roots)
                {
                    AppendGO(sb, root, 0, ref count);
                    if (count > 300 || sb.Length > 12000)
                    {
                        sb.AppendLine("... [truncated]");
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
            sb.Append(pad + go.name + " [" + p.x.ToString("F1") + "," + p.y.ToString("F1") + "," + p.z.ToString("F1") + "]");
            if (compNames.Length > 0) sb.Append(" {" + compNames + "}");
            if (!go.activeSelf) sb.Append(" [OFF]");
            sb.AppendLine();
            
            for (int i = 0; i < go.transform.childCount; i++)
                AppendGO(sb, go.transform.GetChild(i).gameObject, depth + 1, ref count);
        }
        
        private void ScanSceneObjects()
        {
            sceneObjects.Clear();
            try
            {
                var scene = EditorSceneManager.GetActiveScene();
                if (!scene.IsValid()) return;
                foreach (var go in scene.GetRootGameObjects())
                    CollectSceneObjects(go, "");
            }
            catch { }
        }
        
        private void CollectSceneObjects(GameObject go, string parentPath)
        {
            string path = string.IsNullOrEmpty(parentPath) ? go.name : parentPath + "/" + go.name;
            var so = new SceneObject();
            so.name = go.name;
            so.path = path;
            so.position = go.transform.position;
            so.isActive = go.activeSelf;
            so.childCount = go.transform.childCount;
            foreach (var c in go.GetComponents<Component>())
                if (c != null && !(c is Transform))
                    so.components.Add(c.GetType().Name);
            sceneObjects.Add(so);
            for (int i = 0; i < go.transform.childCount; i++)
                CollectSceneObjects(go.transform.GetChild(i).gameObject, path);
        }
        
        private IEnumerator ScanProjectRoutine()
        {
            scanRunning = true;
            scanDone = false;
            allFiles.Clear();
            scriptIndex.Clear();
            
            string[] guids = AssetDatabase.FindAssets("");
            var paths = new HashSet<string>();
            foreach (string g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (p.StartsWith("Assets/") && !p.EndsWith(".meta"))
                    paths.Add(p);
            }
            
            var filtered = new List<string>(paths);
            scanTotal = filtered.Count;
            scanProgress = 0;
            
            for (int i = 0; i < filtered.Count; i++)
            {
                string p = filtered[i];
                scanProgress = i + 1;
                
                var fe = new FileEntry();
                fe.assetPath = p;
                fe.fileName = Path.GetFileName(p);
                fe.ext = Path.GetExtension(p).ToLowerInvariant();
                fe.isText = TextExts.Contains(fe.ext);
                fe.category = CatMap.ContainsKey(fe.ext) ? CatMap[fe.ext] : FileCategory.Other;
                try { fe.sizeBytes = new FileInfo(Path.GetFullPath(Path.Combine(Application.dataPath, "..", p))).Length; } catch { }
                
                allFiles.Add(fe);
                
                if (fe.category == FileCategory.Script && fe.ext == ".cs")
                {
                    try
                    {
                        string content = File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", p)));
                        var info = new ScriptInfo();
                        info.path = p;
                        info.content = content;
                        var m = Regex.Match(content, @"\bclass\s+(\w+)");
                        info.className = m.Success ? m.Groups[1].Value : "";
                        m = Regex.Match(content, @"\bclass\s+\w+\s*:\s*(\w+)");
                        info.baseClass = m.Success ? m.Groups[1].Value : "";
                        m = Regex.Match(content, @"namespace\s+([\w.]+)");
                        info.nameSpace = m.Success ? m.Groups[1].Value : "";
                        info.lineCount = content.Split('\n').Length;
                        info.isMonoBehaviour = content.Contains("MonoBehaviour");
                        info.isEditor = p.Contains("/Editor/");
                        var methods = Regex.Matches(content, @"(?:public|protected)\s+(?:override\s+|static\s+)*(?:\w[\w\[\]]*\s+)+(\w+)\s*\(");
                        foreach (Match mm in methods)
                            if (!info.methods.Contains(mm.Groups[1].Value))
                                info.methods.Add(mm.Groups[1].Value);
                        scriptIndex.Add(info);
                    }
                    catch { }
                }
                
                if (i % 100 == 0) { yield return null; Repaint(); }
            }
            
            projectSummary = "PROJECT: " + allFiles.Count + " files, " + scriptIndex.Count + " scripts";
            scanRunning = false;
            scanDone = true;
            Repaint();
        }
        
        private string GetSystemPrompt()
        {
            return @"Ты дружелюбный Unity ассистент. Отвечай простым языком, как будто объясняешь другу.

ВАЖНЫЕ ПРАВИЛА:
1. ВСЕГДА отвечай на русском языке
2. СНАЧАЛА объясни простыми словами что будешь делать и зачем
3. ПОТОМ дай код (если нужен)
4. НЕ используй сложные термины без объяснения
5. Пиши короткими предложениями
6. Используй эмодзи для наглядности ✨

Если просят код:
- Объясни что делает каждый важный кусок
- Укажи куда вставлять код
- Напиши как протестировать

Если просят объяснить:
- Не давай код если не просят
- Используй аналогии из жизни
- Давай пошаговые инструкции

Ты работаешь с Unity 2019+, C# 7.3.";
        }
        
        private void SendMessage()
        {
            if (string.IsNullOrEmpty(userInput) || isBusy) return;
            if (!HasValidKey())
            {
                statusMsg = "Нет API ключа! Проверь настройки.";
                Debug.LogError("[UnityAI] No API key for " + currentProvider);
                return;
            }
            
            history.Add(new ChatMsg { isUser = true, text = userInput });
            history.Add(new ChatMsg { isUser = false, isPending = true, startTime = EditorApplication.timeSinceStartup, provider = currentProvider });
            pendingIndex = history.Count - 1;
            
            string text = userInput;
            userInput = "";
            isBusy = true;
            retryCount = 0;
            
            string contextInfo = "";
            if (!string.IsNullOrEmpty(ctxScene))
                contextInfo += "Сцена: " + ctxScene + "\n";
            if (!string.IsNullOrEmpty(ctxObject))
                contextInfo += "Выбран объект: " + ctxObject + "\n";
            if (selectedFile != null)
            {
                contextInfo += "Файл: " + selectedFile.fileName + "\n";
                if (selectedFile.isText && fileContent.Length > 0)
                    contextInfo += "Содержимое:\n" + fileContent.Substring(0, Math.Min(3000, fileContent.Length)) + "\n";
            }
            
            string fullMessage = string.IsNullOrEmpty(contextInfo) ? text : contextInfo + "\n---\nВопрос: " + text;
            
            EditorCoroutine.Start(SendToAI(GetSystemPrompt(), fullMessage, OnAIResponse));
        }
        
        private void QuickSend(string text) { userInput = text; SendMessage(); }
        
        private void OnAIResponse(string responseText, string error)
        {
            isBusy = false;
            
            if (pendingIndex >= 0 && pendingIndex < history.Count)
                history[pendingIndex].isPending = false;
            
            if (!string.IsNullOrEmpty(error))
            {
                statusMsg = "Ошибка: " + error;
                Debug.LogError("[UnityAI] " + error);
                pendingIndex = -1;
                Repaint();
                return;
            }
            
            if (string.IsNullOrEmpty(responseText))
            {
                statusMsg = "Пустой ответ от AI";
                if (retryCount < 1)
                {
                    retryCount++;
                    statusMsg = "Повторная попытка...";
                    EditorCoroutine.Start(SendToAI(GetSystemPrompt(), "Повтори ответ", OnAIResponse));
                }
                pendingIndex = -1;
                Repaint();
                return;
            }
            
            string code = ExtractCode(responseText);
            
            if (pendingIndex >= 0 && pendingIndex < history.Count)
            {
                history[pendingIndex].text = responseText;
                history[pendingIndex].code = code;
            }
            
            pendingIndex = -1;
            
            if (!string.IsNullOrEmpty(code) && autoApply)
                AutoApplyCode(code);
            
            Repaint();
        }
        
        [Serializable] private class GeminiRequest { public GeminiContent[] contents; public GeminiConfig generationConfig; }
        [Serializable] private class GeminiContent { public GeminiPart[] parts; }
        [Serializable] private class GeminiPart { public string text; }
        [Serializable] private class GeminiConfig { public int maxOutputTokens; }
        
        [Serializable] private class OpenAIRequest { public string model; public OpenAIMessage[] messages; public int max_tokens; }
        [Serializable] private class OpenAIMessage { public string role; public string content; }
        
        private IEnumerator SendToAI(string systemPrompt, string userMessage, Action<string, string> callback)
        {
            string url = "";
            string apiKey = GetApiKey();
            string model = GetModel();
            string requestBody = "";
            string errorMsg = "";
            
            Debug.Log("[UnityAI] Provider: " + currentProvider + " Model: " + model);
            
            if (currentProvider == AIProvider.Gemini)
            {
                url = GEMINI_ENDPOINT + "/" + model + ":generateContent?key=" + apiKey;
                requestBody = JsonUtility.ToJson(new GeminiRequest
                {
                    contents = new[] { new GeminiContent { parts = new[] { new GeminiPart { text = systemPrompt + "\n\n" + userMessage } } } },
                    generationConfig = new GeminiConfig { maxOutputTokens = maxTokens }
                });
            }
            else
            {
                url = currentProvider == AIProvider.DeepSeek ? DEEPSEEK_ENDPOINT : GROQ_ENDPOINT;
                requestBody = JsonUtility.ToJson(new OpenAIRequest
                {
                    model = model,
                    messages = new[]
                    {
                        new OpenAIMessage { role = "system", content = systemPrompt },
                        new OpenAIMessage { role = "user", content = userMessage }
                    },
                    max_tokens = maxTokens
                });
            }
            
            Debug.Log("[UnityAI] URL: " + url);
            
            byte[] body = Encoding.UTF8.GetBytes(requestBody);
            var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 120;
            
            if (currentProvider != AIProvider.Gemini)
                req.SetRequestHeader("Authorization", "Bearer " + apiKey);
            
            yield return req.SendWebRequest();
            
            string result = "";
            if (req.result == UnityWebRequest.Result.Success)
            {
                result = req.downloadHandler.text;
                Debug.Log("[UnityAI] Response OK, length: " + result.Length);
            }
            else
            {
                errorMsg = "HTTP " + req.responseCode + ": " + req.error;
                if (!string.IsNullOrEmpty(req.downloadHandler.text))
                    errorMsg += "\n" + req.downloadHandler.text;
                Debug.LogError("[UnityAI] " + errorMsg);
            }
            
            req.Dispose();
            callback?.Invoke(result, errorMsg);
        }
        
        private void AutoApplyCode(string code)
        {
            try
            {
                string path = "Assets/Editor/AIStudio_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".cs";
                File.WriteAllText(path, code);
                AssetDatabase.Refresh();
                statusMsg = "Код применён: " + path;
            }
            catch (Exception e) { statusMsg = "Ошибка: " + e.Message; }
        }
        
        private void SaveCodeFromEditor()
        {
            if (string.IsNullOrEmpty(codeEditorPath) || string.IsNullOrEmpty(codeEditor)) return;
            try
            {
                File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", codeEditorPath)), codeEditor);
                AssetDatabase.Refresh();
                codeModified = false;
                statusMsg = "Сохранено: " + codeEditorPath;
            }
            catch (Exception e) { statusMsg = "Ошибка: " + e.Message; }
        }
        
        private void PerformGlobalSearch(string query)
        {
            searchResults.Clear();
            if (string.IsNullOrEmpty(query) || query.Length < 2) return;
            string q = query.ToLowerInvariant();
            
            foreach (var fe in allFiles)
                if (fe.fileName.ToLowerInvariant().Contains(q))
                    searchResults.Add(fe.assetPath);
            
            foreach (var s in scriptIndex)
                if (s.className.ToLowerInvariant().Contains(q) || s.content.ToLowerInvariant().Contains(q))
                    searchResults.Add(s.className + " (" + s.path + ")");
        }
        
        private static string ExtractCode(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            
            string[] markers = { "csharp", "c#", "C#", "cs" };
            foreach (var marker in markers)
            {
                string start = "```" + marker;
                int sIdx = text.IndexOf(start, StringComparison.OrdinalIgnoreCase);
                if (sIdx < 0) continue;
                sIdx += start.Length;
                if (sIdx < text.Length && text[sIdx] == '\n') sIdx++;
                int eIdx = text.IndexOf("```", sIdx);
                if (eIdx > sIdx)
                    return text.Substring(sIdx, eIdx - sIdx).Trim();
            }
            
            int anyStart = text.IndexOf("```");
            if (anyStart >= 0)
            {
                anyStart += 3;
                int lineEnd = text.IndexOf('\n', anyStart);
                if (lineEnd > 0) anyStart = lineEnd + 1;
                int anyEnd = text.IndexOf("```", anyStart);
                if (anyEnd > anyStart)
                    return text.Substring(anyStart, anyEnd - anyStart).Trim();
            }
            
            return "";
        }
        
        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1) { order++; size /= 1024; }
            return size.ToString("0.##") + " " + sizes[order];
        }
        
        // ═══════════════════════════════════════════════════════════════
        // GUI
        // ═══════════════════════════════════════════════════════════════
        
        private void OnGUI()
        {
            DrawToolbar();
            
            string[] tabs = { "Чат", "Файлы", "Поиск", "Редактор", "GitHub", "Настройки" };
            mainTab = GUILayout.Toolbar(mainTab, tabs);
            GUILayout.Space(4);
            
            switch (mainTab)
            {
                case 0: DrawChatTab(); break;
                case 1: DrawFilesTab(); break;
                case 2: DrawSearchTab(); break;
                case 3: DrawCodeEditorTab(); break;
                case 4: DrawGitHubTab(); break;
                case 5: DrawSettingsTab(); break;
            }
        }
        
        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("AI Studio", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            AIProvider newProvider = (AIProvider)EditorGUILayout.EnumPopup(currentProvider, GUILayout.Width(100));
            if (newProvider != currentProvider) { currentProvider = newProvider; SaveSettings(); }
            
            GUILayout.FlexibleSpace();
            
            bool newAuto = GUILayout.Toggle(autoApply, " Авто", EditorStyles.miniButton, GUILayout.Width(50));
            if (newAuto != autoApply) { autoApply = newAuto; SaveSettings(); }
            
            GUI.color = HasValidKey() ? Color.green : Color.red;
            GUILayout.Label(HasValidKey() ? "✓" : "✗", EditorStyles.miniLabel);
            GUI.color = Color.white;
            
            if (scanRunning)
            {
                GUI.color = Color.yellow;
                GUILayout.Label(scanProgress + "/" + scanTotal, EditorStyles.miniLabel);
                GUI.color = Color.white;
                Repaint();
            }
            else if (scanDone)
            {
                GUI.color = Color.green;
                GUILayout.Label(allFiles.Count + " файлов", EditorStyles.miniLabel);
                GUI.color = Color.white;
            }
            
            GUILayout.EndHorizontal();
            
            if (!string.IsNullOrEmpty(ctxScene))
            {
                GUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label("🌍 " + ctxScene, EditorStyles.boldLabel);
                if (!string.IsNullOrEmpty(ctxObject)) GUILayout.Label("→ " + ctxObject, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            
            if (!string.IsNullOrEmpty(statusMsg))
            {
                var style = new GUIStyle(EditorStyles.helpBox);
                style.wordWrap = true;
                GUILayout.Label(statusMsg, style);
            }
        }
        
        private void DrawChatTab()
        {
            // Quick buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("📋 Что в сцене?", GUILayout.Height(24)))
                QuickSend("Что находится в текущей сцене? Опиши кратко.");
            if (GUILayout.Button("🐛 Найди проблемы", GUILayout.Height(24)))
                QuickSend("Есть ли проблемы в текущей сцене?");
            if (GUILayout.Button("💡 Идеи", GUILayout.Height(24))
)
                QuickSend("Предложи что можно улучшить в этой сцене.");
            GUILayout.EndHorizontal();
            
            GUILayout.Space(4);
            
            // Chat messages with word wrap
            float chatH = position.height - 200;
            chatScroll = EditorGUILayout.BeginScrollView(chatScroll, GUILayout.Height(Math.Max(100, chatH)));
            
            foreach (var msg in history)
            {
                GUILayout.BeginVertical(EditorStyles.helpBox);
                
                if (msg.isPending)
                {
                    double elapsed = EditorApplication.timeSinceStartup - msg.startTime;
                    GUI.color = new Color(0.5f, 0.9f, 1f);
                    GUILayout.Label("🤖 AI думает... (" + elapsed.ToString("F0") + "с)", EditorStyles.boldLabel);
                    GUI.color = Color.white;
                }
                else
                {
                    // Header
                    if (msg.isUser)
                    {
                        GUI.color = new Color(0.7f, 1f, 0.7f);
                        GUILayout.Label("👤 Вы:", EditorStyles.boldLabel);
                    }
                    else
                    {
                        GUI.color = new Color(0.7f, 0.9f, 1f);
                        GUILayout.Label("🤖 AI:", EditorStyles.boldLabel);
                    }
                    GUI.color = Color.white;
                    
                    // Message text with word wrap
                    var textStyle = new GUIStyle(EditorStyles.label)
                    {
                        wordWrap = true,
                        richText = false
                    };
                    
                    // Calculate height based on content
                    float textHeight = textStyle.CalcHeight(new GUIContent(msg.text), position.width - 40);
                    textHeight = Mathf.Max(40, Mathf.Min(textHeight, 500));
                    
                    GUILayout.TextArea(msg.text, textStyle, GUILayout.Height(textHeight));
                    
                    // Code section if present
                    if (!string.IsNullOrEmpty(msg.code))
                    {
                        GUILayout.Space(6);
                        
                        GUI.color = new Color(1f, 0.9f, 0.7f);
                        GUILayout.Label("📝 Код:", EditorStyles.miniLabel);
                        GUI.color = Color.white;
                        
                        var codeStyle = new GUIStyle(EditorStyles.textArea)
                        {
                            wordWrap = false,
                            font = Font.CreateDynamicFontFromOSFont("Consolas", 11),
                            fontSize = 11
                        };
                        
                        string displayCode = msg.code;
                        if (displayCode.Length > 3000)
                            displayCode = displayCode.Substring(0, 3000) + "\n... (обрезано)";
                        
                        GUILayout.TextArea(displayCode, codeStyle, GUILayout.Height(Mathf.Min(200, codeStyle.CalcHeight(new GUIContent(displayCode), position.width - 40))));
                        
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("📋 Копировать код", GUILayout.Height(24)))
                        {
                            EditorGUIUtility.systemCopyBuffer = msg.code;
                            statusMsg = "✅ Код скопирован!";
                        }
                        if (!autoApply && GUILayout.Button("⚡ Применить", GUILayout.Height(24)))
                            AutoApplyCode(msg.code);
                        if (GUILayout.Button("💾 Сохранить как файл", GUILayout.Height(24)))
                        {
                            string path = EditorUtility.SaveFilePanel("Сохранить код", "Assets", "NewScript", "cs");
                            if (!string.IsNullOrEmpty(path))
                            {
                                File.WriteAllText(path, msg.code);
                                AssetDatabase.Refresh();
                                statusMsg = "Сохранено: " + path;
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                }
                
                GUILayout.EndVertical();
            }
            
            EditorGUILayout.EndScrollView();
            
            GUILayout.Space(4);
            
            // Input area
            GUILayout.BeginHorizontal();
            GUI.enabled = !isBusy;
            
            // Text input with word wrap
            var inputStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            userInput = EditorGUILayout.TextArea(userInput, inputStyle, GUILayout.Height(50), GUILayout.ExpandWidth(true));
            
            GUI.enabled = !isBusy && !string.IsNullOrEmpty(userInput);
            bool send = GUILayout.Button(isBusy ? "⏳" : "➤", GUILayout.Width(40), GUILayout.Height(50));
            GUI.enabled = true;
            
            if (send || (Event.current.type == EventType.KeyDown && 
                         Event.current.keyCode == KeyCode.Return && 
                         !Event.current.shift &&
                         !string.IsNullOrEmpty(userInput) && !isBusy))
            {
                SendMessage();
                Event.current.Use();
            }
            GUILayout.EndHorizontal();
            
            GUILayout.Label("Enter - отправить, Shift+Enter - новая строка", EditorStyles.miniLabel);
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🗑 Очистить чат", GUILayout.Height(20)))
            {
                history.Clear();
                statusMsg = "";
            }
            if (GUILayout.Button("🔄 Скан проекта", GUILayout.Height(20)))
            {
                scanDone = false;
                EditorCoroutine.Start(ScanProjectRoutine());
            }
            GUILayout.EndHorizontal();
        }
        
        private void DrawFilesTab()
        {
            GUILayout.BeginHorizontal();
            browserSearch = EditorGUILayout.TextField("🔍 Поиск", browserSearch);
            showAllTypes = GUILayout.Toggle(showAllTypes, "Все типы", EditorStyles.miniButton, GUILayout.Width(70));
            GUILayout.EndHorizontal();
            
            browserScroll = EditorGUILayout.BeginScrollView(browserScroll);
            
            var filtered = allFiles.Where(f =>
            {
                if (!showAllTypes && f.category != browserFilter) return false;
                if (!string.IsNullOrEmpty(browserSearch) && !f.fileName.ToLowerInvariant().Contains(browserSearch.ToLowerInvariant())) return false;
                return true;
            }).ToList();
            
            GUILayout.Label("Показано: " + filtered.Count + " из " + allFiles.Count, EditorStyles.miniLabel);
            
            foreach (var fe in filtered.Take(200))
            {
                GUILayout.BeginHorizontal(EditorStyles.helpBox);
                if (GUILayout.Button(fe.fileName, EditorStyles.label))
                {
                    SelectFile(fe.assetPath);
                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(fe.assetPath);
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label(FormatBytes(fe.sizeBytes), EditorStyles.miniLabel);
                GUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
            
            if (selectedFile != null)
            {
                GUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("📄 " + selectedFile.fileName, EditorStyles.boldLabel);
                GUILayout.Label("Путь: " + selectedFile.assetPath, EditorStyles.miniLabel);
                GUILayout.Label("Тип: " + selectedFile.category + " | Размер: " + FormatBytes(selectedFile.sizeBytes), EditorStyles.miniLabel);
                if (selectedFile.dependencies.Count > 0)
                    GUILayout.Label("Зависимости: " + selectedFile.dependencies.Count, EditorStyles.miniLabel);
                GUILayout.EndVertical();
            }
        }
        
        private void DrawSearchTab()
        {
            GUILayout.BeginHorizontal();
            string newSearch = EditorGUILayout.TextField("🔎 Глобальный поиск", globalSearch);
            if (newSearch != globalSearch) { globalSearch = newSearch; PerformGlobalSearch(globalSearch); }
            if (GUILayout.Button("Искать", GUILayout.Width(60))) PerformGlobalSearch(globalSearch);
            GUILayout.EndHorizontal();
            
            mainScroll = EditorGUILayout.BeginScrollView(mainScroll);
            foreach (var r in searchResults)
            {
                if (GUILayout.Button(r, EditorStyles.label))
                {
                    string path = r.Contains("(") ? r.Split('(')[1].TrimEnd(')') : r;
                    if (path.StartsWith("Assets/"))
                        Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                }
            }
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawCodeEditorTab()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(codeEditorPath ?? "Выберите файл", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUI.enabled = codeModified;
            if (GUILayout.Button("💾 Сохранить", GUILayout.Width(80))) SaveCodeFromEditor();
            GUI.enabled = true;
            if (codeModified) { GUI.color = Color.yellow; GUILayout.Label("●", EditorStyles.boldLabel); GUI.color = Color.white; }
            GUILayout.EndHorizontal();
            
            GUI.enabled = !string.IsNullOrEmpty(codeEditorPath);
            codeScroll = EditorGUILayout.BeginScrollView(codeScroll);
            var style = new GUIStyle(EditorStyles.textArea) { wordWrap = false, fontSize = 11 };
            string newCode = EditorGUILayout.TextArea(codeEditor, style, GUILayout.ExpandHeight(true));
            if (newCode != codeEditor) { codeEditor = newCode; codeModified = true; }
            EditorGUILayout.EndScrollView();
            GUI.enabled = true;
        }
        
        private void DrawGitHubTab()
        {
            mainScroll = EditorGUILayout.BeginScrollView(mainScroll);
            GUILayout.Label("🐙 GitHub настройки", EditorStyles.boldLabel);
            GUILayout.BeginVertical(EditorStyles.helpBox);
            ghOwner = EditorGUILayout.TextField("Owner", ghOwner);
            ghRepo = EditorGUILayout.TextField("Repo", ghRepo);
            ghBranch = EditorGUILayout.TextField("Branch", ghBranch);
            ghToken = EditorGUILayout.PasswordField("Token", ghToken);
            if (GUILayout.Button("💾 Сохранить")) { SaveSettings(); ghStatus = "Сохранено"; }
            GUILayout.EndVertical();
            
            if (!string.IsNullOrEmpty(ghStatus)) GUILayout.Label(ghStatus, EditorStyles.helpBox);
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawSettingsTab()
        {
            mainScroll = EditorGUILayout.BeginScrollView(mainScroll);
            GUILayout.Label("⚙️ Настройки", EditorStyles.boldLabel);
            
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("AI Провайдер", EditorStyles.boldLabel);
            AIProvider newProv = (AIProvider)EditorGUILayout.EnumPopup("Провайдер", currentProvider);
            if (newProv != currentProvider) { currentProvider = newProv; SaveSettings(); }
            maxTokens = EditorGUILayout.IntSlider("Max Tokens", maxTokens, 1024, 8192);
            bool newAuto = EditorGUILayout.Toggle("Авто-применение кода", autoApply);
            if (newAuto != autoApply) { autoApply = newAuto; SaveSettings(); }
            GUILayout.EndVertical();
            
            GUILayout.Space(8);
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Статус API ключей", EditorStyles.boldLabel);
            GUILayout.Label("Gemini: " + (HasValidGeminiKey() ? "✅ Настроен" : "❌ Нет ключа") + " [" + GEMINI_MODEL + "]");
            GUILayout.Label("DeepSeek: " + (HasValidDeepSeekKey() ? "✅ Настроен" : "❌ Нет ключа") + " [" + DEEPSEEK_MODEL + "]");
            GUILayout.Label("Groq: " + (HasValidGroqKey() ? "✅ Настроен" : "❌ Нет ключа") + " [" + GROQ_MODEL + "]");
            GUILayout.EndVertical();
            
            GUILayout.Space(8);
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Информация", EditorStyles.boldLabel);
            GUILayout.Label("Файлов: " + allFiles.Count + " | Скриптов: " + scriptIndex.Count);
            GUILayout.Label("Сцена: " + (string.IsNullOrEmpty(ctxScene) ? "нет" : ctxScene));
            GUILayout.EndVertical();
            
            EditorGUILayout.EndScrollView();
        }
    }
    
    public class EditorCoroutine
    {
        private IEnumerator routine;
        private object current;
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
