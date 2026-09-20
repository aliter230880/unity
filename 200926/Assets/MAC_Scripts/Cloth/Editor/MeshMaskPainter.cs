using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MagicAvatarCreator
{
    public class MeshMaskPainter : EditorWindow
    {
        private static readonly int DisplacementMask = Shader.PropertyToID("_Displacement_Mask");
        private static readonly int IsPainting = Shader.PropertyToID("_Is_Painting");
        private const string ClothingLayerName = "MT_AvatarClothing";

        private SkinnedMeshRenderer _targetRenderer;
        private SkinnedMeshRenderer _lastTargetRenderer;
        private MeshCollider _targetCollider;
        private Material[] _targetMaterials;
        private Texture2D _maskTexture;
        private ClothElement _targetCloth;
        private float _brushRadius = 0.05f;
        private float _brushStrength = 1.0f;
        private bool _isPainting;
        private bool _showPreview;
        private string _storePath;
        private MagicAvatarManager _avatarManager;

        private readonly int _texWidth = 1024;
        private readonly int _texHeight = 1024;

        private MeshCollider _workCollider;
        private Mesh _bakedMesh;

        [MenuItem("Tools/Magic Tools/Magic Avatar Creator/Mesh Mask Painter")]
        public static void ShowWindow()
        {
            var window = GetWindow<MeshMaskPainter>("Mask Painter");
            
            window.minSize = new Vector2(370, 500);
            window.maxSize = new Vector2(370, 500);
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            DestroyWorkCollider();
        }

        private void OnDestroy()
        {
            DestroyWorkCollider();
            if (_bakedMesh) DestroyImmediate(_bakedMesh);
        }

        private void OnGUI()
        {
            if (!_avatarManager)
            {
                _avatarManager = FindAnyObjectByType<MagicAvatarManager>();
                _avatarManager.CurrentAvatar.AvatarClothManager.onAvatarClothChanged += UpdateTarget;
            }

            if (!_avatarManager)
            {
                EditorGUILayout.HelpBox(
                    "No avatar manager instance detected",
                    MessageType.Info);
                return;
            }

            _targetRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Target Clothing", _targetRenderer,
                typeof(SkinnedMeshRenderer), true);
            _brushRadius = EditorGUILayout.Slider("Brush Radius", _brushRadius, 0f, 1f);
            _brushStrength = EditorGUILayout.Slider("Strength", _brushStrength, 0f, 1f);

            if (_maskTexture)
            {
                DrawTextureRect(_maskTexture);

                if (GUILayout.Button("Save Texture as Asset"))
                {
                    SaveTextureAsAsset();
                }
            }

            if (_targetRenderer)
            {
                if (_targetRenderer != _lastTargetRenderer)
                {
                    UpdateTarget();
                }

                EditorGUILayout.HelpBox(
                    "Hold Ctrl + Left Mouse to paint in R channel\nHold Ctrl + Right Mouse to paint in G channel\nUse Mouse Wheel to adjust brush radius",
                    MessageType.Info);
            }
            else
            {
                _maskTexture = null;
                _storePath = "";
                _targetMaterials = null;
                _lastTargetRenderer = _targetRenderer;
            }
        }
        
        private void OnSceneGUI(SceneView sceneView)
        {
            if (_targetRenderer == null || _maskTexture == null) return;

            var e = Event.current;
            var controlId = GUIUtility.GetControlID(FocusType.Passive);
            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var clothingLayer = LayerMask.NameToLayer(ClothingLayerName);
            var layerMask = 1 << clothingLayer;
            
            _showPreview = e.control;

            UpdateColliderMesh();

            if (Physics.Raycast(ray, out var hit, Mathf.Infinity, layerMask))
            {
                var circleColor = Color.white;

                if (e.shift)
                    circleColor = Color.cyan;
                else if (e.alt)
                    circleColor = Color.red;

                Handles.color = circleColor;
                Handles.DrawWireDisc(hit.point, hit.normal, _brushRadius);
                HandleUtility.Repaint();
            }

            if (e.isMouse && e.button is 0 or 1 && e.control)
            {
                switch (e.type)
                {
                    case EventType.MouseDown:
                        _isPainting = true;
                        PaintOrBlur(e.mousePosition, e.shift, e.alt, e.button == 1);
                        e.Use();
                        break;
                    case EventType.MouseDrag:
                        if (_isPainting)
                        {
                            PaintOrBlur(e.mousePosition, e.shift, e.alt, e.button == 1);
                            e.Use();
                        }

                        break;
                    case EventType.MouseUp:
                        _isPainting = false;
                        e.Use();
                        break;
                }
            }

            if (e.type == EventType.ScrollWheel && e.control)
            {
                _brushRadius += e.delta.y * 0.001f;
                _brushRadius = Mathf.Clamp(_brushRadius, 0.01f, 0.5f);

                Repaint();
                HandleUtility.Repaint();
                e.Use();
            }

            HandleUtility.AddDefaultControl(controlId);

            if (_targetMaterials != null)
            {
                foreach (var material in _targetMaterials)
                {
                    material.SetTexture(DisplacementMask, _maskTexture);
                    material.SetInt(IsPainting, _showPreview ? 1 : 0);
                }
            }
            
            _lastTargetRenderer = _targetRenderer;
        }

        private void GetStorePath(bool forceUpdate = false)
        {
            if (forceUpdate) _storePath = "";
            
            if (!string.IsNullOrEmpty(_storePath)) return;

            if (!_targetRenderer || _targetRenderer.sharedMaterials.Length == 0) return;

            var materialPath = AssetDatabase.GetAssetPath(_targetRenderer.sharedMaterials[0]);

            if (string.IsNullOrEmpty(materialPath))
            {
                Debug.LogWarning("Material is not saved as an asset. Please extract the material first.");

                _storePath = "Assets/";

                return;
            }

            var directory = System.IO.Path.GetDirectoryName(materialPath);
            _storePath = directory + "/";
        }

        private static void DrawTextureRect(Texture texture)
        {
            if (texture)
            {
                using (new GUILayout.HorizontalScope())
                {
                    var textureRect =
                        GUILayoutUtility.GetRect(350, 350, GUILayout.ExpandWidth(false));

                    EditorGUI.DrawPreviewTexture(
                        textureRect,
                        texture);
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void UpdateTarget()
        {
            if (!_targetRenderer || _targetRenderer.sharedMaterials.Length == 0)
            {
                return;
            }

            _targetMaterials = _targetRenderer.sharedMaterials;
            _targetCloth = _targetRenderer.GetComponentInParent<ClothElement>();

            if (!_targetCloth ||!_avatarManager.CurrentAvatar || !_avatarManager.CurrentAvatar.clothSet)
            {
                _maskTexture = null;
                _storePath = "";
                _targetMaterials = null;
                _lastTargetRenderer = _targetRenderer;
                
                return;
            }

            var maskData = _avatarManager.CurrentAvatar.clothSet.GetMaskData(_targetCloth.clothGuid);
            
            if (maskData != null)
            {
                var combinationName =
                    _avatarManager.CurrentAvatar.clothSet.GetCombinationName(_targetCloth, GetOtherClothes());
                var combination = maskData.combinations.Find(c => 
                    c.name == combinationName);
                    
                if (combination != null && combination.maskTexture)
                {
                    _maskTexture = combination.maskTexture;
                }
                else
                {
                    _maskTexture = null;
                }
            }
            else
            {
                _maskTexture = null;
            }

            if (!_maskTexture)
            {
                CreateNewMaskTexture();
            }
            
            GetStorePath(true);

            foreach (var material in _targetMaterials)
            {
                material.SetTexture(DisplacementMask, _maskTexture);
            }

            if (_lastTargetRenderer != null)
            {
                var meshCollider =  _lastTargetRenderer.GetComponent<MeshCollider>();

                if (meshCollider)
                {
                    DestroyImmediate(meshCollider);
                }
            }
            
            _lastTargetRenderer = _targetRenderer;
            
            Repaint();
        }

        private void CreateNewMaskTexture()
        {
            if (!_targetRenderer || _targetRenderer.sharedMaterials.Length == 0)
            {
                Debug.LogError("No renderer or materials assigned");
                return;
            }

            if (!_maskTexture)
            {
                _maskTexture = new Texture2D(_texWidth, _texHeight, TextureFormat.RGBA32, false);
                var clear = new Color[_texWidth * _texHeight];

                for (var i = 0; i < clear.Length; i++)
                {
                    clear[i] = Color.black;
                }

                _maskTexture.SetPixels(clear);
                _maskTexture.Apply();

                foreach (var material in _targetMaterials)
                {
                    material.SetTexture(DisplacementMask, _maskTexture);
                }
            }
        }

        private void SaveTextureAsAsset()
        {
            if (string.IsNullOrEmpty(_storePath) || !_targetRenderer) return;
            
            var clothSet = _avatarManager.CurrentAvatar.clothSet;

            clothSet.EnsureMaskDataExists(_targetCloth.clothGuid);
            var maskData = clothSet.GetMaskData(_targetCloth.clothGuid);

            var otherClothes = GetOtherClothes();
            
            if (otherClothes.Count > 0)
            {
                var resultName = clothSet.GetCombinationName(_targetCloth, otherClothes);
                MaskCombination resultCombination;

                var existing = maskData.combinations.Find(c => c.name.Equals(resultName));

                if (existing != null)
                {
                    resultCombination = existing;
                }
                else
                {
                    resultCombination = new MaskCombination
                    {
                        targetClothGuid = _targetCloth.clothGuid,
                        name = resultName
                    };

                    foreach (var otherCloth in otherClothes)
                    {
                        resultCombination.requiredClothGuids.Add(otherCloth.clothGuid);
                    }
                    
                    maskData.combinations.Add(resultCombination);
                }
                
                var combinationPath = $"{_storePath}{resultCombination.name}.png";
   
                var combinationPng = _maskTexture.EncodeToPNG();
                System.IO.File.WriteAllBytes(combinationPath, combinationPng);
                AssetDatabase.Refresh();

                var comboImporter = AssetImporter.GetAtPath(combinationPath) as TextureImporter;
                
                if (comboImporter)
                {
                    comboImporter.isReadable = true;
                    comboImporter.sRGBTexture = false;
                    comboImporter.textureCompression = TextureImporterCompression.Uncompressed;
                    comboImporter.SaveAndReimport();
                }

                var savedComboMask = AssetDatabase.LoadAssetAtPath<Texture2D>(combinationPath);
                
                if (savedComboMask)
                {
                    resultCombination.maskTexture = savedComboMask;
                }
                
                foreach (var material in _targetMaterials)
                {
                    material.SetTexture(DisplacementMask, _maskTexture);
                }
            }

            EditorUtility.SetDirty(clothSet);
            AssetDatabase.SaveAssets();
        }
        
        private List<ClothElement> GetActiveClothes()
        {
            if (!_avatarManager.CurrentAvatar || _avatarManager.CurrentAvatar.AvatarClothManager == null) return new List<ClothElement>();
    
            return new List<ClothElement>(_avatarManager.CurrentAvatar.AvatarClothManager.AttachedClothElements.Values);
        }

        private List<ClothElement> GetOtherClothes()
        {
            var activeClothes = GetActiveClothes();
            var otherClothes = new List<ClothElement>(activeClothes);
            
            otherClothes.Remove(_targetCloth);

            return otherClothes;
        }

        private void EnsureWorkCollider()
        {
            if (_targetRenderer == null)
            {
                DestroyWorkCollider();
                return;
            }

            if (_workCollider == null || _workCollider.gameObject != _targetRenderer.gameObject)
            {
                DestroyWorkCollider();

                _workCollider = _targetRenderer.gameObject.AddComponent<MeshCollider>();
                _workCollider.hideFlags = HideFlags.DontSaveInEditor | HideFlags.NotEditable;
                _workCollider.convex = false;
                _workCollider.cookingOptions = MeshColliderCookingOptions.None;
            }
        }

        private void DestroyWorkCollider()
        {
            if (_workCollider)
            {
                if (!Application.isPlaying)
                    DestroyImmediate(_workCollider);
                else
                    Destroy(_workCollider);
                _workCollider = null;
            }
        }

        private void UpdateColliderMesh()
        {
            EnsureWorkCollider();

            if (_targetRenderer == null) return;

            if (!_bakedMesh)
            {
                _bakedMesh = new Mesh();
            }

            _targetRenderer.BakeMesh(_bakedMesh, true);
            _workCollider.sharedMesh = null;
            _workCollider.sharedMesh = _bakedMesh;
        }
        
        private void PaintOrBlur(Vector2 mousePosition, bool isBlur, bool isSubtract, bool useGreenChannel = false)
        {
            var ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            var clothingLayer = LayerMask.NameToLayer(ClothingLayerName);

            if (clothingLayer < 0) return;

            if (Physics.Raycast(ray, out var hit, Mathf.Infinity, 1 << clothingLayer))
            {
                _targetMaterials = _targetRenderer.sharedMaterials;

                if (isBlur)
                    BlurAtUV(hit.textureCoord, _brushRadius, _brushStrength);
                else
                    PaintAtUV(hit.textureCoord, _brushRadius, _brushStrength, isSubtract, useGreenChannel);
            }

            Repaint();
        }

        private void PaintAtUV(Vector2 uv, float uvRadius, float strength, bool subtract, bool useGreenChannel = false)
        {
            var center = new Vector2Int(
                Mathf.RoundToInt(uv.x * _texWidth),
                Mathf.RoundToInt(uv.y * _texHeight));

            var pixelRadius = Mathf.RoundToInt(uvRadius * _texWidth);

            for (var y = center.y - pixelRadius; y <= center.y + pixelRadius; y++)
            {
                for (var x = center.x - pixelRadius; x <= center.x + pixelRadius; x++)
                {
                    if (x < 0 || x >= _texWidth || y < 0 || y >= _texHeight) continue;

                    var dist = Vector2.Distance(new Vector2(x, y), center);

                    if (dist <= pixelRadius)
                    {
                        var falloff = 1f - Mathf.Clamp01(dist / pixelRadius);
                        var paintVal = strength * falloff;
                        var cur = _maskTexture.GetPixel(x, y);

                        float newVal;

                        if (useGreenChannel)
                        {
                            newVal = subtract
                                ? Mathf.Clamp01(cur.g - paintVal)
                                : Mathf.Clamp01(cur.g + paintVal);

                            _maskTexture.SetPixel(x, y, new Color(cur.r - newVal, newVal, cur.b, 1f));
                        }
                        else
                        {
                            newVal = subtract
                                ? Mathf.Clamp01(cur.r - paintVal)
                                : Mathf.Clamp01(cur.r + paintVal);

                            _maskTexture.SetPixel(x, y, new Color(newVal, cur.g - newVal, cur.b, 1f));
                        }
                    }
                }
            }

            _maskTexture.Apply();
            
            foreach (var material in _targetMaterials)
            {
                material.SetTexture(DisplacementMask, _maskTexture);
                material.SetInt(IsPainting, _showPreview ? 1 : 0);
            }
        }

        private void BlurAtUV(Vector2 uv, float uvRadius, float strength = 1.0f)
        {
            var pixelRadius = Mathf.RoundToInt(uvRadius * _texWidth);
            var pixels = _maskTexture.GetPixels();
            var blurred = new Color[pixels.Length];

            System.Array.Copy(pixels, blurred, pixels.Length);

            var center = new Vector2Int(
                Mathf.RoundToInt(uv.x * _texWidth),
                Mathf.RoundToInt(uv.y * _texHeight));

            for (var y = center.y - pixelRadius; y <= center.y + pixelRadius; y++)
            {
                for (var x = center.x - pixelRadius; x <= center.x + pixelRadius; x++)
                {
                    if (x < 0 || x >= _texWidth || y < 0 || y >= _texHeight) continue;

                    var distToCenter = Vector2.Distance(new Vector2(x, y), center);
                    var falloff = 1f - Mathf.Clamp01(distToCenter / pixelRadius);

                    if (falloff <= 0f) continue;

                    float sumR = 0f, sumG = 0f;
                    int count = 0;

                    for (var ky = -1; ky <= 1; ky++)
                    {
                        for (var kx = -1; kx <= 1; kx++)
                        {
                            var px = Mathf.Clamp(x + kx, 0, _texWidth - 1);
                            var py = Mathf.Clamp(y + ky, 0, _texHeight - 1);
                            var c = pixels[py * _texWidth + px];

                            sumR += c.r;
                            sumG += c.g;
                            count++;
                        }
                    }

                    var avgR = sumR / count;
                    var avgG = sumG / count;

                    var original = pixels[y * _texWidth + x];
                    var blurAmount = Mathf.Pow(falloff * strength, 0.5f);
                    var targetR = Mathf.Lerp(original.r, avgR, blurAmount);
                    var targetG = Mathf.Lerp(original.g, avgG, blurAmount);

                    blurred[y * _texWidth + x] = new Color(targetR, targetG, original.b, 1f);
                }
            }

            _maskTexture.SetPixels(blurred);
            _maskTexture.Apply();
            
            foreach (var material in _targetMaterials)
            {
                material.SetTexture(DisplacementMask, _maskTexture);
                material.SetInt(IsPainting, _showPreview ? 1 : 0);
            }
        }
    }
}
