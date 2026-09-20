using UnityEngine;
using UnityEngine.UI;

namespace MagicAvatarCreator
{
    /// <summary>
    /// Ensures the Canvas has required components (CanvasScaler, GraphicRaycaster).
    /// Attach to the same GameObject as UIManager or the Canvas.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class UISetupHelper : MonoBehaviour
    {
        private void Awake()
        {
            var canvas = GetComponent<Canvas>();

            // Add CanvasScaler if missing
            if (GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                Debug.Log("[UISetupHelper] Added CanvasScaler (1920x1080, match 0.5)");
            }

            // Add GraphicRaycaster if missing
            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
                Debug.Log("[UISetupHelper] Added GraphicRaycaster");
            }
        }
    }
}
