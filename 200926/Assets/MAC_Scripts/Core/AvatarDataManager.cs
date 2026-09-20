using System;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Object = UnityEngine.Object;

namespace  MagicAvatarCreator
{
    public class AvatarDataManager
    {
        public enum DataStoringType
        {
            Binary,
            Json
        }
        
        private readonly AvatarObject _avatarObject;
        
        public AvatarDataManager(AvatarObject avatarObject)
        {
            _avatarObject = avatarObject;
        }
        
        private AvatarData CaptureCurrentState(AvatarData newAvatarData)
        {
            var data = newAvatarData ?? ScriptableObject.CreateInstance<AvatarData>();
            data.avatarType = _avatarObject.avatarSex;

            var blendShapes = new Dictionary<string, float>();
            var startIndex = _avatarObject.rootSkinnedMeshRenderer.sharedMesh.GetBlendShapeIndex("Muscular");
            if (startIndex < 0) startIndex = 0; // fallback: capture all blend shapes
            
            for (var i = startIndex; i < _avatarObject.rootSkinnedMeshRenderer.sharedMesh.blendShapeCount; i++)
            {
                var parameterName = _avatarObject.rootSkinnedMeshRenderer.sharedMesh.GetBlendShapeName(i);
                var weight = _avatarObject.rootSkinnedMeshRenderer.GetBlendShapeWeight(i);
                
                blendShapes[parameterName] = weight;
            }

            data.SetBlendShapes(blendShapes);
            data.SetClothes(_avatarObject.AvatarClothManager.AttachedClothElements);
            data.SetMaterialProperties(_avatarObject.AvatarMaterialsManager);
            
            return data;
        }
        
#if UNITY_EDITOR
        public AvatarData SaveToScriptableObject(string assetFileName)
        {
            var path = $"Assets/Magic Avatar Creator/Avatars/{assetFileName}.asset";

            if (File.Exists(path))
            {
                if (!EditorUtility.DisplayDialog("Magic Avatar Creator",
                        $"Asset {assetFileName} already exists. Overwrite it?", "Ok", "Cancel"))
                {
                    return null;
                }
            }
            
            var state = CaptureCurrentState(CreateNewAvatarData(path));

            EditorUtility.SetDirty(state);
            AssetDatabase.SaveAssetIfDirty(state);
            
            return state;
        }
#endif

        private AvatarData LoadFromScriptableObject(AvatarData data)
        {
            if (!data) return null;

            return data;
        }
        
        /// <summary>
        /// Save avatar state to a .json file (replaces deprecated BinaryFormatter).
        /// </summary>
        public void SaveToBinary(string filePath)
        {
            // Migrate: save as JSON even when "binary" is requested.
            // Old .bin files are no longer supported (BinaryFormatter is deprecated/insecure).
            SaveToJson(Path.ChangeExtension(filePath, ".json"));
        }
        
        /// <summary>
        /// Load avatar state from a .json file. Falls back to legacy .bin if .json doesn't exist.
        /// </summary>
        private static AvatarData LoadFromBinary(string filePath)
        {
            // Try JSON first (new format)
            var jsonPath = Path.ChangeExtension(filePath, ".json");
            if (File.Exists(jsonPath))
                return LoadFromJson(jsonPath);
            
            // Legacy .bin no longer supported — BinaryFormatter removed for security
            if (File.Exists(filePath))
                Debug.LogWarning($"[AvatarDataManager] Legacy .bin format no longer supported: {filePath}. Please re-save as JSON.");
            
            return null;
        }
        
        public void SaveToJson(string filePath)
        {
            var state = CaptureCurrentState(null);
            var json = state.ToJson();
            File.WriteAllText(filePath, json);
            
            if (Application.isEditor) Object.DestroyImmediate(state);
            else Object.Destroy(state);
        }

        private static AvatarData LoadFromJson(string filePath)
        {
            if (!File.Exists(filePath)) return null;

            var json = File.ReadAllText(filePath);
            var data = AvatarData.FromJson(json);

            return data;
        }
        
#if UNITY_EDITOR
        private static AvatarData CreateNewAvatarData(string savePath)
        {
            var data = ScriptableObject.CreateInstance<AvatarData>();
            AssetDatabase.CreateAsset(data, savePath);
            AssetDatabase.SaveAssets();
            return data;
        }
#endif

        public static AvatarData TryLoadData(DataStoringType dataStoringType, string avatarName)
        {
            return dataStoringType switch
            {
                DataStoringType.Binary => LoadFromBinary($"{Application.persistentDataPath}/{avatarName}.bin"),
                DataStoringType.Json => LoadFromJson($"{Application.persistentDataPath}/{avatarName}.json"),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}

