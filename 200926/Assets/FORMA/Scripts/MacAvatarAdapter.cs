using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using MagicAvatarCreator;

namespace Forma
{
    /// <summary>
    /// MAC-бэкенд FORMA (IAvatarBackend): MagicAvatarManager + биндинг-таблица
    /// FormaMacBinding (данные, калибруемые без кода). Реализует:
    ///  - Apply: параметры FORMA → блендшейпы MAC (кривые отклика, зеркальные _L/_R
    ///    пары пишутся синхронно, шаблонные лестницы T1..Tn интерполируются);
    ///  - Capture: текущие веса блендшейпов → параметры (обратная связь UI);
    ///  - DumpShapes/ApplyDump: полный слепок весов для 100% round-trip образов;
    ///  - кожа/подтон/гладкость/веснушки/глаза/губы, CC-волосы и одежда
    ///    (каталог MacContentCatalog) с bindpose-ретаргетом на риг MAC.
    /// Строки биндинга без аналога в меше пропускаются; параметры без строки
    /// падают в кейворд-фолбэк (Keys) — регресса нет.
    /// </summary>
    public class MacAvatarAdapter : MonoBehaviour, IAvatarBackend
    {
        public GameObject femaleAvatarBase;
        public GameObject maleAvatarBase;
        [Tooltip("Таблица соответствий. Пусто → Resources/FORMA/FormaMacBinding или программный дефолт")]
        public FormaMacBinding binding;

        public string DebugName => "MAC (Magic Avatar Creator)";

        MagicAvatarManager _mgr;
        AvatarObject _av;
        FormaMacBinding _binding;
        RowSlot[] _slots;
        GameObject _hair, _upper, _lower;
        Color _hairTint = Color.white, _upperTint = Color.white, _lowerTint = Color.white;
        float _hairGloss = 0.5f;
        string _hairStyle = "\0", _upperId = "\0", _lowerId = "\0";
        bool _busy;
        AvatarParams _lastParams;

        struct RowSlot
        {
            public string paramId;
            public int[] shapeIndices;   // прямой набор (зеркальные пары включены)
            public int[] ladderIndices;  // шаблонная лестница T1..Tn
        }

        // ── Фолбэк-кейсворды для параметров, которых нет в биндинге ──
        static readonly (string param, string[] keys)[] Keys =
        {
            ("muscular", new[]{"muscular","muscl"}), ("fat", new[]{"fat"}),
            ("chest", new[]{"chestl","chest","breast"}), ("waist", new[]{"waist"}),
            ("belly", new[]{"belly","stomach"}), ("hips", new[]{"hips","hip"}),
            ("buttocks", new[]{"buttocks","butt","glute"}), ("calves", new[]{"lowerlegs","calf"}),
            ("neck", new[]{"neck"}), ("shoulders", new[]{"shoulder","clavicle"}),
            ("arms", new[]{"arm"}), ("forearms", new[]{"forearm"}), ("hands", new[]{"hand"}),
            ("feet", new[]{"foot","feet"}), ("legs", new[]{"legs"}),
            ("headWidth", new[]{"skull_t1"}), ("headHeight", new[]{"skull_t2"}),
            ("headDepth", new[]{"skull_t3"}), ("forehead", new[]{"skull_t4","forehead"}),
            ("earSize", new[]{"ears_t","ear"}), ("browRidge", new[]{"brow"}),
            ("eyeSize", new[]{"eyesize"}), ("eyeSpacing", new[]{"eyesspread","eyedistance"}),
            ("eyeHeight", new[]{"eyeheight"}), ("eyeDepth", new[]{"eyedepth"}),
            ("eyeShape", new[]{"eyeshape","eyenarrow"}),
            ("noseWidth", new[]{"nose_t1"}), ("noseLength", new[]{"nose_t2"}),
            ("noseBridge", new[]{"nose_t3"}), ("noseTip", new[]{"nose_t4","noseout"}),
            ("nostrils", new[]{"nostril","nosewing"}), ("cheekbones", new[]{"cheekbone"}),
            ("cheeks", new[]{"cheek"}), ("jowls", new[]{"jowl"}),
            ("lipWidth", new[]{"lipwidth","mouthsize","mouthwidth"}),
            ("lipUpper", new[]{"lipupper"}), ("lipLower", new[]{"liplower"}),
            ("jawWidth", new[]{"jawwidth","jawbackbottomwide","jawbacktopwide"}),
            ("jawLength", new[]{"jawheight","jawlength","jawcenterupper"}),
            ("chinWidth", new[]{"chinwidth","chinwide"}), ("chinHeight", new[]{"chinheight"}),
            ("chinProjection", new[]{"chinforward","chinprojection"}),
        };

