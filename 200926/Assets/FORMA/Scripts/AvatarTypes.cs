using System;
using UnityEngine;

namespace Forma
{
    public enum Sex { Female, Male }

    public enum CategoryId { Body, Face, Skin, Hair, Cloth, Preset }

    public enum HairStyle
    {
        None, Buzz, Short, Pixie, Sidepart, Bob, Waves, Long, Ponytail, Bun, Afro, Curls
    }

    public enum UpperGarment { None, Bandeau, Tank, Tee, Crop, Shirt }
    public enum LowerGarment { None, Briefs, Shorts, Pants, Skirt, Leggings }
    public enum FullGarment { None, Bodysuit, Dress }

    public enum SliderKind { Offset, Amount }

    public static class BodyPart
    {
        public const byte Scalp = 1, Face = 2, Ear = 3, Neck = 4, Chest = 5, Belly = 6,
            Waist = 7, Hip = 8, Butt = 9, UpperArm = 10, Forearm = 11, Hand = 12,
            Thigh = 13, Calf = 14, Foot = 15, Lip = 16, Nose = 17, Lid = 18;
    }

    [Serializable]
    public class AvatarParams
    {
        public Sex sex = Sex.Female;
        [Range(0, 1)] public float height = 0.54f;
        [Range(0, 1)] public float build = 0.42f;
        [Range(0, 1)] public float muscular = 0.22f;
        [Range(0, 1)] public float fat = 0.16f;
        [Range(0, 1)] public float neck = 0.38f;
        [Range(0, 1)] public float neckLength = 0.56f;
        [Range(0, 1)] public float shoulders = 0.36f;
        [Range(0, 1)] public float chest = 0.5f;
        [Range(0, 1)] public float chestSep = 0.48f;
        [Range(0, 1)] public float arms = 0.34f;
        [Range(0, 1)] public float forearms = 0.36f;
        [Range(0, 1)] public float hands = 0.4f;
        [Range(0, 1)] public float waist = 0.28f;
        [Range(0, 1)] public float belly = 0.1f;
        [Range(0, 1)] public float hips = 0.56f;
        [Range(0, 1)] public float buttocks = 0.52f;
        [Range(0, 1)] public float thighs = 0.48f;
        [Range(0, 1)] public float calves = 0.42f;
        [Range(0, 1)] public float legs = 0.62f;
        [Range(0, 1)] public float feet = 0.4f;
        [Range(0, 1)] public float headWidth = 0.46f;
        [Range(0, 1)] public float headHeight = 0.52f;
        [Range(0, 1)] public float headDepth = 0.48f;
        [Range(0, 1)] public float forehead = 0.52f;
        [Range(0, 1)] public float browRidge = 0.26f;
        [Range(0, 1)] public float browArch = 0.6f;
        [Range(0, 1)] public float eyeSize = 0.58f;
        [Range(0, 1)] public float eyeSpacing = 0.5f;
        [Range(0, 1)] public float eyeHeight = 0.54f;
        [Range(0, 1)] public float eyeDepth = 0.5f;
        [Range(0, 1)] public float eyeShape = 0.64f;
        [Range(0, 1)] public float noseWidth = 0.4f;
        [Range(0, 1)] public float noseLength = 0.44f;
        [Range(0, 1)] public float noseBridge = 0.4f;
        [Range(0, 1)] public float noseTip = 0.48f;
        [Range(0, 1)] public float nostrils = 0.42f;
        [Range(0, 1)] public float cheekbones = 0.56f;
        [Range(0, 1)] public float cheeks = 0.48f;
        [Range(0, 1)] public float jowls = 0.12f;
        [Range(0, 1)] public float earSize = 0.44f;
        [Range(0, 1)] public float earStick = 0.38f;
        [Range(0, 1)] public float lipWidth = 0.54f;
        [Range(0, 1)] public float lipUpper = 0.5f;
        [Range(0, 1)] public float lipLower = 0.56f;
        [Range(0, 1)] public float jawWidth = 0.4f;
        [Range(0, 1)] public float jawLength = 0.44f;
        [Range(0, 1)] public float chinWidth = 0.42f;
        [Range(0, 1)] public float chinHeight = 0.46f;
        [Range(0, 1)] public float chinProjection = 0.44f;
        public string skin = "#e6c0a4";
        [Range(0, 1)] public float undertone = 0.58f;
        [Range(0, 1)] public float smoothness = 0.7f;
        [Range(0, 1)] public float freckles = 0.12f;
        [Range(0, 1)] public float freckleScale = 0.42f;
        public string freckleColor = "#b56a45";
        public string lipTint = "#c47874";
        [Range(0, 1)] public float blush = 0.18f;
        public string eyeColor = "#6b4a32";
        public HairStyle hairStyle = HairStyle.Waves;
        public string hairColor = "#c4a06a";
        [Range(0, 1)] public float hairShine = 0.48f;
        [Range(0, 1)] public float hairLength = 0.78f;
        [Range(0, 1)] public float hairVolume = 0.62f;
        [Range(0, 1)] public float facialHair;
        public UpperGarment upper = UpperGarment.Bandeau;
        public LowerGarment lower = LowerGarment.Briefs;
        public FullGarment full = FullGarment.None;
        public string upperColor = "#f4f1ea";
        public string lowerColor = "#f4f1ea";

        public AvatarParams Clone()
        {
            return (AvatarParams)MemberwiseClone();
        }
    }

    [Serializable]
    public class SliderDef
    {
        public string id;
        public string label;
        public SliderKind kind;
        public SliderDef(string id, string label, SliderKind kind)
        {
            this.id = id; this.label = label; this.kind = kind;
        }
    }

    [Serializable]
    public class SectionDef
    {
        public string id;
        public string label;
        public SliderDef[] sliders;
        public string picker;
        public SectionDef(string id, string label, SliderDef[] sliders = null, string picker = null)
        {
            this.id = id; this.label = label; this.sliders = sliders; this.picker = picker;
        }
    }

    [Serializable]
    public class CategoryDef
    {
        public CategoryId id;
        public string label;
        public string hint;
        public SectionDef[] sections;
    }

    [Serializable]
    public class SavedLook
    {
        public string id;
        public string name;
        public AvatarParams parameters;
        // Полный слепок весов блендшейпов MAC-аватара (100% round-trip образа):
        // при загрузке применяется поверх parameters, затем Capture() возвращает
        // слайдерам фактическое состояние.
        public string macShapeDump;
    }
}
