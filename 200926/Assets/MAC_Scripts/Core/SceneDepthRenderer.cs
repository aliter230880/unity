using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MagicAvatarCreator
{
    [ExecuteAlways]
    public class SceneDepthRenderer : MonoBehaviour
    {
        public LayerMask environmentLayers = ~0;
        public RenderTextureFormat depthFormat = RenderTextureFormat.Depth;

        private RenderTexture _sceneDepthRT;
        private Camera _depthCamera;
        private static readonly int SceneDepthTexId = Shader.PropertyToID("_SceneDepthTexture");

        private void OnEnable()
        {
            CreateDepthCamera();
            UpdateRenderTexture();
            Camera.onPreRender += OnPreRenderCallback;
        }

        private void OnDisable()
        {
            Camera.onPreRender -= OnPreRenderCallback;
            ReleaseResources();
        }

        private void OnPreRenderCallback(Camera cam)
        {
            // Рендерим глубину только перед основной камерой игры (или сцены)
            if (cam == Camera.main || cam.CompareTag("MainCamera"))
            {
                RenderSceneDepth();
            }
        }

        private void CreateDepthCamera()
        {
            if (_depthCamera != null) return;

            var go = new GameObject("SceneDepthCamera") { hideFlags = HideFlags.HideAndDontSave };
            _depthCamera = go.AddComponent<Camera>();
            _depthCamera.enabled = false;
        }

        private void UpdateRenderTexture()
        {
            int width = Screen.width;
            int height = Screen.height;
            if (_sceneDepthRT != null && _sceneDepthRT.width == width && _sceneDepthRT.height == height)
                return;

            if (_sceneDepthRT != null)
                _sceneDepthRT.Release();

            _sceneDepthRT = new RenderTexture(width, height, 24, depthFormat);
            _sceneDepthRT.Create();
        }

        private void RenderSceneDepth()
        {
            if (_depthCamera == null || _sceneDepthRT == null)
                return;

            // Копируем настройки основной камеры
            var mainCam = Camera.main;
            if (mainCam == null) return;
            _depthCamera.CopyFrom(mainCam);
            _depthCamera.depthTextureMode = DepthTextureMode.Depth;
            _depthCamera.targetTexture = _sceneDepthRT;
            _depthCamera.cullingMask = environmentLayers;
            _depthCamera.clearFlags = CameraClearFlags.SolidColor;
            _depthCamera.backgroundColor = Color.white; // дальняя плоскость = 1.0 (белый) в глубине

            // Рендерим только глубину (цвет не нужен, но камера всё равно рендерит полный проход)
            _depthCamera.Render();

            // Передаём текстуру в глобальные шейдерные переменные
            Shader.SetGlobalTexture(SceneDepthTexId, _sceneDepthRT);
        }

        private void ReleaseResources()
        {
            if (_depthCamera != null)
            {
                if (Application.isPlaying)
                    Destroy(_depthCamera.gameObject);
                else
                    DestroyImmediate(_depthCamera.gameObject);
                _depthCamera = null;
            }

            if (_sceneDepthRT != null)
            {
                _sceneDepthRT.Release();
                _sceneDepthRT = null;
            }
        }

        private void OnDestroy() => ReleaseResources();
    }
}