        static readonly string[] exprWords = {
            "Look_", "Smile", "Frown", "Press", "Pucker", "Funnel", "Blow", "Sneer",
            "Stretch", "Compress", "Squint", "Blink", "Puff", "Dilate", "Wide",
            "Tighten", "Wrinkle", "Crease"
        };
        static bool IsExpression(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            foreach (var w in exprWords) if (n.Contains(w)) return true;
            return false;
        }

        // ══════════════ IAvatarBackend ══════════════
        public bool IsReady => _av != null;

        public void Hide()
        {
            if (_av != null) _av.gameObject.SetActive(false);
        }

        public void EnsureReady()
        {
            if (!Application.isPlaying || _busy || _av != null) return;
            SetSex(false);
        }

        public void SetSex(bool male)
        {
            if (!Application.isPlaying) return;
            if (_mgr != null && _av != null &&
                _mgr.CurrentAvatarType == (male ? MagicAvatarManager.AvatarType.Male : MagicAvatarManager.AvatarType.Female))
                return;
            StartCoroutine(SetSexRoutine(male));
        }

        IEnumerator SetSexRoutine(bool male)
        {
            _busy = true;
            if (_mgr == null)
            {
                _mgr = FindFirstObjectByType<MagicAvatarManager>();
                if (_mgr == null)
                {
                    if (maleAvatarBase == null || femaleAvatarBase == null)
                    {
                        Debug.LogError("[FORMA/MAC] Префабы MAC-аватаров не назначены на MacAvatarAdapter (сцена)");
                        _busy = false;
                        yield break;
                    }
                    var go = new GameObject("MAC Avatar Manager");
                    go.transform.SetParent(transform, false);
                    _mgr = go.AddComponent<MagicAvatarManager>();
                    _mgr.maleAvatarBase = maleAvatarBase;
                    _mgr.femaleAvatarBase = femaleAvatarBase;
                }
            }
            _mgr.ChangeAvatarType(male ? 0 : 1);
            for (int i = 0; i < 300; i++)
            {
                yield return null;
                var av = _mgr.CurrentAvatar;
                if (av != null && av.rootSkinnedMeshRenderer?.sharedMesh != null && av.BlendShapeController != null)
                    break;
            }
            _av = _mgr.CurrentAvatar;
            ClearPieces();
            RebuildSlots();
            _busy = false;
            if (_lastParams != null) Apply(_lastParams);
        }

        // ══════════════ Биндинг → слоты ══════════════
        void EnsureBinding()
        {
            if (binding != null) { _binding = binding; return; }
            if (_binding == null)
            {
                _binding = Resources.Load<FormaMacBinding>("FORMA/FormaMacBinding");
                if (_binding == null) _binding = FormaMacBinding.CreateDefault();
            }
        }

        void RebuildSlots()
        {
            EnsureBinding();
            var list = new List<RowSlot>();
            var smr = _av?.rootSkinnedMeshRenderer;
            var mesh = smr?.sharedMesh;
            if (mesh != null && _binding != null && _binding.rows != null)
            {
                var boundShapes = new HashSet<int>();
                foreach (var row in _binding.rows)
                {
                    if (row == null || string.IsNullOrEmpty(row.paramId)) continue;
                    var slot = new RowSlot { paramId = row.paramId };
                    if (row.templateLadder != null && row.templateLadder.Length > 0)
                    {
                        var l = new List<int>();
                        foreach (var n in row.templateLadder)
                        {
                            var idx = ResolveShape(mesh, n);
                            if (idx >= 0) { l.Add(idx); boundShapes.Add(idx); }
                        }
                        slot.ladderIndices = l.ToArray();
                    }
                    else if (row.shapeNames != null)
                    {
                        var s = new List<int>();
                        foreach (var n in row.shapeNames)
                        {
                            foreach (var idx in ResolveShapeAll(mesh, n))
                            { s.Add(idx); boundShapes.Add(idx); }
                        }
                        slot.shapeIndices = s.ToArray();
                    }
                    if (slot.shapeIndices != null || slot.ladderIndices != null) list.Add(slot);
                }
            }
            _slots = list.ToArray();
            Debug.Log($"[FORMA/MAC] биндинг: {_slots.Length} строк (из {mesh?.blendShapeCount ?? 0} блендшейпов)");
        }

