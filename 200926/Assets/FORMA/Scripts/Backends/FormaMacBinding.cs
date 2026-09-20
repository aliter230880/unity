using System;
using System.Collections.Generic;
using UnityEngine;

namespace Forma
{
    /// <summary>
    /// Таблица соответствий «параметр FORMA ⇄ блендшейпы MAC» как ДАННЫЕ:
    /// одна строка = один слайдер. Зеркальные пары пишутся синхронно (имя без
    /// суффикса автоматически пробует name+_L/name+_R), кривая response задаёт
    /// нелинейность 0..1 → 0..100, templateLadder превращает мьютексные
    /// шаблонные скульпты (Ears_T1..T7) в интерполируемую ось.
    /// Ассет грузится из Resources/FORMA/FormaMacBinding; если его нет —
    /// используется CreateDefault() (программный дефолт). Калибруется окном
    /// FORMA → Calibrate MAC Binding, которое может сохранить ассет на диск.
    /// </summary>
    [CreateAssetMenu(fileName = "FormaMacBinding", menuName = "FORMA/MAC Binding")]
    public class FormaMacBinding : ScriptableObject
    {
        [Serializable]
        public class Row
        {
            [Tooltip("Id параметра FORMA (как в AvatarCatalog)")]
            public string paramId;
            [Tooltip("Имена блендшейпов MAC; пишутся все найденные. Имя без _L/_R пишет и зеркальную пару")]
            public string[] shapeNames;
            [Tooltip("Кривая отклика 0..1 → 0..100 (калибруется)")]
            public AnimationCurve response;
            [Tooltip("Шаблонная лестница: вместо одной формы — интерполяция T1..Tn (Ears_T1..T7 и т.п.)")]
            public string[] templateLadder;
        }

        public Row[] rows = Array.Empty<Row>();

        static AnimationCurve Linear() => new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 100f), new Keyframe(1f, 100f, 100f, 0f));

        /// <summary>Программный дефолт по фактическим именам блендшейпов MAC (male/female меши).</summary>
        public static FormaMacBinding CreateDefault()
        {
            var b = CreateInstance<FormaMacBinding>();
            var list = new List<Row>();
            void R(string id, params string[] shapes)
            {
                list.Add(new Row { paramId = id, shapeNames = shapes, response = Linear() });
            }
            void L(string id, params string[] ladder)
            {
                list.Add(new Row { paramId = id, shapeNames = Array.Empty<string>(), templateLadder = ladder, response = Linear() });
            }

            // ── тело (имена из MaleAvatarBase/FemaleAvatarDefault) ──
            R("muscular", "Muscular");
            R("fat", "Fat");
            R("chest", "ChestL", "Chest");
            R("chestSep", "ChestS");
            R("waist", "Waist");
            R("belly", "Belly");
            R("hips", "Hips");
            R("buttocks", "Buttocks");
            R("calves", "LowerLegs");
            R("neck", "Neck", "NeckScale");
            R("shoulders", "ShoulderWidth", "Shoulder");
            R("arms", "UpperArmScale", "Arm");
            R("forearms", "LowerArmScale", "Forearm");
            R("hands", "Hand");
            R("feet", "Foot");
            R("legs", "LegsWidth", "Leg");
            R("height", "Height");

            // ── голова: шаблоны MAC ──
            R("headWidth", "Skull_T1");
            R("headHeight", "Skull_T2");
            R("headDepth", "Skull_T3");
            R("forehead", "Skull_T4");
            L("earSize", "Ears_T1", "Ears_T2", "Ears_T3", "Ears_T4", "Ears_T5", "Ears_T6", "Ears_T7");

            // ── лицо: ARKit-имена MAC ──
            R("browRidge", "Brow_Inner", "BrowRidge");
            R("browArch", "Brow_Arch", "BrowArch");
            R("eyeSize", "Eye_Size", "EyeSize");
            R("eyeSpacing", "Eye_Spread", "EyeSpacing");
            R("eyeHeight", "Eye_Height", "EyeHeight");
            R("eyeDepth", "Eye_Depth", "EyeDepth");
            R("eyeShape", "Eye_Narrow", "EyeShape");
            R("noseWidth", "Nose_T1");
            R("noseLength", "Nose_T2");
            R("noseBridge", "Nose_T3");
            R("noseTip", "Nose_T4");
            R("nostrils", "Nose_Wing", "Nostril");
            R("cheekbones", "Cheekbone", "Cheek_Raise");
            R("cheeks", "Cheek");
            R("jowls", "Jowl");
            R("lipWidth", "Lip_Width", "Mouth_Width");
            R("lipUpper", "Lip_Upper");
            R("lipLower", "Lip_Lower");
            R("jawWidth", "JawBackBottomWide", "JawBackTopWide", "JawWide");
            R("jawLength", "JawCenterUpper", "JawBackBottomUpper", "JawHeight");
            R("chinWidth", "ChinWide");
            R("chinHeight", "ChinHeight");
            R("chinProjection", "ChinForward");

            b.rows = list.ToArray();
            return b;
        }
    }
}
