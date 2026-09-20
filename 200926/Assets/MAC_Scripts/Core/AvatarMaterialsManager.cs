using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace MagicAvatarCreator
{
    [Serializable]
    public class AvatarMaterialsManager
    {
        private static readonly int BaseColorCorrector = Shader.PropertyToID("_Base_Color_Corrector");
        private static readonly int Metallic = Shader.PropertyToID("_Metallic");
        private static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        private static readonly int FrecklesColor = Shader.PropertyToID("_Frackles_Color");
        private static readonly int FrecklesIntensity = Shader.PropertyToID("_Frackles_Intensity");
        private static readonly int FrecklesScale = Shader.PropertyToID("_Frackles_Scale");
        private static readonly int FrecklesLightness = Shader.PropertyToID("_Frackles_Lightness");
        private static readonly int DetailsColor = Shader.PropertyToID("_Details_Color_Corrector");

        public Color skinColorCorrector;
        public float metallic;
        public float smoothness;
        public Color freckles;
        public float frecklesIntensity;
        public float frecklesScale;
        public float frecklesLightness;
        public Color nipplesColor;
        public Color lipsColor;

        private readonly SkinnedMeshRenderer _bodySkinnedMeshRenderer;
        private readonly List<SkinnedMeshRenderer> _skinRenderers = new();
        private readonly Dictionary<SkinnedMeshRenderer, MaterialPropertyBlock> _blocks = new();
        private MaterialPropertyBlock _headMaterialPropertyBlock;
        private MaterialPropertyBlock _bodyMaterialPropertyBlock;

        public AvatarMaterialsManager(SkinnedMeshRenderer bodySkinnedMeshRenderer)
        {
            _bodySkinnedMeshRenderer = bodySkinnedMeshRenderer;
            if (_bodySkinnedMeshRenderer == null) return;

            _headMaterialPropertyBlock = new MaterialPropertyBlock();
            _bodyMaterialPropertyBlock = new MaterialPropertyBlock();
            RegisterSkinRenderer(_bodySkinnedMeshRenderer);

            // Root body renderer contains the skin slots for body, arms, legs and head.
            ReadInitialProperties();
            UpdateMaterialProperties();
        }

        /// <summary>Registers additional skin renderers without including eyes, teeth, hair or clothing.</summary>
        public void RegisterSkinRenderer(SkinnedMeshRenderer renderer)
        {
            if (!IsValidSkinRenderer(renderer) || _skinRenderers.Contains(renderer)) return;
            _skinRenderers.Add(renderer);
            _blocks[renderer] = new MaterialPropertyBlock();
        }

        public void RegisterSkinRenderers(IEnumerable<SkinnedMeshRenderer> renderers)
        {
            if (renderers == null) return;
            foreach (var renderer in renderers) RegisterSkinRenderer(renderer);
        }

        private bool IsValidSkinRenderer(SkinnedMeshRenderer renderer)
        {
            if (renderer == null || renderer.sharedMaterials == null) return false;
            foreach (var material in renderer.sharedMaterials)
                if (material != null && material.HasProperty(BaseColorCorrector)) return true;
            return false;
        }

        private void ReadInitialProperties()
        {
            var materials = _bodySkinnedMeshRenderer.sharedMaterials;
            if (materials == null || materials.Length == 0) return;

            var skinMaterial = FindMaterialWithProperty(materials, BaseColorCorrector) ?? materials[0];
            if (skinMaterial == null) return;

            metallic = skinMaterial.HasProperty(Metallic) ? skinMaterial.GetFloat(Metallic) : 0f;
            smoothness = skinMaterial.HasProperty(Smoothness) ? skinMaterial.GetFloat(Smoothness) : 0.45f;
            skinColorCorrector = skinMaterial.HasProperty(BaseColorCorrector) ? skinMaterial.GetColor(BaseColorCorrector) : Color.white;
            freckles = skinMaterial.HasProperty(FrecklesColor) ? skinMaterial.GetColor(FrecklesColor) : Color.white;
            frecklesIntensity = skinMaterial.HasProperty(FrecklesIntensity) ? skinMaterial.GetFloat(FrecklesIntensity) : 0f;
            frecklesScale = skinMaterial.HasProperty(FrecklesScale) ? skinMaterial.GetFloat(FrecklesScale) : 0f;
            frecklesLightness = skinMaterial.HasProperty(FrecklesLightness) ? skinMaterial.GetFloat(FrecklesLightness) : 0f;
            lipsColor = skinMaterial.HasProperty(DetailsColor) ? skinMaterial.GetColor(DetailsColor) : Color.white;
            nipplesColor = Color.white;
        }

        private static Material FindMaterialWithProperty(Material[] materials, int property)
        {
            foreach (var material in materials)
                if (material != null && material.HasProperty(property)) return material;
            return null;
        }

        public void UpdateMaterialProperties()
        {
            for (var r = _skinRenderers.Count - 1; r >= 0; r--)
            {
                var renderer = _skinRenderers[r];
                if (!IsValidSkinRenderer(renderer))
                {
                    _skinRenderers.RemoveAt(r);
                    _blocks.Remove(renderer);
                    continue;
                }

                var materials = renderer.sharedMaterials;
                var block = _blocks[renderer];
                for (var slot = 0; slot < materials.Length; slot++)
                {
                    var material = materials[slot];
                    if (material == null || !material.HasProperty(BaseColorCorrector)) continue;

                    renderer.GetPropertyBlock(block, slot);
                    if (material.HasProperty(Metallic)) block.SetFloat(Metallic, metallic);
                    if (material.HasProperty(Smoothness)) block.SetFloat(Smoothness, smoothness);
                    if (material.HasProperty(BaseColorCorrector)) block.SetColor(BaseColorCorrector, skinColorCorrector);
                    if (material.HasProperty(FrecklesColor)) block.SetColor(FrecklesColor, freckles);
                    if (material.HasProperty(FrecklesIntensity)) block.SetFloat(FrecklesIntensity, frecklesIntensity);
                    if (material.HasProperty(FrecklesScale)) block.SetFloat(FrecklesScale, frecklesScale);
                    if (material.HasProperty(FrecklesLightness)) block.SetFloat(FrecklesLightness, frecklesLightness);
                    renderer.SetPropertyBlock(block, slot);
                }
            }

#if UNITY_EDITOR
            SceneView.RepaintAll();
#endif
        }
    }
}