        // имя → индекс; пробует точное имя и пару _L/_R
        static int ResolveShape(Mesh mesh, string name)
        {
            var i = mesh.GetBlendShapeIndex(name);
            if (i >= 0) return i;
            return -1;
        }

        static IEnumerable<int> ResolveShapeAll(Mesh mesh, string name)
        {
            var i = mesh.GetBlendShapeIndex(name);
            if (i >= 0) { yield return i; yield break; }
            // зеркальная пара: JawBackBottomWide → JawBackBottomWide_L + _R
            var l = mesh.GetBlendShapeIndex(name + "_L");
            var r = mesh.GetBlendShapeIndex(name + "_R");
            if (l >= 0) yield return l;
            if (r >= 0) yield return r;
        }

        // ══════════════ Apply: параметры → аватар ══════════════
        public void Apply(AvatarParams p) => ApplyFromParameters(p);

        public void ApplyFromParameters(AvatarParams p)
        {
            if (p == null) return;
            _lastParams = p;
            if (_av == null || _busy) return;
            var smr = _av.rootSkinnedMeshRenderer;
            if (smr == null || smr.sharedMesh == null) return;
            var mesh = smr.sharedMesh;

            // 1) биндинг-строки (кривые, зеркала, лестницы)
            if (_slots != null)
            {
                foreach (var s in _slots)
                {
                    float v = AvatarCatalog.GetFloat(p, s.paramId);
                    float w = EvalResponse(s.paramId, v);
                    if (s.ladderIndices != null && s.ladderIndices.Length > 0)
                    {
                        int n = s.ladderIndices.Length;
                        float pos = Mathf.Clamp01(w / 100f) * (n - 1);
                        int i0 = Mathf.Clamp(Mathf.FloorToInt(pos), 0, n - 1);
                        int i1 = Mathf.Clamp(i0 + 1, 0, n - 1);
                        float frac = pos - i0;
                        for (int k = 0; k < n; k++)
                            smr.SetBlendShapeWeight(s.ladderIndices[k], 0f);
                        smr.SetBlendShapeWeight(s.ladderIndices[i0], (1f - frac) * 100f);
                        smr.SetBlendShapeWeight(s.ladderIndices[i1], frac * 100f);
                    }
                    else if (s.shapeIndices != null)
                    {
                        for (int k = 0; k < s.shapeIndices.Length; k++)
                        {
                            float w2 = w;
                            smr.SetBlendShapeWeight(s.shapeIndices[k], Mathf.Clamp(w2, 0f, 100f));
                        }
                    }
                }
            }

            // 2) фолбэк кейвордами по ещё не связанным формам
            ApplyKeywordFallback(smr, mesh, p);

            // 3) кожа / подтон / гладкость / веснушки / глаза / губы
            var mm = _av.AvatarMaterialsManager;
            if (mm != null)
            {
                var skin = TryHex(p.skin) ? HexColor(p.skin) : mm.skinColorCorrector;
                float u = p.undertone - 0.5f;
                if (Mathf.Abs(u) > 0.02f)
                    skin = Color.Lerp(skin, new Color(skin.r * (1f + u * 0.25f), skin.g, skin.b * (1f - u * 0.25f), skin.a), 0.6f);
                mm.skinColorCorrector = skin;
                if (TryHex(p.lipTint)) mm.lipsColor = HexColor(p.lipTint);
                mm.smoothness = Mathf.Clamp01(p.smoothness);
                mm.frecklesIntensity = Mathf.Clamp01(p.freckles);
                mm.frecklesScale = Mathf.Clamp01(1f - p.freckleScale) * 20f;
                mm.UpdateMaterialProperties();
            }
            if (TryHex(p.eyeColor))
            {
                var c = HexColor(p.eyeColor);
                foreach (var r in _av.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var n = r.gameObject.name.ToLowerInvariant();
                    if (!n.Contains("eye") && !n.Contains("iris")) continue;
                    var b = new MaterialPropertyBlock();
                    r.GetPropertyBlock(b);
                    b.SetColor("_BaseColor", c); b.SetColor("_Color", c);
                    r.SetPropertyBlock(b);
                }
            }

            // 4) волосы / одежда из каталога
            _hairTint = TryHex(p.hairColor) ? HexColor(p.hairColor) : Color.white;
            _hairGloss = Mathf.Clamp01(p.hairShine);
            var hairStyleId = p.hairStyle.ToString();
            if (hairStyleId != _hairStyle)
            {
                _hairStyle = hairStyleId;
                if (_hair != null) Destroy(_hair);
                _hair = null;
                var path = MacContentCatalog.Hair(hairStyleId, p.sex == Sex.Male);
                if (path != null)
                {
                    var pf = Resources.Load<GameObject>(path);
                    if (pf != null) _hair = Equip(pf);
                }
            }
            if (_hair != null) ApplyLook(_hair, _hairTint, _hairGloss);

            bool male = p.sex == Sex.Male;
            _upperTint = TryHex(p.upperColor) ? HexColor(p.upperColor) : Color.white;
            _lowerTint = TryHex(p.lowerColor) ? HexColor(p.lowerColor) : Color.white;
            var upperId = p.upper.ToString();
            if (upperId != _upperId)
            {
                _upperId = upperId;
                if (_upper != null) Destroy(_upper);
                _upper = null;
                var path = MacContentCatalog.Upper(upperId, male);
                if (path != null)
                {
                    var pf = Resources.Load<GameObject>(path);
                    if (pf != null) _upper = Equip(pf);
                }
            }
            var lowerId = p.lower.ToString();
            if (lowerId != _lowerId)
            {
                _lowerId = lowerId;
                if (_lower != null) Destroy(_lower);
                _lower = null;
                var path = MacContentCatalog.Lower(lowerId, male);
                if (path != null)
                {
                    var pf = Resources.Load<GameObject>(path);
                    if (pf != null) _lower = Equip(pf);
                }
            }
            if (_upper != null) ApplyLook(_upper, _upperTint);
            if (_lower != null) ApplyLook(_lower, _lowerTint);
        }

