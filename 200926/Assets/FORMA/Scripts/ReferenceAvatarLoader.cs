using System;
using UnityEngine;

namespace Forma
{
    /// <summary>
    /// Грузит референс-аватаров. Два режима:
    ///  1) ANIMATED — ригованные GLB-персонажи (RigModels) из Resources/FORMA/FemaleAnimated|MaleAnimated
    ///     (префабы-варианты печёт FormaGlbAnimBaker при импорте .glb). Полный риг + анимации.
    ///  2) OBJ — статичные OBJ-референсы v10 (фолбэк, если GLB-префабов нет).
    /// Фемин/маскулин переключается SelectFemale()/SelectMale() из панели FORMA.
    /// </summary>
    public class ReferenceAvatarLoader : MonoBehaviour
    {
        public string FemaleFile = "FemaleReference/FemaleReference";
        public string MaleFile = "MaleReference/MaleReference";
        public Vector3 LocalPosition = Vector3.zero;
        public Vector3 LocalEuler = Vector3.zero;
        public float UniformScale = 0.01f;
        public bool LoadFemaleOnStart = true;

        [Tooltip("Предпочесть ригованные GLB-аватары (FemaleAnimated/MaleAnimated) статичным OBJ-референсам")]
        public bool preferAnimatedPrefabs = true;
        [Tooltip("Запускать первую анимацию сразу после загрузки GLB-аватара")]
        public bool playDefaultAnimation = true;
        [Tooltip("Подгонять рост GLB-аватара к этой высоте (метры)")]
        public float animatedTargetHeight = 1.72f;

        GameObject _instance;
        int _selection;
        string _error;
        bool _animatedMode;
        Animator _animator;

        /// <summary>Активен ли ригованный GLB-аватар (иначе — статичный OBJ)</summary>
        public bool IsAnimated => _animatedMode;
        public Animator Animator => _animator;
        public GameObject Current => _instance;
        public int Selection => _selection;

        void OnEnable() { DisableProceduralPlaceholder(); }
        void Start()
        {
            DisableProceduralPlaceholder();
            // MAC-режим — основной: эталонного GLB/OBJ не грузим, пока его не выберут тумблером
            var fs = GetComponentInParent<FormaStudio>();
            if (fs != null && fs.useMacAvatars) return;
            SelectAvatar(LoadFemaleOnStart ? 0 : 1);
        }
        public void SelectFemale() { SelectAvatar(0); }
        public void SelectMale() { SelectAvatar(1); }

        public void SelectAvatar(int index)
        {
            DisableProceduralPlaceholder();
            _selection = Mathf.Clamp(index, 0, 1);
            _error = null;
            _animator = null;
            _animatedMode = false;
            if (_instance != null) Destroy(_instance);

            GameObject prefab = null;
            if (preferAnimatedPrefabs)
            {
                prefab = Resources.Load<GameObject>(_selection == 0 ? "FORMA/FemaleAnimated" : "FORMA/MaleAnimated");
                if (prefab != null) _animatedMode = true;
                else Debug.LogWarning("[FORMA] GLB-префаб не найден (нужен glTFfast + FORMA → Bake GLB Animations) — фолбэк на OBJ-референс");
            }
            if (prefab == null)
                prefab = Resources.Load<GameObject>("FORMA/" + (_selection == 0 ? FemaleFile : MaleFile));
            if (prefab == null)
            {
                _error = "Avatar model missing: Assets/Resources/FORMA/" +
                    (_selection == 0 ? FemaleFile : MaleFile);
                Debug.LogError("[FORMA] " + _error);
                return;
            }

            _instance = Instantiate(prefab, transform);
            _instance.name = _selection == 0 ? "FORMA Female Reference" : "FORMA Male Reference";
            _instance.transform.localPosition = LocalPosition;
            _instance.transform.localEulerAngles = LocalEuler;
            _instance.transform.localScale = Vector3.one * (_animatedMode ? 1f : UniformScale);

            if (_animatedMode)
            {
                _animator = _instance.GetComponentInChildren<Animator>(true);
                if (_animator != null)
                {
                    _animator.applyRootMotion = false;
                    _animator.updateMode = AnimatorUpdateMode.Normal;
                    _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
                NormalizeHeight(_instance);
                if (_animator != null && playDefaultAnimation) PlayAnimation(0);
            }
            else
            {
                var renderers = _instance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                    ApplyEmbeddedTextures(renderers, _selection == 0);
            }

            AlignFeetToPodium(_instance);

            if (_instance.GetComponent<ReferenceAvatarCustomizer>() == null)
                _instance.AddComponent<ReferenceAvatarCustomizer>();
        }

        // ── Анимации (клипы приходят из GLB через запечённый AnimatorController) ──
        public int AnimationCount
        {
            get
            {
                var c = _animator != null && _animator.runtimeAnimatorController != null
                    ? _animator.runtimeAnimatorController.animationClips : null;
                return c != null ? c.Length : 0;
            }
        }

        public string AnimationName(int index)
        {
            var c = _animator != null && _animator.runtimeAnimatorController != null
                ? _animator.runtimeAnimatorController.animationClips : null;
            if (c == null || index < 0 || index >= c.Length) return null;
            return c[index].name;
        }

        public void PlayAnimation(int index)
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return;
            var clips = _animator.runtimeAnimatorController.animationClips;
            if (index < 0 || index >= clips.Length) return;
            _animator.enabled = true;
            _animator.CrossFade(clips[index].name, 0.25f);
        }

