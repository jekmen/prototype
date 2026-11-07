using UnityEditor;
using UnityEngine;

namespace SpaceAI2.Editor
{
    internal class ExplosionFlipbookImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            bool interpolated = assetPath == "Assets/SpaceAI2/Runtime/Effects/Resources/ExplosionGenerated64.png";
            if (!interpolated && assetPath != "Assets/SpaceAI2/Runtime/Effects/Resources/ExplosionGenerated.png") return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = interpolated ? 4096 : 2048;
        }
    }
}
