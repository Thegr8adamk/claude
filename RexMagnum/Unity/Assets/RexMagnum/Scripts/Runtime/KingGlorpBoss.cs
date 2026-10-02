using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// King Glorp boss fight at the crashed ship (brief sections 2-4), on top of the normal Blob AI.
    ///  - Starts when Rex comes within 40 m: power ballad (Resources/Music/glorp_ballad), boss bar, intro lines.
    ///  - Absorbs objects as armor: loose props near him fly to his body and stick; each piece takes the hits
    ///    until heavy attacks knock it off. He re-absorbs new pieces from time to time.
    ///  - 3 phases by health: crown spikes grow each phase; phase 2 calls tourist blobs out of the slime;
    ///    phase 3 sends slime shockwaves (jump over them) and he moves faster. Phase 2+ spits marked slime pools around Rex.
    ///  - On death: big pop, kill cam, Rex's win line, then the ending (viral video, agent call, bar tab, "I'm back").
    ///  Saved: PlayerPrefs "RexMagnum_GlorpDefeated". Free play continues after the ending.
    /// </summary>
    [RequireComponent(typeof(Blob))]
    public class KingGlorpBoss : MonoBehaviour
    {
        public const string SaveKey = "RexMagnum_GlorpDefeated";
        public Transform crown;                 // spikes: scaled up per phase
        public Blob[] minionPrefabs;
        public float armorRadius = 25f;
        Blob blob;
        Health health;
        Transform rex;
        bool started, dead;
        /// <summary>True while the boss fight runs (its power ballad plays; the normal fight music stays quiet).</summary>
        public static bool Fighting { get; private set; }
        int phase = 1;
        float lastHitByRex = -999f;
        const float LongShotRange = 20f, LongShotFactor = 0.35f;
        bool longShotTold;
        // range punish: a charge after each long shot, a slime volley when Rex keeps out of reach
        const float ChargeTime = 3f, ChargeFactor = 2f, VolleyAfter = 4f, VolleyEvery = 5f, VolleyRange = 80f;
        float chargeUntil = -1f, farSince = -1f, nextVolley;
        bool charging;
        // weak-point window after each volley / charge: dizzy (stunned) for 2 s, every hit counts 1.5x
        const float DizzyTime = 2f, DizzyFactor = 1.5f, DizzyVolleyRange = 30f;
        float dizzyUntil = -1f;
        bool dizzyTold;
        bool Dizzy => Time.time < dizzyUntil;
        float nextAbsorb, nextWave, nextMinions, nextLine, nextPools;
        // slime pools (phase 2/3): 2-3 marked spots near Rex turn into burning slime for PoolTime seconds
        const float PoolWarn = 1.1f, PoolTime = 6f, PoolRadius = 1.6f, PoolDamage = 5f, PoolTick = 0.5f;
        readonly List<(Transform t, float hp)> armor = new List<(Transform, float)>();
        AudioSource music;

        static readonly string[] GlorpLines =
        {
            "WHO DARES DISTURB THE KING?", "Bow, small damp creature!", "My crown! Do not look at my crown!",
            "I absorb! I ABSORB!", "You are not famous. I checked.", "Glorp is ETERNAL! Glorp is... leaking.",
        };
        const string DeathLine = "Glorp... is... not... eternaaal...";
        // the fight's other lines have named clips: Resources/Voice/Glorp/glorp_<name> (optional, subtitle always)
        static readonly (string line, string clip)[] NamedLines =
        {
            ("Rise, my subjects!", "glorp_rise"), ("My beautiful armor!", "glorp_armor"),
            ("NOW YOU SEE MY FINAL FORM!", "glorp_final"), (DeathLine, "glorp_death"),
        };

        void Start()
        {
            blob = GetComponent<Blob>();
            health = GetComponent<Health>();
            var rc = FindAnyObjectByType<RexController>();
            if (rc != null) rex = rc.transform;
            health.Died += by =>
            {
                // the blob destroys itself about 0.5 s after dying (Blob.PopRoutine), which would stop this coroutine
                // before the ending starts: run it on Rex instead
                MonoBehaviour host = rex != null ? rex.GetComponent<RexController>() : null;
                (host != null ? host : this).StartCoroutine(Victory(by, transform.position));
            };
            // voice first: RexVoice.SayLine plays on the first AudioSource (3D, voice volume), not on the music
            var voice = gameObject.AddComponent<AudioSource>();
            voice.spatialBlend = 1f;
            voice.minDistance = 8f;
            voice.maxDistance = 120f;
            music = gameObject.AddComponent<AudioSource>();
            music.clip = Resources.Load<AudioClip>("Music/glorp_ballad");
            music.loop = true;
            music.spatialBlend = 0f;
            music.volume = 0.6f * AudioMix.Music;
            if (PlayerPrefs.GetInt(SaveKey, 0) == 1) gameObject.SetActive(false);   // already beaten
            health.Damaged += OnDamaged;
        }

        /// <summary>King Glorp's lines, voiced when Resources/Voice/Glorp/line_NN exists (index in GlorpLines) or the
        /// line's named clip (NamedLines); silent with the subtitle when the clip is missing. Returns the line's length.
        /// speaker: the AudioSource's object (default: the boss himself).</summary>
        float Say(string line, GameObject speaker = null)
        {
            if (speaker == null) speaker = gameObject;
            int i = System.Array.IndexOf(GlorpLines, line);
            if (i >= 0) return RexVoice.SayLine(speaker, "KING GLORP", line, $"Voice/Glorp/line_{i + 1:00}");
            foreach (var n in NamedLines)
                if (n.line == line) return RexVoice.SayLine(speaker, "KING GLORP", line, "Voice/Glorp/" + n.clip);   // calls RexVoice.Speaking
            RexVoice.Subtitle?.Invoke("KING GLORP: " + line);
            return 2f;
        }

        void OnDisable() { Fighting = false; }

        void Update()
        {
            if (music != null) music.volume = 0.6f * AudioMix.Music * AudioMix.Duck;
            if (dead || rex == null) return;
            float d = Vector3.Distance(rex.position, transform.position);
            if (!started)
            {
                if (d > 40f) return;
                StartFight();
            }
            else if (d > 90f && Time.time - lastHitByRex > 20f)      // a sniper duel from far away keeps the fight on
            {
                // Rex died (woke in room 304) or ran away: stop the ballad and the boss bar until he comes back
                started = false;
                Fighting = false;
                music.Stop();
                if (charging) { charging = false; blob.speed /= ChargeFactor; blob.bossCharging = false; }
                chargeUntil = -1f;
                farSince = -1f;
                return;
            }
            int p = health.Fraction > 0.66f ? 1 : health.Fraction > 0.33f ? 2 : 3;
            if (p != phase) { phase = p; StartCoroutine(PhaseChange()); }
            if (Time.time > nextAbsorb) { nextAbsorb = Time.time + (phase == 3 ? 12f : 18f); StartCoroutine(Absorb()); }
            if (phase >= 2 && Time.time > nextMinions)
            {
                nextMinions = Time.time + 25f;
                if (Blob.LivingNear(transform.position, 30f) < 7) Minions();     // a long fight does not pile up blobs
            }
            if (phase == 3 && Time.time > nextWave) { nextWave = Time.time + 7f; StartCoroutine(Shockwave()); }
            // phase 2+: slime pools spat on the ground around Rex (not while he snipes from far: the volley covers that)
            if (phase >= 2 && Time.time > nextPools && d < 35f && !blob.IsStunned)
            {
                nextPools = Time.time + (phase == 3 ? 11f : 15f) + Random.Range(-1f, 1f);
                StartCoroutine(SlimePools());
            }
            // charge after a long shot: double speed for 3 s (the multiplier comes off again; phase 3's 1.4x stays)
            bool charge = Time.time < chargeUntil;
            if (charge != charging)
            {
                charging = charge;
                blob.bossCharging = charge;                         // Blob caps the charge at MaxBossChargeSpeed (7 m/s)
                blob.speed *= charge ? ChargeFactor : 1f / ChargeFactor;
                if (!charge) StartDizzy();                          // out of breath after the charge
            }
            // Rex stays out of his 14 m reach for over 4 s: a 3-shot slime volley every ~5 s
            if (d > blob.attackRange && d < VolleyRange)
            {
                if (farSince < 0f) farSince = Time.time;
                if (Time.time - farSince > VolleyAfter && Time.time > nextVolley && !blob.IsStunned && !Dizzy)
                {
                    nextVolley = Time.time + VolleyEvery + Random.Range(-0.5f, 0.5f);
                    StartCoroutine(Volley());
                }
            }
            else farSince = -1f;
            if (Time.time > nextLine) { nextLine = Time.time + 14f; Say(GlorpLines[Random.Range(1, GlorpLines.Length)]); }
            // keep armor pieces stuck to the body (they ride along)
            for (int i = armor.Count - 1; i >= 0; i--) if (armor[i].t == null) armor.RemoveAt(i);
        }

        GUIStyle barStyle;

        void OnGUI()
        {
            if (!started || dead || PhotoAlbum.Capturing) return;      // no boss bar in the photo
            float w = Screen.width, h = Screen.height, s = h / 1080f;
            var r = new Rect(w * 0.25f, 40 * s, w * 0.5f, 22 * s);
            GUI.color = new Color(0, 0, 0, 0.6f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.8f, 0.15f);
            GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, (r.width - 6) * health.Fraction, r.height - 6), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (barStyle == null) barStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            barStyle.fontSize = (int)(18 * s);
            var st = barStyle;                                  // made once: no new GUIStyle per OnGUI call
            GUI.Label(new Rect(r.x, r.y - 26 * s, r.width, 24 * s), $"KING GLORP   {(armor.Count > 0 ? $"ARMOR x{armor.Count}" : "")}", st);
        }

        IEnumerator PhaseChange()
        {
            Say(phase == 2 ? "Rise, my subjects!" : "NOW YOU SEE MY FINAL FORM!");
            nextPools = Mathf.Max(nextPools, Time.time + 6f);       // not on top of the phase change and the minions
            if (crown != null)
            {
                Vector3 a = crown.localScale, b = a * 1.35f;
                for (float t = 0; t < 1f; t += Time.deltaTime) { crown.localScale = Vector3.Lerp(a, b, t); yield return null; }
            }
            if (phase == 3) blob.speed *= 1.4f;
        }

        IEnumerator Absorb()
        {
            var picks = new List<Rigidbody>();
            foreach (var c in Physics.OverlapSphere(transform.position, armorRadius))
            {
                var rb = c.attachedRigidbody;
                // not: blobs, Rex or anything he holds, the cart, a live coconut (it would blow up on his body)
                if (rb == null || rb.isKinematic || rb.GetComponentInParent<Blob>() || rb.GetComponentInParent<RexController>() ||
                    (rex != null && rb.transform.IsChildOf(rex)) || rb.GetComponent<GolfCart>() ||
                    rb.GetComponentInParent<CoconutBomb>()) continue;
                if (!picks.Contains(rb)) picks.Add(rb);
                if (picks.Count >= 4) break;
            }
            if (picks.Count == 0) yield break;
            Say(GlorpLines[3]);
            foreach (var rb in picks)
            {
                rb.isKinematic = true;
                foreach (var col in rb.GetComponentsInChildren<Collider>()) col.enabled = false;
                Vector3 from = rb.position;
                Vector3 local = Random.onUnitSphere * 2.2f; local.y = Mathf.Abs(local.y) * 1.5f + 1f;
                for (float t = 0; t < 1f; t += Time.deltaTime / 0.8f)
                {
                    if (rb == null) break;
                    rb.transform.position = Vector3.Lerp(from, transform.TransformPoint(local), t * t);
                    yield return null;
                }
                if (rb == null) continue;
                rb.transform.SetParent(transform, true);
                armor.Add((rb.transform, 60f));
            }
        }

        /// <summary>Ballad, boss bar, intro lines. From walking within 40 m, or from the first hit at any range.</summary>
        void StartFight()
        {
            started = true;
            Fighting = true;
            if (music.clip != null && !music.isPlaying) music.Play();
            Say(GlorpLines[0]);
            if (rex != null) RexVoice.Say(rex.gameObject, "Glorp", true);
        }

        void OnDamaged(float amount, Vector3 point, GameObject by)
        {
            // a sniper shot from the ship approach (or any hit) starts the whole fight: phases, armor, minions
            float taken = amount;                               // what really counts (long shots: less)
            if (!dead && by != null && rex != null && by.transform == rex)
            {
                lastHitByRex = Time.time;
                if (!started) StartFight();
                // his armour soaks long shots: from beyond 20 m only 35% counts (no sniping him from the approach).
                // Health has no damage hook, so the rest is given back (Damaged runs before the death check: works for a last shot too)
                if (Vector3.Distance(rex.position, transform.position) > LongShotRange)
                {
                    health.Heal(amount * (1f - LongShotFactor));
                    taken = amount * LongShotFactor;
                    chargeUntil = Time.time + ChargeTime;           // and he comes for the sniper at double speed
                    if (!longShotTold)
                    {
                        longShotTold = true;
                        RexTestHUD.Notice("His armour soaks long shots: get closer than 20 m", 4f);
                        RexVoice.Subtitle?.Invoke("REX: He's soaking it up. I need to get closer.");
                    }
                }
            }
            if (armor.Count == 0 || dead) { DizzyBonus(taken, point, by); return; }
            // the nearest armor piece takes the hit (health is given back); heavy hits knock it off
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < armor.Count; i++)
            {
                if (armor[i].t == null) continue;
                float dd = Vector3.Distance(armor[i].t.position, point);
                if (dd < bd) { bd = dd; best = i; }
            }
            if (bd > 3f) { DizzyBonus(taken, point, by); return; }
            health.Heal(taken * 0.8f);
            var (t, hp) = armor[best];
            hp -= taken;
            if (hp <= 0f && t != null)
            {
                t.SetParent(null, true);
                var rb = t.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    foreach (var col in rb.GetComponentsInChildren<Collider>()) col.enabled = true;
                    rb.AddForce((t.position - transform.position).normalized * 8f + Vector3.up * 4f, ForceMode.VelocityChange);
                }
                armor.RemoveAt(best);
                Say("My beautiful armor!");
            }
            else armor[best] = (t, hp);
        }

        void Minions()
        {
            if (minionPrefabs == null || minionPrefabs.Length == 0) return;
            for (int i = 0; i < 3; i++)
            {
                if (Blob.AliveCount() >= BlobSpawner.GlobalCap) break;     // the island-wide blob cap counts for him too
                Vector2 c = Random.insideUnitCircle.normalized * 8f;
                var p = transform.position + new Vector3(c.x, 0f, c.y);
                if (UnityEngine.AI.NavMesh.SamplePosition(p, out var hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
                    Instantiate(minionPrefabs[Random.Range(0, minionPrefabs.Length)], hit.position, Quaternion.identity);
            }
            Say("Rise, my subjects!");
        }

        IEnumerator Shockwave()
        {
            // an expanding slime ring along the ground: Rex must jump it (grounded inside the ring band = hit)
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(ring.GetComponent<Collider>());
            Destroy(ring, 3f);          // the coroutine stops if Glorp pops mid-wave: do not leave the ring behind
            ring.transform.position = transform.position + Vector3.up * 0.05f;
            Sfx.Play("shockwave", transform.position);
            var mr = ring.GetComponent<Renderer>();
            mr.material.color = new Color(1f, 0.85f, 0.2f);
            bool hit = false;
            var rc = rex.GetComponent<RexController>();
            for (float r = 1f; r < 30f; r += Time.deltaTime * 12f)
            {
                ring.transform.localScale = new Vector3(r * 2f, 0.05f, r * 2f);
                float dist = Vector3.Distance(new Vector3(rex.position.x, 0, rex.position.z), new Vector3(transform.position.x, 0, transform.position.z));
                if (!hit && Mathf.Abs(dist - r) < 0.8f && rc != null && rc.Grounded)
                {
                    hit = true;
                    var rh = rex.GetComponent<Health>();
                    if (rh != null)
                    {
                        rh.Damage(12f, rex.position, gameObject);
                        if (rh.LastDamageTime == Time.time) rc.Slimed();       // a dodge (invulnerable) or death: no slime
                    }
                }
                yield return null;
            }
            if (ring != null) Destroy(ring);
        }

        /// <summary>Phase 2/3: he spits 2-3 globs that land around Rex. Orange rings mark the spots for PoolWarn s
        /// (time to step out), then they are burning slime pools for PoolTime s: standing in one hurts every PoolTick.</summary>
        static Material poolWarnMat;
        IEnumerator SlimePools()
        {
            if (rex == null) yield break;
            blob.Telegraph();
            blob.Play("Spit");
            Sfx.Play("blob_spit", transform.position, 1f, 0.8f);
            int count = phase == 3 ? 3 : 2;
            var spots = new List<Vector3>();
            var marks = new List<GameObject>();
            for (int i = 0; i < count; i++)
            {
                // the first lands right where Rex stands, the others a few metres around him
                Vector3 p = rex.position;
                if (i > 0) { Vector2 o = Random.insideUnitCircle.normalized * Random.Range(2.5f, 5f); p += new Vector3(o.x, 0f, o.y); }
                if (!UnityEngine.AI.NavMesh.SamplePosition(p, out var nh, 3f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                var mark = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(mark.GetComponent<Collider>());
                Destroy(mark, PoolWarn + PoolTime + 0.5f);           // gone even if the coroutine stops (Glorp popped)
                mark.transform.position = nh.position + Vector3.up * 0.03f;
                mark.transform.localScale = new Vector3(0.3f, 0.01f, 0.3f);
                var mr = mark.GetComponent<Renderer>();
                if (poolWarnMat == null) poolWarnMat = new Material(mr.sharedMaterial) { color = new Color(1f, 0.45f, 0.1f) };   // one, kept
                mr.sharedMaterial = poolWarnMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                spots.Add(nh.position);
                marks.Add(mark);
            }
            if (spots.Count == 0) yield break;
            // warning: the marks grow to the pool size and blink
            for (float t = 0f; t < PoolWarn; t += Time.deltaTime)
            {
                float s = Mathf.Lerp(0.3f, PoolRadius * 2f, t / PoolWarn);
                bool on = Mathf.Repeat(t * 6f, 1f) < 0.6f;
                foreach (var m in marks)
                    if (m != null) { m.transform.localScale = new Vector3(s, 0.01f, s); m.GetComponent<Renderer>().enabled = on; }
                yield return null;
            }
            Color slime = blob.BaseColor();
            foreach (var m in marks)
            {
                if (m == null) continue;
                var r = m.GetComponent<Renderer>();
                r.enabled = true;
                r.sharedMaterial = SlimeFX.SharedSlime(slime);       // shared per colour (no new material per pool)
                SlimeFX.Splat(m.transform.position + Vector3.up * 0.2f, Vector3.up, slime, 10);
            }
            Sfx.Play("blob_hit", spots[0], 0.8f);
            var rh = rex.GetComponent<Health>();
            var rc = rex.GetComponent<RexController>();
            float nextTick = 0f;
            for (float t = 0f; t < PoolTime; t += Time.deltaTime)
            {
                if (dead || rex == null) yield break;
                if (Time.time >= nextTick && rh != null && !rh.IsDead)
                {
                    foreach (var sp in spots)
                    {
                        Vector3 off = rex.position - sp;
                        if (Mathf.Abs(off.y) > 1.2f) continue;              // jumped over it / other floor
                        off.y = 0f;
                        if (off.sqrMagnitude > PoolRadius * PoolRadius) continue;
                        nextTick = Time.time + PoolTick;
                        rh.Damage(PoolDamage, rex.position, gameObject);
                        if (rc != null && rh.LastDamageTime == Time.time && Random.value < 0.3f) rc.Slimed();
                        break;
                    }
                }
                yield return null;
            }
            foreach (var m in marks) if (m != null) Destroy(m);
        }

        /// <summary>Rex keeps out of reach: warning glow, then 3 slime shots, a little spread and led.</summary>
        IEnumerator Volley()
        {
            blob.Telegraph();
            yield return new WaitForSeconds(Blob.TelegraphTime);
            for (int i = 0; i < 3; i++)
            {
                if (dead || rex == null || blob.IsDead || blob.IsStunned) yield break;
                Vector3 to = rex.position - transform.position; to.y = 0f;
                if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to);
                blob.Play("Spit");
                Sfx.Play("blob_spit", transform.position, 0.9f);
                Vector3 mouth = transform.position + Vector3.up * 2.6f + transform.forward * 1.6f;
                Vector3 target = rex.position + Vector3.up * 1.2f + Random.insideUnitSphere * 1.2f;
                SlimeProjectile.Launch(mouth, target, blob.damage * 0.6f, gameObject, blob.BaseColor());
                yield return new WaitForSeconds(0.35f);
            }
            // the volley leaves him dizzy (Rex's window to close in), but only when Rex is close enough to use it:
            // a sniper duel at range must not keep him stunned half the time
            if (rex != null && Vector3.Distance(rex.position, transform.position) <= DizzyVolleyRange) StartDizzy();
        }

        /// <summary>Weak-point window: stunned-looking pause (Blob.Stun), hits count DizzyFactor times.</summary>
        void StartDizzy()
        {
            if (dead || !started || blob.IsDead) return;
            dizzyUntil = Time.time + DizzyTime;
            nextVolley = Mathf.Max(nextVolley, dizzyUntil + VolleyEvery + Random.Range(-0.5f, 0.5f));   // the volley timer starts after the dizzy
            blob.Stun(DizzyTime, true);
            if (crown != null) SlimeFX.Splat(crown.position, Vector3.up, new Color(1f, 0.9f, 0.3f), 12);   // a little burst over his head
            RexTestHUD.Notice(dizzyTold ? "KING GLORP IS DIZZY!" : "KING GLORP IS DIZZY! Hit him now!", 2f);
            dizzyTold = true;
        }

        /// <summary>Dizzy: the extra 50% of a hit that got through. Silent (no second Damaged: no double flash, sound or
        /// reaction); never the killing part, so the hit itself runs Died the normal way.</summary>
        void DizzyBonus(float taken, Vector3 point, GameObject by)
        {
            if (!Dizzy || dead) return;
            float extra = Mathf.Min(taken * (DizzyFactor - 1f), health.Current - 1f);
            if (extra <= 0f) return;
            health.DamageSilent(extra, by);
        }

        IEnumerator Victory(GameObject by, Vector3 at)
        {
            dead = true;
            Fighting = false;
            music.Stop();
            // the boss object is gone ~0.5 s after dying (it would cut his last line): a voice object of its own
            var dv = new GameObject("GlorpDeathVoice");
            dv.transform.position = at + Vector3.up * 2f;
            var src = dv.AddComponent<AudioSource>();
            src.spatialBlend = 1f;
            src.minDistance = 8f;
            src.maxDistance = 120f;
            float deathLen = Say(DeathLine, dv);
            Destroy(dv, deathLen + 0.5f);
            for (int i = 0; i < 10; i++)
            {
                SlimeFX.Splat(at + Random.insideUnitSphere * 3f + Vector3.up * 2f, Random.onUnitSphere, new Color(1f, 0.8f, 0.2f), 40);
                yield return new WaitForSeconds(0.1f);
            }
            if (deathLen > 1f) yield return new WaitForSeconds(deathLen - 1f);   // Rex answers when the king has finished
            if (rex != null)
            {
                RexVoice.Say(rex.gameObject, "GlorpWin", true);
                var rc = rex.GetComponent<RexController>();
                if (rc != null) rc.SetTrigger(Animator.StringToHash("Victory"));   // both fists up
            }
            PlayerPrefs.SetInt(SaveKey, 1);
            PlayerPrefs.Save();
            yield return new WaitForSeconds(4f);
            var ending = new GameObject("Ending").AddComponent<EndingSequence>();
        }
    }
}
