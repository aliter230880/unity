using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicAvatarCreator
{
    [ExecuteAlways]
    public class AvatarObject : MonoBehaviour
    {
        public MagicAvatarManager.AvatarType avatarSex;
        public SkinnedMeshRenderer rootSkinnedMeshRenderer;
        public List<SkinnedMeshRenderer> dependentSkinnedMeshRenderers;
        public Transform clothHolder;
        public AvatarClothSet clothSet;
        public AvatarData currentAvatarData;
        [Header("Debug")] 
        public int depthTextureIndex = 0;
        public float collisionRadius = 0.5f;
        public float pushStrength = 5;
        
        public AvatarClothManager AvatarClothManager { get; set; }
        public AvatarMaterialsManager AvatarMaterialsManager { get; set; }
        public AvatarDataManager AvatarDataManager { get; set; }
        public CameraFocusController CameraFocusController { get; set; }
        public BlendShapeController BlendShapeController { get; set; }

        private void OnEnable()
        {
            // Init(currentAvatarData);
        }

        public void Init(AvatarData avatarData = null)
        {
            if (avatarData)
            {
                currentAvatarData = avatarData;
            }

            BlendShapeController = new BlendShapeController(this);
            AvatarDataManager = new AvatarDataManager(this);
            AvatarClothManager = new AvatarClothManager(this);
            AvatarMaterialsManager = new AvatarMaterialsManager(rootSkinnedMeshRenderer);
            // Register additional skin renderers; materials are filtered by the MAC skin property.
            AvatarMaterialsManager.RegisterSkinRenderers(GetComponentsInChildren<SkinnedMeshRenderer>(true));
            AvatarMaterialsManager.UpdateMaterialProperties();
            CameraFocusController = GetComponentInChildren<CameraFocusController>();

            if (!currentAvatarData) return;
            
            ApplyLoadedBlendShapes(currentAvatarData);
            ApplyLoadedClothes(currentAvatarData);
            ApplyLoadedMaterialProperties(currentAvatarData);
            
            BlendShapeController.SynchronizeBlendShapes();
        }

        private void ApplyLoadedBlendShapes(AvatarData avatarData)
        {
            foreach (var pair in avatarData.GetBlendShapes())
            {
                if (BlendShapeController.BlendShapesMap.TryGetValue(pair.Key, out var index))
                {
                    rootSkinnedMeshRenderer.SetBlendShapeWeight(index, pair.Value);
                }
            }
        }

        private void ApplyLoadedClothes(AvatarData avatarData)
        {
            var clothesToLoad = avatarData.GetClothes();
            var clothesOnAvatar = new List<ClothElement>();

            for (var i = 0; i < clothHolder.transform.childCount; i++)
            {
                var clothElement = clothHolder.transform.GetChild(i).gameObject.GetComponent<ClothElement>();
                
                if (clothElement)
                {
                    clothesOnAvatar.Add(clothElement);
                }
            }
            
            foreach (var (clothGuid, place) in clothesToLoad)
            {
                if (clothSet.clothElements == null) continue;
                
                var clothElement = clothSet.clothElements.Find(item => item.clothGuid == clothGuid);
                    
                if (clothElement)
                {
                    var existCloth = clothesOnAvatar.Find(item => item.clothGuid == clothGuid);
                    
                    if (existCloth)
                    {
                        AvatarClothManager.UpdateClothMap(existCloth);
                    }
                    else
                    {
                        AvatarClothManager.SetClothToAvatar(clothElement);
                    }
                }
            }

            BlendShapeController.SynchronizeBlendShapes();
        }

        private void ApplyLoadedMaterialProperties(AvatarData avatarData)
        {
            avatarData.ApplyToMaterialManager(AvatarMaterialsManager);
        }

        // void OnGUI()
        // {
        //     if (AvatarClothManager == null) return;
        //     
        //     if (AvatarClothManager._depthMapRenderer != null && AvatarClothManager._depthMapRenderer.BodyDepthTexture != null)
        //     {
        //         GUI.DrawTexture(new Rect(Screen.width - 256, 0, 256, 256), AvatarClothManager._depthMapRenderer.BodyDepthTexture);
        //     }
        //     
        //     GUI.DrawTexture(new Rect(Screen.width - 256, 256, 256, 256),
        //         AvatarClothManager._depthMapRenderer.DrawArraySlice(0));
        //     
        //     GUI.DrawTexture(new Rect(Screen.width - 512, 256, 256, 256),
        //         AvatarClothManager._depthMapRenderer.DrawArraySlice(1));
        // }
    }
}

