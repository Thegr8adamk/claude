using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Ambient sound and Rex's body sounds (no builder step needed: it attaches itself when a scene with Rex loads).
    ///  - Beach waves near the shore (the resort beach, or the sea within 40 m anywhere), jungle insects and birds
    ///    inland; the jungle is louder at night.
    ///  - Footsteps (flip-flops) timed by Rex's ground speed, by surface: sand on low terrain by the beach, grass on
    ///    other terrain, tile on buildings. Swim strokes while he swims. A splash when he goes into water.
    ///  - World filters: muffled (low-pass) with the camera under water, Cave reverb in the cave and the tunnels, Room
    ///    reverb under a roof (hotel). Only on the world sounds (Sfx pool + these loops), never on the listener:
    ///    music, voices and UI stay clear.
    /// Clips: Resources/Sfx/amb_beach, amb_jungle, footstep, footstep_sand, footstep_grass, swim_stroke, splash (silent if
    /// missing). Volume: Settings > Sound > Effects.
    /// </summary>
    public class Ambience : MonoBehaviour
    {
        AudioSource beach, jungle;
        CharacterController cc;
        RexController ctrl;
        Transform rex;
        float stepDist, nextStroke;
        bool wasInWater, roofed;
        float nextRoofCheck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            // NEW GAME reloads the scene (a new Rex): attach again on every load. -= first: never subscribed twice.
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            Attach();
        }
        static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => Attach();

        static void Attach()
        {
            var r = Object.FindAnyObjectByType<RexController>();
            if (r != null && r.GetComponent<Ambience>() == null) r.gameObject.AddComponent<Ambience>();
        }

        void Start()
        {
            rex = transform;
            cc = GetComponent<CharacterController>();
            ctrl = GetComponent<RexController>();
            // the loops get their own child object: a filter on Rex's object would also muffle his voice source
            loops = new GameObject("AmbienceLoops");
            loops.transform.SetParent(transform, false);
            beach = Loop("amb_beach");
            jungle = Loop("amb_jungle");
            loopLowPass = loops.AddComponent<AudioLowPassFilter>();
            loopLowPass.cutoffFrequency = 22000f;
            loopLowPass.enabled = false;
        }

        GameObject loops;
        AudioLowPassFilter loopLowPass;
        float shore, nextShoreCheck;
        Terrain[] terrains;
        float nextTerrainFind;

        /// <summary>0..1: how near the sea is. Three rings (10, 25, 40 m, 8 directions) around Rex: a point on
        /// terrain under 1 m (or off every terrain: open sea) is sea. Full at 10 m, gone past 40 m; less high up
        /// (a cliff top 30 m over the surf) and none underground (the cave tunnels).</summary>
        float ShoreNear(Vector3 p)
        {
            if (p.y < -5f) return 0f;
            if (terrains == null || terrains.Length == 0 || Time.time > nextTerrainFind)
            {
                nextTerrainFind = Time.time + 10f;           // zones can load later
                terrains = Terrain.activeTerrains;
            }
            if (terrains == null || terrains.Length == 0) return 0f;
            float nearest = -1f;
            for (int ring = 0; ring < 3 && nearest < 0f; ring++)
            {
                float rad = ring == 0 ? 10f : ring == 1 ? 25f : 40f;
                for (int k = 0; k < 8; k++)
                {
                    float ang = k * Mathf.PI / 4f;
                    Vector3 q = p + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                    if (GroundY(q) < 1f) { nearest = rad; break; }
                }
            }
            if (nearest < 0f) return 0f;
            float height = Mathf.Clamp01(1f - (p.y - 2f) / 30f);
            return Mathf.Clamp01(1f - (nearest - 10f) / 40f) * height;
        }

        /// <summary>Terrain height at q (world y); off every terrain: -10 (the open sea).</summary>
        float GroundY(Vector3 q)
        {
            foreach (var t in terrains)
            {
                if (t == null || t.terrainData == null) continue;
                Vector3 o = t.transform.position, size = t.terrainData.size;
                if (q.x < o.x || q.z < o.z || q.x > o.x + size.x || q.z > o.z + size.z) continue;
                return t.SampleHeight(q) + o.y;
            }
            return -10f;
        }

        AudioSource Loop(string clipName)
        {
            var clip = Sfx.Get(clipName);
            if (clip == null) return null;
            var s = loops.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.spatialBlend = 0f;            // all around the listener
            s.volume = 0f;
            s.time = Random.Range(0f, clip.length);   // the two loops never start in step
            s.Play();
            return s;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool title = GameFlow.Busy;
            Vector3 p = rex.position;
            // beach: within ~60 m of sea level and outside the hotel; jungle: inland and higher up
            float low = Mathf.Clamp01(1f - (p.y - 1f) / 12f);
            // under a roof (hotel, cave): outside sound is muffled; checked twice a second. The roof must be within
            // ~6 m over the head (a pool umbrella or a palm 20 m up is not a room)
            if (Time.time > nextRoofCheck)
            {
                nextRoofCheck = Time.time + 0.5f;
                roofed = Physics.Raycast(p + Vector3.up * 1.8f, Vector3.up, 6f, ~0, QueryTriggerInteraction.Ignore);
            }
            float outside = roofed ? 0.25f : 1f;
            // ...and only near the beach itself: low inland ground (golf course, cave mouth) is not the shore.
            // Flat distance to the beach zone centre: full within 60 m, gone at 150 m
            Vector3 toBeach = p - GameGuide.Zones[2].at;
            toBeach.y = 0f;
            float nearBeach = Mathf.Clamp01(1f - (toBeach.magnitude - 60f) / 90f);
            // any other shore: the sea (terrain under ~1 m, or no terrain) within 40 m, sampled twice a second
            if (Time.time > nextShoreCheck) { nextShoreCheck = Time.time + 0.5f; shore = ShoreNear(p); }
            float wantBeach = title ? 0f : 0.35f * Mathf.Max(low * nearBeach, shore) * outside;
            float wantJungle = title ? 0f : (0.22f + 0.18f * DayNight.Night) * Mathf.Clamp01((p.y - 4f) / 10f + (1f - low) * 0.5f) * outside;
            if (beach != null) beach.volume = Mathf.MoveTowards(beach.volume, wantBeach * AudioMix.Effects, dt * 0.2f);
            if (jungle != null) jungle.volume = Mathf.MoveTowards(jungle.volume, wantJungle * AudioMix.Effects, dt * 0.2f);
            ListenerFX(title);
            if (title || cc == null) return;

            // footsteps: one every ~0.55 m walking, longer strides when running
            bool inWater = WaterVolume.Depth(p) > 0.5f;
            Vector3 v = cc.velocity;
            float speed = new Vector2(v.x, v.z).magnitude;
            if (cc.isGrounded && !inWater && speed > 0.5f)
            {
                stepDist += speed * dt;
                float stride = speed > 4.5f ? 1.1f : 0.6f;
                if (stepDist > stride)
                {
                    stepDist = 0f;
                    Sfx.Play(StepClip(p), p, speed > 4.5f ? 0.5f : 0.3f);
                }
            }
            else stepDist = 0.3f;           // first step comes soon after landing / leaving water
            // swimming: a stroke splash about every 1.1 s while he moves
            if (ctrl != null && ctrl.IsSwimming && speed > 0.4f && Time.time > nextStroke)
            {
                nextStroke = Time.time + 1.1f;
                Sfx.Play("swim_stroke", p + Vector3.up, 0.45f);
            }
            if (inWater && !wasInWater && Mathf.Abs(v.y) + speed > 1.5f) Sfx.Play("splash", p, 0.8f);
            wasInWater = inWater;
        }

        // ------------------------------------------------------------ world filters (no AudioMixer asset)

        AudioListener listener;
        float nextListener, nextUnder, cutoff = 22000f;
        bool camUnder;

        /// <summary>A low-pass while the camera (the active AudioListener) is under water, Room reverb while Rex is under
        /// a roof. Set on the world sounds only (Sfx pool sources, the ambience loops): music, voices and UI stay dry.</summary>
        void ListenerFX(bool title)
        {
            float now = Time.unscaledTime;
            if (now > nextListener || (listener != null && !listener.isActiveAndEnabled))
            {
                nextListener = now + 2f;      // the title camera and the player camera can each have a listener
                listener = null;
                foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                    if (l.isActiveAndEnabled) { listener = l; break; }
            }
            if (now > nextUnder)
            {
                nextUnder = now + 0.1f;
                camUnder = !title && listener != null && WaterVolume.Depth(listener.transform.position) > 0.05f;
            }
            // into the water fast (~0.3 s), out a bit slower
            float want = camUnder ? 700f : 22000f;
            cutoff = Mathf.MoveTowards(cutoff, want, Time.unscaledDeltaTime * (camUnder ? 70000f : 40000f));
            Sfx.SetWorldFX(cutoff, title || camUnder ? AudioReverbPreset.Off : Place());
            if (loopLowPass != null)
            {
                // indoors the outside loops are muffled a bit too (heard through walls and windows)
                float c = Mathf.Min(cutoff, roofed ? 3000f : 22000f);
                loopLowPass.cutoffFrequency = c;
                loopLowPass.enabled = c < 21500f;
            }
        }

        /// <summary>The reverb for where Rex is: Cave in the cave tunnels (deep underground) or the cave zone, Room
        /// under a roof (the hotel, the temple), else none.</summary>
        AudioReverbPreset Place()
        {
            Vector3 p = rex.position;
            if (p.y < -20f || (roofed && GameGuide.InZone(GameGuide.Zones[6], p))) return AudioReverbPreset.Cave;
            return roofed ? AudioReverbPreset.Room : AudioReverbPreset.Off;
        }

        /// <summary>What is under Rex's feet: terrain near sea level = beach sand, other terrain = grass,
        /// anything else (hotel, paths, decks) = tile.</summary>
        static string StepClip(Vector3 p)
        {
            if (!Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, out RaycastHit g, 1.2f, ~0, QueryTriggerInteraction.Ignore))
                return "footstep";
            if (!(g.collider is TerrainCollider)) return "footstep";
            return g.point.y < 4f ? "footstep_sand" : "footstep_grass";
        }
    }
}
