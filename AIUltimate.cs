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
using UnityEngine.UI;

namespace UnityAIUltimate
{
    public enum Mode { Ask, Agent, Plan }
    public enum Provider { Groq, OpenRouter, Together, HuggingFace, Cerebras }
    
    [Serializable]
    public class ToolCall
    {
        public string tool;
        public string action;
        public string target;
        public string param1;
        public string param2;
        public string param3;
        public Vector3 pos;
        public Vector3 rot;
        public Vector3 scale = Vector3.one;
        public Color color = Color.white;
        public bool approved;
    }
    
    [Serializable]
    public class Msg
    {
        public bool isUser;
        public string text;
        public string code;
        public List<ToolCall> tools = new List<ToolCall>();
        public bool pending;
        public double time;
        public Mode mode;
    }

    public class AIUltimateWindow : EditorWindow
    {
        // ═══ API КЛЮЧИ ═══
        const string GROQ_KEY = "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";           // https://console.groq.com/keys
        const string GROQ_MODEL = "llama-3.3-70b-versatile";
        
        const string OR_KEY = "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";            // https://openrouter.ai/keys
        const string OR_MODEL = "meta-llama/llama-3.3-70b-instruct:free";
        
        const string TOGETHER_KEY = "tgp_v1_TkZ5QSDzpcB3a1JUpV2GpsrwQiKRkiISVmFM5LGfRIY";             // https://api.together.xyz/settings/api-keys
        const string TOGETHER_MODEL = "meta-llama/Llama-3.3-70B-Instruct-Turbo-Free";
        
        const string HF_KEY = "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";               // https://huggingface.co/settings/tokens
        const string HF_MODEL = "meta-llama/Llama-3.3-70B-Instruct";
        
        const string CEREBRAS_KEY = "csk-c8xrjrkpfdhd8mkxpt59cx9446pthphf5k96y465m3yv6f8f";         // https://cloud.cerebras.ai/
        const string CEREBRAS_MODEL = "llama3.3-70b";
        // ═══════════════════
        
        Provider prov = Provider.Groq;
        Mode mode = Mode.Ask;
        Vector2 scroll;
        string input = "";
        bool busy = false;
        string status = "";
        List<Msg> msgs = new List<Msg>();
        int pend = -1;
        List<ToolCall> pendingTools = new List<ToolCall>();
        bool showTools = false;

        [MenuItem("Window/AI Ultimate")]
        static void Show() => GetWindow<AIUltimateWindow>("AI Ultimate").minSize = new Vector2(480, 600);

        void OnEnable()
        {
            EditorApplication.update += Tick;
            msgs.Add(new Msg { isUser = false, text = "Unity AI Ultimate\n\nПровайдеры:\n• Groq (бесплатный, быстрый)\n• OpenRouter (много моделей)\n• Together.AI ($100 кредитов)\n• Hugging Face (бесплатный)\n• Cerebras (1M токенов/день)\n\nРежимы: Вопросы | Действия | План\n\nЧто хотите сделать?" });
        }

        void OnDisable() => EditorApplication.update -= Tick;
        void Tick() { if (busy) Repaint(); }

        string GetUrl()
        {
            switch (prov)
            {
                case Provider.Groq: return "https://api.groq.com/openai/v1/chat/completions";
                case Provider.OpenRouter: return "https://openrouter.ai/api/v1/chat/completions";
                case Provider.Together: return "https://api.together.xyz/v1/chat/completions";
                case Provider.HuggingFace: return "https://router.huggingface.co/v1/chat/completions";
                case Provider.Cerebras: return "https://api.cerebras.ai/v1/chat/completions";
                default: return "";
            }
        }

        string GetKey()
        {
            switch (prov)
            {
                case Provider.Groq: return GROQ_KEY;
                case Provider.OpenRouter: return OR_KEY;
                case Provider.Together: return TOGETHER_KEY;
                case Provider.HuggingFace: return HF_KEY;
                case Provider.Cerebras: return CEREBRAS_KEY;
                default: return "";
            }
        }

        string GetModel()
        {
            switch (prov)
            {
                case Provider.Groq: return GROQ_MODEL;
                case Provider.OpenRouter: return OR_MODEL;
                case Provider.Together: return TOGETHER_MODEL;
                case Provider.HuggingFace: return HF_MODEL;
                case Provider.Cerebras: return CEREBRAS_MODEL;
                default: return "";
            }
        }

