using UnityEditor;
using UnityEngine;

namespace RexMagnum.EditorTools
{
    /// <summary>
    /// Import settings for the game audio (Resources/Voice, Sfx, Music), so sounds play on the frame they are
    /// asked for and subtitles stay in sync:
    ///  - voice and sound effects: decompressed on load and preloaded (no decode delay on PlayOneShot);
    ///  - music: streamed (small memory, no long load).
    /// </summary>
    public class AudioImport : AssetPostprocessor
    {
        // preload stays at Unity's default (on)
        void OnPreprocessAudio()
        {
            if (!assetPath.Contains("/RexMagnum/Resources/")) return;
            var ai = (AudioImporter)assetImporter;
            bool music = assetPath.Contains("/Resources/Music/");
            var s = ai.defaultSampleSettings;
            s.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = music ? 0.6f : 0.7f;
            ai.defaultSampleSettings = s;
            ai.loadInBackground = music;
            ai.forceToMono = !music;          // 3D voices and effects: mono is correct and half the size
        }
    }
}
