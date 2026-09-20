using System.Collections.Generic;

namespace Forma
{
    public static class AvatarCatalog
    {
        public static readonly CategoryDef[] Categories =
        {
            new CategoryDef
            {
                id = CategoryId.Body, label = "ТЕЛО", hint = "Пропорции и объёмы",
                sections = new[]
                {
                    Sec("proportions", "Пропорции",
                        S("height", "Рост", SliderKind.Offset),
                        S("build", "Телосложение", SliderKind.Offset),
                        S("muscular", "Мускулы", SliderKind.Amount),
                        S("fat", "Жир", SliderKind.Amount)),
                    Sec("neck", "Шея",
                        S("neck", "Толщина", SliderKind.Offset),
                        S("neckLength", "Длина", SliderKind.Offset)),
                    Sec("shoulders", "Плечи", S("shoulders", "Ширина", SliderKind.Offset)),
                    Sec("chest", "Грудь",
                        S("chest", "Объём", SliderKind.Offset),
                        S("chestSep", "Разведение", SliderKind.Offset)),
                    Sec("arms", "Руки",
                        S("arms", "Плечо", SliderKind.Offset),
                        S("forearms", "Предплечье", SliderKind.Offset),
                        S("hands", "Кисти", SliderKind.Offset)),
                    Sec("waist", "Талия",
                        S("waist", "Талия", SliderKind.Offset),
                        S("belly", "Живот", SliderKind.Amount)),
                    Sec("hips", "Бёдра",
                        S("hips", "Бёдра", SliderKind.Offset),
                        S("buttocks", "Ягодицы", SliderKind.Offset)),
                    Sec("legs", "Ноги",
                        S("thighs", "Бёдра (объём)", SliderKind.Offset),
                        S("calves", "Икры", SliderKind.Offset),
                        S("legs", "Длина ног", SliderKind.Offset),
                        S("feet", "Стопы", SliderKind.Offset)),
                }
            },
            new CategoryDef
            {
                id = CategoryId.Face, label = "ЛИЦО", hint = "Череп и черты",
                sections = new[]
                {
                    Sec("head", "Череп",
                        S("headWidth", "Ширина", SliderKind.Offset),
                        S("headHeight", "Высота", SliderKind.Offset),
                        S("headDepth", "Глубина", SliderKind.Offset),
                        S("forehead", "Лоб", SliderKind.Offset)),
                    Sec("brows", "Брови",
                        S("browRidge", "Надбровье", SliderKind.Offset),
                        S("browArch", "Изгиб", SliderKind.Offset)),
                    Sec("eyes", "Глаза",
                        S("eyeSize", "Размер", SliderKind.Offset),
                        S("eyeSpacing", "Расстояние", SliderKind.Offset),
                        S("eyeHeight", "Высота", SliderKind.Offset),
                        S("eyeDepth", "Посадка", SliderKind.Offset),
                        S("eyeShape", "Миндаль", SliderKind.Offset)),
                    Sec("nose", "Нос",
                        S("noseWidth", "Ширина", SliderKind.Offset),
                        S("noseLength", "Длина", SliderKind.Offset),
                        S("noseBridge", "Переносица", SliderKind.Offset),
                        S("noseTip", "Кончик", SliderKind.Offset),
                        S("nostrils", "Крылья", SliderKind.Offset)),
                    Sec("cheeks", "Скулы",
                        S("cheekbones", "Скулы", SliderKind.Offset),
                        S("cheeks", "Щёки", SliderKind.Offset),
                        S("jowls", "Десны / брыли", SliderKind.Amount)),
                    Sec("ears", "Уши",
                        S("earSize", "Размер", SliderKind.Offset),
                        S("earStick", "Оттопыренность", SliderKind.Offset)),
                    Sec("lips", "Губы",
                        S("lipWidth", "Ширина", SliderKind.Offset),
                        S("lipUpper", "Верхняя", SliderKind.Offset),
                        S("lipLower", "Нижняя", SliderKind.Offset)),
                    Sec("jaw", "Челюсть",
                        S("jawWidth", "Ширина челюсти", SliderKind.Offset),
                        S("jawLength", "Длина", SliderKind.Offset),
                        S("chinWidth", "Подбородок шир.", SliderKind.Offset),
                        S("chinHeight", "Подбородок выс.", SliderKind.Offset),
                        S("chinProjection", "Выступ", SliderKind.Offset)),
                }
            },
            new CategoryDef
            {
                id = CategoryId.Skin, label = "КОЖА", hint = "Тон и детали",
                sections = new[]
                {
                    Pick("tone", "Тон", "skin"),
                    Sec("details", "Детали",
                        S("undertone", "Подтон (холод–тепло)", SliderKind.Offset),
                        S("smoothness", "Гладкость", SliderKind.Amount),
                        S("freckles", "Веснушки", SliderKind.Amount),
                        S("freckleScale", "Масштаб веснушек", SliderKind.Offset),
                        S("blush", "Румянец", SliderKind.Amount)),
                    Pick("eyeskin", "Глаза и губы", "eyes"),
                }
            },
            new CategoryDef
            {
                id = CategoryId.Hair, label = "ВОЛОСЫ", hint = "Стрижка и цвет",
                sections = new[]
                {
                    Pick("styles", "Стиль", "hair"),
                    Pick("color", "Цвет", "hairColor"),
                    Sec("shape", "Форма",
                        S("hairLength", "Длина", SliderKind.Offset),
                        S("hairVolume", "Объём", SliderKind.Offset),
                        S("hairShine", "Блеск", SliderKind.Amount),
                        S("facialHair", "Борода", SliderKind.Amount)),
                }
            },
            new CategoryDef
            {
                id = CategoryId.Cloth, label = "ОДЕЖДА", hint = "Верх, низ, комплект",
                sections = new[]
                {
                    Pick("upper", "Верх", "upper"),
                    Pick("lower", "Низ", "lower"),
                    Pick("full", "Комплект", "full"),
                    Pick("colors", "Цвет", "clothColor"),
                }
            },
            new CategoryDef
            {
                id = CategoryId.Preset, label = "ОБРАЗ", hint = "Готовые и свои",
                sections = new[] { Pick("looks", "Образы", "preset") }
            }
        };