        void ApplyKeywordFallback(SkinnedMeshRenderer smr, Mesh mesh, AvatarParams p)
        {
            var covered = new HashSet<int>();
            if (_slots != null)
                foreach (var s in _slots)
                {
                    if (s.shapeIndices != null) foreach (var i in s.shapeIndices) covered.Add(i);
                    if (s.ladderIndices != null) foreach (var i in s.ladderIndices) covered.Add(i);
                }
            var taken = new HashSet<string>();
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                if (covered.Contains(i)) continue;
                var n = mesh.GetBlendShapeName(i);
                if (IsExpression(n)) continue;
                var low = n.ToLowerInvariant();
                foreach (var (param, keys) in Keys)
                {
                    if (taken.Contains(param)) break;
                    bool hit = false;
                    foreach (var k in keys)
                        if (low.Contains(k)) { hit = true; break; }
                    if (!hit) continue;
                    // зеркальная пара в фолбэке тоже едет вместе
                    var mk = MirrorBase(n);
                    if (mk != n && taken.Contains("@" + mk)) break;
                    taken.Add(param); taken.Add("@" + mk);
                    smr.SetBlendShapeWeight(i, Mathf.Clamp01(AvatarCatalog.GetFloat(p, param)) * 100f);
                    break;
                }
            }
        }

        static string MirrorBase(string n) => n.Replace("_L", "").Replace("_R", "");

        float EvalResponse(string paramId, float v)
        {
            EnsureBinding();
            if (_binding != null && _binding.rows != null)
                foreach (var r in _binding.rows)
                    if (r != null && r.paramId == paramId && r.response != null && r.response.length > 0)
                        return r.response.Evaluate(Mathf.Clamp01(v));
            return Mathf.Clamp01(v) * 100f;
        }

