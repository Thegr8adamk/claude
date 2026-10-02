using System.Collections.Generic;
using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Sound effects from Resources/Sfx/&lt;name&gt; (the ElevenLabs takes, brief section 9). A missing file is
    /// silent, so the game runs before the audio is added. Names:
    /// blob_idle, blob_slide, blob_spit, blob_angry, blob_hit, blob_pop, towel_crack, bottle_break, knife_stab,
    /// golf_swing, swordfish, pistol, harpoon, sniper, coconut, chaos, door_bang, bottle_clink.
    /// </summary>
    public static class Sfx
    {
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        public static AudioClip Get(string name)
        {
            if (!cache.TryGetValue(name, out var c))
            {
                c = Resources.Load<AudioClip>("Sfx/" + name);
                cache[name] = c;
            }
            return c;
        }

        // a small pool of sources (a new GameObject per sound was garbage and Destroy calls in every fight):
        // a free one if there is one, else the one that started longest ago
        const int PoolSize = 24;
        static readonly AudioSource[] pool = new AudioSource[PoolSize];
        static readonly float[] started = new float[PoolSize];
        // each pool source's filters, cached (SetWorldFX runs every frame: no GetComponent per source per frame)
        static readonly AudioLowPassFilter[] lowPass = new AudioLowPassFilter[PoolSize];
        static readonly AudioReverbFilter[] reverb = new AudioReverbFilter[PoolSize];
        static Transform poolRoot;

        /// <summary>The pool index of the source to use.</summary>
        static int Source()
        {
            if (poolRoot == null)
            {
                var root = new GameObject("SfxPool");
                Object.DontDestroyOnLoad(root);
                poolRoot = root.transform;
                System.Array.Clear(pool, 0, PoolSize);
                System.Array.Clear(lowPass, 0, PoolSize);
                System.Array.Clear(reverb, 0, PoolSize);
            }
            int best = 0;
            for (int i = 0; i < PoolSize; i++)
            {
                if (pool[i] == null)
                {
                    var go = new GameObject("Sfx");
                    go.transform.SetParent(poolRoot, false);
                    pool[i] = go.AddComponent<AudioSource>();
                    pool[i].playOnAwake = false;
                    pool[i].spatialBlend = 1f;
                    // world filters on each pool source (not on the listener: music, voices and UI stay dry)
                    var lp = go.AddComponent<AudioLowPassFilter>();
                    lp.cutoffFrequency = 22000f;
                    lp.enabled = false;
                    var rv = go.AddComponent<AudioReverbFilter>();
                    rv.reverbPreset = reverbPreset;
                    rv.enabled = false;
                    lowPass[i] = lp;
                    reverb[i] = rv;
                    best = i;
                    break;
                }
                if (!pool[i].isPlaying) { best = i; break; }
                if (started[i] < started[best]) best = i;
            }
            started[best] = Time.unscaledTime;
            return best;
        }

        /// <summary>One-shot 3D sound. Pitch varies +-8% each time (the same hit twice in a row sounds less canned).
        /// Returns the source (null when the clip is missing) so a caller can cut it short right away; it is pooled,
        /// so do not keep it for later (it may be playing another sound by then).</summary>
        public static AudioSource Play(string name, Vector3 pos, float volume = 1f, float pitch = 1f)
        {
            var c = Get(name);
            if (c == null) return null;
            // own source instead of PlayClipAtPoint: that one has no pitch
            int i = Source();
            var a = pool[i];
            a.Stop();
            a.transform.position = pos;
            a.clip = c;
            a.spatialBlend = 1f;
            a.volume = volume * AudioMix.Effects;
            a.pitch = pitch * Random.Range(0.92f, 1.08f);
            Filter(i, true);
            a.Play();
            return a;
        }

        // a few UI sources: the pitch is per source, so a cue at another pitch takes another source (setting the
        // pitch on a playing one would re-pitch the cue still ringing out)
        const int UISize = 3;
        static readonly AudioSource[] uiSrc = new AudioSource[UISize];
        static readonly float[] uiStarted = new float[UISize];

        /// <summary>A 2D UI sound at the listener (no world filters, not in the world pool): soft cues like a new goal
        /// or a combat tip. PlayOneShot, so a cue does not cut the one before.</summary>
        public static void PlayUI(string name, float volume = 1f, float pitch = 1f)
        {
            var c = Get(name);
            if (c == null) return;
            // same pitch playing: share it; else a quiet one; else the one that started longest ago
            int pick = -1, idle = -1, oldest = 0;
            for (int i = 0; i < UISize; i++)
            {
                if (uiSrc[i] == null)
                {
                    var go = new GameObject("SfxUI");
                    Object.DontDestroyOnLoad(go);
                    uiSrc[i] = go.AddComponent<AudioSource>();
                    uiSrc[i].playOnAwake = false;
                    uiSrc[i].spatialBlend = 0f;
                }
                var u = uiSrc[i];
                if (u.isPlaying && Mathf.Approximately(u.pitch, pitch)) { pick = i; break; }
                if (!u.isPlaying && idle < 0) idle = i;
                if (uiStarted[i] < uiStarted[oldest]) oldest = i;
            }
            if (pick < 0) pick = idle >= 0 ? idle : oldest;
            var a = uiSrc[pick];
            if (a.isPlaying && !Mathf.Approximately(a.pitch, pitch)) a.Stop();     // all busy: the oldest cue ends
            a.pitch = pitch;
            uiStarted[pick] = Time.unscaledTime;
            a.PlayOneShot(c, volume * AudioMix.UI);       // the UI bus (stays up in the Paused snapshot)
        }

        // ------------------------------------------------------------ world filters (Ambience sets them)

        static float cutoff = 22000f;
        static bool reverbOn;
        static AudioReverbPreset reverbPreset = AudioReverbPreset.Room;

        /// <summary>Under water (low-pass cutoff) and under a roof (Room reverb) for the world sounds only: every
        /// pool source follows at once. Music, voices and UI have their own sources.</summary>
        public static void SetWorldFX(float lowPassCutoff, bool roomReverb) =>
            SetWorldFX(lowPassCutoff, roomReverb ? AudioReverbPreset.Room : AudioReverbPreset.Off);

        /// <summary>As above with the reverb by place (Ambience calls this each frame): Cave in the cave and the
        /// tunnels, Room in the hotel, Off (no reverb filter running) outside.</summary>
        public static void SetWorldFX(float lowPassCutoff, AudioReverbPreset preset)
        {
            cutoff = lowPassCutoff;
            reverbOn = preset != AudioReverbPreset.Off;
            bool newPreset = reverbOn && preset != reverbPreset;
            if (reverbOn) reverbPreset = preset;
            // a short sound keeps its reverb for 1 s after it started (the tail is not cut off)
            for (int i = 0; i < PoolSize; i++)
            {
                if (pool[i] == null) continue;
                if (newPreset && reverb[i] != null) reverb[i].reverbPreset = reverbPreset;
                Filter(i, pool[i].isPlaying || Time.unscaledTime - started[i] < 1f);
            }
        }

        static void Filter(int i, bool playing)
        {
            var lp = lowPass[i];
            if (lp != null)
            {
                lp.cutoffFrequency = cutoff;
                lp.enabled = cutoff < 21500f;
            }
            // reverb only on sources that play (an idle reverb filter still costs DSP time)
            var rv = reverb[i];
            if (rv != null) rv.enabled = reverbOn && playing;
        }
    }
}
