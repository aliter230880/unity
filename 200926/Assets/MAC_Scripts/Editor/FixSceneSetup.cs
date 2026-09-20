using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MagicAvatarCreator.EditorTools
{
    /// <summary>
    /// Run from Unity menu: Tools > Magic Tools > Fix Scene Setup
    /// Fixes duplicate avatars and UI layout in the current scene.
    /// </summary>
    public static class FixSceneSetup
    {
        [MenuItem("Tools/Magic Tools/Fix Scene Setup", priority = 100)]
        public static void Execute()
        {
            int fixes = 0;

            // 1. Deactivate duplicate avatars
            var avatars = Object.FindObjectsByType<AvatarObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var manager = Object.FindAnyObjectByType<MagicAvatarManager>();
            foreach (var avatar in avatars)
            {
                if (manager != null && avatar == manager.CurrentAvatar) continue;
                Undo.RecordObject(avatar.gameObject, "Deactivate duplicate avatar");
                avatar.gameObject.SetActive(false);
                fixes++;
                Debug.Log($"[FixSceneSetup] Deactivated duplicate avatar: {avatar.gameObject.name}");
            }

            // 2. Find and fix Canvas layout
            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                // Add CanvasScaler if missing
                if (canvas.GetComponent<CanvasScaler>() == null)
                {
                    var scaler = Undo.AddComponent<CanvasScaler>(canvas.gameObject);
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920, 1080);
                    scaler.matchWidthOrHeight = 0.5f;
                    fixes++;
                    Debug.Log("[FixSceneSetup] Added CanvasScaler");
                }

                // Add GraphicRaycaster if missing
                if (canvas.GetComponent<GraphicRaycaster>() == null)
                {
                    Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);
                    fixes++;
                    Debug.Log("[FixSceneSetup] Added GraphicRaycaster");
                }

                // Fix canvas RectTransform
                var canvasRect = canvas.GetComponent<RectTransform>();
                if (canvasRect != null)
                {
                    Undo.RecordObject(canvasRect, "Fix canvas layout");
                    canvasRect.anchorMin = Vector2.zero;
                    canvasRect.anchorMax = Vector2.one;
                    canvasRect.offsetMin = Vector2.zero;
                    canvasRect.offsetMax = Vector2.zero;
                }
            }

            // 3. Find Base UI and fix its layout
            var baseUI = GameObject.Find("Base UI");
            if (baseUI != null)
            {
                // Activate if inactive
                if (!baseUI.activeSelf)
                {
                    Undo.RecordObject(baseUI, "Activate Base UI");
                    baseUI.SetActive(true);
                    fixes++;
                    Debug.Log("[FixSceneSetup] Activated Base UI");
                }

                // Fix RectTransform to left side
                var rect = baseUI.GetComponent<RectTransform>();
                if (rect == null)
                    rect = baseUI.GetComponentInChildren<RectTransform>(true);

                if (rect != null)
                {
                    Undo.RecordObject(rect, "Fix Base UI layout");
                    rect.anchorMin = new Vector2(0, 0);
                    rect.anchorMax = new Vector2(0.35f, 1);
                    rect.offsetMin = new Vector2(10, 10);
                    rect.offsetMax = new Vector2(-10, -10);
                    fixes++;
                    Debug.Log("[FixSceneSetup] Anchored Base UI to left 35%");
                }
            }

            // 4. Find UIManager and fix camera
            var uiManager = Object.FindAnyObjectByType<UIManager>();
            if (uiManager != null)
            {
                var cam = Camera.main;
                if (cam != null && uiManager.avatarHolder != null)
                {
                    // Offset camera to show avatar on right side
                    Undo.RecordObject(cam.transform, "Fix camera position");
                    cam.transform.position = new Vector3(1.5f, cam.transform.position.y, cam.transform.position.z);
                    fixes++;
                    Debug.Log("[FixSceneSetup] Offset camera to show avatar on right");
                }
            }

            EditorUtility.DisplayDialog("Fix Scene Setup",
                $"Applied {fixes} fixes. Enter Play mode to see changes.", "OK");
        }
    }
}
