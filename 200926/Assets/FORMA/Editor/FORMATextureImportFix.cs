using UnityEditor;

namespace Forma.Editor
{
    public sealed class FORMATextureImportFix : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("Assets/Resources/FORMA/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
        }
    }
}
