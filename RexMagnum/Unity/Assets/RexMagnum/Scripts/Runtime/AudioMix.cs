using UnityEngine;

namespace RexMagnum
{
    /// <summary>Volume groups from Settings > Sound (saved in PlayerPrefs). Master = AudioListener.volume.</summary>
    public static class AudioMix
    {
        public static float Music = 1f, Voice = 1f, Effects = 1f;
        public static bool Subtitles = true;

        public static void Load()
        {
            AudioListener.volume = PlayerPrefs.GetFloat("RexMagnum_Volume", 1f);
            Music = PlayerPrefs.GetFloat("RexMagnum_MusicVol", 1f);
            Voice = PlayerPrefs.GetFloat("RexMagnum_VoiceVol", 1f);
            Effects = PlayerPrefs.GetFloat("RexMagnum_SfxVol", 1f);
            Subtitles = PlayerPrefs.GetInt("RexMagnum_Subtitles", 1) == 1;
        }
    }
}
