// ╔══════════════════════════════════════════════════════════════════════════╗
// ║  Unity AI Studio Pro v1.0                                              ║
// ║  Menu: Window → AI Studio Pro (Ctrl+Shift+Q)                          ║
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
    
    [Serializable] public class FileEntry { public string fullPath = ""; public string assetPath = ""; public string fileName = ""; public string ext = ""; public FileCategory category; public bool isText = false; public long sizeBytes = 0; public string guid = ""; public List<string> dependencies = new List<string>(); }
    [Serializable] public class ScriptInfo { public string path = ""; public string className = ""; public string baseClass = ""; public string nameSpace = ""; public int lineCount = 0; public List<string> methods = new List<string>(); public string content = ""; public bool isMonoBehaviour = false; public bool isEditor = false; }
    [Serializable] public class ChatMsg { public bool isUser = false; public string text = ""; public string code = ""; public bool isPending = false; public double startTime = 0; public AIProvider provider; }
    [Serializable] public class SceneObject { public string name = ""; public string path = ""; public Vector3 position; public List<string> components = new List<string>(); public bool isActive = true; public int childCount = 0; }
    
    public class UnityAIStudioWindow : EditorWindow
    {
        // ═══════════════════════════════════════════════════════════════
        // ВСТАВЬТЕ ВАШИ API КЛЮЧИ СЮДА ↓↓↓
        // ═══════════════════════════════════════════════════════════════
        private const string GEMINI_API_KEY = "AIzaSyC0zIacbiLc9uJUVwznrEo-dufYvb7l48I";
        private const string GEMINI_MODEL = "gemini-2.5-flash";
        private const string GEMINI_ENDPOINT = "https://generativelanguage.googleapis.com/v1beta/models";
        
        private const string DEEPSEEK_API_KEY = "sk-0bf95b2295974143ac2a92d7932c0ab3";
        private const string DEEPSEEK_MODEL = "deepseek-chat";
        private const string DEEPSEEK_ENDPOINT = "https://api.deepseek.com/chat/completions";
        
        private const string GROQ_API_KEY = "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";
        private const string GROQ_MODEL = "llama-4-scout-17b-16e-instruct";
        private const string GROQ_ENDPOINT = "https://api.groq.com/openai/v1/chat/completions";
        // ═══════════════════════════════════════════════════════════════
        
        private AIProvider currentProvider = AIProvider.DeepSeek;
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
            {".cs", FileCategory.Script}, {".js", FileCategory.Script}, {".unity", FileCategory.Scene}, {".prefab", FileCategory.Prefab},
            {".mat", FileCategory.Material}, {".shader", FileCategory.Shader}, {".png", FileCategory.Texture}, {".jpg", FileCategory.Texture},
            {".wav", FileCategory.Audio}, {".mp3", FileCategory.Audio}, {".fbx", FileCategory.Model}, {".obj", FileCategory.Model},
            {".anim", FileCategory.Animation}, {".controller", FileCategory.Animation}, {".json", FileCategory.Config}, {".xml", FileCategory.Config}
        };
        
        private static readonly HashSet<string> TextExts = new HashSet<string> { ".cs", ".js", ".unity", ".prefab", ".mat", ".shader", ".hlsl", ".json", ".xml", ".yaml", ".txt", ".md", ".asmdef" };

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
            history.Add(new ChatMsg { isUser = false, text = "Unity AI Studio Pro v1.0\n\nProviders:\nGemini: " + (HasValidGeminiKey() ? "OK" : "NO KEY") + "\nDeepSeek: " + (HasValidDeepSeekKey() ? "OK" : "NO KEY") + "\nGroq: " + (HasValidGroqKey() ? "OK" : "NO KEY") + "\n\nWrite what you need!", provider = currentProvider });
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
            currentProvider = (AIProvider)EditorPrefs.GetInt(PREF_PROVIDER, 1);
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
        
        private bool HasValidGeminiKey() { return !string.IsNullOrEmpty(GEMINI_API_KEY) && GEMINI_API_KEY.Length > 10 && !GEMINI_API_KEY.Contains("ВСТАВЬ"); }
        private bool HasValidDeepSeekKey() { return !string.IsNullOrEmpty(DEEPSEEK_API_KEY) && DEEPSEEK_API_KEY.Length > 10 && !DEEPSEEK_API_KEY.Contains("ВСТАВЬ"); }
        private bool HasValidGroqKey() { return !string.IsNullOrEmpty(GROQ_API_KEY) && GROQ_API_KEY.Length > 10 && !GROQ_API_KEY.Contains("ВСТАВЬ"); }
        
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
            if (fe.isText && fe.category == FileCategory.Script) { codeEditor = fileContent; codeEditorPath = assetPath; codeModified = false; }
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
                foreach (var root in roots) { AppendGO(sb, root, 0, ref count); if (count > 300 || sb.Length > 12000) { sb.AppendLine("... [truncated]"); break; } }
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
            foreach (var c in comps) { if (c == null || c is Transform) continue; if (compNames.Length > 0) compNames.Append(','); compNames.Append(c.GetType().Name); }
            Vector3 p = go.transform.position;
            sb.Append(pad + go.name + " [" + p.x.ToString("F1") + "," + p.y.ToString("F1") + "," + p.z.ToString("F1") + "]");
            if (compNames.Length > 0) sb.Append(" {" + compNames + "}");
            if (!go.activeSelf) sb.Append(" [OFF]");
            sb.AppendLine();
            for (int i = 0; i < go.transform.childCount; i++) AppendGO(sb, go.transform.GetChild(i).gameObject, depth + 1, ref count);
        }
        
        private void ScanSceneObjects()
        {
            sceneObjects.Clear();
            try { var scene = EditorSceneManager.GetActiveScene(); if (!scene.IsValid()) return; foreach (var go in scene.GetRootGameObjects()) CollectSceneObjects(go, ""); }
            catch { }
        }
        
        private void CollectSceneObjects(GameObject go, string parentPath)
        {
            string path = string.IsNullOrEmpty(parentPath) ? go.name : parentPath + "/" + go.name;
            var so = new SceneObject { name = go.name, path = path, position = go.transform.position, isActive = go.activeSelf, childCount = go.transform.childCount };
            foreach (var c in go.GetComponents<Component>()) if (c != null && !(c is Transform)) so.components.Add(c.GetType().Name);
            sceneObjects.Add(so);
            for (int i = 0; i < go.transform.childCount; i++) CollectSceneObjects(go.transform.GetChild(i).gameObject, path);
        }
        
        private IEnumerator ScanProjectRoutine()
        {
            scanRunning = true; scanDone = false; allFiles.Clear(); scriptIndex.Clear();
            string[] guids = AssetDatabase.FindAssets("");
            var paths = new HashSet<string>();
            foreach (string g in guids) { string p = AssetDatabase.GUIDToAssetPath(g); if (p.StartsWith("Assets/") && !p.EndsWith(".meta")) paths.Add(p); }
            var filtered = new List<string>(paths);
            scanTotal = filtered.Count; scanProgress = 0;
            for (int i = 0; i < filtered.Count; i++)
            {
                string p = filtered[i]; scanProgress = i + 1;
                var fe = new FileEntry { assetPath = p, fileName = Path.GetFileName(p), ext = Path.GetExtension(p).ToLowerInvariant() };
                fe.isText = TextExts.Contains(fe.ext);
                fe.category = CatMap.ContainsKey(fe.ext) ? CatMap[fe.ext] : FileCategory.Other;
                try { fe.sizeBytes = new FileInfo(Path.GetFullPath(Path.Combine(Application.dataPath, "..", p))).Length; } catch { }
                allFiles.Add(fe);
                if (fe.category == FileCategory.Script && fe.ext == ".cs")
                {
                    try
                    {
                        string content = File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", p)));
                        var info = new ScriptInfo { path = p, content = content };
                        var m = Regex.Match(content, @"\bclass\s+(\w+)"); info.className = m.Success ? m.Groups[1].Value : "";
                        m = Regex.Match(content, @"\bclass\s+\w+\s*:\s*(\w+)"); info.baseClass = m.Success ? m.Groups[1].Value : "";
                        m = Regex.Match(content, @"namespace\s+([\w.]+)"); info.nameSpace = m.Success ? m.Groups[1].Value : "";
                        info.lineCount = content.Split('\n').Length;
                        info.isMonoBehaviour = content.Contains("MonoBehaviour");
                        info.isEditor = p.Contains("/Editor/");
                        var methods = Regex.Matches(content, @"(?:public|protected)\s+(?:override\s+|static\s+)*(?:\w[\w\[\]]*\s+)+(\w+)\s*\(");
                        foreach (Match mm in methods) if (!info.methods.Contains(mm.Groups[1].Value)) info.methods.Add(mm.Groups[1].Value);
                        scriptIndex.Add(info);
                    }
                    catch { }
                }
                if (i % 100 == 0) { yield return null; Repaint(); }
            }
            projectSummary = "PROJECT: " + allFiles.Count + " files, " + scriptIndex.Count + " scripts";
            scanRunning = false; scanDone = true; Repaint();
        }
        
        private void SendMessage()
        {
            if (string.IsNullOrEmpty(userInput) || isBusy) return;
            if (!HasValidKey()) { statusMsg = "API key not configured! Edit the .cs file and add your key."; Debug.LogError("[UnityAI] No API key for " + currentProvider); return; }
            
            history.Add(new ChatMsg { isUser = true, text = userInput });
            history.Add(new ChatMsg { isUser = false, isPending = true, startTime = EditorApplication.timeSinceStartup, provider = currentProvider });
            pendingIndex = history.Count - 1;
            string text = userInput; userInput = ""; isBusy = true; retryCount = 0;
            
            string systemPrompt = "You are Unity C# developer assistant. Generate complete code files