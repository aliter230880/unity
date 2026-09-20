using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MagicAvatarCreator
{
    public class DepthMapRenderer
    {
        private const RenderTextureFormat DepthRtFormat = RenderTextureFormat.Depth;
        private const string BodyLayerName = "MT_AvatarBody";
        private const string ClothingLayerName = "MT_AvatarClothing";
        private static readonly int AllowClipping = Shader.PropertyToID("_AllowClipping");
        private static readonly int BodyDepth = Shader.PropertyToID("_BodyDepth");
        private static readonly int DepthTextureCount = Shader.PropertyToID("_ClothingDepthTextureCount");
        private static readonly int ClothingDepthTextures = Shader.PropertyToID("_ClothingDepthTextures");
        private static readonly int RenderOrder = Shader.PropertyToID("_RenderOrder");
        private static readonly int DepthIndex = Shader.PropertyToID("_DepthIndex");
 
        private const int MaxClothingItems = 32;
        
        private RenderTexture _tempDepthRT;
        private RenderTexture _bodyDepthTexture;
        private RenderTexture _clothingDepthArrayTexture;
        private readonly List<ClothElement> _clothingElements;
        private readonly AvatarObject _currentAvatar;
        private Camera _mainCamera;
        private Camera _depthCamera;
        private MaterialPropertyBlock _avatarBodyPropertyBlock;
        
        private bool _isInitialized;

        public RenderTexture BodyDepthTexture => _bodyDepthTexture;
        public int ClothingDepthTextureCount => _clothingElements?.Count ?? 0;
        
        public DepthMapRenderer(AvatarObject currentAvatar)
        {
            _currentAvatar = currentAvatar;
            _clothingElements = new List<ClothElement>();
            
            Initialize();
        }
        
        private void Initialize()
        {
            if (_isInitialized) return;
            
            _tempDepthRT = new RenderTexture(Screen.width, Screen.height, 24, DepthRtFormat);
            _bodyDepthTexture = new RenderTexture(Screen.width, Screen.height, 24, DepthRtFormat);
            
            _clothingDepthArrayTexture = new RenderTexture(Screen.width, Screen.height, 24, DepthRtFormat, 0)
            {
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = MaxClothingItems,
                useMipMap = false,
                filterMode = FilterMode.Point
            };
            
            _tempDepthRT.Create();
            _bodyDepthTexture.Create();
            _clothingDepthArrayTexture.Create();
            
            _mainCamera = Camera.main;

            var depthCamGo = new GameObject("DepthCamera");
            depthCamGo.hideFlags = HideFlags.HideAndDontSave;
            _depthCamera = depthCamGo.AddComponent<Camera>();
            
            _isInitialized = true;
        }
        
        public void UpdateClothingElements(List<ClothElement> allClothingElements)
        {
            if (!_isInitialized) return;
            
            _clothingElements.Clear();
            
            foreach (var element in allClothingElements)
            {
                _clothingElements.Add(element);
            }
            
            _clothingElements.Sort((a, b) => a.renderOrder.CompareTo(b.renderOrder));
        }
        
        public void RenderDepthMaps()
        {
            if (!_isInitialized || _depthCamera == null || _mainCamera == null) return;

            try
            {
                _depthCamera.CopyFrom(_mainCamera);
                _depthCamera.enabled = false;
                _depthCamera.depthTextureMode = DepthTextureMode.Depth;
                _depthCamera.targetTexture = null;
                _depthCamera.cullingMask = (1 << LayerMask.NameToLayer(BodyLayerName)) |
                                           (1 << LayerMask.NameToLayer(ClothingLayerName));

                foreach (var cloth in _clothingElements)
                {
                    cloth.clothSkinnedMesh.enabled = false;
                }

                _currentAvatar.rootSkinnedMeshRenderer.enabled = true;

                _depthCamera.targetTexture = _bodyDepthTexture;
                _depthCamera.Render();

                _currentAvatar.rootSkinnedMeshRenderer.enabled = false;
                
                if (_tempDepthRT.width != Screen.width || _tempDepthRT.height != Screen.height)
                {
                    _tempDepthRT.Release();
                    _tempDepthRT = new RenderTexture(Screen.width, Screen.height, 24, DepthRtFormat);
                    _tempDepthRT.Create();
                }

                for (var i = 0; i < _clothingElements.Count && i < MaxClothingItems; i++)
                {
                    var cloth = _clothingElements[i];
                    cloth.depthIndex = i;

                    if (!cloth || !cloth.clothSkinnedMesh || !cloth.allowIntersectionsCulling) continue;

                    cloth.clothSkinnedMesh.enabled = true;

                    _avatarBodyPropertyBlock = new MaterialPropertyBlock();
                    cloth.clothSkinnedMesh.GetPropertyBlock(_avatarBodyPropertyBlock);
                    _avatarBodyPropertyBlock.SetInt(AllowClipping, 0);
                    cloth.clothSkinnedMesh.SetPropertyBlock(_avatarBodyPropertyBlock);

                    _depthCamera.targetTexture = _tempDepthRT;
                    _depthCamera.Render();

                    cloth.clothSkinnedMesh.enabled = false;

                    Graphics.CopyTexture(_tempDepthRT, 0, _clothingDepthArrayTexture, i);
                }

                foreach (var cloth in _clothingElements)
                {
                    cloth.clothSkinnedMesh.enabled = true;
                }

                _currentAvatar.rootSkinnedMeshRenderer.enabled = true;
            }
            finally
            {
                foreach (var cloth in _clothingElements)
                {
                    cloth.clothSkinnedMesh.enabled = true;
                }
                
                _currentAvatar.rootSkinnedMeshRenderer.enabled = true;
            }

        }
        
        private RenderTexture _debugSliceRT;
        
        public RenderTexture DrawArraySlice(int slice)
        {
            if (_clothingDepthArrayTexture == null) return null;
    
            // Если временный RT не создан или изменился размер – пересоздаём
            if (_debugSliceRT == null || _debugSliceRT.width != _clothingDepthArrayTexture.width || _debugSliceRT.height != _clothingDepthArrayTexture.height)
            {
                if (_debugSliceRT != null) _debugSliceRT.Release();
                _debugSliceRT = new RenderTexture(_clothingDepthArrayTexture.width, _clothingDepthArrayTexture.height, 24, _clothingDepthArrayTexture.format);
                _debugSliceRT.Create();
            }
    
            // Копируем нужный слой из 2DArray во временный RT
            Graphics.CopyTexture(_clothingDepthArrayTexture, slice, _debugSliceRT, 0);
    
            return _debugSliceRT;
        }
        
        public void ApplyToRenderer(ClothElement cloth)
        {
            var renderer = cloth.clothSkinnedMesh;
            
            if (!_isInitialized || renderer == null) return;
            
            var mainCameraVP = _mainCamera.projectionMatrix * _mainCamera.worldToCameraMatrix;
            Shader.SetGlobalMatrix("_MainCameraVP", mainCameraVP);

            for (var i = 0; i < _currentAvatar.rootSkinnedMeshRenderer.sharedMaterials.Length; i++)
            {
                var bodyPropertyBlock = new MaterialPropertyBlock();
                
                _currentAvatar.rootSkinnedMeshRenderer.GetPropertyBlock(bodyPropertyBlock, i);
                bodyPropertyBlock.SetTexture(BodyDepth, _bodyDepthTexture);
                bodyPropertyBlock.SetInt(DepthTextureCount, _clothingElements.Count);
                bodyPropertyBlock.SetTexture(ClothingDepthTextures, _clothingDepthArrayTexture);
                _currentAvatar.rootSkinnedMeshRenderer.SetPropertyBlock(bodyPropertyBlock, i);
            }
            
            var block = new MaterialPropertyBlock();
            
            renderer.GetPropertyBlock(block);
            block.SetTexture(BodyDepth, DrawArraySlice(0));
            block.SetInt(DepthTextureCount, _clothingElements.Count);
            block.SetTexture(ClothingDepthTextures, _clothingDepthArrayTexture);
            block.SetTexture("_ClothingDepthTexture", DrawArraySlice(1));
            block.SetInt(RenderOrder, cloth.renderOrder);
            block.SetInt(DepthIndex, cloth.depthIndex);
            block.SetInt(AllowClipping, cloth.allowIntersectionsCulling ? 1 : 0);
            renderer.SetPropertyBlock(block);
        }
        
        public void Cleanup()
        {
            if (!_isInitialized) return;
            
            if (_depthCamera != null)
            {
                Object.Destroy(_depthCamera.gameObject);
                _depthCamera = null;
            }
            
            if (_bodyDepthTexture != null)
            {
                _bodyDepthTexture.Release();
                _bodyDepthTexture = null;
            }
            
            if (_clothingDepthArrayTexture != null)
            {
                _clothingDepthArrayTexture.Release();
                _clothingDepthArrayTexture = null;
            }
            
            for (var i = 0; i < _clothingElements.Count && i < MaxClothingItems; i++)
            {
                var cloth = _clothingElements[i];
                
                var block = new MaterialPropertyBlock();
                cloth.clothSkinnedMesh.GetPropertyBlock(block);
                block.Clear();
                cloth.clothSkinnedMesh.SetPropertyBlock(block, i);
            }
            
            _isInitialized = false;
        }
    }
}