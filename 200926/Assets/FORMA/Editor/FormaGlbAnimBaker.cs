using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Forma.Editor
{
    /// <summary>
    /// Печёт для каждого .glb из Assets/FORMA/Sources:
    ///  1) AnimatorController со всеми клипами модели (зацикленными),
    ///  2) префаб с Animator'ом → Assets/FORMA/Resources/FORMA/&lt;имя&gt;.prefab,
    /// чтобы ReferenceAvatarLoader брал ригованного аватара через Resources
    /// и играл анимации (Arm Stretching / Running / …).
    /// Запускается автоматически после импорта .glb и вручную: FORMA → Bake GLB Animations.
    /// </summary>
    public sealed class FormaGlbAnimBaker : AssetPostprocessor
    {
        const string SourcesDir = "Assets/FORMA/Sources";
        const string OutputDir = "Assets/FORMA/Resources/FORMA";

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (var p in importedAssets)
                if (p.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)
                    && p.StartsWith(SourcesDir, StringComparison.OrdinalIgnoreCase))
                {
                    BakeAll();
                    return;
                }
        }

        [MenuItem("FORMA/Bake GLB Animations")]
        public static void BakeAll()
        {
            var guids = AssetDatabase.FindAssets("t:GameObject", new[] { SourcesDir });
            int baked = 0;
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                if (!path.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)) continue;
                if (Bake(path)) baked++;
            }
            if (baked > 0) AssetDatabase.SaveAssets();
            Debug.Log($"[FORMA] Bake GLB Animations: готово {baked}");
        }

        static bool Bake(string glbPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
            if (model == null) return false;

            var name = System.IO.Path.GetFileNameWithoutExtension(glbPath);
            var clips = AssetDatabase.LoadAllAssetsAtPath(glbPath)
                .OfType<AnimationClip>()
                .Where(c => c != null)
                .ToArray();
            if (clips.Length == 0)
            {
                Debug.LogWarning($"[FORMA] {name}: в GLB нет клипов — контроллер не печётся");
                return false;
            }

            // зациклить импортированные клипы
            foreach (var clip in clips)
            {
                var so = new SerializedObject(clip);
                var prop = so.FindProperty("m_LoopTime");
                if (prop != null && !prop.boolValue)
                {
                    prop.boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // контроллер: по состоянию на клип
            var ctrlPath = $"{SourcesDir}/{name}_Controller.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            var sm = controller.layers[0].stateMachine;
            sm.states = Array.Empty<ChildAnimatorState>();
            UnityEditor.Animations.AnimatorState first = null;
            foreach (var clip in clips)
            {
                var st = sm.AddState(clip.name);
                st.motion = clip;
                if (first == null) first = st;
            }
            // без default state аниматор стоит в пустоте — поза «замерзает»
            if (first != null) sm.defaultState = first;
            EditorUtility.SetDirty(controller);

            // префаб с Animator → Resources/FORMA
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            if (inst == null) return false;
            inst.hideFlags = HideFlags.DontSave;
            var anim = inst.GetComponentInChildren<Animator>(true);
            if (anim == null) anim = inst.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;

            var outPath = $"{OutputDir}/{name}.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(inst, outPath);
            UnityEngine.Object.DestroyImmediate(inst);
            if (saved == null)
            {
                Debug.LogError($"[FORMA] не удалось сохранить префаб {outPath}");
                return false;
            }
            Debug.Log($"[FORMA] {name}: запечён контроллер ({clips.Length} клип(ов)) → {outPath}");
            return true;
        }
    }
}
