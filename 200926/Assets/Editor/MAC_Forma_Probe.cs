using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// Diagnostic helper for the FORMA reference avatars (editor only, not shipped).
public static class MAC_Forma_Probe
{
    public static string Report()
    {
        var sb = new StringBuilder(4096);
        string[] sexes = { "Male", "Female" };
        foreach (var sex in sexes)
        {
            string path = "Assets/Resources/FORMA/Rigged/" + sex + "Reference.fbx";
            sb.Append("[probe] ").Append(path).Append('\n');
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { sb.Append("  PREFAB NOT FOUND\n"); continue; }

            int clips = 0;
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                var clip = a as AnimationClip;
                if (clip != null) { clips++; sb.Append("  clip: ").Append(clip.name).Append(" len=").Append(clip.length.ToString("0.00")).Append('\n'); }
            }

            var smrs = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            sb.Append("  skinned renderers: ").Append(smrs.Length).Append('\n');
            foreach (var r in smrs)
            {
                var mats = r.sharedMaterials;
                string mname = "-", mtex = "-";
                if (mats.Length > 0 && mats[0] != null)
                {
                    mname = mats[0].name;
                    var t = mats[0].HasProperty("_BaseMap") ? mats[0].GetTexture("_BaseMap") : null;
                    mtex = t != null ? t.name : "NONE";
                }
                sb.Append("    ").Append(r.name).Append(" bones=").Append(r.bones != null ? r.bones.Length : 0)
                  .Append(" shapes=").Append(r.sharedMesh != null ? r.sharedMesh.blendShapeCount : 0)
                  .Append(" mat=").Append(mname).Append(" tex=").Append(mtex).Append('\n');
            }
            sb.Append("  clips=").Append(clips).Append('\n');

            string ap = "Assets/Resources/FORMA/" + sex + "Animated.prefab";
            var animPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ap);
            if (animPrefab != null)
            {
                var an = animPrefab.GetComponent<Animator>();
                sb.Append("  animated prefab: ").Append(ap).Append(" animator=").Append(an != null)
                  .Append(" ctrl=").Append(an != null && an.runtimeAnimatorController != null ? an.runtimeAnimatorController.name : "none").Append('\n');
                var m0 = animPrefab.GetComponent<LODGroup>();
                sb.Append("  prefab lodGroup=").Append(m0 != null).Append('\n');
            }
            else sb.Append("  ANIMATED PREFAB MISSING\n");
        }
        return sb.ToString();
    }

    static SkinnedMeshRenderer[] Parts(GameObject inst)
    {
        return inst == null ? new SkinnedMeshRenderer[0] : inst.GetComponentsInChildren<SkinnedMeshRenderer>(true);
    }

    static float BoneY(GameObject inst, string namePart)
    {
        var parts = Parts(inst);
        foreach (var r in parts)
        {
            if (r.bones == null) continue;
            foreach (var b in r.bones)
            {
                if (b == null) continue;
                if (b.name.ToLowerInvariant().Contains(namePart)) return b.position.y;
            }
        }
        return -999f;
    }

    static Vector3 BoneScale(GameObject inst, string namePart)
    {
        var parts = Parts(inst);
        foreach (var r in parts)
        {
            if (r.bones == null) continue;
            foreach (var b in r.bones)
            {
                if (b == null) continue;
                if (b.name.ToLowerInvariant().Contains(namePart)) return b.localScale;
            }
        }
        return Vector3.zero;
    }

    static int PartsOn(GameObject inst)
    {
        int n = 0;
        foreach (var r in Parts(inst)) if (r.enabled) n++;
        return n;
    }

    static string PartColor(GameObject inst, string namePart)
    {
        foreach (var r in Parts(inst))
        {
            if (!r.name.ToLowerInvariant().Contains(namePart)) continue;
            var m = r.materials.Length > 0 ? r.materials[0] : null;
            if (m == null) continue;
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor").ToString("F2");
        }
        return "n/a";
    }

    public static string RuntimeCheck()
    {
        var sb = new StringBuilder(2048);
        var loader = Object.FindFirstObjectByType<Forma.ReferenceAvatarLoader>();
        var cust = Object.FindFirstObjectByType<Forma.ReferenceAvatarCustomizer>();
        if (loader == null) return "no ReferenceAvatarLoader in scene\n";
        var inst = loader.Current;
        sb.Append("loader: animated=").Append(loader.IsAnimated).Append(" selection=").Append(loader.Selection)
          .Append(" hasAnimator=").Append(loader.Animator != null).Append(" clips=").Append(loader.AnimationCount).Append('\n');
        if (loader.AnimationCount > 0) sb.Append("  clip0=").Append(loader.AnimationName(0)).Append('\n');
        if (inst == null) { sb.Append("instance: NULL\n"); return sb.ToString(); }

        sb.Append("instance: ").Append(inst.name).Append(" scale=").Append(inst.transform.localScale.ToString("0.000"))
          .Append(" pos=").Append(inst.transform.localPosition.ToString("0.000")).Append('\n');
        sb.Append("parts: ").Append(Parts(inst).Length).Append(" enabled=").Append(PartsOn(inst)).Append('\n');
        var rends = inst.GetComponentsInChildren<Renderer>(true);
        if (rends.Length > 0)
        {
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            sb.Append("world bounds: center=").Append(b.center.ToString("0.000")).Append(" size=").Append(b.size.ToString("0.000")).Append('\n');
        }
        sb.Append("head bone: y=").Append(BoneY(inst, "head").ToString("0.000")).Append(" scale=").Append(BoneScale(inst, "head").ToString("0.000")).Append('\n');
        sb.Append("hair color=").Append(PartColor(inst, "hair")).Append(" skin color=").Append(PartColor(inst, "body")).Append('\n');
        var anim = inst.GetComponent<Animator>();
        if (anim != null)
        {
            var st = anim.GetCurrentAnimatorStateInfo(0);
            sb.Append("animator: ctrl=").Append(anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "none")
              .Append(" normTime=").Append(st.normalizedTime.ToString("0.000")).Append(" speed=").Append(anim.speed.ToString("0.00")).Append('\n');
        }
        if (cust != null)
            sb.Append("customizer: head=").Append(cust.head.ToString("0.00")).Append(" shoulder=").Append(cust.shoulder.ToString("0.00"))
              .Append(" waist=").Append(cust.waist.ToString("0.00")).Append(" hairDark=").Append(cust.hairDarkness.ToString("0.00"))
              .Append(" showHair=").Append(cust.showHair).Append(" showClothes=").Append(cust.showClothes).Append('\n');
        return sb.ToString();
    }

    public static void Invoke(object target, string method)
    {
        var m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (m != null) m.Invoke(target, null);
    }

    public static string DriveTools()
    {
        var sb = new StringBuilder(1024);
        var loader = Object.FindFirstObjectByType<Forma.ReferenceAvatarLoader>();
        var cust = Object.FindFirstObjectByType<Forma.ReferenceAvatarCustomizer>();
        if (loader == null || cust == null) return "loader/customizer missing\n";
        var inst = loader.Current;
        if (inst == null) return "instance missing\n";

        float y0 = BoneY(inst, "head");
        int on0 = PartsOn(inst);
        string hair0 = PartColor(inst, "hair");
        string skin0 = PartColor(inst, "body");

        cust.head = 1.20f;
        cust.shoulder = 1.20f;
        cust.waist = 1.20f;
        cust.hairDarkness = 0.95f;
        cust.showHair = false;
        Invoke(cust, "ApplyProportions");
        Invoke(cust, "ApplyVisibility");

        sb.Append("head bone y: ").Append(y0.ToString("0.000")).Append(" -> ").Append(BoneY(inst, "head").ToString("0.000")).Append('\n');
        sb.Append("head bone scale: -> ").Append(BoneScale(inst, "head").ToString("0.000")).Append('\n');
        sb.Append("parts enabled: ").Append(on0).Append(" -> ").Append(PartsOn(inst)).Append('\n');
        sb.Append("hair color: ").Append(hair0).Append(" -> ").Append(PartColor(inst, "hair")).Append('\n');
        sb.Append("skin color: ").Append(skin0).Append(" -> ").Append(PartColor(inst, "body")).Append('\n');
        return sb.ToString();
    }

    public static string ResetTools()
    {
        var cust = Object.FindFirstObjectByType<Forma.ReferenceAvatarCustomizer>();
        if (cust == null) return "no customizer";
        cust.head = 1f; cust.shoulder = 1f; cust.waist = 1f;
        cust.width = 1f; cust.height = 1f;
        cust.hairDarkness = 0.35f;
        cust.showHair = true; cust.showClothes = true; cust.showAccessories = true;
        Invoke(cust, "ApplyProportions");
        Invoke(cust, "ApplyVisibility");
        return "tools reset";
    }

    // Goes through the FORMA panel path (AvatarParams -> customizer).
    public static string DrivePanel()
    {
        var cust = Object.FindFirstObjectByType<Forma.ReferenceAvatarCustomizer>();
        if (cust == null) return "no customizer";
        var p = new Forma.AvatarParams();
        p.height = 1f; p.build = 0.9f; p.shoulders = 0.9f; p.waist = 0.1f; p.headWidth = 0.9f;
        p.hairShine = 0.1f;
        p.skin = "#8a5a3b"; p.hairColor = "#3a2318"; p.upperColor = "#204030";
        cust.ApplyFromParameters(p);
        return "panel applied: height=" + cust.height.ToString("0.00") + " width=" + cust.width.ToString("0.00")
            + " shoulder=" + cust.shoulder.ToString("0.00") + " waist=" + cust.waist.ToString("0.00")
            + " head=" + cust.head.ToString("0.00") + " skinTint=" + cust.skinTint + " hairTint=" + cust.hairTint + " clothTint=" + cust.clothTint;
    }
}
