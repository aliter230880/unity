using System;
using UnityEngine;

namespace MagicAvatarCreator
{
    public class CameraFocusController : MonoBehaviour
    {
        public Transform headFocusObject;
        public Transform bodyFocusObject;
        public Transform faceFocusObject;

        public float headFocusCameraDistance;
        public float bodyFocusCameraDistance;
        public float faceFocusCameraDistance;

        public Transform GetFocusObject(MagicAvatarManager.BlendShapeType contentType)
        {
            return contentType switch
            {
                MagicAvatarManager.BlendShapeType.Head => headFocusObject,
                MagicAvatarManager.BlendShapeType.Body => bodyFocusObject,
                MagicAvatarManager.BlendShapeType.Face => faceFocusObject,
                _ => throw new ArgumentOutOfRangeException(nameof(contentType), contentType, null)
            };
        }

        public float GetCameraDistance(MagicAvatarManager.BlendShapeType contentType)
        {
            return contentType switch
            {
                MagicAvatarManager.BlendShapeType.Head => headFocusCameraDistance,
                MagicAvatarManager.BlendShapeType.Body => bodyFocusCameraDistance,
                MagicAvatarManager.BlendShapeType.Face => faceFocusCameraDistance,
                _ => throw new ArgumentOutOfRangeException(nameof(contentType), contentType, null)
            };
        }
    }
}
