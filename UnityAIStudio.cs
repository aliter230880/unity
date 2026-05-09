// ╔══════════════════════════════════════════════════════════════════════════╗
// ║  Unity AI Studio Pro v1.0 — Полноценный AI-ассистент для Unity        ║
// ║  Установка: Assets/Editor/UnityAIStudio.cs                            ║
// ║  Меню: Window → AI Studio Pro (Ctrl+Shift+Q)                         ║
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
using UnityEngine.Profiling;

namespace UnityAIStudio
{
    // ═══════════════════════════════════════════════════════════════════════
    // ENUMS & DATA CLASSES
    // ═══════════════════════════════════════════════════════════════════════
    
    public enum AIProvider { Gemini, DeepSeek, Groq }
    public enum FileCategory { 
        Script, Scene, Prefab, Material, Shader, Texture, Audio, 
        Model, Animation, Config, Font, Video, Physics, Other 
    }
    public enum AnalysisMode { Quick, Deep, Full }
    
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
        public List<string> interfaces = new List<string>();
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
        public string path = ""; // hierarchy path
        public Vector3 position;
        public List<string> components = new List<string>();
        public bool isActive = true;
        public int childCount = 0;
    }
    
    [Serializable]
    public class AnalysisResult
    {
        public string title = "";
        public string summary = "";
        public List<string> warnings = new List<string>();
        public List<string> suggestions = new List<string>();
        public List<string> stats = new List<string>();
        public double timestamp = 0;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MAIN EDITOR WINDOW
    // ═══════════════════════════════════════════════════════════════════════
    
    public class UnityAIStudioWindow : EditorWindow
    {
        // ── Version ─────────────────────────────────────────────────────
        private const string VERSION = "1.0";
        
        // ── AI Providers Config ─────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════
        // ВСТАВЬТЕ ВАШИ API КЛЮЧИ СЮДА
        // ═══════════════════════════════════════════════════════════════
        
        // Google Gemini
        private const string GEMINI_API_KEY = "AIzaSyC0zIacbiLc9uJUVwznrEo-dufYvb7l48I";
        private const string GEMINI_MODEL = "__GEMINI_MODEL__";
        private const string GEMINI_ENDPOINT = "https://generativelanguage.googleapis.com/v1beta";
        
        // DeepSeek
        private const string DEEPSEEK_API_KEY = "sk-0bf95b2295974143ac2a92d7932c0ab3";
        private const string DEEPSEEK_MODEL = "__DEEPSEEK_MODEL__";
        private const string DEEPSEEK_ENDPOINT = "https://api.deepseek.com/v1";
        
        // Groq
        private const string GROQ_API_KEY = "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";
        private const string GROQ_MODEL = "__GROQ_MODEL__";
        private const string GROQ_ENDPOINT = "https://api.groq.com/openai/v1";
        
        // ═══════════════════════════════════════════════════════════════
        
        private AIProvider currentProvider = AIProvider.Gemini;
        private int maxTokens = 8192;
        
        // ── Limits ──────────────────────────────────────────────────────
        private const int MAX_FILE_CHARS = 10000;
        private const int MAX_HIERARCHY_CHARS = 15000;
        private const int MAX_PROJECT_SUMMARY = 8000;
        private const int MAX_RELATED_SCRIPTS = 8;
        
        // ── EditorPrefs Keys ────────────────────────────────────────────
        private const string PREF_GH_TOKEN = "UnityAI_GH_Token";
        private const string PREF_GH_OWNER = "UnityAI_GH_Owner";
        private const string PREF_GH_REPO = "UnityAI_GH_Repo";
        private const string PREF_GH_BRANCH = "UnityAI_GH_Branch";
        private const string PREF_AUTO = "UnityAI_AutoApply";
        private const string PREF_PROVIDER = "UnityAI_Provider";
        private const string PREF_ANALYSIS = "UnityAI_AnalysisMode";
        
        // ── UI State ────────────────────────────────────────────────────
        private int mainTab = 0;
        private int aiTab = 0;
        private int analysisTab = 0;
        private int toolsTab = 0;
        private bool isBusy = false;
        private string statusMsg = "";
        private Vector2 mainScroll;
        
        // ── Chat ────────────────────────────────────────────────────────
        private List<ChatMsg> history = new List<ChatMsg>();
        private string userInput = "";
        private Vector2 chatScroll;
        private int pendingIndex = -1;
        private int retryCount = 0;
        private string lastJson = "";
        
        // ── Auto-apply ──────────────────────────────────────────────────
        private bool autoApply = false;
        
        // ── Context ─────────────────────────────────────────────────────
        private FileEntry selectedFile = null;
        private string fileContent = "";
        private string ctxScene = "";
        private string ctxObject = "";
        private string sceneHierarchy = "";
        private string lastScannedScene = "";
        
        // ── Project Scan ────────────────────────────────────────────────
        private List<FileEntry> allFiles = new List<FileEntry>();
        private List<ScriptInfo> scriptIndex = new List<ScriptInfo>();
        private string projectSummary = "";
        private bool scanRunning = false;
        private bool scanDone = false;
        private int scanProgress = 0;
        private int scanTotal = 0;
        
        // ── File Browser ────────────────────────────────────────────────
        private Vector2 browserScroll;
        private string browserSearch = "";
        private FileCategory browserFilter = FileCategory.Script;
        private bool showAllTypes = false;
        private bool showDependencies = false;
        
        // ── Scene Analysis ──────────────────────────────────────────────
        private List<SceneObject> sceneObjects = new List<SceneObject>();
        private List<AnalysisResult> analysisResults = new List<AnalysisResult>();
        private AnalysisMode analysisMode = AnalysisMode.Deep;
        
        // ── Code Editor ─────────────────────────────────────────────────
        private string codeEditor = "";
        private string codeEditorPath = "";
        private Vector2 codeScroll;
        private bool codeModified = false;
        
        // ── Search ──────────────────────────────────────────────────────
        private string globalSearch = "";
        private List<string> searchResults = new List<string>();
        private Vector2 searchScroll;
        
        // ── GitHub ──────────────────────────────────────────────────────
        private string ghToken = "";
        private string ghOwner = "";
        private string ghRepo = "";
        private string ghBranch = "main";
        private string ghStatus = "";
        private bool ghBusy = false;
        private string gitLog = "";
        private double lastPush = 0;
        
        // ── Performance ─────────────────────────────────────────────────
        private long totalTextureSize = 0;
        private long totalMeshSize = 0;
        private long totalAudioSize = 0;
        private int drawCallEstimate = 0;
        
        // ── Extension Tables ────────────────────────────────────────────
        private static readonly Dictionary<string, FileCategory> CatMap = new Dictionary<string, FileCategory>
        {
            {".cs", FileCategory.Script}, {".js", FileCategory.Script}, {".boo", FileCategory.Script},
            {".asmdef", FileCategory.Script}, {".asmref", FileCategory.Script},
            {".unity", FileCategory.Scene},
            {".prefab", FileCategory.Prefab},
            {".mat", FileCategory.Material}, {".physicmaterial", FileCategory.Material},
            {".physicsmaterial2d", FileCategory.Material},
            {".shader", FileCategory.Shader}, {".shadergraph", FileCategory.Shader},
            {".shadersubgraph", FileCategory.Shader}, {".cginc", FileCategory.Shader},
            {".hlsl", FileCategory.Shader}, {".compute", FileCategory.Shader},
            {".raytrace", FileCategory.Shader}, {".glslinc", FileCategory.Shader},
            {".png", FileCategory.Texture}, {".jpg", FileCategory.Texture}, {".jpeg", FileCategory.Texture},
            {".tga", FileCategory.Texture}, {".psd", FileCategory.Texture}, {".exr", FileCategory.Texture},
            {".gif", FileCategory.Texture}, {".bmp", FileCategory.Texture}, {".svg", FileCategory.Texture},
            {".tiff", FileCategory.Texture}, {".hdr", FileCategory.Texture},
            {".wav", FileCategory.Audio}, {".mp3", FileCategory.Audio}, {".ogg", FileCategory.Audio},
            {".aif", FileCategory.Audio}, {".aiff", FileCategory.Audio}, {".flac", FileCategory.Audio},
            {".fbx", FileCategory.Model}, {".obj", FileCategory.Model}, {".glb", FileCategory.Model},
            {".gltf", FileCategory.Model}, {".dae", FileCategory.Model}, {".3ds", FileCategory.Model},
            {".blend", FileCategory.Model}, {".stl", FileCategory.Model},
            {".anim", FileCategory.Animation}, {".controller", FileCategory.Animation},
            {".overridecontroller", FileCategory.Animation}, {".motion", FileCategory.Animation},
            {".json", FileCategory.Config}, {".xml", FileCategory.Config}, {".yaml", FileCategory.Config},
            {".yml", FileCategory.Config}, {".txt", FileCategory.Config}, {".md", FileCategory.Config},
            {".csv", FileCategory.Config}, {".inputactions", FileCategory.Config},
            {".asset", FileCategory.Config}, {".preset", FileCategory.Config},
            {".ttf", FileCategory.Font}, {".otf", FileCategory.Font}, {".woff", FileCategory.Font},
            {".woff2", FileCategory.Font},
            {".mp4", FileCategory.Video}, {".avi", FileCategory.Video}, {".mov", FileCategory.Video},
            {".webm", FileCategory.Video},
        };
        
        private static readonly HashSet<string> TextExts = new HashSet<string>
        {
            ".cs", ".js", ".boo", ".asmdef", ".asmref",
            ".unity", ".prefab", ".mat", ".physicmaterial", ".physicsmaterial2d",
            ".shader", ".shadergraph", ".shadersubgraph", ".cginc", ".hlsl", ".compute",
            ".raytrace", ".glslinc",
            ".json", ".xml", ".yaml", ".yml", ".txt", ".md", ".csv",
            ".inputactions", ".asset", ".preset", ".anim", ".controller",
            ".overridecontroller", ".motion", ".lighting", ".terrainlayer",
            ".guiskin", ".jslib", ".uxml", ".uss", ".asmdef"
        };

        // ═══════════════════════════════════════════════════════════════
        // MENU & LIFECYCLE
        // ═══════════════════════════════════════════════════════════════
        
        [MenuItem("Window/AI Studio Pro %#q")]
        public static void ShowWindow()
        {
            var w = GetWindow<UnityAIStudioWindow>("🤖 AI Studio Pro");
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
                text = "🤖 Unity AI Studio Pro v" + VERSION + "\n\n" +
                       "Полноценный AI-ассистент для Unity!\n\n" +
                       "📋 Возможности:\n" +
                       "• 💬 Чат с AI — генерация кода, ответы на вопросы\n" +
                       "• 📁 Файловый менеджер — все файлы проекта\n" +
                       "• 🔍 Анализ сцены — иерархия, объекты, компоненты\n" +
                       "• 📊 Анализ проекта — скрипты, текстуры, производительность\n" +
                       "• 🔎 Поиск — глобальный поиск по проекту\n" +
                       "• ✏️ Редактор кода — встроенный редактор C#\n" +
                       "• 🐙 GitHub — push файлов прямо из редактора\n" +
                       "• 🔧 Инструменты — batch операции, оптимизация\n\n" +
                       "Напишите что нужно сделать — я создам код и применю изменения!",
                provider = currentProvider
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
            }
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
            analysisMode = (AnalysisMode)EditorPrefs.GetInt(PREF_ANALYSIS, 1);
        }
        
        private void SaveSettings()
        {
            EditorPrefs.SetString(PREF_GH_TOKEN, ghToken);
            EditorPrefs.SetString(PREF_GH_OWNER, ghOwner);
            EditorPrefs.SetString(PREF_GH_REPO, ghRepo);
            EditorPrefs.SetString(PREF_GH_BRANCH, ghBranch);
            EditorPrefs.SetBool(PREF_AUTO, autoApply);
            EditorPrefs.SetInt(PREF_PROVIDER, (int)currentProvider);
            EditorPrefs.SetInt(PREF_ANALYSIS, (int)analysisMode);
        }

        // ═══════════════════════════════════════════════════════════════
        // CONTEXT & SCANNING
        // ═══════════════════════════════════════════════════════════════
        
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
            
            // Get dependencies
            try
            {
                var deps = AssetDatabase.GetDependencies(assetPath, false);
                fe.dependencies = new List<string>(deps);
            }
            catch { }
            
            selectedFile = fe;
            fileContent = "";
            
            if (fe.isText)
                try { fileContent = File.ReadAllText(fullPath); } catch { fileContent = "(ошибка чтения)"; }
            
            // Update code editor
            if (fe.isText && fe.category == FileCategory.Script)
            {
                codeEditor = fileContent;
                codeEditorPath = assetPath;
                codeModified = false;
            }
            
            Repaint();
        }

        // ═══════════════════════════════════════════════════════════════
        // SCENE HIERARCHY SCANNER
        // ═══════════════════════════════════════════════════════════════
        
        private string ScanSceneHierarchy()
        {
            try
            {
                var scene = EditorSceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(scene.name)) return "";
                
                var sb = new StringBuilder();
                sb.AppendLine($"[ИЕРАРХИЯ: {scene.name} | объектов: {scene.rootCount}]");
                
                var roots = scene.GetRootGameObjects();
                int count = 0;
                
                foreach (var root in roots)
                {
                    AppendGO(sb, root, 0, ref count);
                    if (count > 500 || sb.Length > MAX_HIERARCHY_CHARS)
                    {
                        sb.AppendLine($"... [обрезано: {roots.Length - Array.IndexOf(roots, root)} объектов]");
                        break;
                    }
                }
                
                return sb.ToString();
            }
            catch { return ""; }
        }
        
        private void AppendGO(StringBuilder sb, GameObject go, int depth, ref int count)
        {
            if (depth > 8 || count > 500 || sb.Length > MAX_HIERARCHY_CHARS) return;
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
            
            if (compNames.Length > 0)
            {
                sb.Append(" {");
                sb.Append(compNames);
                sb.Append('}');
            }
            
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
            {
                if (c != null && !(c is Transform))
                    so.components.Add(c.GetType().Name);
            }
            
            sceneObjects.Add(so);
            
            for (int i = 0; i < go.transform.childCount; i++)
                CollectSceneObjects(go.transform.GetChild(i).gameObject, path);
        }

        // ═══════════════════════════════════════════════════════════════
        // PROJECT SCANNER
        // ═══════════════════════════════════════════════════════════════
        
        private IEnumerator ScanProjectRoutine()
        {
            scanRunning = true;
            scanDone = false;
            allFiles.Clear();
            scriptIndex.Clear();
            
            string[] guids = AssetDatabase.FindAssets("");
            var paths = new List<string>();
            foreach (string g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (!p.StartsWith("Assets/")) continue;
                if (p.EndsWith(".meta")) continue;
                paths.Add(p);
            }
            
            // Filter duplicates
            var uniquePaths = new HashSet<string>(paths);
            var filtered = new List<string>(uniquePaths);
            
            scanTotal = filtered.Count;
            scanProgress = 0;
            
            // Calculate sizes by category
            Dictionary<FileCategory, long> categorySizes = new Dictionary<FileCategory, long>();
            Dictionary<FileCategory, int> categoryCounts = new Dictionary<FileCategory, int>();
            
            for (int i = 0; i < filtered.Count; i++)
            {
                string p = filtered[i];
                scanProgress = i + 1;
                
                var fe = CreateFileEntry(p);
                allFiles.Add(fe);
                
                // Track sizes
                if (!categorySizes.ContainsKey(fe.category))
                {
                    categorySizes[fe.category] = 0;
                    categoryCounts[fe.category] = 0;
                }
                categorySizes[fe.category] += fe.sizeBytes;
                categoryCounts[fe.category]++;
                
                // Index scripts
                if (fe.category == FileCategory.Script && fe.ext == ".cs")
                {
                    var info = IndexScript(p);
                    if (info != null) scriptIndex.Add(info);
                }
                
                if (i % 50 == 0)
                {
                    yield return null;
                    Repaint();
                }
            }
            
            // Calculate performance stats
            totalTextureSize = 0;
            totalMeshSize = 0;
            totalAudioSize = 0;
            
            if (categorySizes.ContainsKey(FileCategory.Texture))
                totalTextureSize = categorySizes[FileCategory.Texture];
            if (categorySizes.ContainsKey(FileCategory.Model))
                totalMeshSize = categorySizes[FileCategory.Model];
            if (categorySizes.ContainsKey(FileCategory.Audio))
                totalAudioSize = categorySizes[FileCategory.Audio];
            
            projectSummary = BuildProjectSummary(categorySizes, categoryCounts);
            scanRunning = false;
            scanDone = true;
            Repaint();
        }
        
        private FileEntry CreateFileEntry(string assetPath)
        {
            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            var fe = new FileEntry();
            fe.assetPath = assetPath;
            fe.fullPath = fullPath;
            fe.fileName = Path.GetFileName(assetPath);
            fe.ext = Path.GetExtension(assetPath).ToLowerInvariant();
            fe.isText = TextExts.Contains(fe.ext);
            fe.category = CatMap.ContainsKey(fe.ext) ? CatMap[fe.ext] : FileCategory.Other;
            fe.guid = AssetDatabase.AssetPathToGUID(assetPath);
            try { fe.sizeBytes = new FileInfo(fullPath).Length; } catch { }
            return fe;
        }
        
        private ScriptInfo IndexScript(string assetPath)
        {
            try
            {
                string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
                if (!File.Exists(fullPath)) return null;
                
                string content = File.ReadAllText(fullPath);
                var info = new ScriptInfo();
                info.path = assetPath;
                info.content = content;
                info.className = ExtractClass(content);
                info.baseClass = ExtractBaseClass(content);
                info.nameSpace = ExtractNamespace(content);
                info.lineCount = CountLines(content);
                info.methods = ExtractMethods(content);
                info.fields = ExtractFields(content);
                info.usings = ExtractUsings(content);
                info.interfaces = ExtractInterfaces(content);
                info.isMonoBehaviour = content.Contains(": MonoBehaviour") || content.Contains(": MonoBehaviour<");
                info.isEditor = assetPath.Contains("/Editor/") || content.Contains("UnityEditor");
                
                return info;
            }
            catch { return null; }
        }
        
        private string BuildProjectSummary(Dictionary<FileCategory, long> sizes, Dictionary<FileCategory, int> counts)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[ПРОЕКТ: {allFiles.Count} файлов, {scriptIndex.Count} C# скриптов]");
            sb.AppendLine();
            
            sb.AppendLine("[СТАТИСТИКА ПО ТИПАМ]");
            foreach (var kv in counts.OrderByDescending(x => x.Value))
            {
                string sizeStr = FormatBytes(sizes.ContainsKey(kv.Key) ? sizes[kv.Key] : 0);
                sb.AppendLine($"  {GetCategoryIcon(kv.Key)} {kv.Key}: {kv.Value} файлов ({sizeStr})");
            }
            sb.AppendLine();
            
            sb.AppendLine("[РАЗМЕРЫ РЕСУРСОВ]");
            sb.AppendLine($"  🖼️ Текстуры: {FormatBytes(totalTextureSize)}");
            sb.AppendLine($"  📐 3D модели: {FormatBytes(totalMeshSize)}");
            sb.AppendLine($"  🔊 Аудио: {FormatBytes(totalAudioSize)}");
            sb.AppendLine();
            
            // Scripts by namespace
            sb.AppendLine("[ПРОСТРАНСТВА ИМЁН]");
            var namespaces = scriptIndex
                .Where(s => !string.IsNullOrEmpty(s.nameSpace))
                .GroupBy(s => s.nameSpace)
                .OrderByDescending(g => g.Count())
                .Take(15);
            foreach (var ns in namespaces)
                sb.AppendLine($"  📦 {ns.Key}: {ns.Count()} классов");
            sb.AppendLine();
            
            // MonoBehaviour scripts
            var monoBehaviours = scriptIndex.Where(s => s.isMonoBehaviour).ToList();
            sb.AppendLine($"[MONOBEHAVIOUR: {monoBehaviours.Count} скриптов]");
            foreach (var s in monoBehaviours.Take(30))
            {
                string b = string.IsNullOrEmpty(s.baseClass) ? "" : ":" + s.baseClass;
                string m = s.methods.Count > 0 ? string.Join(",", s.methods.Take(4).ToArray()) : "—";
                sb.AppendLine($"  {s.className}{b} ({s.lineCount}л) | {m}");
            }
            sb.AppendLine();
            
            // Editor scripts
            var editorScripts = scriptIndex.Where(s => s.isEditor).ToList();
            sb.AppendLine($"[EDITOR: {editorScripts.Count} скриптов]");
            foreach (var s in editorScripts.Take(20))
                sb.AppendLine($"  🔧 {s.className} ({s.lineCount}л)");
            
            return sb.ToString();
        }

        // ═══════════════════════════════════════════════════════════════
        // C# CODE ANALYSIS HELPERS
        // ═══════════════════════════════════════════════════════════════
        
        private static string ExtractClass(string code)
        {
            var m = Regex.Match(code, @"\b(?:public|internal|private|protected)?\s*(?:static|abstract|sealed)?\s*class\s+(\w+)");
            return m.Success ? m.Groups[1].Value : "";
        }
        
        private static string ExtractBaseClass(string code)
        {
            var m = Regex.Match(code, @"\bclass\s+\w+\s*:\s*([\w<>,\s.]+?)(?:\s*where|\s*\{)");
            if (m.Success)
            {
                string baseType = m.Groups[1].Value.Trim();
                // Clean up generic parameters
                int angleIdx = baseType.IndexOf('<');
                if (angleIdx > 0) baseType = baseType.Substring(0, angleIdx);
                return baseType.Split(',')[0].Trim();
            }
            return "";
        }
        
        private static string ExtractNamespace(string code)
        {
            var m = Regex.Match(code, @"namespace\s+([\w.]+)");
            return m.Success ? m.Groups[1].Value : "";
        }
        
        private static int CountLines(string code)
        {
            if (string.IsNullOrEmpty(code)) return 0;
            int n = 1;
            foreach (char ch in code) if (ch == '\n') n++;
            return n;
        }
        
        private static List<string> ExtractMethods(string code)
        {
            var list = new List<string>();
            var ms = Regex.Matches(code, 
                @"(?:public|protected|private|internal)\s+(?:override\s+|static\s+|virtual\s+|abstract\s+|async\s+|new\s+)*(?:\w[\w<>\[\],\s]*\s+)+(\w+)\s*\(");
            foreach (Match m in ms)
            {
                string name = m.Groups[1].Value;
                if (IsKeyword(name)) continue;
                if (!list.Contains(name)) list.Add(name);
            }
            return list;
        }
        
        private static List<string> ExtractFields(string code)
        {
            var list = new List<string>();
            var ms = Regex.Matches(code, 
                @"(?:public|private|protected|internal)\s+(?:static\s+|readonly\s+|const\s+|volatile\s+)*(?:\w[\w<>\[\],\s]*)\s+(\w+)\s*[=;]");
            foreach (Match m in ms)
            {
                string name = m.Groups[1].Value;
                if (IsKeyword(name)) continue;
                if (!list.Contains(name)) list.Add(name);
            }
            return list;
        }
        
        private static List<string> ExtractUsings(string code)
        {
            var list = new List<string>();
            var ms = Regex.Matches(code, @"using\s+([\w.]+)\s*;");
            foreach (Match m in ms)
                list.Add(m.Groups[1].Value);
            return list;
        }
        
        private static List<string> ExtractInterfaces(string code)
        {
            var list = new List<string>();
            var m = Regex.Match(code, @"\bclass\s+\w+\s*:\s*([\w<>,\s.]+?)(?:\s*where|\s*\{)");
            if (m.Success)
            {
                string[] parts = m.Groups[1].Value.Split(',');
                foreach (var part in parts)
                {
                    string trimmed = part.Trim();
                    if (trimmed.StartsWith("I") && char.IsUpper(trimmed[1]))
                        list.Add(trimmed);
                }
            }
            return list;
        }
        
        private static bool IsKeyword(string word)
        {
            return word == "if" || word == "for" || word == "while" || word == "new" || 
                   word == "class" || word == "return" || word == "switch" || word == "foreach";
        }

        // ═══════════════════════════════════════════════════════════════
        // ANALYSIS TOOLS
        // ═══════════════════════════════════════════════════════════════
        
        private AnalysisResult AnalyzePerformance()
        {
            var result = new AnalysisResult();
            result.title = "📊 Анализ производительности";
            result.timestamp = EditorApplication.timeSinceStartup;
            
            // Scene analysis
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.IsValid())
            {
                int totalObjects = sceneObjects.Count;
                int activeObjects = sceneObjects.Count(o => o.isActive);
                
                result.stats.Add($"Объектов в сцене: {totalObjects} (активных: {activeObjects})");
                
                // Check for heavy components
                int particleSystems = sceneObjects.Count(o => o.components.Contains("ParticleSystem"));
                int colliders = sceneObjects.Count(o => o.components.Contains("Collider") || o.components.Contains("BoxCollider") || o.components.Contains("SphereCollider"));
                int lights = sceneObjects.Count(o => o.components.Contains("Light"));
                int cameras = sceneObjects.Count(o => o.components.Contains("Camera"));
                
                result.stats.Add($"Particle Systems: {particleSystems}");
                result.stats.Add($"Colliders: {colliders}");
                result.stats.Add($"Lights: {lights}");
                result.stats.Add($"Cameras: {cameras}");
                
                if (lights > 10)
                    result.warnings.Add($"⚠️ Много источников света ({lights}) — может снизить производительность");
                if (particleSystems > 20)
                    result.warnings.Add($"⚠️ Много Particle Systems ({particleSystems}) — рассмотрите pooling");
            }
            
            // Texture analysis
            result.stats.Add($"Текстуры: {FormatBytes(totalTextureSize)}");
            if (totalTextureSize > 500 * 1024 * 1024)
                result.warnings.Add("⚠️ Текстуры занимают более 500MB — рассмотрите сжатие");
            
            // Model analysis
            result.stats.Add($"3D модели: {FormatBytes(totalMeshSize)}");
            
            // Audio analysis
            result.stats.Add($"Аудио: {FormatBytes(totalAudioSize)}");
            
            // Script analysis
            result.stats.Add($"C# скриптов: {scriptIndex.Count}");
            
            // Suggestions
            result.suggestions.Add("💡 Используйте LOD Groups для сложных моделей");
            result.suggestions.Add("💡 Включите Occlusion Culling для больших сцен");
            result.suggestions.Add("💡 Используйте Sprite Atlases для 2D проектов");
            
            return result;
        }
        
        private AnalysisResult AnalyzeCodeQuality()
        {
            var result = new AnalysisResult();
            result.title = "🔍 Анализ качества кода";
            result.timestamp = EditorApplication.timeSinceStartup;
            
            int totalLines = scriptIndex.Sum(s => s.lineCount);
            int classesWithBase = scriptIndex.Count(s => !string.IsNullOrEmpty(s.baseClass));
            int editorScripts = scriptIndex.Count(s => s.isEditor);
            int monoBehaviours = scriptIndex.Count(s => s.isMonoBehaviour);
            
            result.stats.Add($"Всего строк кода: {totalLines}");
            result.stats.Add($"Классов с наследованием: {classesWithBase}");
            result.stats.Add($"MonoBehaviour: {monoBehaviours}");
            result.stats.Add($"Editor скрипты: {editorScripts}");
            
            // Find large files
            var largeFiles = scriptIndex.Where(s => s.lineCount > 500).OrderByDescending(s => s.lineCount).ToList();
            if (largeFiles.Count > 0)
            {
                result.warnings.Add($"⚠️ {largeFiles.Count} файлов более 500 строк:");
                foreach (var f in largeFiles.Take(5))
                    result.warnings.Add($"   • {f.className} ({f.lineCount} строк)");
            }
            
            // Find scripts without namespace
            int noNamespace = scriptIndex.Count(s => string.IsNullOrEmpty(s.nameSpace));
            if (noNamespace > 0)
                result.warnings.Add($"⚠️ {noNamespace} скриптов без namespace");
            
            // Find empty classes
            int emptyClasses = scriptIndex.Count(s => s.methods.Count == 0 && !s.className.Contains("Editor"));
            if (emptyClasses > 0)
                result.warnings.Add($"⚠️ {emptyClasses} классов без методов");
            
            result.suggestions.Add("💡 Используйте namespace для организации кода");
            result.suggestions.Add("💡 Разделяйте большие классы на smaller components");
            result.suggestions.Add("💡 Следуйте SOLID принципам");
            
            return result;
        }
        
        private AnalysisResult AnalyzeDependencies()
        {
            var result = new AnalysisResult();
            result.title = "🔗 Анализ зависимостей";
            result.timestamp = EditorApplication.timeSinceStartup;
            
            // Asset dependencies
            var scenes = allFiles.Where(f => f.category == FileCategory.Scene).ToList();
            var prefabs = allFiles.Where(f => f.category == FileCategory.Prefab).ToList();
            
            result.stats.Add($"Сцен: {scenes.Count}");
            result.stats.Add($"Prefab'ов: {prefabs.Count}");
            
            // Find unused scripts
            var allScriptPaths = new HashSet<string>(scriptIndex.Select(s => s.path));
            var referencedScripts = new HashSet<string>();
            
            foreach (var fe in allFiles)
            {
                foreach (var dep in fe.dependencies)
                {
                    if (dep.EndsWith(".cs"))
                        referencedScripts.Add(dep);
                }
            }
            
            var unusedScripts = allScriptPaths.Except(referencedScripts).ToList();
            if (unusedScripts.Count > 0)
            {
                result.warnings.Add($"⚠️ Возможно неиспользуемые скрипты: {unusedScripts.Count}");
                foreach (var s in unusedScripts.Take(5))
                    result.warnings.Add($"   • {Path.GetFileName(s)}");
            }
            
            // Most referenced scripts
            var refCounts = new Dictionary<string, int>();
            foreach (var fe in allFiles)
            {
                foreach (var dep in fe.dependencies)
                {
                    if (dep.EndsWith(".cs"))
                    {
                        if (!refCounts.ContainsKey(dep)) refCounts[dep] = 0;
                        refCounts[dep]++;
                    }
                }
            }
            
            var topReferenced = refCounts.OrderByDescending(x => x.Value).Take(10);
            if (topReferenced.Any())
            {
                result.stats.Add("Самые используемые скрипты:");
                foreach (var kv in topReferenced)
                    result.stats.Add($"  • {Path.GetFileName(kv.Key)}: {kv.Value} ссылок");
            }
            
            return result;
        }
        
        private AnalysisResult FindUnusedAssets()
        {
            var result = new AnalysisResult();
            result.title = "🗑️ Поиск неиспользуемых ассетов";
            result.timestamp = EditorApplication.timeSinceStartup;
            
            // Find materials not referenced by any prefab or scene
            var materials = allFiles.Where(f => f.category == FileCategory.Material).ToList();
            var usedMaterials = new HashSet<string>();
            
            foreach (var fe in allFiles)
            {
                if (fe.category == FileCategory.Prefab || fe.category == FileCategory.Scene)
                {
                    foreach (var dep in fe.dependencies)
                    {
                        if (dep.EndsWith(".mat"))
                            usedMaterials.Add(dep);
                    }
                }
            }
            
            var unusedMaterials = materials.Where(m => !usedMaterials.Contains(m.assetPath)).ToList();
            if (unusedMaterials.Count > 0)
            {
                result.warnings.Add($"Возможно неиспользуемые материалы: {unusedMaterials.Count}");
                long totalSize = 0;
                foreach (var m in unusedMaterials.Take(10))
                {
                    result.warnings.Add($"  • {m.fileName} ({FormatBytes(m.sizeBytes)})");
                    totalSize += m.sizeBytes;
                }
                result.stats.Add($"Потенциальная экономия: {FormatBytes(totalSize)}");
            }
            else
            {
                result.stats.Add("✅ Все материалы используются");
            }
            
            return result;
        }

        // ═══════════════════════════════════════════════════════════════
        // AI COMMUNICATION
        // ═══════════════════════════════════════════════════════════════
        
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
        
        private bool HasValidKey()
        {
            string key = GetApiKey();
            return !string.IsNullOrEmpty(key) && 
                   !key.StartsWith("__") && 
                   key.Length > 10;
        }
        
        private void SendMessage()
        {
            if (string.IsNullOrEmpty(userInput) || isBusy) return;
            if (!HasValidKey())
            {
                statusMsg = "❌ API ключ не настроен! Добавьте ключ через AI Keys Manager.";
                return;
            }
            
            var msg = new ChatMsg { isUser = true, text = userInput };
            history.Add(msg);
            
            var pending = new ChatMsg
            {
                isUser = false,
                isPending = true,
                startTime = EditorApplication.timeSinceStartup,
                provider = currentProvider
            };
            history.Add(pending);
            pendingIndex = history.Count - 1;
            
            string text = userInput;
            userInput = "";
            isBusy = true;
            retryCount = 0;
            statusMsg = "";
            
            var related = FindRelated(text, MAX_RELATED_SCRIPTS);
            lastJson = BuildChatJson(text, related);
            
            EditorCoroutine.Start(SendToAI(lastJson, OnAIResponse));
        }
        
        private void QuickSend(string text)
        {
            userInput = text;
            SendMessage();
        }
        
        private void OnAIResponse(string raw)
        {
            if (string.IsNullOrEmpty(raw) && retryCount < 1)
            {
                retryCount++;
                statusMsg = "⚠️ Пустой ответ, повторная попытка...";
                EditorCoroutine.Start(SendToAI(lastJson, OnAIResponse));
                return;
            }
            
            isBusy = false;
            
            if (pendingIndex >= 0 && pendingIndex < history.Count)
                history[pendingIndex].isPending = false;
            pendingIndex = -1;
            
            if (string.IsNullOrEmpty(raw))
            {
                statusMsg = "❌ Не получен ответ от AI";
                Repaint();
                return;
            }
            
            string reply = ExtractReply(raw);
            string code = ExtractCode(raw);
            
            if (pendingIndex >= 0 && pendingIndex < history.Count)
            {
                history[pendingIndex].text = reply;
                history[pendingIndex].code = code;
            }
            
            if (!string.IsNullOrEmpty(code) && autoApply)
                AutoApplyCode(code);
            
            Repaint();
        }
        
        private string BuildChatJson(string userText, List<ScriptInfo> related)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            
            // System prompt
            string systemPrompt = GetSystemPrompt();
            AppendKV(sb, "system", systemPrompt);
            sb.Append(',');
            
            // Messages
            sb.Append("\"messages\":[");
            for (int i = 0; i < history.Count - 1; i++)
            {
                if (history[i].isPending) continue;
                if (i > 0) sb.Append(',');
                sb.Append('{');
                AppendKV(sb, "role", history[i].isUser ? "user" : "assistant");
                sb.Append(',');
                AppendKV(sb, "content", history[i].text);
                sb.Append('}');
            }
            sb.Append(']');
            sb.Append(',');
            
            // Context
            sb.Append("\"context\":{");
            AppendKV(sb, "scene", ctxScene);
            sb.Append(',');
            AppendKV(sb, "selectedObject", ctxObject);
            sb.Append(',');
            
            string hier = sceneHierarchy.Length > MAX_HIERARCHY_CHARS ? 
                sceneHierarchy.Substring(0, MAX_HIERARCHY_CHARS) + "\n...[обрезано]" : sceneHierarchy;
            AppendKV(sb, "sceneHierarchy", hier);
            sb.Append(',');
            
            string fname = selectedFile != null ? selectedFile.fileName : "";
            string fpath = selectedFile != null ? selectedFile.assetPath : "";
            string ftype = selectedFile != null ? selectedFile.category + " (" + selectedFile.ext + ")" : "";
            AppendKV(sb, "scriptName", fname);
            sb.Append(',');
            AppendKV(sb, "scriptPath", fpath);
            sb.Append(',');
            AppendKV(sb, "fileType", ftype);
            sb.Append(',');
            
            string fcontent = "";
            if (selectedFile != null && selectedFile.isText)
                fcontent = fileContent.Length > MAX_FILE_CHARS ? 
                    fileContent.Substring(0, MAX_FILE_CHARS) + "\n...[обрезано]" : fileContent;
            else if (selectedFile != null && !selectedFile.isText)
                fcontent = "[Бинарный: " + (selectedFile.sizeBytes / 1024) + " KB]";
            AppendKV(sb, "scriptContent", fcontent);
            sb.Append(',');
            
            AppendKV(sb, "projectSummary", scanDone ? projectSummary.Substring(0, Math.Min(MAX_PROJECT_SUMMARY, projectSummary.Length)) : "");
            sb.Append(',');
            AppendKVBool(sb, "projectScanned", scanDone);
            sb.Append(',');
            AppendKVInt(sb, "projectScriptCount", scriptIndex.Count);
            sb.Append(',');
            AppendKVBool(sb, "autoApplyMode", autoApply);
            sb.Append(',');
            
            // Related scripts
            sb.Append("\"relatedScripts\":[");
            for (int i = 0; i < related.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var r = related[i];
                sb.Append('{');
                AppendKV(sb, "path", r.path);
                sb.Append(',');
                AppendKV(sb, "className", r.className);
                sb.Append(',');
                string rc = r.content.Length > 3000 ? r.content.Substring(0, 3000) + "\n..." : r.content;
                AppendKV(sb, "content", rc);
                sb.Append('}');
            }
            sb.Append(']');
            
            sb.Append('}');
            sb.Append(',');
            AppendKV(sb, "message", userText);
            sb.Append('}');
            
            return sb.ToString();
        }
        
        private string GetSystemPrompt()
        {
            return @"Ты Unity разработчик и AI-ассистент в Unity AI Studio Pro. 
Твоя задача — помогать с разработкой на Unity, генерировать код, отвечать на вопросы.

Правила:
1. Всегда отвечай на русском языке
2. При генерации кода — всегда полный файл целиком
3. Для изменений сцены используй [InitializeOnLoad] паттерн
4. Используй C# 7.3 (Unity 2019+)
5. НЕ используй: new() shorthand, record types, pattern matching switch expressions
6. Используй UnityWebRequest для HTTP (не HttpClient)
7. Сохраняй сцену после изменений через EditorSceneManager

При запросах на изменение сцены:
1. Прочитай иерархию сцены
2. Найди нужный объект  
3. Сгенерируй Editor-скрипт
4. Объясни что будет сделано

Формат ответа: сначала объяснение, затем полный код в блоке ```csharp```";
        }
        
        private List<ScriptInfo> FindRelated(string query, int max)
        {
            string q = query.ToLowerInvariant();
            string[] words = q.Split(new char[] { ' ', '\t', '\n', '.', ',', '?', '!' }, 
                StringSplitOptions.RemoveEmptyEntries);
            
            var scored = new List<KeyValuePair<float, ScriptInfo>>();
            string skip = selectedFile != null ? selectedFile.assetPath : "";
            
            foreach (var s in scriptIndex)
            {
                if (s.path == skip) continue;
                
                float score = 0f;
                string tgt = (s.className + " " + s.path + " " + s.baseClass).ToLowerInvariant();
                
                foreach (string w in words)
                {
                    if (w.Length < 3) continue;
                    if (tgt.Contains(w)) score += 2f;
                    foreach (var m in s.methods)
                        if (m.ToLowerInvariant().Contains(w)) score += 1f;
                }
                
                if (score > 0f)
                    scored.Add(new KeyValuePair<float, ScriptInfo>(score, s));
            }
            
            scored.Sort((a, b) => b.Key.CompareTo(a.Key));
            
            var result = new List<ScriptInfo>();
            for (int i = 0; i < Math.Min(max, scored.Count); i++)
                result.Add(scored[i].Value);
            
            return result;
        }

        // ═══════════════════════════════════════════════════════════════
        // AI API CALLS
        // ═══════════════════════════════════════════════════════════════
        
        private IEnumerator SendToAI(string json, Action<string> callback)
        {
            string url = "";
            string apiKey = GetApiKey();
            string model = GetModel();
            
            // Build request based on provider
            string requestBody = "";
            
            if (currentProvider == AIProvider.Gemini)
            {
                url = $"{GEMINI_ENDPOINT}/models/{model}:generateContent?key={apiKey}";
                requestBody = ConvertToGeminiFormat(json);
            }
            else if (currentProvider == AIProvider.DeepSeek)
            {
                url = $"{DEEPSEEK_ENDPOINT}/chat/completions";
                requestBody = ConvertToOpenAIFormat(json, model);
            }
            else if (currentProvider == AIProvider.Groq)
            {
                url = $"{GROQ_ENDPOINT}/chat/completions";
                requestBody = ConvertToOpenAIFormat(json, model);
            }
            
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
            }
            else
            {
                string errDetail = req.error ?? "Нет соединения";
                long code = req.responseCode;
                if (code > 0) errDetail = "HTTP " + code + ": " + errDetail;
                statusMsg = "⚠️ " + errDetail;
                result = "";
            }
            
            req.Dispose();
            callback?.Invoke(result);
        }
        
        private string ConvertToGeminiFormat(string json)
        {
            // Parse our format and convert to Gemini format
            string message = JsonGet(json, "message");
            string system = JsonGet(json, "system");
            string context = JsonGetString(json, "context");
            
            string fullPrompt = system + "\n\n";
            if (!string.IsNullOrEmpty(context))
                fullPrompt += "КОНТЕКСТ:\n" + context + "\n\n";
            fullPrompt += "ЗАПРОС: " + message;
            
            var sb = new StringBuilder();
            sb.Append("{\"contents\":[{\"parts\":[{\"text\":\"");
            AppendEscaped(sb, fullPrompt);
            sb.Append("\"}]}],\"generationConfig\":{\"maxOutputTokens\":");
            sb.Append(maxTokens);
            sb.Append("}}");
            
            return sb.ToString();
        }
        
        private string ConvertToOpenAIFormat(string json, string model)
        {
            string message = JsonGet(json, "message");
            string system = JsonGet(json, "system");
            string context = JsonGetString(json, "context");
            
            var sb = new StringBuilder();
            sb.Append("{\"model\":\"");
            sb.Append(model);
            sb.Append("\",\"messages\":[");
            
            // System message
            sb.Append("{\"role\":\"system\",\"content\":\"");
            AppendEscaped(sb, system);
            sb.Append("\"},");
            
            // Context as system message
            if (!string.IsNullOrEmpty(context))
            {
                sb.Append("{\"role\":\"system\",\"content\":\"КОНТЕКСТ:\\n");
                AppendEscaped(sb, context);
                sb.Append("\"},");
            }
            
            // Previous messages from history
            bool first = true;
            foreach (var msg in history)
            {
                if (msg.isPending) continue;
                if (!first) sb.Append(',');
                sb.Append("{\"role\":\"");
                sb.Append(msg.isUser ? "user" : "assistant");
                sb.Append("\",\"content\":\"");
                AppendEscaped(sb, msg.text);
                sb.Append("\"}");
                first = false;
            }
            
            sb.Append("],\"max_tokens\":");
            sb.Append(maxTokens);
            sb.Append("}");
            
            return sb.ToString();
        }

        // ═══════════════════════════════════════════════════════════════
        // CODE APPLICATION
        // ═══════════════════════════════════════════════════════════════
        
        private void AutoApplyCode(string code)
        {
            try
            {
                string path = "Assets/Editor/AIStudio_Edit_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".cs";
                File.WriteAllText(path, code);
                AssetDatabase.Refresh();
                statusMsg = "✅ Код применён: " + path;
            }
            catch (Exception e)
            {
                statusMsg = "❌ Ошибка применения: " + e.Message;
            }
        }
        
        private void SaveCodeFromEditor()
        {
            if (string.IsNullOrEmpty(codeEditorPath) || string.IsNullOrEmpty(codeEditor)) return;
            
            try
            {
                string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", codeEditorPath));
                File.WriteAllText(fullPath, codeEditor);
                AssetDatabase.Refresh();
                codeModified = false;
                statusMsg = "✅ Файл сохранён: " + codeEditorPath;
            }
            catch (Exception e)
            {
                statusMsg = "❌ Ошибка сохранения: " + e.Message;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GITHUB INTEGRATION
        // ═══════════════════════════════════════════════════════════════
        
        private void GitHubPushFile(string assetPath, string content, string commitMsg)
        {
            if (string.IsNullOrEmpty(ghToken))
            {
                ghStatus = "❌ Токен не задан";
                return;
            }
            
            ghBusy = true;
            EditorCoroutine.Start(GitHubPushRoutine(assetPath, content, commitMsg));
        }
        
        private IEnumerator GitHubPushRoutine(string assetPath, string content, string commitMsg)
        {
            ghStatus = "⏳ Получение SHA...";
            
            string apiUrl = $"https://api.github.com/repos/{ghOwner}/{ghRepo}/contents/{assetPath}";
            
            // GET current SHA
            var getReq = UnityWebRequest.Get(apiUrl);
            getReq.SetRequestHeader("Authorization", "token " + ghToken);
            getReq.SetRequestHeader("User-Agent", "UnityAIStudio");
            yield return getReq.SendWebRequest();
            
            string sha = "";
            if (getReq.result == UnityWebRequest.Result.Success)
            {
                sha = JsonGet(getReq.downloadHandler.text, "sha");
            }
            getReq.Dispose();
            
            // PUT new content
            ghStatus = "⏳ Отправка...";
            
            string jsonBody = $"{{\"message\":\"{commitMsg}\",\"content\":\"{Convert.ToBase64String(Encoding.UTF8.GetBytes(content))}\",\"branch\":\"{ghBranch}\"";
            if (!string.IsNullOrEmpty(sha)) jsonBody += $",\"sha\":\"{sha}\"";
            jsonBody += "}";
            
            var putReq = UnityWebRequest.Put(apiUrl, jsonBody);
            putReq.SetRequestHeader("Authorization", "token " + ghToken);
            putReq.SetRequestHeader("User-Agent", "UnityAIStudio");
            putReq.SetRequestHeader("Content-Type", "application/json");
            yield return putReq.SendWebRequest();
            
            if (putReq.result == UnityWebRequest.Result.Success)
                ghStatus = "✅ Отправлено!";
            else
                ghStatus = "❌ Ошибка: " + putReq.error;
            
            putReq.Dispose();
            ghBusy = false;
            Repaint();
        }

        // ═══════════════════════════════════════════════════════════════
        // GLOBAL SEARCH
        // ═══════════════════════════════════════════════════════════════
        
        private void PerformGlobalSearch(string query)
        {
            searchResults.Clear();
            if (string.IsNullOrEmpty(query) || query.Length < 2) return;
            
            string q = query.ToLowerInvariant();
            
            // Search in file names
            foreach (var fe in allFiles)
            {
                if (fe.fileName.ToLowerInvariant().Contains(q))
                    searchResults.Add($"📄 {fe.assetPath}");
            }
            
            // Search in script content
            foreach (var s in scriptIndex)
            {
                if (s.className.ToLowerInvariant().Contains(q))
                    searchResults.Add($"🔵 {s.className} ({s.path})");
                else if (s.content.ToLowerInvariant().Contains(q))
                {
                    int idx = s.content.ToLowerInvariant().IndexOf(q);
                    int start = Math.Max(0, idx - 30);
                    int len = Math.Min(60, s.content.Length - start);
                    string snippet = s.content.Substring(start, len).Replace('\n', ' ').Trim();
                    searchResults.Add($"🔍 {s.className}: ...{snippet}...");
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // BATCH TOOLS
        // ═══════════════════════════════════════════════════════════════
        
        private void FixMissingScripts()
        {
            int fixed_count = 0;
            var prefabs = allFiles.Where(f => f.category == FileCategory.Prefab).ToList();
            
            foreach (var fe in prefabs)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(fe.assetPath);
                if (go == null) continue;
                
                var components = go.GetComponentsInChildren<Component>();
                foreach (var comp in components)
                {
                    if (comp == null)
                    {
                        // Missing script found
                        Debug.LogWarning($"Missing script in: {fe.assetPath}");
                        fixed_count++;
                    }
                }
            }
            
            statusMsg = $"🔍 Найдено {fixed_count} пропущенных скриптов в prefab'ах";
        }
        
        private void AnalyzeUnusedMaterials()
        {
            var result = FindUnusedAssets();
            analysisResults.Clear();
            analysisResults.Add(result);
            analysisTab = 4; // Switch to results tab
        }

        // ═══════════════════════════════════════════════════════════════
        // GUI DRAWING
        // ═══════════════════════════════════════════════════════════════
        
        private void OnGUI()
        {
            DrawToolbar();
            
            string[] mainTabs = { "💬 AI Чат", "📁 Файлы", "🔍 Анализ", "🔎 Поиск", "✏️ Редактор", "🔧 Инструменты", "🐙 GitHub", "⚙️ Настройки" };
            mainTab = GUILayout.Toolbar(mainTab, mainTabs);
            
            GUILayout.Space(4);
            
            switch (mainTab)
            {
                case 0: DrawChatTab(); break;
                case 1: DrawFilesTab(); break;
                case 2: DrawAnalysisTab(); break;
                case 3: DrawSearchTab(); break;
                case 4: DrawCodeEditorTab(); break;
                case 5: DrawToolsTab(); break;
                case 6: DrawGitHubTab(); break;
                case 7: DrawSettingsTab(); break;
            }
        }
        
        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            GUILayout.Label($"🤖 AI Studio Pro v{VERSION}", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            // Provider selector
            AIProvider newProvider = (AIProvider)EditorGUILayout.EnumPopup(currentProvider, GUILayout.Width(100));
            if (newProvider != currentProvider)
            {
                currentProvider = newProvider;
                SaveSettings();
            }
            
            GUILayout.FlexibleSpace();
            
            // Auto-apply toggle
            bool newAuto = GUILayout.Toggle(autoApply, " ⚡ Авто", EditorStyles.miniButton, GUILayout.Width(60));
            if (newAuto != autoApply)
            {
                autoApply = newAuto;
                SaveSettings();
            }
            
            // Scan status
            if (scanRunning)
            {
                GUI.color = Color.yellow;
                GUILayout.Label($"⏳ {scanProgress}/{scanTotal}", EditorStyles.miniLabel);
                GUI.color = Color.white;
                Repaint();
            }
            else if (scanDone)
            {
                GUI.color = Color.green;
                GUILayout.Label($"✅ {allFiles.Count} файлов", EditorStyles.miniLabel);
                GUI.color = Color.white;
            }
            
            GUILayout.EndHorizontal();
            
            // Context bar
            if (!string.IsNullOrEmpty(ctxScene))
            {
                GUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label($"🌍 {ctxScene}", EditorStyles.boldLabel);
                if (!string.IsNullOrEmpty(ctxObject))
                    GUILayout.Label($"→ {ctxObject}", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("🔄", EditorStyles.miniButton, GUILayout.Width(24)))
                    sceneHierarchy = ScanSceneHierarchy();
                GUILayout.EndHorizontal();
            }
            
            // Status message
            if (!string.IsNullOrEmpty(statusMsg))
            {
                var style = new GUIStyle(EditorStyles.helpBox);
                style.wordWrap = true;
                GUILayout.Label(statusMsg, style);
            }
        }
        
        // ── Chat Tab ─────────────────────────────────────────────────
        private void DrawChatTab()
        {
            // Quick actions
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("📋 Что в сцене?", GUILayout.Height(24)))
                QuickSend("Опиши всё что есть в сцене " + ctxScene);
            if (GUILayout.Button("🐛 Найди баги", GUILayout.Height(24)))
                QuickSend("Найди проблемы в сцене " + ctxScene);
            if (GUILayout.Button("⚡ Оптимизация", GUILayout.Height(24)))
                QuickSend("Как оптимизировать сцену " + ctxScene + "?");
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("➕ Добавь объект", GUILayout.Height(24)))
                QuickSend("В сцене " + ctxScene + " добавь: ");
            if (GUILayout.Button("🔄 Refactor", GUILayout.Height(24)))
                QuickSend("Проведи рефакторинг кода в " + (selectedFile?.fileName ?? "выбранном файле"));
            GUILayout.EndHorizontal();
            
            GUILayout.Space(4);
            
            // Chat area
            float chatH = position.height - 280;
            chatScroll = EditorGUILayout.BeginScrollView(chatScroll, GUILayout.Height(Math.Max(100, chatH)));
            
            foreach (var msg in history)
                DrawBubble(msg);
            
            EditorGUILayout.EndScrollView();
            
            // Input
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUI.enabled = !isBusy;
            userInput = GUILayout.TextField(userInput, GUILayout.Height(40));
            bool send = GUILayout.Button(isBusy ? "⏳" : "➤", GUILayout.Width(42), GUILayout.Height(40));
            GUI.enabled = true;
            
            if (send || (Event.current.type == EventType.KeyDown && 
                         Event.current.keyCode == KeyCode.Return && 
                         !string.IsNullOrEmpty(userInput) && !isBusy))
            {
                SendMessage();
                Event.current.Use();
            }
            GUILayout.EndHorizontal();
            
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
        
        private void DrawBubble(ChatMsg msg)
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            
            if (msg.isPending)
            {
                double elapsed = EditorApplication.timeSinceStartup - msg.startTime;
                string[] dots = { "●○○", "○●○", "○○●" };
                string anim = dots[(int)(elapsed * 2) % 3];
                string providerIcon = GetProviderIcon(msg.provider);
                
                GUI.color = new Color(0.5f, 0.9f, 1f);
                GUILayout.Label($"{providerIcon} AI:", EditorStyles.boldLabel);
                GUI.color = Color.white;
                GUILayout.Label($"  {anim} Думаю... ({elapsed:F0}с)");
            }
            else
            {
                string icon = msg.isUser ? "👤" : GetProviderIcon(msg.provider);
                GUI.color = msg.isUser ? new Color(0.7f, 1f, 0.7f) : Color.white;
                GUILayout.Label($"{icon} {(msg.isUser ? "Вы" : "AI")}:", EditorStyles.boldLabel);
                GUI.color = Color.white;
                
                var style = new GUIStyle(EditorStyles.label) { wordWrap = true };
                GUILayout.Label(msg.text, style);
                
                if (!string.IsNullOrEmpty(msg.code))
                {
                    GUILayout.Space(4);
                    var cs = new GUIStyle(EditorStyles.textArea) { wordWrap = false, fontSize = 10 };
                    GUILayout.Label("📝 Код:", EditorStyles.miniLabel);
                    string displayCode = msg.code.Length > 2000 ? msg.code.Substring(0, 2000) + "\n..." : msg.code;
                    GUILayout.TextArea(displayCode, cs, GUILayout.Height(120));
                    
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("📋 Копировать код", GUILayout.Height(22)))
                    {
                        EditorGUIUtility.systemCopyBuffer = msg.code;
                        statusMsg = "✅ Код скопирован!";
                    }
                    if (!autoApply && GUILayout.Button("⚡ Применить", GUILayout.Height(22)))
                        AutoApplyCode(msg.code);
                    GUILayout.EndHorizontal();
                }
            }
            
            GUILayout.EndVertical();
        }
        
        private string GetProviderIcon(AIProvider p)
        {
            switch (p)
            {
                case AIProvider.Gemini: return "✨";
                case AIProvider.DeepSeek: return "🐋";
                case AIProvider.Groq: return "⚡";
                default: return "🤖";
            }
        }
        
        // ── Files Tab ────────────────────────────────────────────────
        private void DrawFilesTab()
        {
            GUILayout.BeginHorizontal();
            browserSearch = EditorGUILayout.TextField("🔍", browserSearch);
            showAllTypes = GUILayout.Toggle(showAllTypes, "Все типы", EditorStyles.miniButton, GUILayout.Width(70));
            GUILayout.EndHorizontal();
            
            // Category filter
            GUILayout.BeginHorizontal();
            if (!showAllTypes)
            {
                var categories = (FileCategory[])Enum.GetValues(typeof(FileCategory));
                foreach (var cat in categories)
                {
                    bool isActive = browserFilter == cat;
                    GUI.color = isActive ? Color.white : new Color(0.7f, 0.7f, 0.7f);
                    if (GUILayout.Toggle(isActive, GetCategoryIcon(cat) + " " + cat, EditorStyles.miniButton))
                        browserFilter = cat;
                }
                GUI.color = Color.white;
            }
            GUILayout.EndHorizontal();
            
            // File list
            browserScroll = EditorGUILayout.BeginScrollView(browserScroll);
            
            var filtered = allFiles.Where(f =>
            {
                if (!showAllTypes && f.category != browserFilter) return false;
                if (!string.IsNullOrEmpty(browserSearch) && 
                    !f.fileName.ToLowerInvariant().Contains(browserSearch.ToLowerInvariant()) &&
                    !f.assetPath.ToLowerInvariant().Contains(browserSearch.ToLowerInvariant()))
                    return false;
                return true;
            }).ToList();
            
            GUILayout.Label($"Показано: {filtered.Count} из {allFiles.Count}", EditorStyles.miniLabel);
            
            foreach (var fe in filtered.Take(200))
            {
                GUILayout.BeginHorizontal(EditorStyles.helpBox);
                
                GUI.color = GetCategoryColor(fe.category);
                GUILayout.Label(GetCategoryIcon(fe.category), GUILayout.Width(20));
                GUI.color = Color.white;
                
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
            
            // Selected file info
            if (selectedFile != null)
            {
                GUILayout.Space(4);
                GUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label($"📄 {selectedFile.fileName}", EditorStyles.boldLabel);
                GUILayout.Label($"Путь: {selectedFile.assetPath}", EditorStyles.miniLabel);
                GUILayout.Label($"Тип: {selectedFile.category} | Размер: {FormatBytes(selectedFile.sizeBytes)}", EditorStyles.miniLabel);
                if (selectedFile.dependencies.Count > 0)
                    GUILayout.Label($"Зависимости: {selectedFile.dependencies.Count}", EditorStyles.miniLabel);
                GUILayout.EndVertical();
            }
        }
        
        // ── Analysis Tab ─────────────────────────────────────────────
        private void DrawAnalysisTab()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("📊 Производительность", GUILayout.Height(30)))
            {
                analysisResults.Clear();
                analysisResults.Add(AnalyzePerformance());
            }
            if (GUILayout.Button("🔍 Качество кода", GUILayout.Height(30)))
            {
                analysisResults.Clear();
                analysisResults.Add(AnalyzeCodeQuality());
            }
            if (GUILayout.Button("🔗 Зависимости", GUILayout.Height(30)))
            {
                analysisResults.Clear();
                analysisResults.Add(AnalyzeDependencies());
            }
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🗑 Неиспользуемые", GUILayout.Height(30)))
                AnalyzeUnusedMaterials();
            if (GUILayout.Button("📐 Сцена", GUILayout.Height(30)))
            {
                analysisResults.Clear();
                var r = new AnalysisResult { title = "📐 Анализ сцены", timestamp = EditorApplication.timeSinceStartup };
                r.stats.Add($"Объектов: {sceneObjects.Count}");
                r.stats.Add($"Активных: {sceneObjects.Count(o => o.isActive)}");
                r.stats.Add($"С компонентами: {sceneObjects.Count(o => o.components.Count > 0)}");
                
                var topComponents = sceneObjects
                    .SelectMany(o => o.components)
                    .GroupBy(c => c)
                    .OrderByDescending(g => g.Count())
                    .Take(10);
                r.stats.Add("Топ компонентов:");
                foreach (var c in topComponents)
                    r.stats.Add($"  • {c.Key}: {c.Count()}");
                
                analysisResults.Add(r);
            }
            if (GUILayout.Button("🔄 Полный анализ", GUILayout.Height(30)))
            {
                analysisResults.Clear();
                analysisResults.Add(AnalyzePerformance());
                analysisResults.Add(AnalyzeCodeQuality());
                analysisResults.Add(AnalyzeDependencies());
                analysisResults.Add(FindUnusedAssets());
            }
            GUILayout.EndHorizontal();
            
            GUILayout.Space(8);
            
            // Results
            mainScroll = EditorGUILayout.BeginScrollView(mainScroll);
            foreach (var r in analysisResults)
            {
                GUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label(r.title, EditorStyles.boldLabel);
                
                if (r.stats.Count > 0)
                {
                    GUILayout.Label("📊 Статистика:", EditorStyles.miniBoldLabel);
                    foreach (var s in r.stats)
                        GUILayout.Label("  " + s, EditorStyles.miniLabel);
                }
                
                if (r.warnings.Count > 0)
                {
                    GUILayout.Space(4);
                    GUI.color = Color.yellow;
                    GUILayout.Label("⚠️ Предупреждения:", EditorStyles.miniBoldLabel);
                    GUI.color = Color.white;
                    foreach (var w in r.warnings)
                    {
                        GUI.color = new Color(1f, 0.9f, 0.5f);
                        GUILayout.Label("  " + w, EditorStyles.wordWrappedMiniLabel);
                        GUI.color = Color.white;
                    }
                }
                
                if (r.suggestions.Count > 0)
                {
                    GUILayout.Space(4);
                    GUILayout.Label("💡 Рекомендации:", EditorStyles.miniBoldLabel);
                    foreach (var s in r.suggestions)
                        GUILayout.Label("  " + s, EditorStyles.miniLabel);
                }
                
                GUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }
        
        // ── Search Tab ───────────────────────────────────────────────
        private void DrawSearchTab()
        {
            GUILayout.BeginHorizontal();
            string newSearch = EditorGUILayout.TextField("🔎 Глобальный поиск", globalSearch);
            if (newSearch != globalSearch)
            {
                globalSearch = newSearch;
                PerformGlobalSearch(globalSearch);
            }
            if (GUILayout.Button("🔍", GUILayout.Width(30)))
                PerformGlobalSearch(globalSearch);
            GUILayout.EndHorizontal();
            
            searchScroll = EditorGUILayout.BeginScrollView(searchScroll);
            
            if (searchResults.Count == 0 && !string.IsNullOrEmpty(globalSearch))
                GUILayout.Label("Ничего не найдено", EditorStyles.centeredGreyMiniLabel);
            
            foreach (var r in searchResults)
            {
                if (GUILayout.Button(r, EditorStyles.label))
                {
                    // Try to select the asset
                    string path = r.Split('(').Last().TrimEnd(')');
                    if (path.StartsWith("Assets/"))
                        Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                }
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        // ── Code Editor Tab ──────────────────────────────────────────
        private void DrawCodeEditorTab()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"✏️ {codeEditorPath ?? "Выберите файл"}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            
            GUI.enabled = codeModified;
            if (GUILayout.Button("💾 Сохранить", GUILayout.Width(80)))
                SaveCodeFromEditor();
            GUI.enabled = true;
            
            if (codeModified)
            {
                GUI.color = Color.yellow;
                GUILayout.Label("●", EditorStyles.boldLabel);
                GUI.color = Color.white;
            }
            GUILayout.EndHorizontal();
            
            GUI.enabled = !string.IsNullOrEmpty(codeEditorPath);
            codeScroll = EditorGUILayout.BeginScrollView(codeScroll);
            
            var style = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = false,
                font = Font.CreateDynamicFontFromOSFont("Consolas", 12),
                fontSize = 12
            };
            
            string newCode = EditorGUILayout.TextArea(codeEditor, style, GUILayout.ExpandHeight(true));
            if (newCode != codeEditor)
            {
                codeEditor = newCode;
                codeModified = true;
            }
            
            EditorGUILayout.EndScrollView();
            GUI.enabled = true;
        }
        
        // ── Tools Tab ────────────────────────────────────────────────
        private void DrawToolsTab()
        {
            GUILayout.Label("🔧 Batch инструменты", EditorStyles.boldLabel);
            GUILayout.Space(4);
            
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Поиск проблем", EditorStyles.boldLabel);
            
            if (GUILayout.Button("🔍 Найти пропущенные скрипты в Prefab'ах"))
                FixMissingScripts();
            
            if (GUILayout.Button("📦 Найти неиспользуемые материалы"))
                AnalyzeUnusedMaterials();
            
            if (GUILayout.Button("🖼️ Найти большие текстуры (>4MB)"))
            {
                var largeTextures = allFiles
                    .Where(f => f.category == FileCategory.Texture && f.sizeBytes > 4 * 1024 * 1024)
                    .OrderByDescending(f => f.sizeBytes)
                    .ToList();
                statusMsg = $"Найдено {largeTextures.Count} текстур больше 4MB";
                foreach (var t in largeTextures.Take(10))
                    Debug.Log($"Large texture: {t.assetPath} ({FormatBytes(t.sizeBytes)})");
            }
            
            GUILayout.EndVertical();
            
            GUILayout.Space(8);
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Сцена", EditorStyles.boldLabel);
            
            if (GUILayout.Button("📊 Подсчитать объекты в сцене"))
            {
                ScanSceneObjects();
                statusMsg = $"Объектов: {sceneObjects.Count}, активных: {sceneObjects.Count(o => o.isActive)}";
            }
            
            if (GUILayout.Button("🔄 Обновить иерархию"))
            {
                sceneHierarchy = ScanSceneHierarchy();
                ScanSceneObjects();
                statusMsg = "Иерархия обновлена";
            }
            
            if (GUILayout.Button("🔎 Найти объекты без компонентов"))
            {
                var empty = sceneObjects.Where(o => o.components.Count == 0 && o.childCount == 0).ToList();
                statusMsg = $"Найдено {empty.Count} пустых объектов";
                foreach (var o in empty.Take(20))
                    Debug.Log($"Empty: {o.path}");
            }
            
            GUILayout.EndVertical();
            
            GUILayout.Space(8);
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Код", EditorStyles.boldLabel);
            
            if (GUILayout.Button("📋 Экспорт списка всех классов"))
            {
                var sb = new StringBuilder();
                foreach (var s in scriptIndex.OrderBy(s => s.className))
                    sb.AppendLine($"{s.className} : {s.baseClass} ({s.lineCount}л) - {s.path}");
                
                EditorGUIUtility.systemCopyBuffer = sb.ToString();
                statusMsg = $"Скопировано {scriptIndex.Count} классов";
            }
            
            if (GUILayout.Button("📊 Топ 20 самых больших скриптов"))
            {
                var largest = scriptIndex.OrderByDescending(s => s.lineCount).Take(20).ToList();
                Debug.Log("=== Top 20 Largest Scripts ===");
                foreach (var s in largest)
                    Debug.Log($"{s.lineCount} lines: {s.className} ({s.path})");
                statusMsg = "Смотрите Console (Ctrl+Shift+C)";
            }
            
            GUILayout.EndVertical();
        }
        
        // ── GitHub Tab ───────────────────────────────────────────────
        private void DrawGitHubTab()
        {
            mainScroll = EditorGUILayout.BeginScrollView(mainScroll);
            
            GUILayout.Label("🐙 GitHub настройки", EditorStyles.boldLabel);
            GUILayout.BeginVertical(EditorStyles.helpBox);
            
            ghOwner = EditorGUILayout.TextField("Owner", ghOwner);
            ghRepo = EditorGUILayout.TextField("Repo", ghRepo);
            ghBranch = EditorGUILayout.TextField("Branch", ghBranch);
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Token", GUILayout.Width(50));
            ghToken = EditorGUILayout.PasswordField(ghToken);
            GUILayout.EndHorizontal();
            
            GUILayout.Label("Токен хранится в EditorPrefs", EditorStyles.miniLabel);
            
            if (GUILayout.Button("💾 Сохранить"))
            {
                SaveSettings();
                ghStatus = "✅ Сохранено";
            }
            GUILayout.EndVertical();
            
            if (!string.IsNullOrEmpty(ghStatus))
                GUILayout.Label(ghStatus, EditorStyles.helpBox);
            
            GUILayout.Space(8);
            GUILayout.Label("Push файлов", EditorStyles.boldLabel);
            GUILayout.BeginVertical(EditorStyles.helpBox);
            
            GUI.enabled = !ghBusy && !string.IsNullOrEmpty(ghToken) && selectedFile != null && selectedFile.isText;
            if (selectedFile != null && GUILayout.Button($"🐙 Push: {selectedFile.fileName}"))
                GitHubPushFile(selectedFile.assetPath, fileContent, "edit: " + selectedFile.fileName);
            GUI.enabled = true;
            
            GUILayout.EndVertical();
            
            EditorGUILayout.EndScrollView();
        }
        
        // ── Settings Tab ─────────────────────────────────────────────
        private void DrawSettingsTab()
        {
            mainScroll = EditorGUILayout.BeginScrollView(mainScroll);
            
            GUILayout.Label("⚙️ Настройки", EditorStyles.boldLabel);
            
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("AI", EditorStyles.boldLabel);
            
            AIProvider newProv = (AIProvider)EditorGUILayout.EnumPopup("Провайдер", currentProvider);
            if (newProv != currentProvider)
            {
                currentProvider = newProv;
                SaveSettings();
            }
            
            maxTokens = EditorGUILayout.IntSlider("Max Tokens", maxTokens, 1024, 16384);
            
            bool newAuto = EditorGUILayout.Toggle("Авто-применение кода", autoApply);
            if (newAuto != autoApply)
            {
                autoApply = newAuto;
                SaveSettings();
            }
            
            GUILayout.EndVertical();
            
            GUILayout.Space(8);
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Анализ", EditorStyles.boldLabel);
            
            AnalysisMode newMode = (AnalysisMode)EditorGUILayout.EnumPopup("Режим анализа", analysisMode);
            if (newMode != analysisMode)
            {
                analysisMode = newMode;
                SaveSettings();
            }
            
            GUILayout.EndVertical();
            
            GUILayout.Space(8);
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Информация", EditorStyles.boldLabel);
            GUILayout.Label($"Версия: {VERSION}", EditorStyles.miniLabel);
            GUILayout.Label($"Файлов: {allFiles.Count}", EditorStyles.miniLabel);
            GUILayout.Label($"Скриптов: {scriptIndex.Count}", EditorStyles.miniLabel);
            GUILayout.Label($"Сцена: {ctxScene}", EditorStyles.miniLabel);
            GUILayout.EndVertical();
            
            EditorGUILayout.EndScrollView();
        }

        // ═══════════════════════════════════════════════════════════════
        // UTILITY METHODS
        // ═══════════════════════════════════════════════════════════════
        
        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }
        
        private static string GetCategoryIcon(FileCategory cat)
        {
            switch (cat)
            {
                case FileCategory.Script: return "📜";
                case FileCategory.Scene: return "🌍";
                case FileCategory.Prefab: return "📦";
                case FileCategory.Material: return "🎨";
                case FileCategory.Shader: return "✨";
                case FileCategory.Texture: return "🖼️";
                case FileCategory.Audio: return "🔊";
                case FileCategory.Model: return "📐";
                case FileCategory.Animation: return "🎬";
                case FileCategory.Config: return "⚙️";
                case FileCategory.Font: return "🔤";
                case FileCategory.Video: return "🎥";
                default: return "📄";
            }
        }
        
        private static Color GetCategoryColor(FileCategory cat)
        {
            switch (cat)
            {
                case FileCategory.Script: return new Color(0.6f, 1f, 0.6f);
                case FileCategory.Scene: return new Color(1f, 0.9f, 0.4f);
                case FileCategory.Prefab: return new Color(0.5f, 0.8f, 1f);
                case FileCategory.Material: return new Color(1f, 0.6f, 0.4f);
                case FileCategory.Shader: return new Color(0.9f, 0.5f, 1f);
                case FileCategory.Texture: return new Color(1f, 0.8f, 0.9f);
                case FileCategory.Model: return new Color(0.7f, 0.7f, 1f);
                case FileCategory.Animation: return new Color(1f, 0.7f, 0.5f);
                default: return Color.white;
            }
        }
        
        private static void AppendKV(StringBuilder sb, string k, string v)
        {
            sb.Append('"'); sb.Append(k); sb.Append('"'); sb.Append(':'); sb.Append('"');
            AppendEscaped(sb, v ?? "");
            sb.Append('"');
        }
        
        private static void AppendKVBool(StringBuilder sb, string k, bool v)
        {
            sb.Append('"'); sb.Append(k); sb.Append('"'); sb.Append(':');
            sb.Append(v ? "true" : "false");
        }
        
        private static void AppendKVInt(StringBuilder sb, string k, int v)
        {
            sb.Append('"'); sb.Append(k); sb.Append('"'); sb.Append(':');
            sb.Append(v.ToString());
        }
        
        private static void AppendEscaped(StringBuilder sb, string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            foreach (char c in s)
            {
                if (c == '"') { sb.Append('\\'); sb.Append('"'); }
                else if (c == '\\') { sb.Append('\\'); sb.Append('\\'); }
                else if (c == '\n') { sb.Append('\\'); sb.Append('n'); }
                else if (c == '\r') { sb.Append('\\'); sb.Append('r'); }
                else if (c == '\t') { sb.Append('\\'); sb.Append('t'); }
                else sb.Append(c);
            }
        }
        
        private static string JsonGet(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return "";
            string needle = "\"" + key + "\"";
            int pos = json.IndexOf(needle);
            if (pos < 0) return "";
            int i = pos + needle.Length;
            while (i < json.Length && json[i] != ':') i++;
            i++;
            while (i < json.Length && json[i] == ' ') i++;
            if (i >= json.Length || json[i] != '"') return "";
            i++;
            var sb = new StringBuilder();
            while (i < json.Length)
            {
                if (json[i] == '\\' && i + 1 < json.Length)
                {
                    char next = json[i + 1];
                    if (next == 'n') sb.Append('\n');
                    else if (next == 'r') sb.Append('\r');
                    else if (next == 't') sb.Append('\t');
                    else sb.Append(next);
                    i += 2;
                }
                else if (json[i] == '"') break;
                else { sb.Append(json[i]); i++; }
            }
            return sb.ToString();
        }
        
        private static string JsonGetString(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return "";
            string needle = "\"" + key + "\"";
            int pos = json.IndexOf(needle);
            if (pos < 0) return "";
            int braceStart = json.IndexOf('{', pos);
            if (braceStart < 0) return "";
            int depth = 1;
            int i = braceStart + 1;
            while (i < json.Length && depth > 0)
            {
                if (json[i] == '{') depth++;
                else if (json[i] == '}') depth--;
                i++;
            }
            return json.Substring(braceStart, i - braceStart);
        }
        
        private static string ExtractReply(string json)
        {
            // Try different response formats
            string reply = JsonGet(json, "reply");
            if (!string.IsNullOrEmpty(reply)) return reply;
            
            // Gemini format
            string text = JsonGet(json, "text");
            if (!string.IsNullOrEmpty(text)) return text;
            
            // Try to extract from candidates
            int candIdx = json.IndexOf("\"candidates\"");
            if (candIdx > 0)
            {
                int textIdx = json.IndexOf("\"text\"", candIdx);
                if (textIdx > 0)
                    return JsonGet(json.Substring(textIdx), "text");
            }
            
            return json.Length > 500 ? json.Substring(0, 500) : json;
        }
        
        private static string ExtractCode(string json)
        {
            // Try to find code blocks
            var patterns = new[] { "csharp", "hlsl", "yaml", "json", "xml", "c#" };
            
            foreach (var lang in patterns)
            {
                string start = "```" + lang;
                int sIdx = json.IndexOf(start);
                if (sIdx < 0) continue;
                sIdx += start.Length;
                if (sIdx < json.Length && json[sIdx] == '\n') sIdx++;
                int eIdx = json.IndexOf("```", sIdx);
                if (eIdx > sIdx)
                    return json.Substring(sIdx, eIdx - sIdx).Trim();
            }
            
            // Try any code block
            int anyStart = json.IndexOf("```");
            if (anyStart >= 0)
            {
                anyStart += 3;
                int lineEnd = json.IndexOf('\n', anyStart);
                if (lineEnd > 0) anyStart = lineEnd + 1;
                int anyEnd = json.IndexOf("```", anyStart);
                if (anyEnd > anyStart)
                    return json.Substring(anyStart, anyEnd - anyStart).Trim();
            }
            
            return "";
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EDITOR COROUTINE HELPER
    // ═══════════════════════════════════════════════════════════════════════
    
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
            
            if (!routine.MoveNext())
            {
                EditorApplication.update -= Tick;
                return;
            }
            current = routine.Current;
        }
    }
}
