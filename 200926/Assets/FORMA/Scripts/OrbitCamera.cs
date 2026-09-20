using UnityEngine;

namespace Forma
{
    public class OrbitCamera : MonoBehaviour
    {
        public Transform Target;
        public float Distance = 2.55f;
        public float MinDistance = 0.35f;
        public float MaxDistance = 6f;
        public float Yaw = 6f;
        public float Pitch = 12f;
        public float MinPitch = -8f;
        public float MaxPitch = 72f;
        public float Sensitivity = 0.18f;
        public float ZoomSpeed = 0.35f;
        public float Damping = 8f;

        float _ty, _tdist, _t = 1f, _fromY, _fromDist, _toY, _toDist;
        public float TargetY = 0.95f;

        public void Focus(float y, float dist)
        {
            _fromY = TargetY;
            _fromDist = Distance;
            _toY = y;
            _toDist = dist;
            _t = 0f;
        }

        void LateUpdate()
        {
            if (Target == null) return;
            if (_t < 1f)
            {
                _t = Mathf.Min(1f, _t + Time.deltaTime * 1.6f);
                float e = 1f - Mathf.Pow(1f - _t, 3f);
                TargetY = Mathf.Lerp(_fromY, _toY, e);
                Distance = Mathf.Lerp(_fromDist, _toDist, e);
            }

            if (Input.GetMouseButton(0) && !FormaStudio.PointerOverUi)
            {
                Yaw += Input.GetAxis("Mouse X") * Sensitivity * 18f;
                Pitch -= Input.GetAxis("Mouse Y") * Sensitivity * 18f;
                Pitch = Mathf.Clamp(Pitch, MinPitch, MaxPitch);
            }
            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0 && !FormaStudio.PointerOverUi)
                Distance = Mathf.Clamp(Distance - scroll * ZoomSpeed, MinDistance, MaxDistance);

            var pivot = Target.position + Vector3.up * TargetY;
            var rot = Quaternion.Euler(Pitch, Yaw, 0);
            var desired = pivot + rot * new Vector3(0, 0, -Distance);
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-Damping * Time.deltaTime));
            transform.LookAt(pivot);
        }
    }
}
