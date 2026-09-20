using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Wires the rigged FORMA reference avatars (converted from the source GLB files):
//  1) explicit URP materials with the extracted base maps,
//  2) material remaps on the model importers,
//  3) AnimatorController per avatar (states named after the clips, looped),
//  4) prefabs in the slot the loader expects: Resources/FORMA/<Sex>Animated.
// Idempotent. Editor only.
public static class MAC_Forma_Setup
{
    const string Root = "Assets/Resources/FORMA/Rigged";
    const string MatRoot = "Assets/Resources/FORMA/RefMaterials";
    const string OutRoot = "Assets/Resources/FORMA";

    static readonly string[] Sexes = { "Male", "Female" };

    // sex, source material name in the FBX, part role, base color texture file, transparent
    static readonly string[,] Table = new string[,]
    {
        { "Male",   "Modelbody_Material004",         "Body",       "MaleReference_Image_2.png",   "0" },
        { "Male",   "Modelcloth_Material012",        "Cloth",      "MaleReference_Image_8.png",   "0" },
        { "Male",   "Modelelashes_Material010",      "Eyelashes",  "MaleReference_Image_7.png",   "1" },
        { "Male",   "Modeleyes_Material005",         "Eyes",       "MaleReference_Image_3.png",   "0" },
        { "Male",   "Modelface_Material006",         "Face",       "MaleReference_Image_4.png",   "0" },
        { "Male",   "Modelhair_Material001",         "Hair",       "MaleReference_Image_0.png",   "1" },
        { "Male",   "Modelhands_Material007",        "Hands",      "MaleReference_Image_5.png",   "0" },
        { "Male",   "Modelled_Material003",          "Led",        "MaleReference_Image_1.png",   "1" },
        { "Male",   "Modelteeth_Material008",        "Teeth",      "MaleReference_Image_6.png",   "0" },
        { "Female", "_4_body_1_0_0",                 "Body",       "FemaleReference_Image_2.png", "0" },
        { "Female", "_4_face_0_3_1_1",               "Face",       "FemaleReference_Image_6.png", "0" },
        { "Female", "_4_eyes_5_1_1",                 "Eyes",       "FemaleReference_Image_4.png", "0" },
        { "Female", "_4_+bracelet_bracelet_1_0_0",   "Bracelet",   "FemaleReference_Image_0.png", "0" },
        { "Female", "_6_+earrings_earrings_0_15_1_1","Earrings",   "FemaleReference_Image_22.png","0" },
        { "Female", "_5_alpha10_1_1_1",              "Hair",       "FemaleReference_Image_8.png", "1" },
        { "Female", "_5_alpha11_1_1_1",              "Hair",       "FemaleReference_Image_10.png","1" },
        { "Female", "_5_alpha13_1_1_1",              "Hair",       "FemaleReference_Image_12.png","1" },
        { "Female", "_5_alpha2_0_5_1_1",             "Hair",       "FemaleReference_Image_14.png","1" },
        { "Female", "_5_alpha4_0_5_1_1",             "Hair",       "FemaleReference_Image_16.png","1" },
        { "Female", "_5_alpha6_0_5_1_1",             "Hair",       "FemaleReference_Image_18.png","1" },
        { "Female", "_5_alpha8_0_5_1_1",             "Hair",       "FemaleReference_Image_20.png","1" },
    };

    [MenuItem("Tools/FORMA/Setup Reference Avatars")]
    public static void SetupFromMenu() { Debug.Log(Build()); }

