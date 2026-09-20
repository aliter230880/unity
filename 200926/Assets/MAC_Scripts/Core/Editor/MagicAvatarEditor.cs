using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MagicAvatarCreator.EditorTools
{
public class MagicAvatarEditor : EditorWindow
{
    private readonly string[] _avatarTypes = {"Male", "Female"};
    private readonly string[] _mainTabs = { "Morphs", "Cloth", "Hairs", "Skin" };
    private readonly string[] _clothTabs = { "Underwear", "Outerwear", "Footwear" };
    
    public GameObject baseMale;
    public GameObject baseFemale;
    
    private static GUIStyle _captionStyle;
    private static GUIStyle _centeredLabel;
    private static GUIStyle _centeredCaptionLabel;
    
    private static MagicAvatarEditor _managerWindow;
    private bool _stylesInitialized;

    private int _avatarIndex;
    private GameObject _currentAvatar;
    private GameObject _lastAvatar;
    private int _selectedTabMain;
    private int _selectedTabCloth;
    private int _lastSelectedTabCloth;
    private int _selectedTabHair;
    private AvatarObject _avatarObject;
    private int _selectedGridElement;
    private int _lastSelectedGridElement;
    private string _assetFileName = "New Avatar";
    private bool _hasChanges;
    private bool _pendingSaveDialog;
    private bool _pendingDiscard;

    [MenuItem("Tools/Magic Tools/Magic Avatar Creator/Magic Avatar Manager", priority = 0)]
    private static void Init()
    {
        _managerWindow = (MagicAvatarEditor)GetWindow(typeof(MagicAvatarEditor), false, "Magic Avatar Manager");
        _managerWindow.minSize = new Vector2(300 * EditorGUIUtility.pixelsPerPoint, 150 * EditorGUIUtility.pixelsPerPoint);
        _managerWindow.Show();
    }
    
    void OnInspectorUpdate()
    {
        Repaint();
    }

    private void InitStyles()
    {
        if (_captionStyle != null) return;
        
        _captionStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            margin = new RectOffset(),
            padding = new RectOffset(10, 10, 5, 5),
            fontSize = 11,
            fontStyle = FontStyle.Bold
        };
        
        _centeredLabel = new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleCenter,
            margin = new RectOffset(),
            padding = new RectOffset(0, 0, 5, 0),
            fontSize = 11
        };
        
        _centeredCaptionLabel = new GUIStyle(EditorStyles.largeLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            margin = new RectOffset(),
            padding = new RectOffset(0, 0, 5, 0),
            fontSize = 16
        };
    }

    private void CreateNewAvatar(int avatarIndex)
    {
        _currentAvatar = avatarIndex switch
        {
            0 => Instantiate(baseMale),
            1 => Instantiate(baseFemale),
            _ => _currentAvatar
        };

        Selection.activeGameObject = _currentAvatar;
    }
    
    private void SetupCurrentAvatar()
    {
        _avatarObject = _currentAvatar.GetComponent<AvatarObject>();
        _avatarObject.Init();

        if (_avatarObject.currentAvatarData)
        {
            _assetFileName = _avatarObject.currentAvatarData.name;
        }
    }

    private void WearCloth(ClothElement clothElement)
    {
        if (_avatarObject.AvatarClothManager.AttachedClothElementsPlaces.TryGetValue(clothElement.clothPlace, out var clothElementByPlace))
        {
            if (!clothElementByPlace)
            {
                _avatarObject.AvatarClothManager.AttachedClothElementsPlaces.Remove(clothElement.clothPlace);
                _avatarObject.AvatarClothManager.AttachedClothElements.Remove(clothElement.clothGuid);
                WearCloth(clothElement);
                return;
            }
            
            RemoveCloth(clothElementByPlace);
        }
        else
        {
            if (_avatarObject.AvatarClothManager.AttachedClothElements.TryGetValue(clothElement.clothGuid, out var clothElementByGuid))
            {
                if (!clothElementByGuid)
                {
                    _avatarObject.AvatarClothManager.AttachedClothElements.Remove(clothElement.clothGuid);
                    _avatarObject.AvatarClothManager.AttachedClothElementsPlaces.Remove(clothElement.clothPlace);
                    WearCloth(clothElement);
                    return;
                }
                
                RemoveCloth(clothElementByGuid);
            }
        }

        var clothObject = Instantiate(clothElement.gameObject, _avatarObject.clothHolder);

        _avatarObject.AvatarClothManager.AttachedClothElements.TryAdd(clothElement.clothGuid, clothObject.GetComponent<ClothElement>());
        _avatarObject.AvatarClothManager.AttachedClothElementsPlaces.TryAdd(clothElement.clothPlace, clothObject.GetComponent<ClothElement>());
        
        _avatarObject.BlendShapeController.SynchronizeBlendShapes();
        
        _hasChanges = true;
    }

    private void RemoveCloth(ClothElement clothElement)
    {
        if (!_avatarObject.AvatarClothManager.AttachedClothElementsPlaces.TryGetValue(clothElement.clothPlace, out var placedCloth) ||
            placedCloth.clothGuid != clothElement.clothGuid) return;
        
        DestroyImmediate(placedCloth.gameObject);
        
        _avatarObject.AvatarClothManager.AttachedClothElementsPlaces.Remove(clothElement.clothPlace);
        _avatarObject.AvatarClothManager.AttachedClothElements.Remove(clothElement.clothGuid);
        
        _hasChanges = true;
    }

    private void BuildClothGrid(AvatarClothManager.ClothGeneralType generalType)
    {
        const float cellSize = 100f;
        const float spacing = 10f;

        var clothElements = new List<ClothElement>();
        
        foreach (var clothElement in _avatarObject.clothSet.clothElements)
        {
            if (clothElement.generalType != generalType) continue;
            
            clothElements.Add(clothElement);
        }

        if (clothElements.Count == 0)
        {
            GUILayout.Label("No cloths in this category", _centeredLabel);
            return;
        }

        var availableWidth = position.width - cellSize;
        var cellCount = Mathf.Max(1, Mathf.FloorToInt((availableWidth) / (cellSize + spacing)));
        cellCount = Mathf.Min(cellCount, clothElements.Count);

        var totalGridWidth = cellCount * cellSize + (cellCount - 1) * spacing;
        var leftPadding = (position.width - totalGridWidth) / 2f;

        GUILayout.Space(10);

        var buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 9, padding = new RectOffset(2, 2, 2, 2) };

        using (new GUILayout.HorizontalScope())
        {
            using (new GUILayout.VerticalScope())
            {
                for (var i = 0; i < clothElements.Count; i += cellCount)
                {
                    var rowItemCount = Mathf.Min(cellCount, clothElements.Count - i);
                    
                    using (new GUILayout.HorizontalScope())
                    {
                        for (var j = 0; j < rowItemCount; j++)
                        {
                            var index = i + j;
                            var element = clothElements[index];

                            using (new GUILayout.VerticalScope(GUI.skin.box, GUILayout.Width(cellSize), GUILayout.Height(cellSize)))
                            {
                                GUILayout.Label(element.clothName, _centeredLabel);
                                
                                GUILayout.Label(element.clothTexture);

                                if (GUILayout.Button("Wear", buttonStyle))
                                {
                                    WearCloth(element);
                                }

                                if (GUILayout.Button("Remove", buttonStyle))
                                {
                                    RemoveCloth(element);
                                }
                            }
                            
                            GUILayout.Space(spacing);
                        }
                    }
                }
            }
        }
    }
    
    private void ApplyBlendShape(int parameterIndex, float value)
    {
        _avatarObject.rootSkinnedMeshRenderer.SetBlendShapeWeight(parameterIndex, value);

        var avatarBlendShapeName = _avatarObject.rootSkinnedMeshRenderer.sharedMesh.GetBlendShapeName(parameterIndex);

        foreach (var dependentMesh in _avatarObject.dependentSkinnedMeshRenderers)
        {
            var dependentMeshBlendShapeIndex = dependentMesh.sharedMesh.GetBlendShapeIndex(avatarBlendShapeName);

            if (dependentMeshBlendShapeIndex >= 0)
            {
                dependentMesh.SetBlendShapeWeight(dependentMeshBlendShapeIndex, value);
            }
        }

        foreach (var attachedClothElement in _avatarObject.AvatarClothManager.AttachedClothElements)
        {
            var clothSkinnedMesh = attachedClothElement.Value.clothSkinnedMesh;
            var clothBlendShapeIndex = clothSkinnedMesh.sharedMesh.GetBlendShapeIndex(avatarBlendShapeName);

            if (clothBlendShapeIndex >= 0)
            {
                clothSkinnedMesh.SetBlendShapeWeight(clothBlendShapeIndex, value);
            }
        }
    }

    private void SaveAsset()
    {
        if (string.IsNullOrEmpty(_assetFileName))
        {
            EditorUtility.DisplayDialog("Magic Avatar Creator", "Asset File Name is empty!", "OK");
        }
        else
        {
            var savedData = _avatarObject.AvatarDataManager.SaveToScriptableObject(_assetFileName);

            if (savedData)
            {
                _avatarObject.gameObject.name = _assetFileName;
                _avatarObject.currentAvatarData = savedData;
                _hasChanges = false;
                
                if (PrefabUtility.IsPartOfPrefabInstance(_avatarObject.gameObject))
                {
                    GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(_avatarObject.gameObject);
                    
                    PrefabUtility.ApplyPrefabInstance(prefabRoot, InteractionMode.UserAction);
                    AssetDatabase.SaveAssets();
                }
            }
        }
    }
  
    private void OnGUI()
    {
        if (_pendingSaveDialog)
        {
            _pendingSaveDialog = false;
            bool save = EditorUtility.DisplayDialog(
                "Magic Avatar Creator",
                "The changes you have made will be lost if you do not save them. Save it as an asset?",
                "Yes", "No");
            if (save)
                SaveAsset();
        
            _currentAvatar = null;
            _lastAvatar = null;
            _hasChanges = false;
            Repaint();
            return;
        }
        
        var currentSelectedObject = Selection.activeGameObject;

        if (currentSelectedObject && currentSelectedObject.activeInHierarchy&& currentSelectedObject.GetComponent<AvatarObject>())
        {
            _currentAvatar = currentSelectedObject;
            
            if (_lastAvatar != _currentAvatar)
            { 
                _lastAvatar = currentSelectedObject;
                SetupCurrentAvatar();
            }
        }
        else
        {
            if (_currentAvatar )
            {
                if (_hasChanges && !_pendingSaveDialog)
                {
                    _pendingSaveDialog = true;
                    Repaint();
                    return;
                }
            
                _currentAvatar = null;
                _lastAvatar = null;
                _hasChanges = false;
            }
        }

        InitStyles();
        
        if (!_currentAvatar)
        {
            GUILayout.Label("Create New Avatar", _captionStyle);
            
            GUILayout.Space(10);

            using (new GUILayout.HorizontalScope())
            {
                _avatarIndex = EditorGUILayout.Popup(_avatarIndex, _avatarTypes, GUILayout.MaxWidth(50));
            
                if (GUILayout.Button("Create New Avatar"))
                {
                    CreateNewAvatar(_avatarIndex);
                }
            }
        }
        else
        {
            GUILayout.Label(_assetFileName, _centeredCaptionLabel);
            
            GUILayout.Space(10);
            
            using (new GUILayout.HorizontalScope())
            {
                _assetFileName = EditorGUILayout.TextField("", _assetFileName);

                if (GUILayout.Button("Save To Asset"))
                {
                    SaveAsset();
                }
            }
            
            _selectedTabMain = GUILayout.Toolbar(_selectedTabMain, _mainTabs);

            switch (_selectedTabMain)
            {
                case 0:
                    EditorGUI.BeginChangeCheck();
                    
                    using (new GUILayout.VerticalScope(GUI.skin.box))
                    {
                        foreach (var (blendShapeName, blendShapeIndex) in _avatarObject.BlendShapeController.BlendShapesMap)
                        {
                            var shapeValue = _avatarObject.rootSkinnedMeshRenderer.GetBlendShapeWeight(blendShapeIndex);

                            using (new GUILayout.HorizontalScope())
                            {
                                GUILayout.Label(blendShapeName, GUILayout.MinWidth(70));
                                shapeValue = GUILayout.HorizontalSlider(shapeValue, 0, 100, GUILayout.MaxWidth(230));
                                GUILayout.Label(shapeValue.ToString(CultureInfo.InvariantCulture),
                                    GUILayout.MaxWidth(40));

                                ApplyBlendShape(blendShapeIndex, shapeValue);
                            }
                        }
                    }
                    
                    if (EditorGUI.EndChangeCheck())
                    {
                        _hasChanges = true;
                    }
                    break;
                case 1:
                    using (new GUILayout.VerticalScope(GUI.skin.box))
                    {
                        _selectedTabCloth = GUILayout.Toolbar(_selectedTabCloth, _clothTabs);
                        
                        switch (_selectedTabCloth)
                        {
                            case 0:
                                BuildClothGrid(AvatarClothManager.ClothGeneralType.Underwear);
                                break;
                            case 1:
                                BuildClothGrid(AvatarClothManager.ClothGeneralType.Outerwear);
                                break;
                            case 2:
                                BuildClothGrid(AvatarClothManager.ClothGeneralType.Footwear);
                                break;
                        }
                    }
                    break;
                case 2:
                    break;
                case 3:
                    EditorGUI.BeginChangeCheck();
                    
                    _avatarObject.AvatarMaterialsManager.metallic =
                        EditorGUILayout.Slider("Metallic",
                            _avatarObject.AvatarMaterialsManager.metallic, 0, 1);
                    _avatarObject.AvatarMaterialsManager.smoothness =
                        EditorGUILayout.Slider("Smoothness",
                            _avatarObject.AvatarMaterialsManager.smoothness, 0, 1);
                    _avatarObject.AvatarMaterialsManager.skinColorCorrector = 
                        EditorGUILayout.ColorField("Skin Color Corrector", 
                            _avatarObject.AvatarMaterialsManager.skinColorCorrector);
                    _avatarObject.AvatarMaterialsManager.lipsColor = 
                        EditorGUILayout.ColorField("Lips Color Corrector", 
                            _avatarObject.AvatarMaterialsManager.lipsColor);
                    _avatarObject.AvatarMaterialsManager.nipplesColor = 
                        EditorGUILayout.ColorField("Nipples Color Corrector", 
                            _avatarObject.AvatarMaterialsManager.nipplesColor);
                    _avatarObject.AvatarMaterialsManager.freckles = 
                        EditorGUILayout.ColorField("Freckles Color", 
                            _avatarObject.AvatarMaterialsManager.freckles);
                    _avatarObject.AvatarMaterialsManager.frecklesIntensity =
                        EditorGUILayout.Slider("Freckles Intensity",
                            _avatarObject.AvatarMaterialsManager.frecklesIntensity, 0, 1);
                    _avatarObject.AvatarMaterialsManager.frecklesScale =
                        EditorGUILayout.Slider("Freckles Scale",
                            _avatarObject.AvatarMaterialsManager.frecklesScale, 5, 20);
                    _avatarObject.AvatarMaterialsManager.frecklesLightness =
                        EditorGUILayout.Slider("Freckles Lightness",
                            _avatarObject.AvatarMaterialsManager.frecklesLightness, 0, 40);
                    
                    _avatarObject.AvatarMaterialsManager.UpdateMaterialProperties();
                    
                    if (EditorGUI.EndChangeCheck())
                    {
                        _hasChanges = true;
                    }
                    break;
            }
        }
    }
}
}
