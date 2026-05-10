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
    public enum Provider { Gemini, DeepSeek, Groq }
    
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
    public class Message
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
        // ЗАМЕНИ КЛЮЧИ
        const string GK = "AIzaSy..."; // Gemini
        const string GM = "gemini-2.5-flash";
        const string DK = "sk-..."; // DeepSeek
        const string DM = "deepseek-chat";
        const string RK = "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"; // Groq
        const string RM = "llama-3.3-70b-versatile";
        
        Provider prov = Provider.Groq;
        Mode mode = Mode.Ask;
        Vector2 scroll;
        string input = "";
        bool busy = false;
        string status = "";
        List<Message> msgs = new List<Message>();
        int pend = -1;
        List<ToolCall> pendingTools = new List<ToolCall>();
        bool showTools = false;

        [MenuItem("Window/AI Ultimate")]
        static void Show() => GetWindow<AIUltimateWindow>("AI Ultimate").minSize = new Vector2(480, 600);

        void OnEnable()
        {
            EditorApplication.update += Tick;
            msgs.Add(new Message { isUser = false, text = "Unity AI Ultimate - 40+ инструментов\n\nРежимы: Вопросы | Действия | План\n\nПримеры:\n- Создай красный куб\n- Добавь Rigidbody\n- Скрипт для прыжка\n\nЧто хотите сделать?" });
        }

        void OnDisable() => EditorApplication.update -= Tick;
        void Tick() { if (busy) Repaint(); }

        string GetPrompt()
        {
            string scene = "";
            try { scene = SceneManager.GetActiveScene().name; } catch { }
            string sel = Selection.activeGameObject?.name ?? "ничего";
            
            string p = "";
            if (mode == Mode.Ask)
                p = "Ты Unity эксперт. Отвечай простым языком на русском. Не давай код если не просят.";
            else if (mode == Mode.Agent)
                p = "Ты Unity AI. Используй теги для действий.\n\n" +
                    "[CREATE:тип:имя:X,Y,Z] создать (Cube/Sphere/Cylinder/Plane/Capsule/Empty)\n" +
                    "[DELETE:имя] удалить\n" +
                    "[MOVE:имя:X,Y,Z] переместить\n" +
                    "[ROTATE:имя:X,Y,Z] повернуть\n" +
                    "[SCALE:имя:X,Y,Z] масштаб\n" +
                    "[SET_COLOR:имя:R,G,B] цвет\n" +
                    "[ADD:имя:компонент] добавить (Rigidbody/BoxCollider/Light/Camera/ParticleSystem/Animator)\n" +
                    "[REMOVE:имя:компонент] удалить компонент\n" +
                    "[PROP:имя:компонент:свойство:значение] свойство\n" +
                    "[SCRIPT:имя] создать скрипт (код после)\n" +
                    "[MATERIAL:имя:R,G,B] создать материал\n" +
                    "[LIGHT:имя:X,Y,Z:Point/Spot:R,G,B] свет\n" +
                    "[CAMERA:имя:X,Y,Z] камера\n" +
                    "[CANVAS:имя] UI Canvas\n" +
                    "[BUTTON:имя:текст:X,Y] кнопка\n" +
                    "[TEXT:имя:текст:X,Y] текст\n" +
                    "[PARTICLES:имя:X,Y,Z] частицы\n" +
                    "[AUDIO:имя:X,Y,Z] звук\n" +
                    "[SCENE:new/load/save:имя] сцена\n" +
                    "[FIND:имя] найти объект\n" +
                    "[DUPLICATE:имя] дублировать\n" +
                    "[RENAME:старое:новое] переименовать\n" +
                    "[PARENT:ребенок:родитель] привязать\n" +
                    "[TAG:имя:тег] тег\n" +
                    "[LAYER:имя:слой] слой\n" +
                    "[UNDO] отменить\n\n" +
                    "Сначала объясни, потом теги. Отвечай на русском.";
            else
                p = "Ты Unity планировщик. Составляй пошаговые планы на русском.";
            
            return p + "\n\nКонтекст: Сцена=" + scene + ", Выбран=" + sel;
        }

        void Send()
        {
            if (string.IsNullOrEmpty(input) || busy) return;
            bool valid = false;
            if (prov == Provider.Gemini) valid = GK.Length > 10 && !GK.Contains("...");
            else if (prov == Provider.DeepSeek) valid = DK.Length > 10 && !DK.Contains("...");
            else valid = RK.Length > 10 && !RK.Contains("...");
            if (!valid) { status = "Вставь API ключ!"; return; }
            
            msgs.Add(new Message { isUser = true, text = input });
            msgs.Add(new Message { pending = true, time = EditorApplication.timeSinceStartup, mode = mode });
            pend = msgs.Count - 1;
            string t = input; input = ""; busy = true;
            EditorCoroutine.Start(Post(GetPrompt(), t));
        }

        IEnumerator Post(string sys, string usr)
        {
            string url = "", key = "", body = "";
            string mdl = "";
            
            if (prov == Provider.Gemini)
            {
                url = "https://generativelanguage.googleapis.com/v1beta/models/" + GM + ":generateContent?key=" + GK;
                body = JsonUtility.ToJson(new GemReq { contents = new[] { new GemC { parts = new[] { new GemP { text = sys + "\n\n" + usr } } } }, generationConfig = new GemCfg { maxOutputTokens = 8192 } });
            }
            else
            {
                url = prov == Provider.DeepSeek ? "https://api.deepseek.com/chat/completions" : "https://api.groq.com/openai/v1/chat/completions";
                key = prov == Provider.DeepSeek ? DK : RK;
                mdl = prov == Provider.DeepSeek ? DM : RM;
                body = JsonUtility.ToJson(new OAIReq { model = mdl, messages = new[] { new OAIMsg { role = "system", content = sys }, new OAIMsg { role = "user", content = usr } }, max_tokens = 8192 });
            }
            
            var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 120;
            if (prov != Provider.Gemini) req.SetRequestHeader("Authorization", "Bearer " + key);
            
            yield return req.SendWebRequest();
            busy = false;
            
            if (req.result == UnityWebRequest.Result.Success)
            {
                string json = req.downloadHandler.text;
                string txt = ExtractText(json);
                
                if (pend >= 0 && pend < msgs.Count)
                {
                    msgs[pend].pending = false;
                    msgs[pend].text = txt;
                    msgs[pend].code = GetCode(txt);
                    if (mode == Mode.Agent)
                    {
                        msgs[pend].tools = ParseTools(txt);
                        pendingTools = msgs[pend].tools;
                        showTools = pendingTools.Count > 0;
                    }
                }
                status = "Готово!";
            }
            else
            {
                status = "Ошибка: " + req.responseCode + " " + req.error;
                if (pend >= 0 && pend < msgs.Count)
                {
                    msgs[pend].pending = false;
                    msgs[pend].text = "Ошибка: " + req.error;
                }
            }
            
            pend = -1;
            req.Dispose();
            Repaint();
        }

        string ExtractText(string json)
        {
            string key = prov == Provider.Gemini ? "\"text\"" : "\"content\"";
            int idx = json.IndexOf(key);
            if (idx < 0) return json;
            int s = json.IndexOf('"', idx + key.Length) + 1;
            int e = s;
            while (e < json.Length)
            {
                if (json[e] == '\\' && e + 1 < json.Length) { e += 2; continue; }
                if (json[e] == '"') break;
                e++;
            }
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
            int as2 = t.IndexOf("```");
            if (as2 >= 0)
            {
                as2 += 3;
                int le = t.IndexOf('\n', as2);
                if (le > 0) as2 = le + 1;
                int ae = t.IndexOf("```", as2);
                if (ae > as2) return t.Substring(as2, ae - as2).Trim();
            }
            return "";
        }

        List<ToolCall> ParseTools(string t)
        {
            var tools = new List<ToolCall>();
            if (string.IsNullOrEmpty(t)) return tools;
            
            void Parse3(string pattern, string toolName)
            {
                foreach (Match m in Regex.Matches(t, pattern))
                {
                    var tc = new ToolCall { tool = toolName, target = m.Groups[1].Value.Trim() };
                    if (m.Groups.Count > 2 && m.Groups[2].Success)
                    {
                        var p = m.Groups[2].Value.Split(',');
                        if (p.Length == 3)
                        {
                            float.TryParse(p[0].Trim(), out tc.pos.x);
                            float.TryParse(p[1].Trim(), out tc.pos.y);
                            float.TryParse(p[2].Trim(), out tc.pos.z);
                        }
                    }
                    tools.Add(tc);
                }
            }
            
            Parse3(@"\[CREATE:([^:]+):([^:\]]+)(?::([^:\]]+))?\]", "CREATE");
            Parse3(@"\[DELETE:([^\]]+)\]", "DELETE");
            Parse3(@"\[MOVE:([^:]+):([^:\]]+)\]", "MOVE");
            Parse3(@"\[ROTATE:([^:]+):([^:\]]+)\]", "ROTATE");
            Parse3(@"\[SCALE:([^:]+):([^:\]]+)\]", "SCALE");
            Parse3(@"\[DUPLICATE:([^\]]+)\]", "DUPLICATE");
            Parse3(@"\[FIND:([^\]]+)\]", "FIND");
            Parse3(@"\[EMPTY:([^:]+):([^:\]]+)\]", "EMPTY");
            
            foreach (Match m in Regex.Matches(t, @"\[ADD:([^:]+):([^\]]+)\]"))
                tools.Add(new ToolCall { tool = "ADD", target = m.Groups[1].Value.Trim(), param1 = m.Groups[2].Value.Trim() });
            
            foreach (Match m in Regex.Matches(t, @"\[REMOVE:([^:]+):([^\]]+)\]"))
                tools.Add(new ToolCall { tool = "REMOVE", target = m.Groups[1].Value.Trim(), param1 = m.Groups[2].Value.Trim() });
            
            foreach (Match m in Regex.Matches(t, @"\[PROP:([^:]+):([^:]+):([^:]+):([^\]]+)\]"))
                tools.Add(new ToolCall { tool = "PROP", target = m.Groups[1].Value.Trim(), param1 = m.Groups[2].Value.Trim(), param2 = m.Groups[3].Value.Trim(), param3 = m.Groups[4].Value.Trim() });
            
            foreach (Match m in Regex.Matches(t, @"\[SCRIPT:([^\]]+)\]"))
                tools.Add(new ToolCall { tool = "SCRIPT", target = m.Groups[1].Value.Trim() });
            
            foreach (Match m in Regex.Matches(t, @"\[MATERIAL:([^:]+):([^:\]]+)\]"))
            {
                var tc = new ToolCall { tool = "MATERIAL", target = m.Groups[1].Value.Trim() };
                var p = m.Groups[2].Value.Split(',');
                if (p.Length == 3) { float.TryParse(p[0], out tc.color.r); float.TryParse(p[1], out tc.color.g); float.TryParse(p[2], out tc.color.b); }
                tools.Add(tc);
            }
            
            foreach (Match m in Regex.Matches(t, @"\[SET_COLOR:([^:]+):([^:\]]+)\]"))
            {
                var tc = new ToolCall { tool = "SET_COLOR", target = m.Groups[1].Value.Trim() };
                var p = m.Groups[2].Value.Split(',');
                if (p.Length == 3) { float.TryParse(p[0], out tc.color.r); float.TryParse(p[1], out tc.color.g); float.TryParse(p[2], out tc.color.b); }
                tools.Add(tc);
            }
            
            foreach (Match m in Regex.Matches(t, @"\[LIGHT:([^:]+):([^:]+):([^:]+):([^:\]]+)\]"))
            {
                var tc = new ToolCall { tool = "LIGHT", target = m.Groups[1].Value.Trim(), param1 = m.Groups[3].Value.Trim() };
                var p = m.Groups[2].Value.Split(',');
                if (p.Length == 3) { float.TryParse(p[0], out tc.pos.x); float.TryParse(p[1], out tc.pos.y); float.TryParse(p[2], out tc.pos.z); }
                var c = m.Groups[4].Value.Split(',');
                if (c.Length == 3) { float.TryParse(c[0], out tc.color.r); float.TryParse(c[1], out tc.color.g); float.TryParse(c[2], out tc.color.b); }
                tools.Add(tc);
            }
            
            foreach (Match m in Regex.Matches(t, @"\[CAMERA:([^:]+):([^:\]]+)\]"))
            {
                var tc = new ToolCall { tool = "CAMERA", target = m.Groups[1].Value.Trim() };
                var p = m.Groups[2].Value.Split(',');
                if (p.Length == 3) { float.TryParse(p[0], out tc.pos.x); float.TryParse(p[1], out tc.pos.y); float.TryParse(p[2], out tc.pos.z); }
                tools.Add(tc);
            }
            
            foreach (Match m in Regex.Matches(t, @"\[CANVAS:([^\]]+)\]"))
                tools.Add(new ToolCall { tool = "CANVAS", target = m.Groups[1].Value.Trim() });
            
            foreach (Match m in Regex.Matches(t, @"\[BUTTON:([^:]+):([^:]+):([^:\]]+)\]"))
            {
                var tc = new ToolCall { tool = "BUTTON", target = m.Groups[1].Value.Trim(), param1 = m.Groups[2].Value.Trim() };
                var p = m.Groups[3].Value.Split(',');
                if (p.Length == 2) { float.TryParse(p[0], out tc.pos.x); float.TryParse(p[1], out tc.pos.y); }
                tools.Add(tc);
            }
            
            foreach (Match m in Regex.Matches(t, @"\[TEXT:([^:]+):([^:]+):([^:\]]+)\]"))
            {
                var tc = new ToolCall { tool = "TEXT", target = m.Groups[1].Value.Trim(), param1 = m.Groups[2].Value.Trim() };
                var p = m.Groups[3].Value.Split(',');
                if (p.Length == 2) { float.TryParse(p[0], out tc.pos.x); float.TryParse(p[1], out tc.pos.y); }
                tools.Add(tc);
            }
            
            foreach (Match m in Regex.Matches(t, @"\[PARTICLES:([^:]+):([^:\]]+)\]"))
            {
                var tc = new ToolCall { tool = "PARTICLES", target = m.Groups[1].Value.Trim() };
                var p = m.Groups[2].Value.Split(',');
                if (p.Length == 3) { float.TryParse(p[0], out tc.pos.x); float.TryParse(p[1], out tc.pos.y); float.TryParse(p[2], out tc.pos.z); }
                tools.Add(tc);
            }
            
            foreach (Match m in Regex.Matches(t, @"\[AUDIO:([^:]+):([^:\]]+)\]"))
            {
                var tc = new ToolCall { tool = "AUDIO", target = m.Groups[1].Value.Trim() };
                var p = m.Groups[2].Value.Split(',');
                if (p.Length == 3) { float.TryParse(p[0], out tc.pos.x); float.TryParse(p[1], out tc.pos.y); float.TryParse(p[2], out tc.pos.z); }
                tools.Add(tc);
            }
            
            foreach (Match m in Regex.Matches(t, @"\[SCENE:([^:]+)(?::([^\]]+))?\]"))
                tools.Add(new ToolCall { tool = "SCENE", action = m.Groups[1].Value.Trim(), target = m.Groups[2].Success ? m.Groups[2].Value.Trim() : "" });
            
            foreach (Match m in Regex.Matches(t, @"\[RENAME:([^:]+):([^\]]+)\]"))
                tools.Add(new ToolCall { tool = "RENAME", target = m.Groups[1].Value.Trim(), param1 = m.Groups[2].Value.Trim() });
            
            foreach (Match m in Regex.Matches(t, @"\[PARENT:([^:]+):([^\]]+)\]"))
                tools.Add(new ToolCall { tool = "PARENT", target = m.Groups[1].Value.Trim(), param1 = m.Groups[2].Value.Trim() });
            
            foreach (Match m in Regex.Matches(t, @"\[TAG:([^:]+):([^\]]+)\]"))
                tools.Add(new ToolCall { tool = "TAG", target = m.Groups[1].Value.Trim(), param1 = m.Groups[2].Value.Trim() });
            
            foreach (Match m in Regex.Matches(t, @"\[LAYER:([^:]+):([^\]]+)\]"))
                tools.Add(new ToolCall { tool = "LAYER", target = m.Groups[1].Value.Trim(), param1 = m.Groups[2].Value.Trim() });
            
            if (t.Contains("[UNDO]"))
                tools.Add(new ToolCall { tool = "UNDO" });
            
            return tools;
        }

        void ExecuteAll()
        {
            foreach (var tc in pendingTools)
            {
                if (!tc.approved) continue;
                try { ExecuteOne(tc); }
                catch (Exception e) { Debug.LogError("[AI] " + e.Message); }
            }
            pendingTools.Clear();
            showTools = false;
            status = "Выполнено!";
            Repaint();
        }

        void ExecuteOne(ToolCall tc)
        {
            GameObject go = null;
            
            switch (tc.tool)
            {
                case "CREATE":
                    switch (tc.action.ToLower())
                    {
                        case "cube": case "куб": go = GameObject.CreatePrimitive(PrimitiveType.Cube); break;
                        case "sphere": case "сфера": go = GameObject.CreatePrimitive(PrimitiveType.Sphere); break;
                        case "cylinder": case "цилиндр": go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); break;
                        case "plane": case "плоскость": go = GameObject.CreatePrimitive(PrimitiveType.Plane); break;
                        case "capsule": case "капсула": go = GameObject.CreatePrimitive(PrimitiveType.Capsule); break;
                        default: go = new GameObject(tc.action); break;
                    }
                    if (go != null) { go.name = tc.target; go.transform.position = tc.pos; Undo.RegisterCreatedObjectUndo(go, "AI"); Selection.activeGameObject = go; }
                    break;
                    
                case "DELETE":
                    go = GameObject.Find(tc.target);
                    if (go != null) Undo.DestroyObjectImmediate(go);
                    break;
                    
                case "MOVE":
                    go = GameObject.Find(tc.target);
                    if (go != null) { Undo.RecordObject(go.transform, "AI"); go.transform.position = tc.pos; }
                    break;
                    
                case "ROTATE":
                    go = GameObject.Find(tc.target);
                    if (go != null) { Undo.RecordObject(go.transform, "AI"); go.transform.rotation = Quaternion.Euler(tc.rot); }
                    break;
                    
                case "SCALE":
                    go = GameObject.Find(tc.target);
                    if (go != null) { Undo.RecordObject(go.transform, "AI"); go.transform.localScale = tc.scale; }
                    break;
                    
                case "ADD":
                    go = GameObject.Find(tc.target);
                    if (go != null) { var t = GetComponentType(tc.param1); if (t != null) Undo.AddComponent(go, t); }
                    break;
                    
                case "REMOVE":
                    go = GameObject.Find(tc.target);
                    if (go != null) { var c = go.GetComponent(tc.param1); if (c != null) Undo.DestroyObjectImmediate(c); }
                    break;
                    
                case "SET_COLOR":
                    go = GameObject.Find(tc.target);
                    if (go != null)
                    {
                        var r = go.GetComponent<Renderer>();
                        if (r != null)
                        {
                            Undo.RecordObject(r, "AI");
                            if (r.sharedMaterial == null || r.sharedMaterial.name == "Default-Material")
                            {
                                var mat = new Material(Shader.Find("Standard"));
                                mat.color = tc.color;
                                r.sharedMaterial = mat;
                            }
                            else r.sharedMaterial.color = tc.color;
                        }
                    }
                    break;
                    
                case "SCRIPT":
                    string code = GetCode(msgs.LastOrDefault(m => !string.IsNullOrEmpty(m.code))?.code ?? "");
                    if (!string.IsNullOrEmpty(code))
                    {
                        File.WriteAllText("Assets/" + tc.target + ".cs", code);
                        AssetDatabase.Refresh();
                    }
                    break;
                    
                case "MATERIAL":
                    var mat2 = new Material(Shader.Find("Standard"));
                    mat2.name = tc.target;
                    mat2.color = tc.color;
                    if (!Directory.Exists("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
                    AssetDatabase.CreateAsset(mat2, "Assets/Materials/" + tc.target + ".mat");
                    AssetDatabase.SaveAssets();
                    break;
                    
                case "LIGHT":
                    var lg = new GameObject(tc.target);
                    lg.transform.position = tc.pos;
                    var l = lg.AddComponent<Light>();
                    l.type = tc.param1.ToLower() == "spot" ? LightType.Spot : tc.param1.ToLower() == "directional" ? LightType.Directional : LightType.Point;
                    l.color = tc.color;
                    Undo.RegisterCreatedObjectUndo(lg, "AI");
                    break;
                    
                case "CAMERA":
                    var cg = new GameObject(tc.target);
                    cg.transform.position = tc.pos;
                    cg.AddComponent<Camera>();
                    cg.AddComponent<AudioListener>();
                    Undo.RegisterCreatedObjectUndo(cg, "AI");
                    break;
                    
                case "CANVAS":
                    var cv = new GameObject(tc.target);
                    cv.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                    cv.AddComponent<CanvasScaler>();
                    cv.AddComponent<GraphicRaycaster>();
                    Undo.RegisterCreatedObjectUndo(cv, "AI");
                    break;
                    
                case "BUTTON":
                    var canvas = FindObjectOfType<Canvas>();
                    if (canvas == null) { var cg2 = new GameObject("Canvas"); canvas = cg2.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; cg2.AddComponent<CanvasScaler>(); cg2.AddComponent<GraphicRaycaster>(); }
                    var bg = new GameObject(tc.target);
                    bg.transform.SetParent(canvas.transform);
                    bg.AddComponent<Button>();
                    bg.AddComponent<Image>().color = Color.white;
                    var br = bg.GetComponent<RectTransform>();
                    br.anchoredPosition = new Vector2(tc.pos.x, tc.pos.y);
                    br.sizeDelta = new Vector2(160, 40);
                    var tg = new GameObject("Text");
                    tg.transform.SetParent(bg.transform);
                    var tx = tg.AddComponent<Text>();
                    tx.text = tc.param1;
                    tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    tx.color = Color.black;
                    tx.alignment = TextAnchor.MiddleCenter;
                    var tr = tg.GetComponent<RectTransform>();
                    tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.sizeDelta = Vector2.zero;
                    Undo.RegisterCreatedObjectUndo(bg, "AI");
                    break;
                    
                case "TEXT":
                    var cObj = FindObjectOfType<Canvas>();
                    if (cObj == null) { var cg3 = new GameObject("Canvas"); cObj = cg3.AddComponent<Canvas>(); cObj.renderMode = RenderMode.ScreenSpaceOverlay; cg3.AddComponent<CanvasScaler>(); cg3.AddComponent<GraphicRaycaster>(); }
                    var tGo = new GameObject(tc.target);
                    tGo.transform.SetParent(cObj.transform);
                    var tComp = tGo.AddComponent<Text>();
                    tComp.text = tc.param1;
                    tComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    tComp.color = Color.white;
                    tComp.fontSize = 24;
                    tGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(tc.pos.x, tc.pos.y);
                    Undo.RegisterCreatedObjectUndo(tGo, "AI");
                    break;
                    
                case "PARTICLES":
                    var pg = new GameObject(tc.target);
                    pg.transform.position = tc.pos;
                    pg.AddComponent<ParticleSystem>();
                    Undo.RegisterCreatedObjectUndo(pg, "AI");
                    break;
                    
                case "AUDIO":
                    var ag = new GameObject(tc.target);
                    ag.transform.position = tc.pos;
                    ag.AddComponent<AudioSource>();
                    Undo.RegisterCreatedObjectUndo(ag, "AI");
                    break;
                    
                case "SCENE":
                    if (tc.action == "new") EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                    else if (tc.action == "save") EditorSceneManager.SaveOpenScenes();
                    else if (tc.action == "load" && !string.IsNullOrEmpty(tc.target))
                        EditorSceneManager.OpenScene("Assets/" + tc.target + ".unity");
                    break;
                    
                case "FIND":
                    go = GameObject.Find(tc.target);
                    if (go != null) Selection.activeGameObject = go;
                    break;
                    
                case "DUPLICATE":
                    go = GameObject.Find(tc.target);
                    if (go != null) { var dup = Instantiate(go); dup.name = tc.target + "_Copy"; Undo.RegisterCreatedObjectUndo(dup, "AI"); Selection.activeGameObject = dup; }
                    break;
                    
                case "RENAME":
                    go = GameObject.Find(tc.target);
                    if (go != null) { Undo.RecordObject(go, "AI"); go.name = tc.param1; }
                    break;
                    
                case "PARENT":
                    var child = GameObject.Find(tc.target);
                    var parent = GameObject.Find(tc.param1);
                    if (child != null && parent != null) Undo.SetTransformParent(child.transform, parent.transform, "AI");
                    break;
                    
                case "TAG":
                    go = GameObject.Find(tc.target);
                    if (go != null) { Undo.RecordObject(go, "AI"); go.tag = tc.param1; }
                    break;
                    
                case "LAYER":
                    go = GameObject.Find(tc.target);
                    if (go != null) { Undo.RecordObject(go, "AI"); go.layer = LayerMask.NameToLayer(tc.param1); }
                    break;
                    
                case "UNDO":
                    Undo.PerformUndo();
                    break;
            }
        }

        Type GetComponentType(string name)
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
                case "navmeshagent": return typeof(UnityEngine.AI.NavMeshAgent);
                case "canvas": return typeof(Canvas);
                case "button": return typeof(Button);
                case "text": return typeof(Text);
                case "image": return typeof(Image);
            }
            return null;
        }

        // GUI
        void OnGUI()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            prov = (Provider)EditorGUILayout.EnumPopup(prov, GUILayout.Width(80));
            GUILayout.FlexibleSpace();
            GUI.color = (prov == Provider.Groq && RK.Length > 10 && !RK.Contains("...")) ? Color.green : Color.red;
            GUILayout.Label("●");
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
            
            if (!string.IsNullOrEmpty(status))
            {
                var st = new GUIStyle(EditorStyles.helpBox) { wordWrap = true };
                GUILayout.Label(status, st);
            }
            
            GUILayout.BeginHorizontal();
            GUI.color = mode == Mode.Ask ? new Color(0.5f, 0.8f, 1f) : Color.white;
            if (GUILayout.Button("Вопросы", EditorStyles.miniButtonLeft, GUILayout.Height(26))) mode = Mode.Ask;
            GUI.color = mode == Mode.Agent ? new Color(0.5f, 1f, 0.5f) : Color.white;
            if (GUILayout.Button("Действия", EditorStyles.miniButtonMid, GUILayout.Height(26))) mode = Mode.Agent;
            GUI.color = mode == Mode.Plan ? new Color(1f, 0.8f, 0.5f) : Color.white;
            if (GUILayout.Button("План", EditorStyles.miniButtonRight, GUILayout.Height(26))) mode = Mode.Plan;
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
            
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

        // JSON classes
        [Serializable] class GemReq { public GemC[] contents; public GemCfg generationConfig; }
        [Serializable] class GemC { public GemP[] parts; }
        [Serializable] class GemP { public string text; }
        [Serializable] class GemCfg { public int maxOutputTokens; }
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