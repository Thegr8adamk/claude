using UnityEngine;

namespace RexMagnum
{
    /// <summary>Volume groups from Settings > Sound (saved in PlayerPrefs). Master = AudioListener.volume.</summary>
    public static class AudioMix
    {
        public static float Music = 1f, Voice = 1f, Effects = 1f;
        public static bool Subtitles = true;

        /// <summary>Music multiplier, 1 or down to 0.6 while a voice line plays (RexVoice.Speaking): the words stay
        /// clear without an AudioMixer asset. Dips in 0.25 s, comes back over 0.8 s after the line. Music sources
        /// multiply it in (GameFlow day + fight music; DayNight night music).</summary>
        public static float Duck
        {
            get
            {
                float t = Time.unscaledTime, d;
                if (t < RexVoice.SpeakingUntil) d = Mathf.Clamp01((t - RexVoice.SpeakingSince) / 0.25f);
                else d = Mathf.Clamp01(1f - (t - RexVoice.SpeakingUntil) / 0.8f);
                return Mathf.Lerp(1f, 0.6f, d);
            }
        }

        // before any Start: the boss / elevator / music sources read the saved volumes, not the defaults
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init() => Load();

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
