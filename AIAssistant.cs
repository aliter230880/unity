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

namespace UnityAIAssistant
{
    public enum AIMode { Ask, Agent, Plan }
    public enum AIProvider { Gemini, DeepSeek, Groq }
    public enum ActionType { None, CreateObject, ModifyObject, DeleteObject, CreateScript, ModifyScript, CreateMaterial }
    
    [Serializable]
    public class PendingAction
    {
        public ActionType type;
        public string description;
        public string code;
        public string targetObject;
        public Vector3 position;
        public Vector3 scale = Vector3.one;
        public Vector3 rotation;
        public string componentName;
        public string propertyName;
        public string propertyValue;
        public bool approved;
    }
    
    [Serializable]
    public class ChatMessage
    {
        public bool isUser;
        public string text;
        public string code;
        public List<PendingAction> actions = new List<PendingAction>();
        public bool isPending;
        public double startTime;
        public AIProvider provider;
        public AIMode mode;
    }

    public class UnityAIAssistantWindow : EditorWindow
    {
        // ═══ API КЛЮЧИ - ЗАМЕНИ НА СВОИ ═══
        private const string GEMINI_KEY = "ВСТАВЬ_GEMINI";
        private const string GEMINI_MODEL = "gemini-2.5-flash";
        private const string DEEPSEEK_KEY = "ВСТАВЬ_DEEPSEEK";
        private const string DEEPSEEK_MODEL = "deepseek-chat";
        private const string GROQ_KEY = "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";
        private const string GROQ_MODEL = "llama-3.3-70b-versatile";
        // ═══════════════════════════════════
        
        private AIProvider provider = AIProvider.Groq;
        private AIMode currentMode = AIMode.Ask;
        private Vector2 chatScroll;
        private string userInput = "";
        private bool isBusy = false;
        private string statusMsg = "";
        private List<ChatMessage> history = new List<ChatMessage>();
        private int pendingIndex = -1;
        private List<PendingAction> pendingActions = new List<PendingAction>();
        private bool showPreview = false;
        private string sceneName = "";
        private string selectedObj = "";
        private List<GameObject> sceneObjects = new List<GameObject>();

        [MenuItem("Window/AI Assistant")]
        static void ShowWindow()
        {
            var w = GetWindow<UnityAIAssistantWindow>("AI Assistant");
            w.minSize = new Vector2(450, 600);
        }
        
        void OnEnable()
        {
            EditorApplication.update += OnUpdate;
            RefreshContext();
            history.Add(new ChatMessage
            {
                isUser = false,
                text = "Привет! Я AI-ассистент для Unity.\n\n" +
                       "Режимы:\n" +
                       "Вопросы - задавайте вопросы\n" +
                       "Действия - выполняю команды\n" +
                       "План - составляю планы\n\n" +
                       "Примеры:\n" +
                       "- Создай красный куб\n" +
                       "- Добавь Rigidbody\n" +
                       "- Скрипт для стрельбы\n\n" +
                       "Что хотите сделать?",
                provider = provider,
                mode = currentMode
            });
        }
        
        void OnDisable() { EditorApplication.update -= OnUpdate; }
        
        void OnUpdate()
        {
            RefreshContext();
            if (isBusy) Repaint();
        }
        
        void RefreshContext()
        {
            try { sceneName = SceneManager.GetActiveScene().name; } catch { }
            var go = Selection.activeGameObject;
            selectedObj = go != null ? go.name : "";
            sceneObjects.Clear();
            try
            {
                var scene = SceneManager.GetActiveScene();
                if (scene.IsValid())
                    foreach (var root in scene.GetRootGameObjects())
                        CollectObjects(root);
            }
            catch { }
        }
        
        void CollectObjects(GameObject go)
        {
            sceneObjects.Add(go);
            for (int i = 0; i < go.transform.childCount; i++)
                CollectObjects(go.transform.GetChild(i).gameObject);
        }
        
