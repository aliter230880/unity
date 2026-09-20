using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicAvatarCreator
{
    [CreateAssetMenu(fileName = "BlendShapeBoneSyncData", menuName = "Magic Avatar Creator/Blend Shape Bone Sync Data")]
    public class BlendShapeBoneSyncData : ScriptableObject
    {
        [Serializable]
        public struct BoneSyncConfig
        {
            public string boneName;
            public Vector3 positionAt0;
            public Vector3 positionAt100;
        }

        public List<BoneSyncConfig> boneConfigs = new List<BoneSyncConfig>();

        public Vector3 GetInterpolatedPosition(string boneName, float blendShapeValue)
        {
            var config = boneConfigs.Find(b => b.boneName == boneName);
            if (config.Equals(default(BoneSyncConfig)))
            {
                return Vector3.zero;
            }

            // Интерполяция: blendShapeValue от 0 до 100
            float t = Mathf.Clamp01(blendShapeValue / 100f);
            return Vector3.Lerp(config.positionAt0, config.positionAt100, t);
        }

        public bool TryGetBoneConfig(string boneName, out BoneSyncConfig config)
        {
            return boneConfigs.TryGetBoneConfig(boneName, out config);
        }
    }

    public static class BlendShapeBoneSyncDataExtensions
    {
        public static bool TryGetBoneConfig(this List<BlendShapeBoneSyncData.BoneSyncConfig> list, string boneName, out BlendShapeBoneSyncData.BoneSyncConfig config)
        {
            config = default;
            foreach (var item in list)
            {
                if (item.boneName == boneName)
                {
                    config = item;
                    return true;
                }
            }
            return false;
        }
    }
}
