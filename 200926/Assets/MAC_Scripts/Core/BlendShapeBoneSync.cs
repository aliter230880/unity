using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace MagicAvatarCreator
{
    [ExecuteInEditMode]
    public class BlendShapeBoneSync : MonoBehaviour
    {
        [Serializable]
        public class BoneDrivenByBlendShape
        {
            public Transform bone;
            public SkinnedMeshRenderer sourceMesh;
            public string blendShapeName;
            public Vector3 targetPositionOffset = Vector3.zero;
        
            [SerializeField] private Vector3 targetRotationOffsetEuler = Vector3.zero;
            [SerializeField] private Quaternion targetRotationOffsetQuat = Quaternion.identity;
        
            public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);
        
            [HideInInspector] public Vector3 originalPosition;
            [HideInInspector] public Quaternion originalRotation;
            [HideInInspector] public bool originalSaved = false;
        
            public Quaternion TargetRotationOffset => targetRotationOffsetQuat;
        
            public void SetTargetRotationOffset(Quaternion offset)
            {
                targetRotationOffsetQuat = offset;
                targetRotationOffsetEuler = offset.eulerAngles;
            }
        }

        public List<BoneDrivenByBlendShape> drivenBones = new List<BoneDrivenByBlendShape>();
        public bool updateEveryFrame = true;

        void Start()
        {
            foreach (var item in drivenBones)
            {
                if (item.bone == null) continue;
                if (!item.originalSaved)
                {
                    item.originalPosition = item.bone.localPosition;
                    item.originalRotation = item.bone.localRotation;
                    item.originalSaved = true;
                }
            }
        }

        void LateUpdate()
        {
            if (updateEveryFrame)
                UpdateBones();
        }

        public void UpdateBones()
        {
            foreach (var item in drivenBones)
            {
                if (item.bone == null || item.sourceMesh == null || string.IsNullOrEmpty(item.blendShapeName))
                    continue;

                float weight = GetBlendShapeWeight(item.sourceMesh, item.blendShapeName);
                float t = item.curve.Evaluate(weight);

                Vector3 originalPos = item.originalPosition;
                Quaternion originalRot = item.originalRotation;

                Vector3 targetPos = originalPos + item.targetPositionOffset;
                Quaternion targetRot = originalRot * item.TargetRotationOffset;

                item.bone.localPosition = Vector3.Lerp(originalPos, targetPos, t);
                item.bone.localRotation = Quaternion.Slerp(originalRot, targetRot, t);
            }
        }

        private float GetBlendShapeWeight(SkinnedMeshRenderer smr, string name)
        {
            Mesh mesh = smr.sharedMesh;
            if (mesh == null) return 0f;
            int index = mesh.GetBlendShapeIndex(name);
            if (index < 0) return 0f;
            return smr.GetBlendShapeWeight(index) / 100f;
        }

        // Автосохранение оригинала при назначении кости в редакторе
        private void OnValidate()
        {
            foreach (var item in drivenBones)
            {
                if (item.bone != null && !item.originalSaved)
                {
                    item.originalPosition = item.bone.localPosition;
                    item.originalRotation = item.bone.localRotation;
                    item.originalSaved = true;
                }
            }
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(BlendShapeBoneSync))]
    public class BlendShapeDrivenBonesEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();

            var script = (BlendShapeBoneSync)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Utilities", EditorStyles.boldLabel);

            for (int i = 0; i < script.drivenBones.Count; i++)
            {
                var item = script.drivenBones[i];
                if (item.bone == null) continue;

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Bone: {item.bone.name}", EditorStyles.boldLabel);

                if (GUILayout.Button("Update Original (from current bone pose)"))
                {
                    Undo.RecordObject(script, "Update Original");
                    item.originalPosition = item.bone.localPosition;
                    item.originalRotation = item.bone.localRotation;
                    item.originalSaved = true;
                    EditorUtility.SetDirty(script);
                    Debug.Log(
                        $"Original updated for {item.bone.name}: pos={item.originalPosition}, rot={item.originalRotation.eulerAngles}");
                }

                if (GUILayout.Button("Compute Offset from current pose (blend shape at 100)"))
                {
                    Undo.RecordObject(script, "Compute Offset");
                    Vector3 currentPos = item.bone.localPosition;
                    Quaternion currentRot = item.bone.localRotation;

                    // Смещение позиции
                    item.targetPositionOffset = currentPos - item.originalPosition;

                    // Кватернионное смещение поворота
                    Quaternion offsetRot = currentRot * Quaternion.Inverse(item.originalRotation);
                    item.SetTargetRotationOffset(offsetRot);

                    EditorUtility.SetDirty(script);
                    Debug.Log(
                        $"Offset computed for {item.bone.name}: pos={item.targetPositionOffset}, rot={offsetRot.eulerAngles}");
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            serializedObject.ApplyModifiedProperties();
        }
        
    }
#endif
    
    public static class QuaternionExtensions
    {
        public static Quaternion Diff(this Quaternion to, Quaternion from)
        {
            return to * Quaternion.Inverse(from);
        }
        public static Quaternion Add(this Quaternion start, Quaternion diff)
        {
            return diff * start;
        }
    }
}