        bool IsValid()
        {
            string k = GetKey();
            return k.Length > 10 && !k.Contains("ВСТАВЬ");
        }

        void OnGUI()
        {
            // Toolbar
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            prov = (Provider)EditorGUILayout.EnumPopup(prov, GUILayout.Width(100));
            GUILayout.FlexibleSpace();
            GUI.color = IsValid() ? Color.green : Color.red;
            GUILayout.Label("●");
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
            
            if (!string.IsNullOrEmpty(status))
            {
                var st = new GUIStyle(EditorStyles.helpBox) { wordWrap = true };
                GUILayout.Label(status, st);
            }
            
            // Modes
            GUILayout.BeginHorizontal();
            GUI.color = mode == Mode.Ask ? new Color(0.5f, 0.8f, 1f) : Color.white;
            if (GUILayout.Button("Вопросы", EditorStyles.miniButtonLeft, GUILayout.Height(26))) mode = Mode.Ask;
            GUI.color = mode == Mode.Agent ? new Color(0.5f, 1f, 0.5f) : Color.white;
            if (GUILayout.Button("Действия", EditorStyles.miniButtonMid, GUILayout.Height(26))) mode = Mode.Agent;
            GUI.color = mode == Mode.Plan ? new Color(1f, 0.8f, 0.5f) : Color.white;
            if (GUILayout.Button("План", EditorStyles.miniButtonRight, GUILayout.Height(26))) mode = Mode.Plan;
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
            
            // Tools preview
            if (showTools && pendingTools.Count > 0)
            {
                GUILayout.Space(4);
                GUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("Действия:", EditorStyles.boldLabel);
                for (int i = 0; i < pendingTools.Count; i++)
                {
                    GUILayout.BeginHorizontal();
                    pendingTools[i].approved = GUILayout.Toggle(pendingTools[i].approved, "");
                    GUILayout.Label(pendingTools[i].tool + ": " + pendingTools[i].target, EditorStyles.miniLabel);
                    GUILayout.EndHorizontal();
                }
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Выполнить")) { foreach (var t in pendingTools) t.approved = true; ExecuteAll(); }
                if (GUILayout.Button("Отмена")) { pendingTools.Clear(); showTools = false; }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            
            // Chat
            float h = position.height - (showTools ? 300 : 150);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(Math.Max(80, h)));
            foreach (var m in msgs)
            {
                GUILayout.BeginVertical(EditorStyles.helpBox);
                if (m.pending)
                {
                    double e = EditorApplication.timeSinceStartup - m.time;
                    GUILayout.Label("Думаю... (" + e.ToString("F0") + "с)", EditorStyles.boldLabel);
                }
                else
                {
                    GUILayout.Label(m.isUser ? "Вы:" : "AI [" + m.mode + "]:", EditorStyles.boldLabel);
                    var s = new GUIStyle(EditorStyles.label) { wordWrap = true };
                    float th = s.CalcHeight(new GUIContent(m.text), position.width - 40);
                    th = Mathf.Max(25, Mathf.Min(th, 300));
                    GUILayout.TextArea(m.text, s, GUILayout.Height(th));
                    if (!string.IsNullOrEmpty(m.code))
                    {
                        var cs = new GUIStyle(EditorStyles.textArea) { wordWrap = false, fontSize = 10 };
                        GUILayout.TextArea(m.code.Length > 1500 ? m.code.Substring(0, 1500) : m.code, cs, GUILayout.Height(80));
                        if (GUILayout.Button("Копировать код")) EditorGUIUtility.systemCopyBuffer = m.code;
                    }
                    if (m.tools.Count > 0) GUILayout.Label("Действий: " + m.tools.Count, EditorStyles.miniLabel);
                }
                GUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
            
            // Input
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUI.enabled = !busy;
            var inpStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            input = EditorGUILayout.TextArea(input, inpStyle, GUILayout.Height(45), GUILayout.ExpandWidth(true));
            GUI.enabled = !busy && !string.IsNullOrEmpty(input);
            bool send = GUILayout.Button(busy ? "..." : ">", GUILayout.Width(35), GUILayout.Height(45));
            GUI.enabled = true;
            if (send || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return && !Event.current.shift && !busy && !string.IsNullOrEmpty(input)))
            { Send(); Event.current.Use(); }
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Очистить", EditorStyles.miniButton)) { msgs.Clear(); status = ""; }
            if (GUILayout.Button("Отменить", EditorStyles.miniButton)) Undo.PerformUndo();
            if (GUILayout.Button("Повторить", EditorStyles.miniButton)) Undo.PerformRedo();
            GUILayout.EndHorizontal();
        }