        public static readonly (HairStyle id, string label)[] HairOptions =
        {
            (HairStyle.None, "Без волос"), (HairStyle.Buzz, "Ёжик"), (HairStyle.Short, "Короткие"),
            (HairStyle.Pixie, "Пикси"), (HairStyle.Sidepart, "Пробор"), (HairStyle.Bob, "Каре"),
            (HairStyle.Waves, "Волны"), (HairStyle.Long, "Длинные"), (HairStyle.Ponytail, "Хвост"),
            (HairStyle.Bun, "Пучок"), (HairStyle.Afro, "Афро"), (HairStyle.Curls, "Локоны")
        };

        public static readonly (UpperGarment id, string label)[] UpperOptions =
        {
            (UpperGarment.None, "Нет"), (UpperGarment.Bandeau, "Халтер"), (UpperGarment.Tank, "Майка"),
            (UpperGarment.Tee, "Футболка"), (UpperGarment.Crop, "Кроп"), (UpperGarment.Shirt, "Рубашка")
        };

        public static readonly (LowerGarment id, string label)[] LowerOptions =
        {
            (LowerGarment.None, "Нет"), (LowerGarment.Briefs, "Бельё"), (LowerGarment.Shorts, "Шорты"),
            (LowerGarment.Pants, "Брюки"), (LowerGarment.Skirt, "Юбка"), (LowerGarment.Leggings, "Легинсы")
        };

        public static readonly (FullGarment id, string label)[] FullOptions =
        {
            (FullGarment.None, "Нет"), (FullGarment.Bodysuit, "Боди"), (FullGarment.Dress, "Платье")
        };

        static SliderDef S(string id, string label, SliderKind kind) => new SliderDef(id, label, kind);
        static SectionDef Sec(string id, string label, params SliderDef[] sliders) => new SectionDef(id, label, sliders);
        static SectionDef Pick(string id, string label, string picker) => new SectionDef(id, label, null, picker);

        public static float GetFloat(AvatarParams p, string id)
        {
            var f = typeof(AvatarParams).GetField(id);
            if (f == null) return 0;
            if (f.FieldType == typeof(float)) return (float)f.GetValue(p);
            return 0;
        }

        public static void SetFloat(AvatarParams p, string id, float value)
        {
            var f = typeof(AvatarParams).GetField(id);
            if (f != null && f.FieldType == typeof(float)) f.SetValue(p, value);
        }
    }
}
