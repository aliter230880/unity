using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace MagicAvatarCreator
{
    [System.Serializable]
    public class MaskCombination
    {
        [SerializeField] public string name;
        [SerializeField] public string targetClothGuid;
        [SerializeField] public List<string> requiredClothGuids = new();
        [SerializeField] public Texture2D maskTexture;
    }
    
    [System.Serializable]
    public class ClothMaskData
    {
        [SerializeField] public string clothGuid;
        [SerializeField] public List<MaskCombination> combinations = new();
    }
    
    [CreateAssetMenu(fileName = "New Cloth Set", menuName = "Magic Tools/Magic Avatar Creator/Create New Cloth Set", order = 1)]
    public class AvatarClothSet : ScriptableObject
    {
        [SerializeField] public List<ClothElement> clothElements;
        [SerializeField] public List<ClothMaskData> clothMaskData = new();

        public ClothMaskData GetMaskData(string clothGuid)
        {
            return clothMaskData.Find(data => data.clothGuid == clothGuid);
        }

        public void EnsureMaskDataExists(string clothGuid)
        {
            if (GetMaskData(clothGuid) == null)
            {
                clothMaskData.Add(new ClothMaskData { clothGuid = clothGuid });
            }
        }

        public string GetCombinationName(ClothElement targetCloth, List<ClothElement> otherCloths)
        {
            var combinationName = $"{targetCloth.clothName}_With_";
                
            foreach (var otherCloth in otherCloths)
            {
                combinationName += $"{otherCloth.clothName}_";
            }
                
            combinationName = combinationName.TrimEnd('_');
                
            return FromString(combinationName).ToString();
        }

        private static Guid FromString(string input)
        {
            using (MD5 md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                return new Guid(hash);
            }
        }
        
        private Material _additiveMaterial;
        private Material GetAdditiveMaterial()
        {
            if (_additiveMaterial == null)
            {
                // Создаем материал для additive blending
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }
                
                _additiveMaterial = new Material(shader);
                _additiveMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                _additiveMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                _additiveMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                _additiveMaterial.SetInt("_ZWrite", 0);
                _additiveMaterial.DisableKeyword("_ALPHATEST_ON");
                _additiveMaterial.DisableKeyword("_ALPHABLEND_ON");
                _additiveMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                _additiveMaterial.EnableKeyword("_ALPHAMODULATE_ON");
                _additiveMaterial.renderQueue = 3000;
            }
            return _additiveMaterial;
        }
    }
}