        string GetPrompt()
        {
            string scene = "";
            try { scene = SceneManager.GetActiveScene().name; } catch { }
            string sel = Selection.activeGameObject?.name ?? "ничего";
            
            string p = "";
            if (mode == Mode.Ask)
                p = "Ты Unity эксперт. Отвечай простым языком на русском.";
            else if (mode == Mode.Agent)
                p = "Ты Unity AI. Используй теги.\n\n" +
                    "[CREATE:тип:имя:X,Y,Z] создать (Cube/Sphere/Cylinder/Plane/Capsule/Empty)\n" +
                    "[DELETE:имя] удалить\n" +
                    "[MOVE:имя:X,Y,Z] переместить\n" +
                    "[ROTATE:имя:X,Y,Z] повернуть\n" +
                    "[SCALE:имя:X,Y,Z] масштаб\n" +
                    "[SET_COLOR:имя:R,G,B] цвет\n" +
                    "[ADD:имя:компонент] (Rigidbody/BoxCollider/Light/Camera/Animator)\n" +
                    "[REMOVE:имя:компонент]\n" +
                    "[SCRIPT:имя] создать скрипт\n" +
                    "[MATERIAL:имя:R,G,B] материал\n" +
                    "[LIGHT:имя:X,Y,Z:Point:R,G,B] свет\n" +
                    "[CAMERA:имя:X,Y,Z] камера\n" +
                    "[CANVAS:имя] UI\n" +
                    "[BUTTON:имя:текст:X,Y] кнопка\n" +
                    "[TEXT:имя:текст:X,Y] текст\n" +
                    "[PARTICLES:имя:X,Y,Z] частицы\n" +
                    "[SCENE:new/load/save:имя] сцена\n" +
                    "[FIND:имя] найти\n" +
                    "[DUPLICATE:имя] копировать\n" +
                    "[UNDO] отменить\n\n" +
                    "Сначала объясни, потом теги.";
            else
                p = "Ты Unity планировщик. Составляй пошаговые планы на русском.";
            
            return p + "\n\nКонтекст: Сцена=" + scene + ", Выбран=" + sel;
        }

        void Send()
        {
            if (string.IsNullOrEmpty(input) || busy) return;
            if (!IsValid()) { status = "Вставь API ключ!"; return; }
            
            msgs.Add(new Msg { isUser = true, text = input });
            msgs.Add(new Msg { pending = true, time = EditorApplication.timeSinceStartup, mode = mode });
            pend = msgs.Count - 1;
            string t = input; input = ""; busy = true;
            EditorCoroutine.Start(Post(GetPrompt(), t));
        }