        string GetSystemPrompt()
        {
            string p = "";
            switch (currentMode)
            {
                case AIMode.Ask:
                    p = "Ты Unity ассистент. Отвечай простым языком на русском. Объясняй концепции, давай советы. Не генерируй код если не просят.";
                    break;
                case AIMode.Agent:
                    p = @"Ты Unity ассистент в режиме AGENT.
Доступные действия (используй эти теги):
[CREATE:Имя:Тип] - создать (Cube, Sphere, Cylinder, Plane, Empty)
[CREATE:Имя:Тип:X,Y,Z] - создать на позиции
[DELETE:Имя] - удалить объект
[ADD_COMPONENT:Имя:Компонент] - добавить компонент
[SET_POSITION:Имя:X,Y,Z] - переместить
[SET_SCALE:Имя:X,Y,Z] - масштаб
[SET_COLOR:Имя:R,G,B] - цвет материала
[CREATE_SCRIPT:Имя] - создать скрипт
[CODE]код[/CODE] - C# код

Сначала объясни что сделаешь, потом теги. Отвечай на русском.";
                    break;
                case AIMode.Plan:
                    p = "Ты Unity ассистент в режиме PLAN. Составляй пошаговые планы. Формат: Шаг 1:, Шаг 2: и т.д. Отвечай на русском.";
                    break;
            }
            p += "\n\nКонтекст: Сцена=" + sceneName + ", Выбран=" + selectedObj + ", Объектов=" + sceneObjects.Count;
            return p;
        }
        
        void SendMessage()
        {
            if (string.IsNullOrEmpty(userInput) || isBusy) return;
            if (!HasValidKey()) { statusMsg = "Нет API ключа!"; return; }
            
            history.Add(new ChatMessage { isUser = true, text = userInput });
            history.Add(new ChatMessage { isUser = false, isPending = true, startTime = EditorApplication.timeSinceStartup, provider = provider, mode = currentMode });
            pendingIndex = history.Count - 1;
            string text = userInput;
            userInput = "";
            isBusy = true;
            EditorCoroutine.Start(SendToAI(GetSystemPrompt(), text));
        }
        
        bool HasValidKey()
        {
            switch (provider)
            {
                case AIProvider.Gemini: return GEMINI_KEY.Length > 10 && !GEMINI_KEY.Contains("ВСТАВЬ");
                case AIProvider.DeepSeek: return DEEPSEEK_KEY.Length > 10 && !DEEPSEEK_KEY.Contains("ВСТАВЬ");
                case AIProvider.Groq: return GROQ_KEY.Length > 10 && !GROQ_KEY.Contains("ВСТАВЬ");
                default: return false;
            }
        }
        
        IEnumerator SendToAI(string system, string user)
        {
            string url = "", key = "", model = "", body = "";
            switch (provider)
            {
                case AIProvider.Gemini:
                    url = "https://generativelanguage.googleapis.com/v1beta/models/" + GEMINI_MODEL + ":generateContent?key=" + GEMINI_KEY;
                    body = JsonUtility.ToJson(new GeminiReq { contents = new[] { new GeminiContent { parts = new[] { new GeminiPart { text = system + "\n\n" + user } } } }, generationConfig = new GeminiConfig { maxOutputTokens = 4096 } });
                    break;
                case AIProvider.DeepSeek:
                    url = "https://api.deepseek.com/chat/completions";
                    key = DEEPSEEK_KEY;
                    body = JsonUtility.ToJson(new OpenAIReq { model = DEEPSEEK_MODEL, messages = new[] { new Msg { role = "system", content = system }, new Msg { role = "user", content = user } }, max_tokens = 4096 });
                    break;
                case AIProvider.Groq:
                    url = "https://api.groq.com/openai/v1/chat/completions";
                    key = GROQ_KEY;
                    body = JsonUtility.ToJson(new OpenAIReq { model = GROQ_MODEL, messages = new[] { new Msg { role = "system", content = system }, new Msg { role = "user", content = user } }, max_tokens = 4096 });
                    break;
            }
            
            var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 120;
            if (provider != AIProvider.Gemini) req.SetRequestHeader("Authorization", "Bearer " + key);
            
            yield return req.SendWebRequest();
            isBusy = false;
            
            if (req.result == UnityWebRequest.Result.Success)
            {
                string response = req.downloadHandler.text;
                string reply = ExtractResponse(response);
                if (pendingIndex >= 0 && pendingIndex < history.Count)
                {
                    history[pendingIndex].isPending = false;
                    history[pendingIndex].text = reply;
                    if (currentMode == AIMode.Agent)
                    {
                        history[pendingIndex].actions = ParseActions(reply);
                        pendingActions = history[pendingIndex].actions;
                        showPreview = pendingActions.Count > 0;
                    }
                    history[pendingIndex].code = ExtractCode(reply);
                }
                statusMsg = "Готово!";
            }
            else
            {
                statusMsg = "Ошибка: " + req.responseCode;
                if (pendingIndex >= 0 && pendingIndex < history.Count)
                {
                    history[pendingIndex].isPending = false;
                    history[pendingIndex].text = "Ошибка: " + req.error;
                }
            }
            pendingIndex = -1;
            req.Dispose();
            Repaint();
        }
        
