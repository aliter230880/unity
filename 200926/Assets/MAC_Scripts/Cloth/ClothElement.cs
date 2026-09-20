using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicAvatarCreator
{
    [System.Serializable]
    public class ClothElement : MonoBehaviour
    {
        public Texture2D clothTexture;
        public string clothName;
        public AvatarClothManager.ClothSex clothSex;
        public AvatarClothManager.ClothGeneralType generalType;
        public AvatarClothManager.ClothPlace clothPlace;
        public SkinnedMeshRenderer clothSkinnedMesh;
        public GameObject clothBonesRoot;
        public int depthIndex;
        
        [SerializeField]
        public string clothGuid;
        [SerializeField]
        public ClothMaskData maskData;

        public bool allowIntersectionsCulling = true;
        public int renderOrder = 0;
        
        private AvatarObject _avatarObject;

        private void Awake()
        {
            _avatarObject = GetComponentInParent<AvatarObject>();
            
            AssignBones();
            SynchronizeBlendShapes();
        }
        
        public void InitializeMaskData()
        {
            maskData ??= new ClothMaskData();
        }

        private void AssignBones()
        {
            var targetSkinnedMesh = _avatarObject.rootSkinnedMeshRenderer;
            var boneMap = new Dictionary<string, Transform>();
            var newBones = new Transform[clothSkinnedMesh.bones.Length];
            
            foreach (var bone in targetSkinnedMesh.bones)
            {
                if (bone)
                    boneMap.TryAdd(bone.name, bone);
            }
            
            for (var i = 0; i < clothSkinnedMesh.bones.Length; i++)
            {
                if (clothSkinnedMesh.bones[i] == null)
                    continue;
            
                var boneName = clothSkinnedMesh.bones[i].name;

                if (boneMap.TryGetValue(boneName, out var value))
                {
                    newBones[i] = value;
                }
                else
                {
                    newBones[i] = clothSkinnedMesh.bones[i];
                }
            }

            clothSkinnedMesh.bones = newBones;
            clothSkinnedMesh.rootBone = targetSkinnedMesh.rootBone;
            
            Destroy(clothBonesRoot);
        }

        private void SynchronizeBlendShapes()
        {
            for (var i = 0; i < clothSkinnedMesh.sharedMesh.blendShapeCount; i++)
            {
                var clothShapeName = clothSkinnedMesh.sharedMesh.GetBlendShapeName(i);
                var avatarShapeIndex = _avatarObject.rootSkinnedMeshRenderer.sharedMesh.GetBlendShapeIndex(clothShapeName);
                if (avatarShapeIndex < 0) continue; // blend shape not found on avatar
                var avatarShapeWeight = _avatarObject.rootSkinnedMeshRenderer.GetBlendShapeWeight(avatarShapeIndex);
                
                clothSkinnedMesh.SetBlendShapeWeight(i, avatarShapeWeight);
            }
        }

        public void Reinit()
        {
            AssignBones();
            SynchronizeBlendShapes();
        }

        public void GenerateGuid()
        {
            clothGuid = Guid.NewGuid().ToString();
        }
    }
}