        public void StopAnimation()
        {
            if (_animator != null) _animator.enabled = false;
        }

        /// <summary>Вписать GLB-аватар в целевой рост: ригы RigModels бывают в сантиметрах.</summary>
        void NormalizeHeight(GameObject avatar)
        {
            var renderers = avatar.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            float h = bounds.size.y;
            if (h <= 0.01f || Mathf.Abs(h - animatedTargetHeight) < 0.02f) return;
            float s = animatedTargetHeight / h;
            var t = avatar.transform;
            t.localScale = Vector3.Scale(t.localScale, new Vector3(s, s, s));
        }

        static void AlignFeetToPodium(GameObject avatar)
        {
            var renderers = avatar.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            var pos = avatar.transform.localPosition;
            pos.y += -bounds.min.y + 0.015f;
            avatar.transform.localPosition = pos;
        }

        static void ApplyEmbeddedTextures(Renderer[] renderers, bool female)
        {
            string[] slots = female
                ? new[] { "tex_00", "tex_02", "tex_04", "tex_06", "tex_08", "tex_10", "tex_12", "tex_14", "tex_16", "tex_18", "tex_20", "tex_22" }
                : new[] { "tex_00", "tex_01", "tex_02", "tex_03", "tex_04", "tex_05", "tex_06", "tex_07", "tex_08" };
            // URP рендерит Built-in материалы розевым — OBJ-импорт отдаёт Standard,
            // поэтому при активном SRP подменяем шейдер на URP/Lit до назначения текстур.
            var urpLit = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
                ? Shader.Find("Universal Render Pipeline/Lit") : null;
            foreach (var r in renderers)
            {
                var mats = r.materials;
                for (int i = 0; i < mats.Length && i < slots.Length; i++)
                {
                    if (mats[i] == null) continue;
                    if (urpLit != null && mats[i].shader != urpLit) mats[i].shader = urpLit;
                    var tex = Resources.Load<Texture2D>((female ? "FORMA/FemaleReference/" : "FORMA/MaleReference/") + slots[i]);
                    if (tex == null) continue;
                    if (mats[i].HasProperty("_MainTex")) mats[i].SetTexture("_MainTex", tex);
                    if (mats[i].HasProperty("_BaseMap")) mats[i].SetTexture("_BaseMap", tex);
                }
                r.materials = mats;
            }
        }

        /// <summary>Спрятать текущего аватара (при переключении источника на MAC).</summary>
        public void HideCurrent()
        {
            if (_instance != null) _instance.SetActive(false);
        }

        void DisableProceduralPlaceholder()
        {
            var generator = GetComponent<AvatarGenerator>();
            if (generator != null) generator.enabled = false;
            var oldRoot = transform.Find("AvatarRoot");
            if (oldRoot != null) oldRoot.gameObject.SetActive(false);
        }

        void OnGUI()
        {
            if (_error == null) return;
            GUI.color = new Color(1f, .78f, .72f);
            GUI.Box(new Rect(Screen.width * .5f - 260f, 18f, 520f, 48f), _error);
            GUI.color = Color.white;
        }
    }
}
