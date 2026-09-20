using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MagicAvatarCreator
{
    [CustomEditor(typeof(HairPhysicsManager))]
    public class HairPhysicsManagerEditor : Editor
    {
        private bool isPlacingRoots = false;
        private bool enableSymmetry = true;
        private HairPhysicsManager hairManager;
        private Mesh tempBakedMesh;
        private MeshCollider tempMeshCollider;

        public void OnEnable()
        {
            hairManager = (HairPhysicsManager)target;
            SceneView.duringSceneGui += OnSceneGui;
        }

        public void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            RemoveTempCollision();
            isPlacingRoots = false;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            
            if (GUILayout.Button("Generate SDF Texture (Shader)"))
            {
                hairManager.GenerateSDFInShader();
            }

            EditorGUILayout.Space();
            
            EditorGUI.BeginChangeCheck();
            isPlacingRoots = EditorGUILayout.Toggle("Place Root Markers", isPlacingRoots);
            if (EditorGUI.EndChangeCheck())
            {
                if (isPlacingRoots)
                {
                    SetupTempCollision();
                    SceneView.RepaintAll();
                }
                else
                {
                    RemoveTempCollision();
                }
            }
            
            if (isPlacingRoots)
            {
                EditorGUI.indentLevel++;
                enableSymmetry = EditorGUILayout.Toggle("Symmetry (X axis)", enableSymmetry);
                EditorGUI.indentLevel--;
                EditorGUILayout.HelpBox("Click in Scene View to place root markers. Right-click to stop. Symmetry mirrors across X axis of the source model.", MessageType.Info);
            }
        }

        private void SetupTempCollision()
        {
            if (hairManager.sourceGameObject == null) return;
            
            var skinnedRenderer = hairManager.sourceGameObject.GetComponent<SkinnedMeshRenderer>();
            if (skinnedRenderer == null) return;
            
            // Запекаем меш с учётом скиннинга
            tempBakedMesh = new Mesh();
            skinnedRenderer.BakeMesh(tempBakedMesh, true);
            
            // Добавляем временный MeshCollider
            tempMeshCollider = hairManager.sourceGameObject.AddComponent<MeshCollider>();
            tempMeshCollider.sharedMesh = tempBakedMesh;
            tempMeshCollider.convex = false;
            tempMeshCollider.isTrigger = false;
        }
        
        private void RemoveTempCollision()
        {
            if (tempMeshCollider != null)
            {
                if (Application.isPlaying)
                    Destroy(tempMeshCollider);
                else
                    DestroyImmediate(tempMeshCollider);
                tempMeshCollider = null;
            }
            
            if (tempBakedMesh != null)
            {
                if (Application.isPlaying)
                    Destroy(tempBakedMesh);
                else
                    DestroyImmediate(tempBakedMesh);
                tempBakedMesh = null;
            }
        }
        
        private HairGuide CreateMarker(Vector3 worldPosition, Vector3 worldNormal)
        {
            var marker = new GameObject("RootMarker_" + hairManager.rootMarkers.Length)
            {
                transform =
                {
                    position = worldPosition,
                    parent = hairManager.hairRootsParent,
                    up = worldNormal
                }
            };

            // Добавляем компонент HairGuide
            var guide = marker.AddComponent<HairGuide>();
            guide.physicsManager = hairManager;
    
            ArrayUtility.Add(ref hairManager.rootMarkers, marker.transform);
            EditorUtility.SetDirty(hairManager);

            return guide;
        }
        
        private Vector3 GetReflectedNormal(Vector3 worldNormal)
        {
            if (hairManager.sourceGameObject == null) return worldNormal;
            // Переводим нормаль в локальное пространство модели
            Vector3 localNormal = hairManager.sourceGameObject.transform.InverseTransformDirection(worldNormal);
            // Отражаем по оси X
            localNormal.x = -localNormal.x;
            // Обратно в мировое пространство
            return hairManager.sourceGameObject.transform.TransformDirection(localNormal);
        }
        
        private Vector3 GetSymmetricalPosition(Vector3 worldPos)
        {
            if (hairManager.sourceGameObject == null) return worldPos;

            // Переводим мировую позицию в локальные координаты источника (голова)
            Vector3 localPos = hairManager.sourceGameObject.transform.InverseTransformPoint(worldPos);
            // Отражаем по X
            localPos.x = -localPos.x;
            // Обратно в мировые
            return hairManager.sourceGameObject.transform.TransformPoint(localPos);
        }

        private void OnSceneGui(SceneView sceneView)
        {
            if (hairManager == null) return;
            
            // --- Выбор маркера по Ctrl+Shift+Click ---
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && e.control && e.shift)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    HairGuide guide = hit.collider.GetComponent<HairGuide>();
                    if (guide != null)
                    {
                        Selection.activeGameObject = guide.gameObject;
                        e.Use();
                        return;
                    }
                }
            }
            
            // --- Режим расстановки маркеров (если включён) ---
            if (!isPlacingRoots)
                return;

            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlId);

            // Правая кнопка – выход
            if (e.type == EventType.MouseDown && e.button == 1)
            {
                e.Use();
                isPlacingRoots = false;
                RemoveTempCollision();
                Repaint();
                return;
            }
            
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                e.Use();
                
                var bodyLayer = LayerMask.NameToLayer("MT_AvatarBody");
                var layerMask = 1 << bodyLayer;

                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, layerMask))
                {
                    if (hit.collider.gameObject == hairManager.sourceGameObject)
                    {
                        var guide = CreateMarker(hit.point, hit.normal);
                        
                        if (enableSymmetry)
                        {
                            Vector3 symPos = GetSymmetricalPosition(hit.point);
                            Vector3 symNormal = GetReflectedNormal(hit.normal);
                            if (Vector3.Distance(symPos, hit.point) > 0.001f)
                            {
                                var symmetryGuide = CreateMarker(symPos, symNormal);
                                guide.symmetryPartner = symmetryGuide;
                                symmetryGuide.symmetryPartner = guide;
                            }
                        }
                        EditorUtility.SetDirty(hairManager);
                        hairManager.Init();
                    }
                }
            }
        }
    }
}