        IEnumerator Post(string sys, string usr)
        {
            string body = JsonUtility.ToJson(new OAIReq
            {
                model = GetModel(),
                messages = new[] { new OAIMsg { role = "system", content = sys }, new OAIMsg { role = "user", content = usr } },
                max_tokens = 8192
            });
            
            var req = new UnityWebRequest(GetUrl(), "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + GetKey());
            req.timeout = 120;
            
            // OpenRouter extras
            if (prov == Provider.OpenRouter)
            {
                req.SetRequestHeader("HTTP-Referer", "https://unity.ai");
                req.SetRequestHeader("X-Title", "Unity AI Ultimate");
            }
            
            yield return req.SendWebRequest();
            busy = false;
            
            if (req.result == UnityWebRequest.Result.Success)
            {
                string json = req.downloadHandler.text;
                string txt = Extract(json);
                if (pend >= 0 && pend < msgs.Count)
                {
                    msgs[pend].pending = false;
                    msgs[pend].text = txt;
                    msgs[pend].code = GetCode(txt);
                    if (mode == Mode.Agent)
                    {
                        msgs[pend].tools = Parse(txt);
                        pendingTools = msgs[pend].tools;
                        showTools = pendingTools.Count > 0;
                    }
                }
                status = "Готово!";
            }
            else
            {
                string errMsg = "Ошибка: " + req.responseCode;
                if (req.responseCode == 401) errMsg = "Неверный API ключ!";
                if (req.responseCode == 429) errMsg = "Лимит запросов! Подождите минуту.";
                if (req.responseCode == 402) errMsg = "Нет кредитов на аккаунте!";
                
                status = errMsg;
                Debug.LogError("[AI] " + req.error + "\n" + req.downloadHandler.text);
                if (pend >= 0 && pend < msgs.Count) { msgs[pend].pending = false; msgs[pend].text = errMsg; }
            }
            pend = -1; req.Dispose(); Repaint();
        }

        string Extract(string json)
        {
            int idx = json.IndexOf("\"content\"");
            if (idx < 0) return json;
            int s = json.IndexOf('"', idx + 9) + 1;
            int e = s;
            while (e < json.Length) { if (json[e] == '\\' && e + 1 < json.Length) { e += 2; continue; } if (json[e] == '"') break; e++; }
            return s < e ? json.Substring(s, e - s).Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\") : json;
        }

        string GetCode(string t)
        {
            if (string.IsNullOrEmpty(t)) return "";
            foreach (var m in new[] { "csharp", "c#", "cs" })
            {
                int s = t.IndexOf("```" + m, StringComparison.OrdinalIgnoreCase);
                if (s < 0) continue;
                s += 3 + m.Length;
                if (s < t.Length && t[s] == '\n') s++;
                int e = t.IndexOf("```", s);
                if (e > s) return t.Substring(s, e - s).Trim();
            }
            return "";
        }

        List<ToolCall> Parse(string t)
        {
            var tools = new List<ToolCall>();
            if (string.IsNullOrEmpty(t)) return tools;
            
            foreach (Match m in Regex.Matches(t, @"\[CREATE:([^:]+):([^:\]]+)(?::([^:\]]+))?\]"))
            {
                var tc = new ToolCall { tool = "CREATE", action = m.Groups[1].Value, target = m.Groups[2].Value };
                if (m.Groups[3].Success) { var p = m.Groups[3].Value.Split(','); if (p.Length == 3) { float.TryParse(p[0], out tc.pos.x); float.TryParse(p[1], out tc.pos.y); float.TryParse(p[2], out tc.pos.z); } }
                tools.Add(tc);
            }
            foreach (Match m in Regex.Matches(t, @"\[DELETE:([^\]]+)\]")) tools.Add(new ToolCall { tool = "DELETE", target = m.Groups[1].Value });
            foreach (Match m in Regex.Matches(t, @"\[MOVE:([^:]+):([^:\]]+)\]"))
            {
                var tc = new ToolCall { tool = "MOVE", target = m.Groups[1].Value };
                var p = m.Groups[2].Value.Split(','); if (p.Length == 3) { float.TryParse(p[0], out tc.pos.x); float.TryParse(p[1], out tc.pos.y); float.TryParse(p[2], out tc.pos.z); }
                tools.Add(tc);
            }
            foreach (Match m in Regex.Matches(t, @"\[ADD:([^:]+):([^\]]+)\]")) tools.Add(new ToolCall { tool = "ADD", target = m.Groups[1].Value, param1 = m.Groups[2].Value });
            foreach (Match m in Regex.Matches(t, @"\[SCRIPT:([^\]]+)\]")) tools.Add(new ToolCall { tool = "SCRIPT", target = m.Groups[1].Value });
            foreach (Match m in Regex.Matches(t, @"\[MATERIAL:([^:]+):([^:\]]+)\]"))
            {
                var tc = new ToolCall { tool = "MATERIAL", target = m.Groups[1].Value };
                var p = m.Groups[2].Value.Split(','); if (p.Length == 3) { float.TryParse(p[0], out tc.color.r); float.TryParse(p[1], out tc.color.g); float.TryParse(p[2], out tc.color.b); }
                tools.Add(tc);
            }
            foreach (Match m in Regex.Matches(t, @"\[FIND:([^\]]+)\]")) tools.Add(new ToolCall { tool = "FIND", target = m.Groups[1].Value });
            foreach (Match m in Regex.Matches(t, @"\[DUPLICATE:([^\]]+)\]")) tools.Add(new ToolCall { tool = "DUPLICATE", target = m.Groups[1].Value });
            if (t.Contains("[UNDO]")) tools.Add(new ToolCall { tool = "UNDO" });
            return tools;
        }

        void ExecuteAll()
        {
            foreach (var tc in pendingTools)
            {
                if (!tc.approved) continue;
                try { Exec(tc); }
                catch (Exception e) { Debug.LogError("[AI] " + e.Message); }
            }
            pendingTools.Clear(); showTools = false; status = "Выполнено!"; Repaint();
        }

        void Exec(ToolCall tc)
        {
            GameObject go = null;
            switch (tc.tool)
            {
                case "CREATE":
                    switch (tc.action.ToLower())
                    {
                        case "cube": go = GameObject.CreatePrimitive(PrimitiveType.Cube); break;
                        case "sphere": go = GameObject.CreatePrimitive(PrimitiveType.Sphere); break;
                        case "cylinder": go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); break;
                        case "plane": go = GameObject.CreatePrimitive(PrimitiveType.Plane); break;
                        case "capsule": go = GameObject.CreatePrimitive(PrimitiveType.Capsule); break;
                        default: go = new GameObject(tc.action); break;
                    }
                    if (go != null) { go.name = tc.target; go.transform.position = tc.pos; Undo.RegisterCreatedObjectUndo(go, "AI"); Selection.activeGameObject = go; }
                    break;
                case "DELETE": go = GameObject.Find(tc.target); if (go != null) Undo.DestroyObjectImmediate(go); break;
                case "MOVE": go = GameObject.Find(tc.target); if (go != null) { Undo.RecordObject(go.transform, "AI"); go.transform.position = tc.pos; } break;
                case "ADD":
                    go = GameObject.Find(tc.target);
                    if (go != null)
                    {
                        Type t = null;
                        switch (tc.param1.ToLower())
                        {
                            case "rigidbody": t = typeof(Rigidbody); break;
                            case "boxcollider": t = typeof(BoxCollider); break;
                            case "light": t = typeof(Light); break;
                            case "camera": t = typeof(Camera); break;
                            case "audiosource": t = typeof(AudioSource); break;
                            case "particlesystem": t = typeof(ParticleSystem); break;
                            case "animator": t = typeof(Animator); break;
                            case "canvas": t = typeof(Canvas); break;
                        }
                        if (t != null) Undo.AddComponent(go, t);
                    }
                    break;
                case "SCRIPT":
                    string code = GetCode(msgs.LastOrDefault(m => !string.IsNullOrEmpty(m.code))?.code ?? "");
                    if (!string.IsNullOrEmpty(code)) { File.WriteAllText("Assets/" + tc.target + ".cs", code); AssetDatabase.Refresh(); }
                    break;
                case "MATERIAL":
                    var mat = new Material(Shader.Find("Standard"));
                    mat.name = tc.target; mat.color = tc.color;
                    if (!Directory.Exists("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
                    AssetDatabase.CreateAsset(mat, "Assets/Materials/" + tc.target + ".mat");
                    AssetDatabase.SaveAssets();
                    break;
                case "FIND":
                    go = GameObject.Find(tc.target);
                    if (go != null) Selection.activeGameObject = go;
                    break;
                case "DUPLICATE":
                    go = GameObject.Find(tc.target);
                    if (go != null) { var dup = Instantiate(go); dup.name = tc.target + "_Copy"; Undo.RegisterCreatedObjectUndo(dup, "AI"); Selection.activeGameObject = dup; }
                    break;
                case "UNDO": Undo.PerformUndo(); break;
            }
        }

        [Serializable] class OAIReq { public string model; public OAIMsg[] messages; public int max_tokens; }
        [Serializable] class OAIMsg { public string role; public string content; }
    }

    public class EditorCoroutine
    {
        IEnumerator r; object c;
        EditorCoroutine(IEnumerator e) { r = e; }
        public static EditorCoroutine Start(IEnumerator e)
        {
            var co = new EditorCoroutine(e);
            if (!co.r.MoveNext()) return co;
            co.c = co.r.Current;
            EditorApplication.update += co.Tick;
            return co;
        }
        void Tick()
        {
            if (c is AsyncOperation op && !op.isDone) return;
            if (!r.MoveNext()) { EditorApplication.update -= Tick; return; }
            c = r.Current;
        }
    }
}