        // ══════════════ Capture: аватар → параметры ══════════════
        public AvatarParams Capture()
        {
            var p = _lastParams != null ? _lastParams.Clone() : AvatarDefaults.Create(Sex.Female);
            var smr = _av?.rootSkinnedMeshRenderer;
            if (smr == null || smr.sharedMesh == null || _slots == null) return p;
            EnsureBinding();
            foreach (var s in _slots)
            {
                float w;
                if (s.ladderIndices != null && s.ladderIndices.Length > 0)
                {
                    // центр масс по лестнице
                    float sum = 0f, ws = 0f;
                    for (int k = 0; k < s.ladderIndices.Length; k++)
                    {
                        float v = smr.GetBlendShapeWeight(s.ladderIndices[k]);
                        sum += v * k; ws += v;
                    }
                    w = ws > 0.01f ? (sum / ws) / (s.ladderIndices.Length - 1) * 100f : 0f;
                }
                else if (s.shapeIndices != null && s.shapeIndices.Length > 0)
                {
                    float sum = 0f;
                    for (int k = 0; k < s.shapeIndices.Length; k++) sum += smr.GetBlendShapeWeight(s.shapeIndices[k]);
                    w = sum / s.shapeIndices.Length;
                }
                else continue;
                float param = InverseResponse(s.paramId, w);
                AvatarCatalog.SetFloat(p, s.paramId, Mathf.Clamp01(param));
            }
            return p;
        }

        float InverseResponse(string paramId, float weight)
        {
            EnsureBinding();
            if (_binding != null && _binding.rows != null)
                foreach (var r in _binding.rows)
                    if (r != null && r.paramId == paramId && r.response != null && r.response.length > 1)
                    {
                        // обратная кривая: value → time
                        var keys = r.response.keys;
                        if (keys[0].value >= keys[^1].value) return weight / 100f;
                        for (int i = 1; i < keys.Length; i++)
                        {
                            if (weight <= keys[i].value)
                            {
                                float t0 = keys[i - 1].value, t1 = keys[i].value;
                                float f = t1 > t0 ? Mathf.InverseLerp(t0, t1, weight) : 0f;
                                return Mathf.Lerp(keys[i - 1].time, keys[i].time, f);
                            }
                        }
                        return 1f;
                    }
            return weight / 100f;
        }

