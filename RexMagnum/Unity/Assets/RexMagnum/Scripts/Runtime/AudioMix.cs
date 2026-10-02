using UnityEngine;

namespace RexMagnum
{
    /// <summary>Volume groups from Settings > Sound (saved in PlayerPrefs). Master = AudioListener.volume.
    /// A small bus model without an AudioMixer asset (Unity cannot make one from script): five buses (Music, Voice,
    /// Effects, World = ambience loops, UI = own Settings volume), each the Settings volume times the blended snapshot gain. Sources read
    /// Music / Voice / Effects / World / UI as their volume factor; a snapshot change blends over 0.5 s.</summary>
    public static class AudioMix
    {
        /// <summary>Settings > Sound volumes (0..1).</summary>
        public static float MusicVolume = 1f, VoiceVolume = 1f, EffectsVolume = 1f;
        /// <summary>Settings > Sound > UI sounds (0..1): menu clicks and HUD cues have their own volume, so Sound
        /// effects at 0% does not mute the menus.</summary>
        public static float UIVolume = 1f;
        public static bool Subtitles = true;

        // bus order in the snapshot table: music, voice, effects, world, UI
        const int Buses = 5;
        public enum Snapshot { Normal, Combat, Indoor, Underwater, Paused, Ending }
        static readonly float[,] Table =
        {
            { 1f,    1f,   1f,    1f,    1f   },     // Normal
            { 1.15f, 1f,   1f,    0.55f, 1f   },     // Combat: the fight music up a bit, the ambience down
            { 0.85f, 1f,   1f,    0.6f,  1f   },     // Indoor: the outside loops further away
            { 0.55f, 0.7f, 0.8f,  0.5f,  1f   },     // Underwater: everything but the menus muffled and lower
            { 0.35f, 1f,   0.3f,  0.3f,  1f   },     // Paused: world + effects down, music low (a low-pass feel)
            { 1f,    1f,   0.6f,  0.4f,  0.8f },     // Ending: the music and the voices carry it
        };
        static readonly float[] bus = { 1f, 1f, 1f, 1f, 1f };
        static readonly float[] busFrom = { 1f, 1f, 1f, 1f, 1f };
        static float blend = 1f;

        /// <summary>The snapshot playing now (it blends in over 0.5 s).</summary>
        public static Snapshot Current { get; private set; }

        /// <summary>Set each frame by the game: Ambience (camera under water, Rex under a roof), GameFlow (fight music).</summary>
        public static bool Underwater, Indoor, Combat;

        /// <summary>The bus volume factors (Settings volume x snapshot gain). AudioSource volume clamps at 1.</summary>
        public static float Music => MusicVolume * bus[0];
        public static float Voice => VoiceVolume * bus[1];
        public static float Effects => EffectsVolume * bus[2];
        public static float World => EffectsVolume * bus[3];
        public static float UI => UIVolume * bus[4];

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
        static void Init()
        {
            Load();
            ResetSnapshot();                                    // editor with Domain Reload off: no snapshot left over
            // NEW GAME / back to the title reloads the scene: a flag set by the last scene (a fight, a roof, under
            // water) would hold its snapshot until something set it again (the title has no fight music to clear Combat)
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            // the driver: blends the snapshots and keeps the soft limiter on the active listener
            var go = new GameObject("AudioMix");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<AudioMixDriver>();
        }

        static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => ResetSnapshot();

        /// <summary>All snapshot flags off and the buses straight at Normal (no blend left over from the last scene).
        /// The Ending snapshot follows EndingSequence.Playing, which that class clears on its own.</summary>
        public static void ResetSnapshot()
        {
            Underwater = Indoor = Combat = false;
            for (int i = 0; i < Buses; i++) { bus[i] = 1f; busFrom[i] = 1f; }
            Current = Snapshot.Normal; blend = 1f;
            Sfx.ResetBusGain();                                 // (Domain Reload off: a stale cached gain skipped the first follow)
        }

