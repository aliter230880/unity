using System;
using UnityEngine;
using UnityEngine.Events;

namespace MagicAvatarCreator
{
    [Flags]
    public enum SymmetryFlags
    {
        None = 0,
        Position = 1 << 0,
        Rotation = 1 << 1,
        Strength = 1 << 2,
        Length = 1 << 3,
        AtlasIndex = 1 << 4,
        All = Position | Rotation | Strength | Length
    }
    
    [ExecuteAlways]
    public class HairGuide : MonoBehaviour
    {
        [Header("Guide Parameters")]
        [Header("Curl")]
        [Range(0f, 1f)] public float curlStrength = 0f;
        [Range(0f, 5f)] public float curlTurns = 1f;
        [Range(-1f, 1f)] public float curlDirection = 1f;
        [Header("Strength")]
        [Range(0f, 1f)] 
        public float strandDirectionStrength = 0.6f;
        [Range(-1f, 1f)] 
        public float tipDirectionStrength = -1f;
        [Range(0.001f, 0.5f)]
        public float customLength = 0f;
        [Header("Rendering")]
        [Range(0, 3)] public int atlasIndex = 0;
        
        [Header("Symmetry")]
        public bool enableSymmetryEditing = true;
        public SymmetryFlags symmetryMask = SymmetryFlags.Position | SymmetryFlags.Rotation | SymmetryFlags.Strength;
        
        [HideInInspector]
        public HairGuide symmetryPartner;
        [HideInInspector]
        public HairPhysicsManager physicsManager;
        private float _lastCustomLength;

        private void OnEnable()
        {
            if (physicsManager == null)
            {
                physicsManager = GetComponentInParent<HairPhysicsManager>();
            }
        }

        private void OnValidate()
        {
            if (physicsManager != null && physicsManager.enabled)
                physicsManager.Init();
        }
  
        private void OnDrawGizmos()
        {
            if (!enabled || physicsManager == null) return;

            Gizmos.color = new Color(1f, 0.8f, 0f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, physicsManager.rootSphereRadius);
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.2f);
            Gizmos.DrawSphere(transform.position, physicsManager.rootSphereRadius);

            Vector3 dir = transform.up;
            float arrowLen = 0.06f;
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, dir * arrowLen);
        
            Vector3 arrowTip = transform.position + dir * arrowLen;
            Vector3 right = Vector3.Cross(dir, Vector3.up).normalized;
            Vector3 up = Vector3.Cross(right, dir);
            float arrowHeadSize = 0.01f;
            Gizmos.DrawLine(arrowTip, arrowTip - dir * arrowHeadSize + right * arrowHeadSize);
            Gizmos.DrawLine(arrowTip, arrowTip - dir * arrowHeadSize - right * arrowHeadSize);
            Gizmos.DrawLine(arrowTip, arrowTip - dir * arrowHeadSize + up * arrowHeadSize);
            Gizmos.DrawLine(arrowTip, arrowTip - dir * arrowHeadSize - up * arrowHeadSize);
        }
        
        private void OnDrawGizmosSelected()
        {
            if (symmetryPartner != null)
            {
                Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.6f);
                Gizmos.DrawLine(transform.position, symmetryPartner.transform.position);
            }
        }
    }
}