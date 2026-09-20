using System.Collections.Generic;
using UnityEngine;
using MagicAvatarCreator;

namespace Forma
{
    [ExecuteAlways]
    public class FormaStudio : MonoBehaviour
    {
        public static bool PointerOverUi { get; private set; }

        public AvatarParams Parameters;
        [Header("Reference base assets")]
        public AvatarData FemaleReferenceBase;
        public AvatarData MaleReferenceBase;
        public bool ShowRuntimeUi = true;
        public bool RebuildInEditMode;
        [Header("Аватары: MAC (Magic Avatar Creator) вместо эталонных GLB/OBJ")]
        public bool useMacAvatars = true;

        AvatarGenerator _gen;
        ReferenceAvatarLoader _referenceLoader;
        MacAvatarAdapter _mac;
        ReferenceBackend _referenceBackend;
        OrbitCamera _orbit;

        /// <summary>Активный источник аватара (MAC или эталоны) — единый контракт для панели.</summary>
        IAvatarBackend Backend
        {
            get
            {
                if (useMacAvatars && _mac != null) return _mac;
                if (_referenceBackend == null)
                {
                    var cust = GetComponent<ReferenceAvatarCustomizer>();
                    if (cust == null) { cust = gameObject.AddComponent<ReferenceAvatarCustomizer>(); cust.showEditorUi = false; }
                    _referenceBackend = new ReferenceBackend(_referenceLoader, cust);
                }
                return _referenceBackend;
            }
        }
        CategoryId _tab = CategoryId.Body;
        string _section = "proportions";
        Vector2 _scroll;
        string _lookName = "Образ";
        readonly List<SavedLook> _looks = new List<SavedLook>();
        AvatarPreset[] _presets;
        bool _dirty = true;
        float _idle;
        GUIStyle _panel, _title, _hint, _tabOn, _tabOff, _chipOn, _chipOff, _label, _btn, _btnAccent, _word, _sub;
        Texture2D _pxBg, _pxSurf, _pxAccent, _pxLine, _pxChip;
        bool _stylesReady;
        int _uiWidth = 380;

        static readonly Dictionary<CategoryId, (float y, float dist)> Focus = new Dictionary<CategoryId, (float, float)>
        {
            { CategoryId.Body, (0.95f, 2.55f) },
            { CategoryId.Face, (1.62f, 0.46f) },
            { CategoryId.Skin, (1.62f, 0.40f) },
            { CategoryId.Hair, (1.64f, 0.60f) },
            { CategoryId.Cloth, (0.95f, 2.40f) },
            { CategoryId.Preset, (0.95f, 2.60f) }
        };

        void OnEnable()
        {
            _referenceLoader = GetComponent<ReferenceAvatarLoader>();
            if (_referenceLoader == null) _referenceLoader = gameObject.AddComponent<ReferenceAvatarLoader>();
            if (GetComponent<ReferenceAvatarCustomizer>() == null) gameObject.AddComponent<ReferenceAvatarCustomizer>();
            _mac = FindFirstObjectByType<MacAvatarAdapter>();
            _gen = GetComponent<AvatarGenerator>();
            if (_gen != null) _gen.enabled = false;
            HideProceduralAvatar();
            // MAC-режим по умолчанию: запускаем MAC-аватар сразу, эталонный GLB не грузим
            if (useMacAvatars && Application.isPlaying && _mac != null)
                _mac.SetSex(Parameters != null && Parameters.sex == Sex.Male);
            ApplyReferenceBaseDefaults();
            // Upgrade the original schematic starter stored in older scene YAML,
            // while preserving any genuinely customized serialized look.
            if (Parameters == null || IsLegacyStarter(Parameters))
                Parameters = AvatarDefaults.Create(Parameters == null ? Sex.Female : Parameters.sex);
            _presets = AvatarPresets.All();
            SetupScene();
            _dirty = true;
        }

        void ApplyReferenceBaseDefaults()
        {
            var baseData = Parameters != null && Parameters.sex == Sex.Male ? MaleReferenceBase : FemaleReferenceBase;
            if (baseData == null) return;
            if (Parameters == null) Parameters = AvatarDefaults.Create(System.Convert.ToInt32(baseData.avatarType) == 0 ? Sex.Male : Sex.Female);
            Parameters.smoothness = Mathf.Clamp01(baseData.smoothness + 0.18f);
            Parameters.freckles = Mathf.Clamp01(baseData.fracklesIntensity * 0.32f);
            Parameters.freckleScale = Mathf.Clamp01(1f - baseData.fracklesScale / 14f);
            Parameters.freckleColor = ColorUtility.ToHtmlStringRGB(baseData.frackles).Insert(0, "#");
            if (System.Convert.ToInt32(baseData.avatarType) == 1) Parameters.lipTint = ColorUtility.ToHtmlStringRGB(baseData.lipsColor).Insert(0, "#");
        }

