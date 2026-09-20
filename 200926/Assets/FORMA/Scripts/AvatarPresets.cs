namespace Forma
{
    public class AvatarPreset
    {
        public string id;
        public string name;
        public AvatarParams parameters;
    }

    public static class AvatarPresets
    {
        public static AvatarPreset[] All()
        {
            return new[]
            {
                new AvatarPreset { id = "female-ref", name = "Женский — референс", parameters = AvatarDefaults.Create(Sex.Female) },
                new AvatarPreset { id = "male-ref", name = "Мужской — референс", parameters = AvatarDefaults.Create(Sex.Male) },
                Patch("female-athletic", "Женский — атлет", Sex.Female, p =>
                {
                    p.muscular = 0.58f; p.fat = 0.1f; p.shoulders = 0.5f; p.chest = 0.44f;
                    p.waist = 0.3f; p.belly = 0.08f; p.hips = 0.5f; p.buttocks = 0.56f; p.thighs = 0.58f;
                    p.arms = 0.5f; p.hairStyle = HairStyle.Ponytail; p.upper = UpperGarment.Crop;
                    p.lower = LowerGarment.Shorts; p.upperColor = "#1c1c1f"; p.lowerColor = "#1c1c1f";
                }),
                Patch("male-athletic", "Мужской — атлет", Sex.Male, p =>
                {
                    p.muscular = 0.78f; p.fat = 0.08f; p.shoulders = 0.78f; p.chest = 0.62f;
                    p.waist = 0.38f; p.belly = 0.08f; p.arms = 0.74f; p.thighs = 0.64f;
                    p.hairStyle = HairStyle.Buzz; p.facialHair = 0.2f;
                    p.upper = UpperGarment.Tank; p.lower = LowerGarment.Shorts;
                }),
                Patch("female-soft", "Женский — мягкий", Sex.Female, p =>
                {
                    p.muscular = 0.1f; p.fat = 0.42f; p.chest = 0.62f; p.waist = 0.46f;
                    p.belly = 0.32f; p.hips = 0.68f; p.buttocks = 0.64f; p.thighs = 0.6f;
                    p.hairStyle = HairStyle.Long;
                }),
                Patch("male-lean", "Мужской — стройный", Sex.Male, p =>
                {
                    p.muscular = 0.42f; p.fat = 0.1f; p.shoulders = 0.58f; p.chest = 0.42f;
                    p.waist = 0.34f; p.arms = 0.48f; p.thighs = 0.46f; p.facialHair = 0.12f;
                    p.hairStyle = HairStyle.Sidepart;
                }),
                Patch("female-tall", "Женский — высокий", Sex.Female, p =>
                {
                    p.height = 0.78f; p.legs = 0.72f; p.neckLength = 0.62f; p.hairStyle = HairStyle.Waves;
                }),
                Patch("male-compact", "Мужской — компакт", Sex.Male, p =>
                {
                    p.height = 0.38f; p.build = 0.7f; p.muscular = 0.72f; p.legs = 0.36f;
                }),
            };
        }

        static AvatarPreset Patch(string id, string name, Sex sex, System.Action<AvatarParams> fn)
        {
            var p = AvatarDefaults.Create(sex);
            fn(p);
            p.sex = sex;
            return new AvatarPreset { id = id, name = name, parameters = p };
        }
    }
}
