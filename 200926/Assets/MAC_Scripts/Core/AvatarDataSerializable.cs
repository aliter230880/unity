using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicAvatarCreator
{
    public class AvatarDataSerializable
    {
        public MagicAvatarManager.AvatarType avatarType;
        public List<string> blendShapeNames = new List<string>();
        public List<float> blendShapeValues = new List<float>();

        public Dictionary<string, float> ToDictionary()
        {
            var dict = new Dictionary<string, float>();
            
            for (int i = 0; i < Math.Min(blendShapeNames.Count, blendShapeValues.Count); i++)
            {
                dict[blendShapeNames[i]] = blendShapeValues[i];
            }
            return dict;
        }

        public static AvatarDataSerializable FromDictionary(
            MagicAvatarManager.AvatarType type,
            Dictionary<string, float> blendShapes)
        {
            var data = new AvatarDataSerializable
            {
                avatarType = type
            };
            
            data.blendShapeNames.Clear();
            data.blendShapeValues.Clear();

            foreach (var kvp in blendShapes)
            {
                data.blendShapeNames.Add(kvp.Key);
                data.blendShapeValues.Add(kvp.Value);
            }

            return data;
        }
    }
}

