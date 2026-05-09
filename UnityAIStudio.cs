// ╔══════════════════════════════════════════════════════════════════════════╗
// ║  Unity AI Studio Pro v1.0 — AI Assistant for Unity                      ║
// ║  Install: Assets/Editor/UnityAIStudio.cs                                ║
// ║  Menu: Window → AI Studio Pro (Ctrl+Shift+Q)                           ║
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
        public List<string> fields = new List<string>();
        public List<string> usings = new List<string>();
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
        // API KEYS - Auto-generated from web interface
        // ═══════════════════════════════════════════════════════════════
        private const string GEMINI_API_KEY = "AIzaSyBqg_NXLMetxOArTLDvfJ0fDA7cHYz85Ok";
        private const string GEMINI_MODEL = "gemini-2.5-flash";
        private const string GEMINI_ENDPOINT = "https://generativelanguage.googleapis.com/v1beta/models";
        
        private const string DEEPSEEK_API_KEY = "sk-0bf95b2295974143ac2a92d7932c0ab3";
        private const string DEEPSEEK_MODEL = "deepseek-chat";
        private const string DEEPSEEK_ENDPOINT = "https://api.deepseek.com/chat/completions";
        
        private const string GROQ_API_KEY = "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";
        private const string GROQ_MODEL = "llama-4-scout-17b-16e-instruct";
        private const string GROQ_ENDPOINT = "https://api.groq.com/openai/v1/chat/completions";
        // ═══════════════════════════════════════════════════════════════
        
        private AIProvider currentProvider = AIProvider.Gemini;
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
        private string lastRequestBody = "";
        
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
            {".json", FileCategory.Config}, {".xml", FileCategory.Config}, {".yaml", FileCategory.Config},
        };
        
        private static readonly HashSet<string> TextExts = new HashSet<string>
        {
            ".cs", ".js", ".unity", ".prefab", ".mat", ".shader", ".hlsl",
            ".json", ".xml", ".yaml", ".yml", ".txt", ".md", ".asmdef"
        };

        [MenuItem("Window/AI Studio Pro %#q")]
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
                text = "Unity AI Studio Pro v1.0\n" +
                       "─────────────────────\n" +
                       "AI Providers configured:\n" +
                       "  Gemini: " + (HasValidGeminiKey() ? "OK" : "NO KEY") + "\n" +
                       "  DeepSeek: " + (HasValidDeepSeekKey() ? "OK" : "NO KEY") + "\n" +
                       "  Groq: " + (HasValidGroqKey() ? "OK" : "NO KEY") + "\n" +
                       "\nWrite what you need to change in the scene!",
                provider = currentProvider
            });
        }
        
        private void OnDisable() { EditorApplication.update -= OnTick; }
        
        private void OnTick()
        {
            RefreshContext();
            double now = EditorApplication.timeSinceStartup;
            if (now - lastPush > 3.0) lastPush = now;
            if (isBusy) Repaint();
        }
        
        private void LoadSettings()
        {
            ghToken = EditorPrefs.GetString(PREF_GH_TOKEN, "");
            ghOwner = EditorPrefs.GetString(PREF_GH_OWNER, "");
            ghRepo = EditorPrefs.GetString(PREF_GH_REPO, "");
            ghBranch = EditorPrefs.GetString(PREF_GH_BRANCH, "main");
            autoApply = EditorPrefs.GetBool(PREF_AUTO, false);
            currentProvider = (AIProvider)EditorPrefs.GetInt(PREF_PROVIDER, 0);
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
        
        private List<ScriptInfo> FindRelated(string query, int max)
        {
            string q = query.ToLowerInvariant();
            string[] words = q.Split(new char[] { ' ', '\t', '.', ',' }, StringSplitOptions.RemoveEmptyEntries);
            var scored = new List<KeyValuePair<float, ScriptInfo>>();
            
            foreach (var s in scriptIndex)
            {
                float score = 0f;
                string tgt = (s.className + " " + s.path).ToLowerInvariant();
                foreach (string w in words)
                    if (w.Length >= 3 && tgt.Contains(w)) score += 2f;
                if (score > 0f) scored.Add(new KeyValuePair<float, ScriptInfo>(score, s));
            }
            
            scored.Sort((a, b) => b.Key.CompareTo(a.Key));
            return scored.Take(max).Select(x => x.Value).ToList();
        }
        
        private void SendMessage()
        {
            if (string.IsNullOrEmpty(userInput) || isBusy) return;
            if (!HasValidKey())
            {
                statusMsg = "API key not configured! Check Settings.";
                Debug.LogError("[UnityAI] API key not configured for provider: " + currentProvider);
                return;
            }
            
            history.Add(new ChatMsg { isUser = true, text = userInput });
            history.Add(new ChatMsg { isUser = false, isPending = true, startTime = EditorApplication.timeSinceStartup, provider = currentProvider });
            pendingIndex = history.Count - 1;
            
            string text = userInput;
            userInput = "";
            isBusy = true;
            retryCount = 0;
            
            var related = FindRelated(text, 5);
            string systemPrompt = "You are Unity developer assistant. Generate C# code for Unity. Respond in Russian. Always provide complete file code.";
            
            string contextInfo = "Scene: " + ctxScene + "\n";
            contextInfo += "Selected: " + ctxObject + "\n";
            if (selectedFile != null)
            {
                contextInfo += "File: " + selectedFile.fileName + "\n";
                if (selectedFile.isText && fileContent.Length > 0)
                    contextInfo += "Content:\n" + fileContent.Substring(0, Math.Min(5000, fileContent.Length)) + "\n";
            }
            if (sceneHierarchy.Length > 0)
                contextInfo += "Hierarchy:\n" + sceneHierarchy.Substring(0, Math.Min(3000, sceneHierarchy.Length)) + "\n";
            if (scanDone && projectSummary.Length > 0)
                contextInfo += projectSummary + "\n";
            
            string fullMessage = contextInfo + "\n---\nUser request: " + text;
            
            EditorCoroutine.Start(SendToAI(systemPrompt, fullMessage, OnAIResponse));
        }
        
        private void QuickSend(string text) { userInput = text; SendMessage(); }
        
        private void OnAIResponse(string responseText, string error)
        {
            isBusy = false;
            
            if (pendingIndex >= 0 && pendingIndex < history.Count)
                history[pendingIndex].isPending = false;
            
            if (!string.IsNullOrEmpty(error))
            {
                statusMsg = "Error: " + error;
                Debug.LogError("[UnityAI] " + error);
                pendingIndex = -1;
                Repaint();
                return;
            }
            
            if (string.IsNullOrEmpty(responseText))
            {
                statusMsg = "Empty response from AI";
                if (retryCount < 1)
                {
                    retryCount++;
                    statusMsg = "Retrying...";
                    string systemPrompt = "You are Unity developer assistant. Generate C# code for Unity.";
                    EditorCoroutine.Start(SendToAI(systemPrompt, "Repeat last request", OnAIResponse));
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
        
        private IEnumerator SendToAI(string systemPrompt, string userMessage, Action<string, string> callback)
        {
            string url = "";
            string apiKey = GetApiKey();
            string model = GetModel();
            string requestBody = "";
            string errorMsg = "";
            
            Debug.Log("[UnityAI] Sending to " + currentProvider + " model: " + model);
            
            if (currentProvider == AIProvider.Gemini)
            {
                // Gemini API format
                url = GEMINI_ENDPOINT + "/" + model + ":generateContent?key=" + apiKey;
                
                string fullPrompt = systemPrompt + "\n\n" + userMessage;
                
                requestBody = JsonUtility.ToJson(new GeminiRequest
                {
                    contents = new GeminiContent[]
                    {
                        new GeminiContent
                        {
                            parts = new GeminiPart[]
                            {
                                new GeminiPart { text = fullPrompt }
                            }
                        }
                    },
                    generationConfig = new GeminiConfig { maxOutputTokens = maxTokens }
                });
            }
            else
            {
                // OpenAI-compatible format (DeepSeek, Groq)
                if (currentProvider == AIProvider.DeepSeek)
                    url = DEEPSEEK_ENDPOINT;
                else
                    url = GROQ_ENDPOINT;
                
                var messages = new List<OpenAIMessage>
                {
                    new OpenAIMessage { role = "system", content = systemPrompt },
                    new OpenAIMessage { role = "user", content = userMessage }
                };
                
                requestBody = JsonUtility.ToJson(new OpenAIRequest
                {
                    model = model,
                    messages = messages.ToArray(),
                    max_tokens = maxTokens
                });
            }
            
            Debug.Log("[UnityAI] URL: " + url);
            Debug.Log("[UnityAI] Request length: " + requestBody.Length);
            
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
                Debug.Log("[UnityAI] Response length: " + result.Length);
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
        
        // JSON helper classes for proper serialization
        [Serializable] private class GeminiRequest { public GeminiContent[] contents; public GeminiConfig generationConfig; }
        [Serializable] private class GeminiContent { public GeminiPart[] parts; }
        [Serializable] private class GeminiPart { public string text; }
        [Serializable] private class GeminiConfig { public int maxOutputTokens; }
        
        [Serializable] private class OpenAIRequest { public string model; public OpenAIMessage[] messages; public int max_tokens; }
        [Serializable] private class OpenAIMessage { public string role; public string content; }
        
        private void AutoApplyCode(string code)
        {
            try
            {
                string path = "Assets/Editor/AIStudio_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".cs";
                File.WriteAllText(path, code);
                AssetDatabase.Refresh();
                statusMsg = "Code applied: " + path;
            }
            catch (Exception e) { statusMsg = "Error: " + e.Message; }
        }
        
        private void SaveCodeFromEditor()
        {
            if (string.IsNullOrEmpty(codeEditorPath) || string.IsNullOrEmpty(codeEditor)) return;
            try
            {
                File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", codeEditorPath)), codeEditor);
                AssetDatabase.Refresh();
                codeModified = false;
                statusMsg = "Saved: " + codeEditorPath;
            }
            catch (Exception e) { statusMsg = "Error: " + e.Message; }
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
            
            // Try csharp first
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
            
            // Try any code block
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
            
            string[] tabs = { "AI Chat", "Files", "Search", "Editor", "GitHub", "Settings" };
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
            GUILayout.Label("AI Studio Pro", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            AIProvider newProvider = (AIProvider)EditorGUILayout.EnumPopup(currentProvider, GUILayout.Width(100));
            if (newProvider != currentProvider) { currentProvider = newProvider; SaveSettings(); }
            
            GUILayout.FlexibleSpace();
            
            bool newAuto = GUILayout.Toggle(autoApply, " Auto", EditorStyles.miniButton, GUILayout.Width(50));
            if (newAuto != autoApply) { autoApply = newAuto; SaveSettings(); }
            
            GUI.color = HasValidKey() ? Color.green : Color.red;
            GUILayout.Label(HasValidKey() ? "KEY OK" : "NO KEY", EditorStyles.miniLabel);
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
                GUILayout.Label(allFiles.Count + " files", EditorStyles.miniLabel);
                GUI.color = Color.white;
            }
            
            GUILayout.EndHorizontal();
            
            if (!string.IsNullOrEmpty(ctxScene))
            {
                GUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label("Scene: " + ctxScene, EditorStyles.boldLabel);
                if (!string.IsNullOrEmpty(ctxObject)) GUILayout.Label("-> " + ctxObject, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("R", EditorStyles.miniButton, GUILayout.Width(20))) sceneHierarchy = ScanSceneHierarchy();
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
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Scene?", GUILayout.Height(24))) QuickSend("Describe scene " + ctxScene);
            if (GUILayout.Button("Bugs", GUILayout.Height(24))) QuickSend("Find problems in " + ctxScene);
            if (GUILayout.Button("Optimize", GUILayout.Height(24))) QuickSend("Optimize " + ctxScene);
            GUILayout.EndHorizontal();
            
            float chatH = position.height - 200;
            chatScroll = EditorGUILayout.BeginScrollView(chatScroll, GUILayout.Height(Math.Max(100, chatH)));
            
            foreach (var msg in history)
            {
                GUILayout.BeginVertical(EditorStyles.helpBox);
                if (msg.isPending)
                {
                    double elapsed = EditorApplication.timeSinceStartup - msg.startTime;
                    GUILayout.Label("AI: Thinking... (" + elapsed.ToString("F0") + "s)", EditorStyles.boldLabel);
                }
                else
                {
                    string icon = msg.isUser ? ">" : "<";
                    string label = msg.isUser ? "You:" : "AI (" + msg.provider + "):";
                    GUILayout.Label(label, EditorStyles.boldLabel);
                    var style = new GUIStyle(EditorStyles.label) { wordWrap = true };
                    GUILayout.Label(msg.text, style);
                    
                    if (!string.IsNullOrEmpty(msg.code))
                    {
                        GUILayout.Space(4);
                        var cs = new GUIStyle(EditorStyles.textArea) { wordWrap = false, fontSize = 10 };
                        string code = msg.code.Length > 2000 ? msg.code.Substring(0, 2000) + "\n..." : msg.code;
                        GUILayout.TextArea(code, cs, GUILayout.Height(100));
                        
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("Copy Code"))
                            EditorGUIUtility.systemCopyBuffer = msg.code;
                        if (!autoApply && GUILayout.Button("Apply"))
                            AutoApplyCode(msg.code);
                        GUILayout.EndHorizontal();
                    }
                }
                GUILayout.EndVertical();
            }
            
            EditorGUILayout.EndScrollView();
            
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUI.enabled = !isBusy;
            userInput = GUILayout.TextField(userInput, GUILayout.Height(40));
            bool send = GUILayout.Button(isBusy ? "..." : "Send", GUILayout.Width(60), GUILayout.Height(40));
            GUI.enabled = true;
            
            if (send || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return && !string.IsNullOrEmpty(userInput) && !isBusy))
            {
                SendMessage();
                Event.current.Use();
            }
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear", GUILayout.Height(20))) { history.Clear(); statusMsg = ""; }
            if (GUILayout.Button("Rescan", GUILayout.Height(20))) { scanDone = false; EditorCoroutine.Start(ScanProjectRoutine()); }
            GUILayout.EndHorizontal();
        }
        
        private void DrawFilesTab()
        {
            GUILayout.BeginHorizontal();
            browserSearch = EditorGUILayout.TextField("Search", browserSearch);
            showAllTypes = GUILayout.Toggle(showAllTypes, "All", EditorStyles.miniButton, GUILayout.Width(40));
            GUILayout.EndHorizontal();
            
            browserScroll = EditorGUILayout.BeginScrollView(browserScroll);
            
            var filtered = allFiles.Where(f =>
            {
                if (!showAllTypes && f.category != browserFilter) return false;
                if (!string.IsNullOrEmpty(browserSearch) && !f.fileName.ToLowerInvariant().Contains(browserSearch.ToLowerInvariant())) return false;
                return true;
            }).ToList();
            
            GUILayout.Label("Shown: " + filtered.Count + " of " + allFiles.Count, EditorStyles.miniLabel);
            
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
                GUILayout.Label(selectedFile.fileName, EditorStyles.boldLabel);
                GUILayout.Label("Path: " + selectedFile.assetPath, EditorStyles.miniLabel);
                GUILayout.Label("Type: " + selectedFile.category + " | Size: " + FormatBytes(selectedFile.sizeBytes), EditorStyles.miniLabel);
                if (selectedFile.dependencies.Count > 0)
                    GUILayout.Label("Dependencies: " + selectedFile.dependencies.Count, EditorStyles.miniLabel);
                GUILayout.EndVertical();
            }
        }
        
        private void DrawSearchTab()
        {
            GUILayout.BeginHorizontal();
            string newSearch = EditorGUILayout.TextField("Global Search", globalSearch);
            if (newSearch != globalSearch) { globalSearch = newSearch; PerformGlobalSearch(globalSearch); }
            if (GUILayout.Button("Go", GUILayout.Width(30))) PerformGlobalSearch(globalSearch);
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
            GUILayout.Label(codeEditorPath ?? "Select file", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUI.enabled = codeModified;
            if (GUILayout.Button("Save", GUILayout.Width(60))) SaveCodeFromEditor();
            GUI.enabled = true;
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
            GUILayout.Label("GitHub Settings", EditorStyles.boldLabel);
            GUILayout.BeginVertical(EditorStyles.helpBox);
            ghOwner = EditorGUILayout.TextField("Owner", ghOwner);
            ghRepo = EditorGUILayout.TextField("Repo", ghRepo);
            ghBranch = EditorGUILayout.TextField("Branch", ghBranch);
            ghToken = EditorGUILayout.PasswordField("Token", ghToken);
            if (GUILayout.Button("Save")) { SaveSettings(); ghStatus = "Saved"; }
            GUILayout.EndVertical();
            
            if (!string.IsNullOrEmpty(ghStatus)) GUILayout.Label(ghStatus, EditorStyles.helpBox);
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawSettingsTab()
        {
            mainScroll = EditorGUILayout.BeginScrollView(mainScroll);
            GUILayout.Label("Settings", EditorStyles.boldLabel);
            
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("AI Provider", EditorStyles.boldLabel);
            AIProvider newProv = (AIProvider)EditorGUILayout.EnumPopup("Provider", currentProvider);
            if (newProv != currentProvider) { currentProvider = newProv; SaveSettings(); }
            maxTokens = EditorGUILayout.IntSlider("Max Tokens", maxTokens, 1024, 8192);
            bool newAuto = EditorGUILayout.Toggle("Auto-apply code", autoApply);
            if (newAuto != autoApply) { autoApply = newAuto; SaveSettings(); }
            GUILayout.EndVertical();
            
            GUILayout.Space(8);
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("API Keys Status", EditorStyles.boldLabel);
            GUILayout.Label("Gemini: " + (HasValidGeminiKey() ? "Configured" : "NO KEY"), HasValidGeminiKey() ? EditorStyles.miniLabel : EditorStyles.centeredGreyMiniLabel);
            GUILayout.Label("Model: " + GEMINI_MODEL, EditorStyles.miniLabel);
            GUILayout.Label("DeepSeek: " + (HasValidDeepSeekKey() ? "Configured" : "NO KEY"), HasValidDeepSeekKey() ? EditorStyles.miniLabel : EditorStyles.centeredGreyMiniLabel);
            GUILayout.Label("Model: " + DEEPSEEK_MODEL, EditorStyles.miniLabel);
            GUILayout.Label("Groq: " + (HasValidGroqKey() ? "Configured" : "NO KEY"), HasValidGroqKey() ? EditorStyles.miniLabel : EditorStyles.centeredGreyMiniLabel);
            GUILayout.Label("Model: " + GROQ_MODEL, EditorStyles.miniLabel);
            GUILayout.EndVertical();
            
            GUILayout.Space(8);
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Info", EditorStyles.boldLabel);
            GUILayout.Label("Files: " + allFiles.Count, EditorStyles.miniLabel);
            GUILayout.Label("Scripts: " + scriptIndex.Count, EditorStyles.miniLabel);
            GUILayout.Label("Scene: " + ctxScene, EditorStyles.miniLabel);
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