    public static string Build()
    {
        var sb = new StringBuilder(4096);
        EnsureFolder(MatRoot);
        EnsureFolder(OutRoot);

        foreach (var sex in Sexes)
        {
            string fbx = Root + "/" + sex + "Reference.fbx";
            string texDir = Root + "/" + sex + "_Textures";
            string matDir = MatRoot + "/" + sex;
            EnsureFolder(matDir);

            // which roles repeat inside this sex (hair on the female side)
            var roleTotal = new Dictionary<string, int>();
            for (int i = 0; i < Table.GetLength(0); i++)
            {
                if (Table[i, 0] != sex) continue;
                string role = Table[i, 2];
                if (!roleTotal.ContainsKey(role)) roleTotal[role] = 0;
                roleTotal[role]++;
            }

            var used = new Dictionary<string, int>();
            var remaps = new List<KeyValuePair<string, Material>>();
            var expectedNames = new HashSet<string>();

            for (int i = 0; i < Table.GetLength(0); i++)
            {
                if (Table[i, 0] != sex) continue;
                string srcMat = Table[i, 1];
                string role = Table[i, 2];
                string texFile = Table[i, 3];
                bool transparent = Table[i, 4] == "1";

                used[role] = used.ContainsKey(role) ? used[role] + 1 : 1;
                string suffix = roleTotal[role] > 1 ? "_" + used[role] : "";
                string matName = "FORMA_" + sex + "_" + role + suffix;
                expectedNames.Add(matName);

                string texPath = texDir + "/" + texFile;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex == null) { sb.Append("  MISSING TEXTURE ").Append(texPath).Append('\n'); continue; }

                var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
                if (ti != null && (!ti.sRGBTexture || !ti.alphaIsTransparency || ti.wrapMode != TextureWrapMode.Clamp))
                {
                    ti.sRGBTexture = true;
                    ti.alphaIsTransparency = true;
                    ti.wrapMode = TextureWrapMode.Clamp;
                    ti.mipmapEnabled = true;
                    ti.maxTextureSize = 2048;
                    ti.SaveAndReimport();
                }

                string matPath = matDir + "/" + matName + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                mat.name = matName;
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Smoothness", role == "Eyes" ? 0.85f : 0.35f);
                if (transparent)
                {
                    mat.SetFloat("_Surface", 1f);
                    mat.SetFloat("_Blend", 0f);
                    mat.SetFloat("_AlphaClip", 0f);
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
                else
                {
                    mat.SetFloat("_Surface", 0f);
                    mat.SetFloat("_AlphaClip", 0f);
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetInt("_ZWrite", 1);
                    mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.SetOverrideTag("RenderType", "Opaque");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                }
                EditorUtility.SetDirty(mat);
                remaps.Add(new KeyValuePair<string, Material>(srcMat, mat));
            }

            // drop stale materials from earlier runs
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { matDir }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var nm = System.IO.Path.GetFileNameWithoutExtension(p);
                if (!expectedNames.Contains(nm)) { AssetDatabase.DeleteAsset(p); sb.Append("  removed stale ").Append(nm).Append('\n'); }
            }

            // loops + controller
            var clips = new List<AnimationClip>();
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbx))
            {
                var c = a as AnimationClip;
                if (c != null && !c.name.StartsWith("__preview__")) clips.Add(c);
            }
            foreach (var clip in clips)
            {
                var so = new SerializedObject(clip);
                var loop = so.FindProperty("m_LoopTime");
                if (loop != null && !loop.boolValue) { loop.boolValue = true; so.ApplyModifiedPropertiesWithoutUndo(); }
            }

            string ctrlPath = OutRoot + "/" + sex + "Animator.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath) != null) AssetDatabase.DeleteAsset(ctrlPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            var sm = ctrl.layers[0].stateMachine;
            AnimatorState first = null;
            foreach (var clip in clips)
            {
                var st = sm.AddState(clip.name);
                st.motion = clip;
                if (first == null) first = st;
            }
            if (first != null) sm.defaultState = first;
            EditorUtility.SetDirty(ctrl);
            sb.Append(sex).Append(": controller with ").Append(clips.Count).Append(" clip(s)\n");

            // material remaps
            var mi = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (mi != null)
            {
                mi.animationType = ModelImporterAnimationType.Generic;
                mi.importAnimation = true;
                foreach (var r in remaps)
                    mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), r.Key), r.Value);
                mi.SaveAndReimport();
                sb.Append("  remaps: ").Append(remaps.Count).Append('\n');
            }
            else sb.Append("  NO MODEL IMPORTER ").Append(fbx).Append('\n');

            // prefab in the slot the loader expects
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            string outPath = OutRoot + "/" + sex + "Animated.prefab";
            if (model != null && ctrl != null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                var anim = inst.GetComponent<Animator>();
                if (anim == null) anim = inst.AddComponent<Animator>();
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                PrefabUtility.SaveAsPrefabAsset(inst, outPath);
                Object.DestroyImmediate(inst);
                sb.Append("  prefab: ").Append(outPath).Append('\n');
            }
            else sb.Append("  PREFAB SKIPPED (model=").Append(model != null).Append(" ctrl=").Append(ctrl != null).Append(")\n");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        sb.Append("[setup] done\n");
        return sb.ToString();
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        string leaf = path.Substring(slash + 1);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
