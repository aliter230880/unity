using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MagicAvatarCreator
{
    public class AvatarClothManager
    {
        public enum ClothSex
        {
            Male,
            Female,
            Unisex
        }
        
        public enum ClothGeneralType
        {
            Underwear,
            Outerwear,
            Footwear,
            Accessory
        }

        [System.Serializable]
        public enum ClothPlace
        {
            Head,
            Face,
            Ears,
            Nose,
            Eyes,
            Neck,
            Body,
            Legs,
            Feet,
            FullBody
        }

        public Dictionary<string, ClothElement> AttachedClothElements
        {
            get => _clothElementsMap;
            set => _clothElementsMap = value;
        }

        public Dictionary<ClothPlace, ClothElement> AttachedClothElementsPlaces
        {
            get => _clothElementsPlacesMap;
            set => _clothElementsPlacesMap = value;
        }
        
        private Dictionary<string, ClothElement> _clothElementsMap;
        private Dictionary<ClothPlace, ClothElement> _clothElementsPlacesMap;
        private Dictionary<ClothElement, ComputeBuffer> _clothBuffers = new();
        private readonly AvatarObject _currentAvatar;
        public DepthMapRenderer _depthMapRenderer;
        public UnityAction onAvatarClothChanged;

        public AvatarClothManager(AvatarObject currentAvatar)
        {
            _currentAvatar = currentAvatar;
            _currentAvatar.rootSkinnedMeshRenderer.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
            _clothElementsMap = new Dictionary<string, ClothElement>();
            _clothElementsPlacesMap =  new Dictionary<ClothPlace, ClothElement>();
            // _depthMapRenderer = new DepthMapRenderer(_currentAvatar);
        }

        public void SetClothToAvatar(ClothElement cloth)
        {
            if (_clothElementsPlacesMap.TryGetValue(cloth.clothPlace, out var existingCloth))
            {
#if UNITY_EDITOR
                Object.DestroyImmediate(existingCloth.gameObject);
#else
                Object.Destroy(existingCloth.gameObject);
#endif
                _clothElementsPlacesMap.Remove(existingCloth.clothPlace);
                _clothElementsMap.Remove(existingCloth.clothGuid);
            }

            var clothObject = Object.Instantiate(cloth.gameObject, _currentAvatar.clothHolder, false);
            var clothElement = clothObject.GetComponent<ClothElement>();
            
            _clothElementsMap[clothElement.clothGuid] = clothElement;
            _clothElementsPlacesMap[clothElement.clothPlace] = clothElement;
            
            UpdateDisplacementMask();
        }
        
        private void UpdateDisplacementMask()
        {
            var activeClothes = new List<ClothElement>(_clothElementsMap.Values);
        
            foreach (var kvp in _clothElementsMap)
            {
                var cloth = kvp.Value;
                var renderer = cloth.clothSkinnedMesh;
                var otherClothes = new List<ClothElement>(activeClothes);
                otherClothes.Remove(cloth);
                
                var maskData = _currentAvatar.clothSet.GetMaskData(cloth.clothGuid);

                if (maskData != null)
                {
                    var combination = maskData.combinations.Find(c =>
                        c.name == _currentAvatar.clothSet.GetCombinationName(cloth, otherClothes));

                    if (combination != null)
                    {
                        SetDisplacementMask(renderer, combination.maskTexture);
                    }
                    else
                    {
                        SetDisplacementMask(renderer, null);
                    }
                }
                else
                {
                    SetDisplacementMask(renderer, null);
                }
            }
            
            onAvatarClothChanged?.Invoke();
        }

        public void UpdateClothMap(ClothElement cloth)
        {
            _clothElementsMap.TryAdd(cloth.clothGuid, cloth);
            _clothElementsPlacesMap.TryAdd(cloth.clothPlace, cloth);
        }

        private static readonly int DisplacementMaskId = Shader.PropertyToID("_Displacement_Mask");
        private static Texture2D _emptyMaskTexture;

        /// <summary>
        /// Set displacement mask via MaterialPropertyBlock instead of modifying sharedMaterials directly.
        /// </summary>
        private static void SetDisplacementMask(SkinnedMeshRenderer renderer, Texture2D mask)
        {
            if (_emptyMaskTexture == null)
            {
                _emptyMaskTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _emptyMaskTexture.SetPixel(0, 0, Color.black);
                _emptyMaskTexture.Apply();
            }

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block, 0);
            block.SetTexture(DisplacementMaskId, mask != null ? mask : _emptyMaskTexture);
            renderer.SetPropertyBlock(block, 0);
        }
        
        public void Cleanup()
        {
            if (_depthMapRenderer != null)
            {
                _depthMapRenderer.Cleanup();
                _depthMapRenderer = null;
            }
        } 
    }
}