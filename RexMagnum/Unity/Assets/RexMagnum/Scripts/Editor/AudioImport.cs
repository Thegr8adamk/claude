using UnityEditor;
using UnityEngine;

namespace RexMagnum.EditorTools
{
    /// <summary>
    /// Import settings for the game audio (Resources/Voice, Sfx, Music), so sounds play on the frame they are
    /// asked for and subtitles stay in sync:
    ///  - sound effects: decompressed on load and preloaded (no decode delay on PlayOneShot);
    ///  - voice lines (Resources/Voice): compressed in memory (many long lines; Vorbis decodes fast enough to start on time);
    ///  - music and the long ambience loops (Sfx/amb_*): streamed, stereo (they play all around the listener).
    /// </summary>
    public class AudioImport : AssetPostprocessor
    {
        // bump when the rules below change: Unity then reimports the clips with them
        public override uint GetVersion() => 3;

        // preload stays at Unity's default (on)
        void OnPreprocessAudio()
        {
            if (!assetPath.Contains("/RexMagnum/Resources/")) return;
            var ai = (AudioImporter)assetImporter;
            // amb_* loops are long 2D beds: like music (stereo kept, streamed), not like 3D one-shots
            bool music = assetPath.Contains("/Resources/Music/") || System.IO.Path.GetFileName(assetPath).StartsWith("amb_");
            bool voice = assetPath.Contains("/Resources/Voice/");
            var s = ai.defaultSampleSettings;
            // voices: decompressed they would take ~10x the memory (hundreds of lines); short effects stay decompressed
            s.loadType = music ? AudioClipLoadType.Streaming : voice ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = music ? 0.6f : 0.7f;
            ai.defaultSampleSettings = s;
            // web build: the browser decodes every clip whole (AAC); a lower bit rate and an optimised sample rate
            // keep the download smaller (Web.data was 217 MB). PC / phone keep the settings above
            var web = s;
            web.loadType = AudioClipLoadType.CompressedInMemory;
            web.compressionFormat = AudioCompressionFormat.AAC;
            web.quality = music ? 0.4f : 0.3f;
            web.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
            ai.SetOverrideSampleSettings("WebGL", web);
            ai.loadInBackground = music;
            ai.forceToMono = !music;          // 3D voices and effects: mono is correct and half the size
        }
    }
}