        string ExtractResponse(string json)
        {
            if (provider == AIProvider.Gemini)
            {
                int idx = json.IndexOf("\"text\"");
                if (idx > 0)
                {
                    int start = json.IndexOf("\"", idx + 6) + 1;
                    int end = FindEnd(json, start);
                    if (end > start) return Clean(json.Substring(start, end - start));
                }
            }
            else
            {
                int idx = json.IndexOf("\"content\"");
                if (idx > 0)
                {
                    int start = json.IndexOf("\"", idx + 9) + 1;
                    int end = FindEnd(json, start);
                    if (end > start) return Clean(json.Substring(start, end - start));
                }
            }
            return json;
        }
        
        int FindEnd(string json, int start)
        {
            int i = start;
            while (i < json.Length) { if (json[i] == '\\' && i + 1 < json.Length) { i += 2; continue; } if (json[i] == '"') return i; i++; }
            return json.Length;
        }
        
        string Clean(string s) { return s.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\"); }
        
        string ExtractCode(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            int s = text.IndexOf("[CODE]");
            if (s >= 0) { s += 6; int e = text.IndexOf("[/CODE]", s); if (e > s) return text.Substring(s, e - s).Trim(); }
            string[] markers = { "csharp", "c#", "cs" };
            foreach (var m in markers)
            {
                string start = "```" + m;
                int si = text.IndexOf(start, StringComparison.OrdinalIgnoreCase);
                if (si < 0) continue;
                si += start.Length;
                if (si < text.Length && text[si] == '\n') si++;
                int ei = text.IndexOf("```", si);
                if (ei > si) return text.Substring(si, ei - si).Trim();
            }
            return "";
        }
        
        List<PendingAction> ParseActions(string text)
        {
            var actions = new List<PendingAction>();
            foreach (Match m in Regex.Matches(text, @"\[CREATE:([^:]+):([^:\]]+)(?::([^:\]]+))?\]"))
            {
                var a = new PendingAction { type = ActionType.CreateObject, targetObject = m.Groups[1].Value.Trim(), description = "Создать " + m.Groups[2].Value };
                if (m.Groups[3].Success) { var p = m.Groups[3].Value.Split(','); if (p.Length == 3) { float.TryParse(p[0], out a.position.x); float.TryParse(p[1], out a.position.y); float.TryParse(p[2], out a.position.z); } }
                actions.Add(a);
            }
            foreach (Match m in Regex.Matches(text, @"\[DELETE:([^\]]+)\]"))
                actions.Add(new PendingAction { type = ActionType.DeleteObject, targetObject = m.Groups[1].Value.Trim(), description = "Удалить " + m.Groups[1].Value });
            foreach (Match m in Regex.Matches(text, @"\[ADD_COMPONENT:([^:]+):([^\]]+)\]"))
                actions.Add(new PendingAction { type = ActionType.ModifyObject, targetObject = m.Groups[1].Value.Trim(), componentName = m.Groups[2].Value.Trim(), description = "Добавить " + m.Groups[2].Value });
            foreach (Match m in Regex.Matches(text, @"\[SET_POSITION:([^:]+):([^:\]]+)\]"))
            {
                var a = new PendingAction { type = ActionType.ModifyObject, targetObject = m.Groups[1].Value.Trim(), description = "Переместить " + m.Groups[1].Value };
                var p = m.Groups[2].Value.Split(',');
                if (p.Length == 3) { float.TryParse(p[0], out a.position.x); float.TryParse(p[1], out a.position.y); float.TryParse(p[2], out a.position.z); }
                actions.Add(a);
            }
            foreach (Match m in Regex.Matches(text, @"\[CREATE_SCRIPT:([^\]]+)\]"))
                actions.Add(new PendingAction { type = ActionType.CreateScript, targetObject = m.Groups[1].Value.Trim(), description = "Создать скрипт" });
            return actions;
        }
        
        void ExecuteActions()
        {
            foreach (var action in pendingActions)
            {
                if (!action.approved) continue;
                try
                {
                    switch (action.type)
                    {
                        case ActionType.CreateObject: CreateObject(action); break;
                        case ActionType.DeleteObject: DeleteObject(action); break;
                        case ActionType.ModifyObject: ModifyObject(action); break;
                        case ActionType.CreateScript: CreateScript(action); break;
                    }
                }
                catch (Exception e) { Debug.LogError("[AI] " + e.Message); }
            }
            pendingActions.Clear();
            showPreview = false;
            statusMsg = "Выполнено!";
            Repaint();
        }
        
        void CreateObject(PendingAction a)
        {
            GameObject go = null;
            switch (a.targetObject.ToLower())
            {
                case "cube": case "куб": go = GameObject.CreatePrimitive(PrimitiveType.Cube); break;
                case "sphere": case "сфера": go = GameObject.CreatePrimitive(PrimitiveType.Sphere); break;
                case "cylinder": case "цилиндр": go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); break;
                case "plane": case "плоскость": go = GameObject.CreatePrimitive(PrimitiveType.Plane); break;
                default: go = new GameObject(a.targetObject); break;
            }
            if (go != null) { go.transform.position = a.position; go.transform.localScale = a.scale; Undo.RegisterCreatedObjectUndo(go, "AI Create"); Selection.activeGameObject = go; }
        }
        
