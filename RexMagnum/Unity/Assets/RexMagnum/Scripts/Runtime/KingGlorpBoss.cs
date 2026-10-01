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
    ///    phase 3 sends slime shockwaves (jump over them) and he moves faster.
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
        int phase = 1;
        float nextAbsorb, nextWave, nextMinions, nextLine;
        readonly List<(Transform t, float hp)> armor = new List<(Transform, float)>();
        AudioSource music;

        static readonly string[] GlorpLines =
        {
            "WHO DARES DISTURB THE KING?", "Bow, small damp creature!", "My crown! Do not look at my crown!",
            "I absorb! I ABSORB!", "You are not famous. I checked.", "Glorp is ETERNAL! Glorp is... leaking.",
        };

        void Start()
        {
            blob = GetComponent<Blob>();
            health = GetComponent<Health>();
            var rc = FindFirstObjectByType<RexController>();
            if (rc != null) rex = rc.transform;
            health.Died += by => StartCoroutine(Victory(by));
            music = gameObject.AddComponent<AudioSource>();
            music.clip = Resources.Load<AudioClip>("Music/glorp_ballad");
            music.loop = true;
            music.spatialBlend = 0f;
            music.volume = 0.6f;
            if (PlayerPrefs.GetInt(SaveKey, 0) == 1) gameObject.SetActive(false);   // already beaten
            health.Damaged += OnDamaged;
        }

        void Say(string line) => RexVoice.Subtitle?.Invoke("KING GLORP: " + line);

        void Update()
        {
            if (dead || rex == null) return;
            float d = Vector3.Distance(rex.position, transform.position);
            if (!started)
            {
                if (d > 40f) return;
                started = true;
                if (music.clip != null) music.Play();
                Say(GlorpLines[0]);
                RexVoice.Say(rex.gameObject, "Glorp", true);
            }
            int p = health.Fraction > 0.66f ? 1 : health.Fraction > 0.33f ? 2 : 3;
            if (p != phase) { phase = p; StartCoroutine(PhaseChange()); }
            if (Time.time > nextAbsorb) { nextAbsorb = Time.time + (phase == 3 ? 12f : 18f); StartCoroutine(Absorb()); }
            if (phase >= 2 && Time.time > nextMinions) { nextMinions = Time.time + 25f; Minions(); }
            if (phase == 3 && Time.time > nextWave) { nextWave = Time.time + 7f; StartCoroutine(Shockwave()); }
            if (Time.time > nextLine) { nextLine = Time.time + 14f; Say(GlorpLines[Random.Range(1, GlorpLines.Length)]); }
            // keep armor pieces stuck to the body (they ride along)
            for (int i = armor.Count - 1; i >= 0; i--) if (armor[i].t == null) armor.RemoveAt(i);
        }

        void OnGUI()
        {
            if (!started || dead) return;
            float w = Screen.width, h = Screen.height, s = h / 1080f;
            var r = new Rect(w * 0.25f, 40 * s, w * 0.5f, 22 * s);
            GUI.color = new Color(0, 0, 0, 0.6f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.8f, 0.15f);
            GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, (r.width - 6) * health.Fraction, r.height - 6), Texture2D.whiteTexture);
            GUI.color = Color.white;
            var st = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = (int)(18 * s) };
            GUI.Label(new Rect(r.x, r.y - 26 * s, r.width, 24 * s), $"KING GLORP   {(armor.Count > 0 ? $"ARMOR x{armor.Count}" : "")}", st);
        }

        IEnumerator PhaseChange()
        {
            Say(phase == 2 ? "Rise, my subjects!" : "NOW YOU SEE MY FINAL FORM!");
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
                if (rb == null || rb.isKinematic || rb.GetComponentInParent<Blob>() || rb.GetComponentInParent<RexController>() ||
                    rb.GetComponent<GolfCart>()) continue;
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

        void OnDamaged(float amount, Vector3 point, GameObject by)
        {
            if (armor.Count == 0 || dead) return;
            // the nearest armor piece takes the hit (health is given back); heavy hits knock it off
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < armor.Count; i++)
            {
                if (armor[i].t == null) continue;
                float dd = Vector3.Distance(armor[i].t.position, point);
                if (dd < bd) { bd = dd; best = i; }
            }
            if (bd > 3f) return;
            health.Heal(amount * 0.8f);
            var (t, hp) = armor[best];
            hp -= amount;
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
            ring.transform.position = transform.position + Vector3.up * 0.05f;
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
                    rex.GetComponent<Health>()?.Damage(12f, rex.position, gameObject);
                    rc.Slimed();
                }
                yield return null;
            }
            Destroy(ring);
        }

        IEnumerator Victory(GameObject by)
        {
            dead = true;
            music.Stop();
            Say("Glorp... is... not... eternaaal...");
            for (int i = 0; i < 10; i++)
            {
                SlimeFX.Splat(transform.position + Random.insideUnitSphere * 3f + Vector3.up * 2f, Random.onUnitSphere, new Color(1f, 0.8f, 0.2f), 40);
                yield return new WaitForSeconds(0.1f);
            }
            if (rex != null) RexVoice.Say(rex.gameObject, "GlorpWin", true);
            PlayerPrefs.SetInt(SaveKey, 1);
            PlayerPrefs.Save();
            yield return new WaitForSeconds(4f);
            var ending = new GameObject("Ending").AddComponent<EndingSequence>();
        }
    }
}
