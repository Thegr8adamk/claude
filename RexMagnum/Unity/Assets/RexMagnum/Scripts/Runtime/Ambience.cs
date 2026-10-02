using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Ambient sound and Rex's body sounds (no builder step needed: it attaches itself when a scene with Rex loads).
    ///  - Beach waves near the shore (low ground), jungle insects and birds inland; the jungle is louder at night.
    ///  - Footsteps (flip-flops) timed by Rex's ground speed, by surface: sand on low terrain by the beach, grass on
    ///    other terrain, tile on buildings. Swim strokes while he swims. A splash when he goes into water.
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
            beach = Loop("amb_beach");
            jungle = Loop("amb_jungle");
        }

        AudioSource Loop(string clipName)
        {
            var clip = Sfx.Get(clipName);
            if (clip == null) return null;
            var s = gameObject.AddComponent<AudioSource>();
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
            // under a roof (hotel, cave): outside sound is muffled; checked twice a second
            if (Time.time > nextRoofCheck)
            {
                nextRoofCheck = Time.time + 0.5f;
                roofed = Physics.Raycast(p + Vector3.up * 2f, Vector3.up, 25f, ~0, QueryTriggerInteraction.Ignore);
            }
            float outside = roofed ? 0.25f : 1f;
            float wantBeach = title ? 0f : 0.35f * low * outside;
            float wantJungle = title ? 0f : (0.22f + 0.18f * DayNight.Night) * Mathf.Clamp01((p.y - 4f) / 10f + (1f - low) * 0.5f) * outside;
            if (beach != null) beach.volume = Mathf.MoveTowards(beach.volume, wantBeach * AudioMix.Effects, dt * 0.2f);
            if (jungle != null) jungle.volume = Mathf.MoveTowards(jungle.volume, wantJungle * AudioMix.Effects, dt * 0.2f);
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