        // ══════════════ Слепок весов (100% round-trip образов) ══════════════
        public string DumpShapes()
        {
            var smr = _av?.rootSkinnedMeshRenderer;
            if (smr == null || smr.sharedMesh == null) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
            {
                var w = smr.GetBlendShapeWeight(i);
                if (Mathf.Approximately(w, 0f)) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(i).Append(':').Append(w.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        public void ApplyDump(string dump)
        {
            var smr = _av?.rootSkinnedMeshRenderer;
            if (smr == null || smr.sharedMesh == null || string.IsNullOrEmpty(dump)) return;
            var mesh = smr.sharedMesh;
            for (int i = 0; i < mesh.blendShapeCount; i++) smr.SetBlendShapeWeight(i, 0f);
            foreach (var tok in dump.Split(';'))
            {
                var pp = tok.Split(':');
                if (pp.Length != 2) continue;
                if (!int.TryParse(pp[0], out var bi) || bi >= mesh.blendShapeCount) continue;
                if (!float.TryParse(pp[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w)) continue;
                smr.SetBlendShapeWeight(bi, w);
            }
        }

        void ClearPieces()
        {
            if (_hair != null) Destroy(_hair);
            if (_upper != null) Destroy(_upper);
            if (_lower != null) Destroy(_lower);
            _hair = _upper = _lower = null;
            _hairStyle = _upperId = _lowerId = "\0";
        }

        static bool TryHex(string hex)
        {
            return !string.IsNullOrEmpty(hex)
                && ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out _);
        }
        static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c);
            return c;
        }

        // ══════════════ Ретаргет CC-префабов на риг MAC ══════════════
        GameObject Equip(GameObject pf)
        {
            var av = _mgr.CurrentAvatar;
            var body = av.rootSkinnedMeshRenderer;
            if (body == null || body.rootBone == null) return null;
            var bonesByName = new Dictionary<string, Transform>();
            foreach (var b in body.rootBone.GetComponentsInChildren<Transform>(true))
                if (b != null && !bonesByName.ContainsKey(b.name)) bonesByName.Add(b.name, b);
            Aliases(bonesByName);

            var inst = Instantiate(pf, av.transform, false);
            var smrs = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (smrs.Length == 0) { Destroy(inst); return null; }
            foreach (var r in smrs)
                foreach (var bn in r.bones)
                    if (bn != null && !bonesByName.ContainsKey(bn.name))
                    { Destroy(inst); return null; }

            foreach (var lg in inst.GetComponentsInChildren<LODGroup>(true)) Destroy(lg);
            StripShadowMeshes(inst);
            var bindWorld = BindWorldOf(body);
            foreach (var r in smrs)
            {
                Ret(r, bonesByName, body, bindWorld);
                r.enabled = true;
                r.updateWhenOffscreen = true;
                var mats = r.materials;
                for (int m = 0; m < mats.Length; m++) if (mats[m] != null) mats[m].renderQueue = 2225;
                r.materials = mats;
            }
            inst.transform.SetParent(av.transform, true);
            return inst;
        }

        static void ApplyLook(GameObject h, Color tint, float gloss = -1f)
        {
            if (h == null) return;
            foreach (var r in h.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    if (mats[i].HasProperty("_BaseColor")) mats[i].SetColor("_BaseColor", tint);
                    if (mats[i].HasProperty("_Color")) mats[i].SetColor("_Color", tint);
                    if (gloss >= 0f)
                    {
                        if (mats[i].HasProperty("_Smoothness")) mats[i].SetFloat("_Smoothness", gloss);
                        if (mats[i].HasProperty("_Glossiness")) mats[i].SetFloat("_Glossiness", gloss);
                    }
                }
                r.materials = mats;
            }
        }

