namespace Forma
{
    public static class AvatarDefaults
    {
        public static readonly string[] SkinSwatches =
        {
            "#f6e0cc","#f0d0b0","#e6c0a4","#d4a088","#c48a6a",
            "#a86b4a","#8a5238","#6b3b28","#4a281c","#2e1810"
        };

        public static readonly string[] HairSwatches =
        {
            "#1a1410","#2a1810","#3d2314","#6a4a32","#8a5a28","#c4a06a",
            "#d8b06a","#e8d2a8","#f0e2c4","#c8c4c0","#6b1c28","#1c2438"
        };

        public static readonly string[] EyeSwatches =
        {
            "#6b4a32","#3d2918","#6b8a4a","#3d5a40","#3a7ab8","#1c2838","#8a8a90","#c4a06a"
        };

        public static readonly string[] ClothSwatches =
        {
            "#141416","#f4f1ea","#8a8780","#3a2a22","#1c2430","#4a1c22","#2a3a32","#c4b8a1"
        };

        public static AvatarParams Create(Sex sex)
        {
            var p = new AvatarParams
            {
                sex = sex,
                undertone = 0.58f,
                smoothness = 0.7f,
                freckles = 0.12f,
                freckleScale = 0.42f,
                freckleColor = "#b56a45",
                blush = 0.18f,
                hairShine = 0.48f,
                full = FullGarment.None
            };

            if (sex == Sex.Female)
            {
                // Tuned against the supplied female reference.
                p.height = 0.62f; p.build = 0.44f; p.muscular = 0.28f; p.fat = 0.18f;
                p.neck = 0.36f; p.neckLength = 0.62f; p.shoulders = 0.42f;
                p.chest = 0.58f; p.chestSep = 0.46f; p.arms = 0.38f; p.forearms = 0.38f; p.hands = 0.42f;
                p.waist = 0.22f; p.belly = 0.08f; p.hips = 0.62f; p.buttocks = 0.58f;
                p.thighs = 0.52f; p.calves = 0.46f; p.legs = 0.72f; p.feet = 0.42f;
                p.headWidth = 0.43f; p.headHeight = 0.56f; p.headDepth = 0.47f; p.forehead = 0.54f;
                p.browRidge = 0.22f; p.browArch = 0.64f; p.eyeSize = 0.55f; p.eyeSpacing = 0.49f;
                p.eyeHeight = 0.55f; p.eyeDepth = 0.48f; p.eyeShape = 0.68f;
                p.noseWidth = 0.36f; p.noseLength = 0.46f; p.noseBridge = 0.42f; p.noseTip = 0.5f; p.nostrils = 0.38f;
                p.cheekbones = 0.62f; p.cheeks = 0.46f; p.jowls = 0.06f; p.earSize = 0.4f; p.earStick = 0.34f;
                p.lipWidth = 0.56f; p.lipUpper = 0.52f; p.lipLower = 0.6f;
                p.jawWidth = 0.34f; p.jawLength = 0.46f; p.chinWidth = 0.38f; p.chinHeight = 0.48f; p.chinProjection = 0.46f;
                p.skin = "#dca98c"; p.lipTint = "#b96968"; p.blush = 0.18f; p.eyeColor = "#6b8a4a";
                p.hairStyle = HairStyle.Waves; p.hairColor = "#8a5a28"; p.hairLength = 0.86f; p.hairVolume = 0.58f;
                p.facialHair = 0f; p.upper = UpperGarment.Bandeau; p.lower = LowerGarment.Briefs;
                p.upperColor = "#f4f1ea"; p.lowerColor = "#f4f1ea";
            }
            else
            {
                // Tuned against the supplied male reference.
                p.height = 0.72f; p.build = 0.68f; p.muscular = 0.92f; p.fat = 0.045f;
                p.neck = 0.68f; p.neckLength = 0.4f; p.shoulders = 0.92f;
                p.chest = 0.86f; p.chestSep = 0.5f; p.arms = 0.9f; p.forearms = 0.84f; p.hands = 0.6f;
                p.waist = 0.24f; p.belly = 0.045f; p.hips = 0.44f; p.buttocks = 0.52f;
                p.thighs = 0.84f; p.calves = 0.8f; p.legs = 0.62f; p.feet = 0.58f;
                p.headWidth = 0.52f; p.headHeight = 0.51f; p.headDepth = 0.53f; p.forehead = 0.47f;
                p.browRidge = 0.7f; p.browArch = 0.3f; p.eyeSize = 0.45f; p.eyeSpacing = 0.5f;
                p.eyeHeight = 0.49f; p.eyeDepth = 0.48f; p.eyeShape = 0.32f;
                p.noseWidth = 0.52f; p.noseLength = 0.58f; p.noseBridge = 0.62f; p.noseTip = 0.48f; p.nostrils = 0.48f;
                p.cheekbones = 0.55f; p.cheeks = 0.28f; p.jowls = 0.1f; p.earSize = 0.48f; p.earStick = 0.4f;
                p.lipWidth = 0.44f; p.lipUpper = 0.3f; p.lipLower = 0.34f;
                p.jawWidth = 0.72f; p.jawLength = 0.6f; p.chinWidth = 0.58f; p.chinHeight = 0.52f; p.chinProjection = 0.58f;
                p.skin = "#c98e70"; p.lipTint = "#9b625c"; p.blush = 0.04f; p.eyeColor = "#3a7ab8";
                p.hairStyle = HairStyle.Short; p.hairColor = "#3d2314"; p.hairLength = 0.32f; p.hairVolume = 0.5f;
                p.facialHair = 0.42f; p.smoothness = 0.46f;
                p.upper = UpperGarment.None; p.lower = LowerGarment.Briefs;
                p.upperColor = "#141416"; p.lowerColor = "#141416";
            }
            return p;
        }
    }
}
