using UnityEditor;
using UnityEngine;

namespace MagicAvatarCreator
{
    [CustomEditor(typeof(HairGuide))]
    public class HairGuideEditor : Editor
    {
        private HairGuide _hairGuide;
        private Vector3 _lastPosition;
        private Vector3 _lastDirection;
        private float _lastStrandStrength;
        private float _lastTipStrength;
        private float _lastCustomLength;
        private int _lastAtlasIndex;
        
        public void OnEnable()
        {
            _hairGuide = (HairGuide)target;
            _lastPosition = _hairGuide.transform.position;
            _lastDirection = _hairGuide.transform.up;
            _lastStrandStrength = _hairGuide.strandDirectionStrength;
            _lastTipStrength = _hairGuide.tipDirectionStrength;
            _lastCustomLength = _hairGuide.customLength;
            _lastAtlasIndex = _hairGuide.atlasIndex;
            
            SceneView.duringSceneGui += OnSceneGui;
        }

        public void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
        }
        
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            bool strengthChanged = false;
            if (!Mathf.Approximately(_hairGuide.strandDirectionStrength, _lastStrandStrength))
            {
                strengthChanged = true;
                _lastStrandStrength = _hairGuide.strandDirectionStrength;
            }
            if (!Mathf.Approximately(_hairGuide.tipDirectionStrength, _lastTipStrength))
            {
                strengthChanged = true;
                _lastTipStrength = _hairGuide.tipDirectionStrength;
            }

            if (strengthChanged && ShouldSync(SymmetryFlags.Strength))
            {
                _hairGuide.symmetryPartner.strandDirectionStrength = _hairGuide.strandDirectionStrength;
                _hairGuide.symmetryPartner.tipDirectionStrength = _hairGuide.tipDirectionStrength;
                EditorUtility.SetDirty(_hairGuide.symmetryPartner);
                _hairGuide.physicsManager.Init();
            }
            
            if (ShouldSync(SymmetryFlags.AtlasIndex) && _hairGuide.atlasIndex != _lastAtlasIndex)
            {
                _hairGuide.symmetryPartner.atlasIndex = _hairGuide.atlasIndex;
                EditorUtility.SetDirty(_hairGuide.symmetryPartner);
                _hairGuide.physicsManager.Init();
                _lastAtlasIndex = _hairGuide.atlasIndex;
            }
            
            bool lengthChanged = !Mathf.Approximately(_hairGuide.customLength, _lastCustomLength);
            if (lengthChanged)
            {
                _lastCustomLength = _hairGuide.customLength;
                if (ShouldSync(SymmetryFlags.Length))
                {
                    _hairGuide.symmetryPartner.customLength = _hairGuide.customLength;
                    EditorUtility.SetDirty(_hairGuide.symmetryPartner);
                    _hairGuide.physicsManager.Init();
                }
            }
        }

        private void OnSceneGui(SceneView sceneView)
        {
            var e = Event.current;
            if (e.type == EventType.MouseUp && e.button == 0)
            {
                bool changed = false;
                
                if (_hairGuide.transform.position != _lastPosition && ShouldSync(SymmetryFlags.Position))
                {
                    Transform head = _hairGuide.physicsManager.sourceGameObject.transform;
                    Vector3 localPos = head.InverseTransformPoint(_hairGuide.transform.position);
                    localPos.x = -localPos.x;
                    Vector3 symWorldPos = head.TransformPoint(localPos);
                    _hairGuide.symmetryPartner.transform.position = symWorldPos;
                    changed = true;
                    _lastPosition = _hairGuide.transform.position;
                }
                
                if (_hairGuide.transform.up != _lastDirection && ShouldSync(SymmetryFlags.Rotation))
                {
                    Transform head = _hairGuide.physicsManager.sourceGameObject.transform;
                    Vector3 localDir = head.InverseTransformDirection(_hairGuide.transform.up);
                    localDir.x = -localDir.x;
                    Vector3 symWorldDir = head.TransformDirection(localDir);
                    _hairGuide.symmetryPartner.transform.up = symWorldDir;
                    changed = true;
                    _lastDirection = _hairGuide.transform.up;
                }
                
                if (changed && _hairGuide.physicsManager != null)
                {
                    _hairGuide.physicsManager.Init();
                }
            }
        }
        
        private bool ShouldSync(SymmetryFlags flag)
        {
            return _hairGuide.enableSymmetryEditing && 
                   _hairGuide.symmetryPartner && 
                   _hairGuide.physicsManager &&
                   (_hairGuide.symmetryMask & flag) != 0;
        }
    }
}