        static bool IsLegacyStarter(AvatarParams p)
        {
            if (p == null) return true;
            if (p.sex == Sex.Female)
                return Mathf.Abs(p.height - 0.54f) < 0.001f
                    && Mathf.Abs(p.build - 0.42f) < 0.001f
                    && Mathf.Abs(p.waist - 0.28f) < 0.001f;
            return Mathf.Abs(p.height - 0.70f) < 0.001f
                && Mathf.Abs(p.muscular - 0.90f) < 0.001f
                && Mathf.Abs(p.shoulders - 0.86f) < 0.001f;
        }

        void SetupScene()
        {
            // Real imported GLB avatars are the primary representation.
            // Never create or rebuild the old procedural placeholder here.
            _gen = GetComponent<AvatarGenerator>();
            if (_gen != null) _gen.enabled = false;
            HideProceduralAvatar();

            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("FORMA Camera");
                cam = go.AddComponent<Camera>();
                go.tag = "MainCamera";
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.035f, 0.037f, 0.043f);
            cam.fieldOfView = 34f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 40f;
            cam.allowHDR = true;
            _orbit = cam.GetComponent<OrbitCamera>();
            if (_orbit == null) _orbit = cam.gameObject.AddComponent<OrbitCamera>();
            _orbit.Target = transform;
            _orbit.TargetY = 0.95f;
            _orbit.Distance = 2.55f;
            cam.transform.position = new Vector3(0.28f, 1.12f, -2.55f);

            EnsureLight("FORMA Key", new Vector3(1.4f, 2.6f, -4.4f), new Color(1f, 0.93f, 0.86f), 1.85f);
            EnsureLight("FORMA Fill", new Vector3(-2.6f, 1.5f, -2.4f), new Color(0.74f, 0.82f, 1f), 0.68f);
            EnsureLight("FORMA Rim", new Vector3(0.2f, 2.2f, 3.4f), new Color(1f, 0.88f, 0.72f), 1.05f);

