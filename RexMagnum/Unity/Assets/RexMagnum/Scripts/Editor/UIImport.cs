using UnityEditor;

namespace RexMagnum.EditorTools
{
    /// <summary>Pictures in Resources/UI (title picture, touch icons): sharp on screen, no mip maps, alpha kept.</summary>
    public class UIImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/RexMagnum/Resources/UI/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.maxTextureSize = assetPath.Contains("title_bg") ? 2048 : 512;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        }
    }
}