        /// <summary>Which snapshot fits now: the pause menu, then the ending, under water, a fight, a roof, else Normal.</summary>
        static Snapshot Pick()
        {
            if (PauseMenu.Open) return Snapshot.Paused;
            if (EndingSequence.Playing) return Snapshot.Ending;
            if (Underwater) return Snapshot.Underwater;
            if (Combat || KingGlorpBoss.Fighting) return Snapshot.Combat;
            if (Indoor) return Snapshot.Indoor;
            return Snapshot.Normal;
        }

        /// <summary>Once a frame (unscaled: it runs while paused): a new snapshot starts a 0.5 s smooth blend from the
        /// gains heard right now (no jump when it changes mid-blend).</summary>
        internal static void Tick(float dt)
        {
            var want = Pick();
            if (want != Current)
            {
                bool resume = Current == Snapshot.Paused;
                Current = want;
                System.Array.Copy(bus, busFrom, Buses);
                blend = 0f;
                // back from the pause menu: effects and world loops are right there at once (a 0.5 s swell made the
                // first shots after RESUME sound quiet); the music still blends up
                if (resume)
                    for (int i = 2; i <= 3; i++) { busFrom[i] = Table[(int)want, i]; bus[i] = busFrom[i]; }
            }
            if (blend >= 1f) return;
            blend = Mathf.Min(1f, blend + dt / 0.5f);
            float k = Mathf.SmoothStep(0f, 1f, blend);
            int row = (int)Current;
            for (int i = 0; i < Buses; i++) bus[i] = Mathf.Lerp(busFrom[i], Table[row, i], k);
        }

        public static void Load()
        {
            AudioListener.volume = PlayerPrefs.GetFloat("RexMagnum_Volume", 1f);
            MusicVolume = PlayerPrefs.GetFloat("RexMagnum_MusicVol", 1f);
            VoiceVolume = PlayerPrefs.GetFloat("RexMagnum_VoiceVol", 1f);
            EffectsVolume = PlayerPrefs.GetFloat("RexMagnum_SfxVol", 1f);
            UIVolume = PlayerPrefs.GetFloat("RexMagnum_UIVol", 1f);
            Subtitles = PlayerPrefs.GetInt("RexMagnum_Subtitles", 1) == 1;
        }
    }

    /// <summary>AudioMix's frame tick, and the soft limiter kept on the active AudioListener (the title camera and the
    /// player camera each have one; checked every second).</summary>
    public class AudioMixDriver : MonoBehaviour
    {
        float nextListener;

        void Update()
        {
            AudioMix.Tick(Time.unscaledDeltaTime);
            Sfx.FollowBus();                                        // playing effects follow the blended bus
            if (Time.unscaledTime < nextListener) return;
            nextListener = Time.unscaledTime + 1f;
            foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (!Application.isMobilePlatform && l.isActiveAndEnabled && l.GetComponent<AudioLimiter>() == null) l.gameObject.AddComponent<AudioLimiter>();   // phones: no managed DSP on the audio thread
        }
    }

    /// <summary>A soft limiter on the whole mix (OnAudioFilterRead on the listener's GameObject gets the final mix):
    /// clean below the knee (makeup gain 1), a tanh curve into the headroom above it, so stacked hits round off
    /// instead of clipping harshly. Audio thread: no Unity API, no allocation.</summary>
    public class AudioLimiter : MonoBehaviour
    {
        const float Knee = 0.7f;

        void OnAudioFilterRead(float[] data, int channels)
        {
            for (int i = 0; i < data.Length; i++)
            {
                float x = data[i];
                float a = x < 0f ? -x : x;
                if (a <= Knee) continue;
                // slope 1 at the knee (no kink), never past 1.0
                float y = Knee + (1f - Knee) * (float)System.Math.Tanh((a - Knee) / (1f - Knee));
                data[i] = x < 0f ? -y : y;
            }
        }
    }
}