        void DeleteObject(PendingAction a)
        {
            var go = GameObject.Find(a.targetObject);
            if (go != null) Undo.DestroyObjectImmediate(go);
        }
        
        void ModifyObject(PendingAction a)
        {
            var go = GameObject.Find(a.targetObject);
            if (go == null) return;
            Undo.RecordObject(go.transform, "AI Modify");
            if (a.position != Vector3.zero) go.transform.position = a.position;
            if (a.scale != Vector3.one && a.scale != Vector3.zero) go.transform.localScale = a.scale;
            if (!string.IsNullOrEmpty(a.componentName))
            {
                var type = GetTypeByName(a.componentName);
                if (type != null) Undo.AddComponent(go, type);
            }
        }
        
        void CreateScript(PendingAction a)
        {
            string code = ExtractCode(history.LastOrDefault(h => !string.IsNullOrEmpty(h.code))?.code ?? "");
            if (string.IsNullOrEmpty(code)) { statusMsg = "Нет кода"; return; }
            string path = "Assets/" + a.targetObject + ".cs";
            File.WriteAllText(path, code);
            AssetDatabase.Refresh();
            statusMsg = "Скрипт создан: " + path;
        }
        
        Type GetTypeByName(string name)
        {
            switch (name.ToLower())
            {
                case "rigidbody": return typeof(Rigidbody);
                case "boxcollider": return typeof(BoxCollider);
                case "spherecollider": return typeof(SphereCollider);
                case "capsulecollider": return typeof(CapsuleCollider);
                case "meshcollider": return typeof(MeshCollider);
                case "charactercontroller": return typeof(CharacterController);
                case "audiosource": return typeof(AudioSource);
                case "light": return typeof(Light);
                case "camera": return typeof(Camera);
                case "particlesystem": return typeof(ParticleSystem);
                case "meshrenderer": return typeof(MeshRenderer);
                case "meshfilter": return typeof(MeshFilter);
                case "animator": return typeof(Animator);
            }
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetTypes().FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (type != null) return type;
            }
            return null;
        }

        [Serializable] class GeminiReq { public GeminiContent[] contents; public GeminiConfig generationConfig; }
        [Serializable] class GeminiContent { public GeminiPart[] parts; }
        [Serializable] class GeminiPart { public string text; }
        [Serializable] class GeminiConfig { public int maxOutputTokens; }
        [Serializable] class OpenAIReq { public string model; public Msg[] messages; public int max_tokens; }
        [Serializable] class Msg { public string role; public string content; }
        void OnGUI()
        {
            DrawToolbar();
            DrawModeSelector();
            if (showPreview && pendingActions.Count > 0) DrawPreview();
            DrawChat();
            DrawInput();
        }
        
