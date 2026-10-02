using UnityEditor;

namespace RexMagnum.EditorTools
{
    /// <summary>Pictures in Resources/UI (title picture, touch icons): sharp on screen, no mip maps, alpha kept.</summary>
    public class UIImport : AssetPostprocessor
    {
        // bump when the rules below change: Unity then reimports the pictures with them
        public override uint GetVersion() => 2;

        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/RexMagnum/Resources/UI/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.npotScale = TextureImporterNPOTScale.None;
            // full-screen pictures (title, island map) stay sharp at 1080p+; small icons stay small
            ti.maxTextureSize = assetPath.Contains("title_bg") || assetPath.Contains("/map.") ? 2048 : 512;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        }
    }
}