        static void StripShadowMeshes(GameObject inst)
        {
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                if (r != null && r.gameObject.name.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    r.gameObject.SetActive(false);
        }

        static Dictionary<Transform, Matrix4x4> BindWorldOf(SkinnedMeshRenderer body)
        {
            var map = new Dictionary<Transform, Matrix4x4>();
            if (body == null || body.sharedMesh == null || body.bones == null) return map;
            var bps = body.sharedMesh.bindposes;
            var bones = body.bones;
            var meshWorld = body.transform.localToWorldMatrix;
            for (int i = 0; i < bones.Length && i < bps.Length; i++)
                if (bones[i] != null && !map.ContainsKey(bones[i]))
                    map[bones[i]] = meshWorld * bps[i].inverse;
            return map;
        }

        static void Ret(SkinnedMeshRenderer r, Dictionary<string, Transform> bonesByName, SkinnedMeshRenderer body, Dictionary<Transform, Matrix4x4> bindWorld)
        {
            var src = r.bones;
            if (src == null || src.Length == 0) return;
            var bp = r.sharedMesh != null ? r.sharedMesh.bindposes : null;
            if (bp == null || bp.Length != src.Length) return;

            var dst = new Transform[src.Length];
            var nbp = new Matrix4x4[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                Transform t;
                if (src[i] != null && bonesByName.TryGetValue(src[i].name, out t))
                {
                    Matrix4x4 bindW;
                    if (!bindWorld.TryGetValue(t, out bindW)) bindW = t.localToWorldMatrix;
                    nbp[i] = bindW.inverse * src[i].localToWorldMatrix * bp[i];
                    dst[i] = t;
                }
                else { nbp[i] = bp[i]; dst[i] = src[i]; }
            }

            var mesh = Object.Instantiate(r.sharedMesh);
            mesh.bindposes = nbp;
            r.bones = dst;
            r.sharedMesh = mesh;
            if (body != null) { r.rootBone = body.rootBone; r.localBounds = body.localBounds; }
            r.updateWhenOffscreen = true;
        }

        static void A(Dictionary<string, Transform> m, string s, string t) { if (!m.ContainsKey(s) && m.TryGetValue(t, out var tr)) m.Add(s, tr); }
        static void Aliases(Dictionary<string, Transform> m)
        {
            A(m, "root", "CC_Base_BoneRoot"); A(m, "pelvis", "CC_Base_Pelvis");
            A(m, "spine_01", "CC_Base_Waist"); A(m, "spine_02", "CC_Base_Spine01"); A(m, "spine_03", "CC_Base_Spine02"); A(m, "spine_04", "CC_Base_Spine02"); A(m, "spine_05", "CC_Base_Spine02");
            A(m, "pelvis_wt", "CC_Base_Pelvis"); A(m, "spine_01_wt", "CC_Base_Waist"); A(m, "spine_02_wt", "CC_Base_Spine01"); A(m, "spine_03_wt", "CC_Base_Spine02"); A(m, "spine_04_wt", "CC_Base_Spine02"); A(m, "spine_05_wt", "CC_Base_Spine02");
            A(m, "neck_01_wt", "CC_Base_NeckTwist01"); A(m, "neck_02_wt", "CC_Base_NeckTwist02");
            A(m, "neck_01", "CC_Base_NeckTwist01"); A(m, "neck_02", "CC_Base_NeckTwist02"); A(m, "head", "CC_Base_Head");
            A(m, "Head", "CC_Base_Head"); A(m, "mixamorig:Head", "CC_Base_Head"); A(m, "Bip001 Head", "CC_Base_Head");
            A(m, "Neck", "CC_Base_NeckTwist02"); A(m, "mixamorig:Neck", "CC_Base_NeckTwist02");
            A(m, "Hips", "CC_Base_Pelvis"); A(m, "mixamorig:Hips", "CC_Base_Pelvis");
            A(m, "Spine", "CC_Base_Spine01"); A(m, "mixamorig:Spine", "CC_Base_Spine01");
            A(m, "Spine1", "CC_Base_Spine02"); A(m, "mixamorig:Spine1", "CC_Base_Spine02");
            A(m, "Spine2", "CC_Base_Spine02"); A(m, "mixamorig:Spine2", "CC_Base_Spine02");
            foreach (var (s, t) in new[]{
                ("clavicle_l","CC_Base_L_Clavicle"),("clavicle_r","CC_Base_R_Clavicle"),
                ("upperarm_l","CC_Base_L_Upperarm"),("upperarm_r","CC_Base_R_Upperarm"),
                ("upperarm_twist_01_l","CC_Base_L_UpperarmTwist01"),("upperarm_twist_01_r","CC_Base_R_UpperarmTwist01"),
                ("upperarm_twist_02_l","CC_Base_L_UpperarmTwist02"),("upperarm_twist_02_r","CC_Base_R_UpperarmTwist02"),
                ("lowerarm_l","CC_Base_L_Forearm"),("lowerarm_r","CC_Base_R_Forearm"),
                ("lowerarm_twist_01_l","CC_Base_L_ForearmTwist01"),("lowerarm_twist_01_r","CC_Base_R_ForearmTwist01"),
                ("lowerarm_twist_02_l","CC_Base_L_ForearmTwist02"),("lowerarm_twist_02_r","CC_Base_R_ForearmTwist02"),
                ("thigh_l","CC_Base_L_Thigh"),("thigh_r","CC_Base_R_Thigh"),
                ("thigh_twist_01_l","CC_Base_L_ThighTwist01"),("thigh_twist_01_r","CC_Base_R_ThighTwist01"),
                ("thigh_twist_02_l","CC_Base_L_ThighTwist02"),("thigh_twist_02_r","CC_Base_R_ThighTwist02"),
                ("calf_l","CC_Base_L_Calf"),("calf_r","CC_Base_R_Calf"),
                ("calf_twist_01_l","CC_Base_L_CalfTwist01"),("calf_twist_01_r","CC_Base_R_CalfTwist01"),
                ("calf_twist_02_l","CC_Base_L_CalfTwist02"),("calf_twist_02_r","CC_Base_R_CalfTwist02"),
                ("foot_l","CC_Base_L_Foot"),("foot_r","CC_Base_R_Foot"),
                ("hand_l","CC_Base_L_Hand"),("hand_r","CC_Base_R_Hand"),
            }) A(m, s, t);
        }
    }
}
