using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicAvatarCreator
{
    /// <summary>
    /// Сериализуемое состояние аватара: пол, блендшейпы, надетая одежда и свойства материалов.
    /// Восстановлено 20.09.2026 по местам использования (AvatarObject, AvatarDataManager,
    /// MagicAvatarManager, MAC_CC_UI_Bridge) и сериализованным .asset из Avatars/.
    /// Файл ДВАЖДЫ перезаписывался укороченной заглушкой + diff-патчем — при правке
    /// убедитесь, что методы ниже остаются на месте: на них завязано сохранение/загрузка.
    /// GUID скрипта в .meta — исходный (5f0c2dafb7f22e84890d8d72644fb6da).
    /// </summary>
    [CreateAssetMenu(fileName = "AvatarData", menuName = "Magic Avatar Creator/Avatar Data")]
    public class AvatarData : ScriptableObject
    {
        public MagicAvatarManager.AvatarType avatarType;

        public string[] blendShapeNames = Array.Empty<string>();
        public float[] blendShapeValues = Array.Empty<float>();

        public string[] clothGuids = Array.Empty<string>();
        public string clothPlaces;

        public float metallic;
        public float smoothness = 0.45f;
        public Color skinColorCorrector = Color.white;
        public Color lipsColor = Color.white;
        public Color nipplesColor = Color.white;
        public Color frackles = Color.clear;
        public float fracklesIntensity = 0.5f;
        public float fracklesScale = 10f;
        public float fracklesLightness;

        public float GetValue(string name, float fallback = 0f)
        {
            if (blendShapeNames == null || blendShapeValues == null) return fallback;
            for (int i = 0; i < blendShapeNames.Length && i < blendShapeValues.Length; i++)
                if (blendShapeNames[i] == name) return blendShapeValues[i];
            return fallback;
        }

        public void SetBlendShapes(Dictionary<string, float> blendShapes)
        {
            if (blendShapes == null) { blendShapeNames = Array.Empty<string>(); blendShapeValues = Array.Empty<float>(); return; }
            var names = new List<string>(blendShapes.Count);
            var values = new List<float>(blendShapes.Count);
            foreach (var kvp in blendShapes) { names.Add(kvp.Key); values.Add(kvp.Value); }
            blendShapeNames = names.ToArray();
            blendShapeValues = values.ToArray();
        }

        public Dictionary<string, float> GetBlendShapes()
        {
            var dict = new Dictionary<string, float>();
            if (blendShapeNames == null || blendShapeValues == null) return dict;
            for (int i = 0; i < blendShapeNames.Length && i < blendShapeValues.Length; i++)
                dict[blendShapeNames[i]] = blendShapeValues[i];
            return dict;
        }

        public void SetClothes(Dictionary<string, ClothElement> attachedClothes)
        {
            var guids = new List<string>();
            var places = new List<string>();
            if (attachedClothes != null)
                foreach (var kvp in attachedClothes)
                {
                    if (kvp.Value == null) continue;
                    guids.Add(kvp.Value.clothGuid);
                    places.Add(kvp.Value.clothPlace.ToString());
                }
            clothGuids = guids.ToArray();
            clothPlaces = string.Join(";", places);
        }

        /// <summary>guid надетой вещи → место (ClothPlace)</summary>
        public Dictionary<string, string> GetClothes()
        {
            var dict = new Dictionary<string, string>();
            if (clothGuids == null) return dict;
            var places = string.IsNullOrEmpty(clothPlaces) ? Array.Empty<string>() : clothPlaces.Split(';');
            for (int i = 0; i < clothGuids.Length; i++)
                dict[clothGuids[i]] = i < places.Length ? places[i] : string.Empty;
            return dict;
        }

        public void SetMaterialProperties(AvatarMaterialsManager manager)
        {
            if (manager == null) return;
            skinColorCorrector = manager.skinColorCorrector;
            metallic = manager.metallic;
            smoothness = manager.smoothness;
            frackles = manager.freckles;
            fracklesIntensity = manager.frecklesIntensity;
            fracklesScale = manager.frecklesScale;
            fracklesLightness = manager.frecklesLightness;
            nipplesColor = manager.nipplesColor;
            lipsColor = manager.lipsColor;
        }

        public void ApplyToMaterialManager(AvatarMaterialsManager manager)
        {
            if (manager == null) return;
            manager.skinColorCorrector = skinColorCorrector;
            manager.metallic = metallic;
            manager.smoothness = smoothness;
            manager.freckles = frackles;
            manager.frecklesIntensity = fracklesIntensity;
            manager.frecklesScale = fracklesScale;
            manager.frecklesLightness = fracklesLightness;
            manager.nipplesColor = nipplesColor;
            manager.lipsColor = lipsColor;
            manager.UpdateMaterialProperties();
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }

        public static AvatarData FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var data = CreateInstance<AvatarData>();
            JsonUtility.FromJsonOverwrite(json, data);
            return data;
        }
    }
}