        void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            provider = (AIProvider)EditorGUILayout.EnumPopup(provider, GUILayout.Width(80));
            GUILayout.FlexibleSpace();
            GUI.color = HasValidKey() ? Color.green : Color.red;
            GUILayout.Label("●", EditorStyles.boldLabel);
            GUI.color = Color.white;
            if (!string.IsNullOrEmpty(sceneName)) GUILayout.Label(sceneName, EditorStyles.miniLabel);
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(statusMsg))
            {
                var s = new GUIStyle(EditorStyles.helpBox) { wordWrap = true };
                GUILayout.Label(statusMsg, s);
            }
        }
        
        void DrawModeSelector()
        {
            GUILayout.BeginHorizontal();
            GUI.color = currentMode == AIMode.Ask ? new Color(0.5f, 0.8f, 1f) : Color.white;
            if (GUILayout.Button("Вопросы", EditorStyles.miniButtonLeft, GUILayout.Height(28))) { currentMode = AIMode.Ask; statusMsg = "Режим: Вопросы"; }
            GUI.color = currentMode == AIMode.Agent ? new Color(0.5f, 1f, 0.5f) : Color.white;
            if (GUILayout.Button("Действия", EditorStyles.miniButtonMid, GUILayout.Height(28))) { currentMode = AIMode.Agent; statusMsg = "Режим: Действия"; }
            GUI.color = currentMode == AIMode.Plan ? new Color(1f, 0.8f, 0.5f) : Color.white;
            if (GUILayout.Button("План", EditorStyles.miniButtonRight, GUILayout.Height(28))) { currentMode = AIMode.Plan; statusMsg = "Режим: План"; }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
        }
        
        void DrawPreview()
        {
            GUILayout.Space(5);
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Действия:", EditorStyles.boldLabel);
            for (int i = 0; i < pendingActions.Count; i++)
            {
                GUILayout.BeginHorizontal();
                pendingActions[i].approved = GUILayout.Toggle(pendingActions[i].approved, "");
                GUILayout.Label(pendingActions[i].description);
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Выполнить", GUILayout.Height(28)))
            {
                foreach (var a in pendingActions) a.approved = true;
                ExecuteActions();
            }
            if (GUILayout.Button("Отмена", GUILayout.Height(28))) { pendingActions.Clear(); showPreview = false; }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }
        
        void DrawChat()
        {
            float chatH = position.height - (showPreview ? 320 : 180);
            chatScroll = EditorGUILayout.BeginScrollView(chatScroll, GUILayout.Height(Math.Max(100, chatH)));
            foreach (var msg in history)
            {
                GUILayout.BeginVertical(EditorStyles.helpBox);
                if (msg.isPending)
                {
                    double elapsed = EditorApplication.timeSinceStartup - msg.startTime;
                    GUILayout.Label("Думаю... (" + elapsed.ToString("F0") + "с)", EditorStyles.boldLabel);
                }
                else
                {
                    GUILayout.Label(msg.isUser ? "Вы:" : "AI [" + msg.mode + "]:", EditorStyles.boldLabel);
                    var style = new GUIStyle(EditorStyles.label) { wordWrap = true };
                    float h = style.CalcHeight(new GUIContent(msg.text), position.width - 40);
                    h = Mathf.Max(30, Mathf.Min(h, 400));
                    GUILayout.TextArea(msg.text, style, GUILayout.Height(h));
                    if (!string.IsNullOrEmpty(msg.code))
                    {
                        GUILayout.Space(5);
                        var cs = new GUIStyle(EditorStyles.textArea) { wordWrap = false, fontSize = 10 };
                        string code = msg.code.Length > 2000 ? msg.code.Substring(0, 2000) + "\n..." : msg.code;
                        GUILayout.TextArea(code, cs, GUILayout.Height(120));
                        if (GUILayout.Button("Копировать код"))
                            EditorGUIUtility.systemCopyBuffer = msg.code;
                    }
                }
                GUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }
        
        void DrawInput()
        {
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            GUI.enabled = !isBusy;
            var inputStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            userInput = EditorGUILayout.TextArea(userInput, inputStyle, GUILayout.Height(50), GUILayout.ExpandWidth(true));
            GUI.enabled = !isBusy && !string.IsNullOrEmpty(userInput);
            bool send = GUILayout.Button(isBusy ? "..." : ">", GUILayout.Width(40), GUILayout.Height(50));
            GUI.enabled = true;
            if (send || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return && !Event.current.shift && !isBusy && !string.IsNullOrEmpty(userInput)))
            {
                SendMessage();
                Event.current.Use();
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Enter - отправить, Shift+Enter - новая строка", EditorStyles.miniLabel);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Очистить", EditorStyles.miniButton)) { history.Clear(); statusMsg = ""; }
            if (GUILayout.Button("Отменить", EditorStyles.miniButton)) { Undo.PerformUndo(); statusMsg = "Отменено"; }
            GUILayout.EndHorizontal();
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