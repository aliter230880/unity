using UnityEngine;
using UnityEngine.UI;

namespace MagicAvatarCreator
{
    /// <summary>
    /// Attach to any GameObject in the scene. Fixes duplicate avatars and UI layout at runtime.
    /// Can be added during Play mode via Add Component.
    /// </summary>
    public class RuntimeSceneFixer : MonoBehaviour
    {
        [Header("Settings")]
        public float uiLeftPercent = 0.35f;
        public float cameraXOffset = 1.5f;

        private void Start()
        {
            FixNow();
        }

        [ContextMenu("Fix Scene Now")]
        public void FixNow()
        {
            Debug.Log("[RuntimeSceneFixer] Starting scene fix...");

            // 1. Destroy duplicate avatars
            var avatars = FindObjectsByType<AvatarObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var manager = FindAnyObjectByType<MagicAvatarManager>();
            int destroyed = 0;

            foreach (var avatar in avatars)
            {
                if (manager != null && avatar == manager.CurrentAvatar) continue;
                Debug.Log("[RuntimeSceneFixer] Destroying duplicate: " + avatar.gameObject.name);
                Destroy(avatar.gameObject);
                destroyed++;
            }

            // 2. Fix Canvas layout
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                // Add CanvasScaler if missing
                if (canvas.GetComponent<CanvasScaler>() == null)
                {
                    var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920, 1080);
                    scaler.matchWidthOrHeight = 0.5f;
                    Debug.Log("[RuntimeSceneFixer] Added CanvasScaler");
                }

                // Add GraphicRaycaster if missing
                if (canvas.GetComponent<GraphicRaycaster>() == null)
                {
                    canvas.gameObject.AddComponent<GraphicRaycaster>();
                    Debug.Log("[RuntimeSceneFixer] Added GraphicRaycaster");
                }

                // Fix canvas to fill screen
                var canvasRect = canvas.GetComponent<RectTransform>();
                if (canvasRect != null)
                {
                    canvasRect.anchorMin = Vector2.zero;
                    canvasRect.anchorMax = Vector2.one;
                    canvasRect.offsetMin = Vector2.zero;
                    canvasRect.offsetMax = Vector2.zero;
                }
            }

            // 3. Fix Base UI layout
            var baseUI = GameObject.Find("Base UI");
            if (baseUI != null)
            {
                if (!baseUI.activeSelf)
                {
                    baseUI.SetActive(true);
                    Debug.Log("[RuntimeSceneFixer] Activated Base UI");
                }

                var rect = baseUI.GetComponent<RectTransform>();
                if (rect == null)
                    rect = baseUI.GetComponentInChildren<RectTransform>(true);

                if (rect != null)
                {
                    rect.anchorMin = new Vector2(0, 0);
                    rect.anchorMax = new Vector2(uiLeftPercent, 1);
                    rect.offsetMin = new Vector2(10, 10);
                    rect.offsetMax = new Vector2(-10, -10);
                    Debug.Log("[RuntimeSceneFixer] UI anchored to left " + (uiLeftPercent * 100) + "%");
                }
            }

            // 4. Fix camera position
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(cameraXOffset, cam.transform.position.y, cam.transform.position.z);
                Debug.Log("[RuntimeSceneFixer] Camera offset to X=" + cameraXOffset);
            }

            Debug.Log($"[RuntimeSceneFixer] Done. Destroyed {destroyed} duplicate avatars.");
        }
    }
}