            if (FindFloor() == null)
            {
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                floor.name = "FORMA Floor";
                floor.transform.SetParent(transform, false);
                floor.transform.localScale = new Vector3(2.4f, 0.002f, 2.4f);
                floor.transform.position = Vector3.zero;
                Object.Destroy(floor.GetComponent<Collider>());
            }
            FixFloorShader();

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.77f, 0.78f, 0.83f);
            RenderSettings.ambientEquatorColor = new Color(0.35f, 0.32f, 0.28f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.1f, 0.09f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.047f, 0.047f, 0.055f);
            RenderSettings.fogStartDistance = 6f;
            RenderSettings.fogEndDistance = 14f;
        }

        static Transform FindFloor()
        {
            var t = GameObject.Find("FORMA Floor");
            return t ? t.transform : null;
        }

        // URP рендерит Built-in-шейдеры розовым (error shader) — подставка всегда
        // получает материал шейдера АКТИВНОГО пайплайна, включая уже существующую в сцене.
        static void FixFloorShader()
        {
            var t = FindFloor();
            var r = t != null ? t.GetComponent<MeshRenderer>() : null;
            if (r == null) return;
            bool srp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
            var shader = Shader.Find(srp ? "Universal Render Pipeline/Lit" : "FORMA/LitVertexColor")
                ?? Shader.Find(srp ? "FORMA/LitVertexColor" : "Standard");
            if (shader == null) return;
            var m = new Material(shader);
            var c = new Color(0.086f, 0.086f, 0.094f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            r.sharedMaterial = m;
        }

        static void EnsureLight(string name, Vector3 pos, Color col, float intensity)
        {
            var existing = GameObject.Find(name);
            Light l;
            if (existing == null)
            {
                var go = new GameObject(name);
                l = go.AddComponent<Light>();
                l.type = LightType.Directional;
            }
            else l = existing.GetComponent<Light>();
            l.color = col;
            l.intensity = intensity;
            l.transform.position = pos;
            l.transform.LookAt(new Vector3(0, 1f, 0));
            l.shadows = LightShadows.Soft;
        }

        void Update()
        {
            // ReferenceAvatarLoader owns the realistic GLB. Calling AvatarGenerator.Apply
            // here used to recreate the low-poly placeholder over the real model.
            HideProceduralAvatar();
            if (useMacAvatars && _mac != null && Application.isPlaying)
            {
                _mac.EnsureReady();
                _mac.ApplyFromParameters(Parameters);
                var custHide = GetComponent<ReferenceAvatarCustomizer>();
                if (custHide != null && custHide.showEditorUi) custHide.showEditorUi = false;
            }
            else if (Application.isPlaying)
            {
                var customizer = GetComponent<ReferenceAvatarCustomizer>();
                if (customizer == null) customizer = gameObject.AddComponent<ReferenceAvatarCustomizer>();
                customizer.showEditorUi = true;
                customizer.ApplyFromParameters(Parameters);
            }
            _dirty = false;
            _idle += Time.deltaTime;
        }

        void HideProceduralAvatar()
        {
            var oldRoot = transform.Find("AvatarRoot");
            if (oldRoot != null && oldRoot.gameObject.activeSelf)
                oldRoot.gameObject.SetActive(false);
        }

        void OnGUI()
        {
            if (!ShowRuntimeUi || !Application.isPlaying) return;
            EnsureStyles();
            float w = Mathf.Min(_uiWidth, Screen.width * 0.92f);
            var panel = new Rect(0, 0, w, Screen.height);
            PointerOverUi = panel.Contains(Event.current.mousePosition)
                            || new Rect(Screen.width - 280, 0, 280, 90).Contains(Event.current.mousePosition);

            GUI.Box(panel, GUIContent.none, _panel);
            GUILayout.BeginArea(new Rect(12, 16, w - 24, Screen.height - 28));
            GUILayout.Label("FORMA", _title);
            GUILayout.Label("IDENTITY LAB  /  UNITY", _hint);
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            foreach (var c in AvatarCatalog.Categories)
            {
                var st = _tab == c.id ? _tabOn : _tabOff;
                if (GUILayout.Button(c.label, st, GUILayout.Height(36)))
                    SetTab(c.id);
            }
            GUILayout.EndHorizontal();

            var cat = CurrentCat();
            GUILayout.Space(8);
            GUILayout.Label(cat.label, _word);
            GUILayout.Label(cat.hint, _hint);

            GUILayout.BeginHorizontal();
            foreach (var s in cat.sections)
            {
                var st = _section == s.id ? _chipOn : _chipOff;
                if (GUILayout.Button(s.label, st, GUILayout.Height(28)))
                    _section = s.id;
            }
            GUILayout.EndHorizontal();

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            var page = CurrentSection(cat);
            DrawSection(page);
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Случайно", _btn, GUILayout.Height(40))) { Randomize(); }
            if (GUILayout.Button("Сброс", _btn, GUILayout.Height(40))) { Parameters = AvatarDefaults.Create(Parameters.sex); _dirty = true; }
            if (GUILayout.Button("Назад", _btn, GUILayout.Height(40))) NextSection(-1);
            if (GUILayout.Button("Далее", _btnAccent, GUILayout.Height(40))) NextSection(1);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            DrawTopBar();
        }

        void DrawTopBar()
        {
            GUILayout.BeginArea(new Rect(Screen.width - 272, 16, 256, 118));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Parameters.sex == Sex.Female ? "●  01  Женский" : "01  Женский",
                    Parameters.sex == Sex.Female ? _chipOn : _chipOff, GUILayout.Height(36)))
            {
                Parameters = AvatarDefaults.Create(Sex.Female);
                if (useMacAvatars && _mac != null) _mac.SetSex(false);
                else if (_referenceLoader != null) _referenceLoader.SelectFemale();
                _dirty = true;
            }
            if (GUILayout.Button(Parameters.sex == Sex.Male ? "●  02  Мужской" : "02  Мужской",
                    Parameters.sex == Sex.Male ? _chipOn : _chipOff, GUILayout.Height(36)))
            {
                Parameters = AvatarDefaults.Create(Sex.Male);
                if (useMacAvatars && _mac != null) _mac.SetSex(true);
                else if (_referenceLoader != null) _referenceLoader.SelectMale();
                _dirty = true;
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Экспорт PNG", _btnAccent, GUILayout.Height(32))) Capture();

            // Переключение источника аватаров: эталонные GLB/OBJ ⇄ MAC (Magic Avatar Creator)
            if (GUILayout.Button(useMacAvatars ? "●  Аватары: MAC" : "Аватары: MAC",
                    useMacAvatars ? _chipOn : _chipOff, GUILayout.Height(26)))
            {
                useMacAvatars = !useMacAvatars;
                if (useMacAvatars)
                {
                    if (_mac == null) _mac = FindFirstObjectByType<MacAvatarAdapter>();
                    if (_referenceLoader != null) _referenceLoader.HideCurrent();
                    if (_mac != null) _mac.SetSex(Parameters.sex == Sex.Male);
                }
                else
                {
                    if (_mac != null) _mac.Hide();
                    if (_referenceLoader != null)
                    {
                        if (Parameters.sex == Sex.Male) _referenceLoader.SelectMale();
                        else _referenceLoader.SelectFemale();
                    }
                }
                var cust = GetComponent<ReferenceAvatarCustomizer>();
                if (cust != null) cust.showEditorUi = !useMacAvatars;
            }
            GUILayout.EndArea();

            GUI.Label(new Rect(Screen.width * 0.5f - 140, Screen.height - 36, 280, 24),
                "Тяните, чтобы сменить ракурс  ·  колесо — масштаб", _sub);
        }

        CategoryDef CurrentCat()
        {
            foreach (var c in AvatarCatalog.Categories)
                if (c.id == _tab) return c;
            return AvatarCatalog.Categories[0];
        }

        SectionDef CurrentSection(CategoryDef cat)
        {
            foreach (var s in cat.sections)
                if (s.id == _section) return s;
            return cat.sections[0];
        }

        void SetTab(CategoryId id)
        {
            _tab = id;
            _section = CurrentCat().sections[0].id;
            if (_orbit != null && Focus.TryGetValue(id, out var f))
            {
                float h = MathUtil.Lerp(0.9f, 1.16f, Parameters.height);
                _orbit.Focus(f.y * h, f.dist);
            }
        }

        void NextSection(int dir)
        {
            var cats = AvatarCatalog.Categories;
            var cat = CurrentCat();
            int i = 0;
            for (; i < cat.sections.Length; i++) if (cat.sections[i].id == _section) break;
            int j = i + dir;
            if (j >= cat.sections.Length)
            {
                int ci = System.Array.FindIndex(cats, c => c.id == _tab);
                var next = cats[(ci + 1) % cats.Length];
                SetTab(next.id);
                return;
            }
            if (j < 0)
            {
                int ci = System.Array.FindIndex(cats, c => c.id == _tab);
                var prev = cats[(ci - 1 + cats.Length) % cats.Length];
                SetTab(prev.id);
                _section = prev.sections[prev.sections.Length - 1].id;
                return;
            }
            _section = cat.sections[j].id;
        }

        void DrawSection(SectionDef page)
        {
            if (page.sliders != null)
            {
                foreach (var s in page.sliders)
                {
                    float v = AvatarCatalog.GetFloat(Parameters, s.id);
                    GUILayout.Space(6);
                    GUILayout.Label(s.label.ToUpperInvariant(), _label);
                    float nv = GUILayout.HorizontalSlider(v, 0f, 1f, GUILayout.Height(18));
                    if (!Mathf.Approximately(nv, v))
                    {
                        AvatarCatalog.SetFloat(Parameters, s.id, nv);
                        _dirty = true;
                    }
                }
                return;
            }
            switch (page.picker)
            {
                case "skin": DrawSwatches(AvatarDefaults.SkinSwatches, Parameters.skin, c => { Parameters.skin = c; _dirty = true; }); break;
                case "eyes":
                    GUILayout.Label("ГЛАЗА", _label);
                    DrawSwatches(AvatarDefaults.EyeSwatches, Parameters.eyeColor, c => { Parameters.eyeColor = c; _dirty = true; });
                    GUILayout.Space(8);
                    GUILayout.Label("ГУБЫ", _label);
                    DrawSwatches(new[] { "#c47874", "#b07870", "#8a3038", "#d4a09a", "#6b1c28", "#3a2a22" }, Parameters.lipTint, c => { Parameters.lipTint = c; _dirty = true; });
                    break;
                case "hair":
                    foreach (var o in AvatarCatalog.HairOptions)
                        if (GUILayout.Button((Parameters.hairStyle == o.id ? "●  " : "   ") + o.label, Parameters.hairStyle == o.id ? _chipOn : _btn, GUILayout.Height(32)))
                        { Parameters.hairStyle = o.id; _dirty = true; }
                    break;
                case "hairColor": DrawSwatches(AvatarDefaults.HairSwatches, Parameters.hairColor, c => { Parameters.hairColor = c; _dirty = true; }); break;
                case "upper":
                    foreach (var o in AvatarCatalog.UpperOptions)
                        if (GUILayout.Button((Parameters.upper == o.id ? "●  " : "   ") + o.label, Parameters.upper == o.id ? _chipOn : _btn, GUILayout.Height(32)))
                        { Parameters.upper = o.id; _dirty = true; }
                    break;
                case "lower":
                    foreach (var o in AvatarCatalog.LowerOptions)
                        if (GUILayout.Button((Parameters.lower == o.id ? "●  " : "   ") + o.label, Parameters.lower == o.id ? _chipOn : _btn, GUILayout.Height(32)))
                        { Parameters.lower = o.id; _dirty = true; }
                    break;
                case "full":
                    foreach (var o in AvatarCatalog.FullOptions)
                        if (GUILayout.Button((Parameters.full == o.id ? "●  " : "   ") + o.label, Parameters.full == o.id ? _chipOn : _btn, GUILayout.Height(32)))
                        { Parameters.full = o.id; _dirty = true; }
                    break;
                case "clothColor":
                    GUILayout.Label("ВЕРХ", _label);
                    DrawSwatches(AvatarDefaults.ClothSwatches, Parameters.upperColor, c => { Parameters.upperColor = c; _dirty = true; });
                    GUILayout.Space(8);
                    GUILayout.Label("НИЗ", _label);
                    DrawSwatches(AvatarDefaults.ClothSwatches, Parameters.lowerColor, c => { Parameters.lowerColor = c; _dirty = true; });
                    break;
                case "preset":
                    foreach (var pr in _presets)
                        if (GUILayout.Button(pr.name, _btn, GUILayout.Height(34)))
                        { Parameters = pr.parameters.Clone(); _dirty = true; }
                    GUILayout.Space(12);
                    GUILayout.Label("СОХРАНИТЬ", _label);
                    _lookName = GUILayout.TextField(_lookName, GUILayout.Height(28));
                    if (GUILayout.Button("Сохранить образ", _btnAccent, GUILayout.Height(32)))
                    {
                        _looks.Insert(0, new SavedLook
                        {
                            id = System.DateTime.Now.Ticks.ToString(),
                            name = string.IsNullOrWhiteSpace(_lookName) ? "Образ" : _lookName,
                            parameters = Parameters.Clone(),
                            // 100% round-trip: слепок весов MAC-аватара рядом с параметрами
                            macShapeDump = useMacAvatars && _mac != null && _mac.IsReady ? _mac.DumpShapes() : null,
                        });
                        if (_looks.Count > 24) _looks.RemoveAt(_looks.Count - 1);
                    }
                    foreach (var look in _looks.ToArray())
                    {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button(look.name, _btn, GUILayout.Height(30)))
                        {
                            Parameters = look.parameters.Clone();
                            _dirty = true;
                            // восстановить точные веса MAC, затем прочитать их обратно в слайдеры
                            if (useMacAvatars && _mac != null && _mac.IsReady && !string.IsNullOrEmpty(look.macShapeDump))
                            {
                                _mac.ApplyFromParameters(Parameters);
                                _mac.ApplyDump(look.macShapeDump);
                                Parameters = _mac.Capture();
                            }
                        }
                        if (GUILayout.Button("×", _btn, GUILayout.Width(32), GUILayout.Height(30)))
                            _looks.Remove(look);
                        GUILayout.EndHorizontal();
                    }
                    break;
            }
        }

        void DrawSwatches(string[] colors, string current, System.Action<string> onPick)
        {
            int cols = 5;
            int i = 0;
            while (i < colors.Length)
            {
                GUILayout.BeginHorizontal();
                for (int c = 0; c < cols && i < colors.Length; c++, i++)
                {
                    var hex = colors[i];
                    var old = GUI.backgroundColor;
                    GUI.backgroundColor = MathUtil.Hex(hex);
                    var mark = hex == current ? "●" : " ";
                    if (GUILayout.Button(mark, GUILayout.Width(44), GUILayout.Height(32)))
                        onPick(hex);
                    GUI.backgroundColor = old;
                }
                GUILayout.EndHorizontal();
            }
        }

        void Randomize()
        {
            var b = AvatarDefaults.Create(Parameters.sex);
            float R(float center, float spread) => Mathf.Clamp01(center + (Random.value * 2f - 1f) * spread);
            var n = b.Clone();
            n.skin = Parameters.skin;
            n.eyeColor = Parameters.eyeColor;
            n.height = R(b.height, 0.18f); n.build = R(b.build, 0.16f);
            n.muscular = R(b.muscular, 0.22f); n.fat = R(b.fat, 0.18f);
            n.neck = R(b.neck, 0.14f); n.shoulders = R(b.shoulders, 0.16f);
            n.chest = R(b.chest, 0.2f); n.waist = R(b.waist, 0.16f);
            n.belly = R(b.belly, 0.16f); n.hips = R(b.hips, 0.16f);
            n.buttocks = R(b.buttocks, 0.18f); n.thighs = R(b.thighs, 0.16f);
            n.arms = R(b.arms, 0.16f);
            n.headWidth = R(b.headWidth, 0.12f); n.headHeight = R(b.headHeight, 0.1f); n.headDepth = R(b.headDepth, 0.1f);
            n.forehead = R(b.forehead, 0.14f); n.browRidge = R(b.browRidge, 0.16f);
            n.eyeSize = R(b.eyeSize, 0.14f); n.eyeSpacing = R(b.eyeSpacing, 0.12f);
            n.eyeHeight = R(b.eyeHeight, 0.1f); n.eyeShape = R(b.eyeShape, 0.16f);
            n.noseWidth = R(b.noseWidth, 0.16f); n.noseLength = R(b.noseLength, 0.16f);
            n.noseBridge = R(b.noseBridge, 0.16f); n.noseTip = R(b.noseTip, 0.14f);
            n.cheekbones = R(b.cheekbones, 0.16f); n.cheeks = R(b.cheeks, 0.14f);
            n.earSize = R(b.earSize, 0.14f);
            n.lipWidth = R(b.lipWidth, 0.14f); n.lipUpper = R(b.lipUpper, 0.16f); n.lipLower = R(b.lipLower, 0.16f);
            n.jawWidth = R(b.jawWidth, 0.16f); n.chinProjection = R(b.chinProjection, 0.14f);
            n.freckles = R(0.2f, 0.25f); n.blush = R(b.blush, 0.12f);
            n.hairLength = R(b.hairLength, 0.2f); n.hairVolume = R(b.hairVolume, 0.2f);
            n.facialHair = Parameters.sex == Sex.Male ? R(0.45f, 0.3f) : 0f;
            Parameters = n;
            _dirty = true;
        }

        void Capture()
        {
            string path = System.IO.Path.Combine(Application.dataPath, "..", "forma-avatar.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("FORMA exported → " + path);
        }

        Texture2D Pix(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }

        void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;
            _pxBg = Pix(new Color(0.055f, 0.055f, 0.06f, 0.94f));
            _pxSurf = Pix(new Color(0.12f, 0.12f, 0.13f, 1f));
            _pxAccent = Pix(new Color(0.77f, 0.63f, 0.42f, 1f));
            _pxLine = Pix(new Color(0.22f, 0.21f, 0.2f, 1f));
            _pxChip = Pix(new Color(0.09f, 0.09f, 0.1f, 1f));
            var ivory = new Color(0.91f, 0.89f, 0.86f);
            var muted = new Color(0.55f, 0.53f, 0.5f);

            _panel = new GUIStyle(GUI.skin.box) { normal = { background = _pxBg } };
            _title = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, normal = { textColor = ivory } };
            _word = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = ivory } };
            _hint = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = muted } };
            _sub = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter, normal = { textColor = muted } };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, normal = { textColor = muted } };
            _tabOn = Btn(_pxSurf, ivory, 10);
            _tabOff = Btn(_pxChip, muted, 10);
            _chipOn = Btn(_pxAccent, new Color(0.08f, 0.07f, 0.06f), 11);
            _chipOff = Btn(_pxSurf, muted, 11);
            _btn = Btn(_pxSurf, ivory, 12);
            _btnAccent = Btn(_pxAccent, new Color(0.08f, 0.07f, 0.06f), 12);
        }

        static GUIStyle Btn(Texture2D bg, Color fg, int size)
        {
            return new GUIStyle(GUI.skin.button)
            {
                fontSize = size,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = bg, textColor = fg },
                hover = { background = bg, textColor = fg },
                active = { background = bg, textColor = fg }
            };
        }
    }
}
