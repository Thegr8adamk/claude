using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace RexMagnum
{
    /// <summary>
    /// Blob alien behaviour (brief section 4). One script for all types:
    ///  Tourist   - slow, spits slime
    ///  Lifeguard - fast, throws pool floats / chairs
    ///  Bouncer   - strong tentacle whip; when it pops it splits into 2 half-size blobs
    ///  KingGlorp - boss: spits and whips, a lot of health
    /// Reactions: Rex's taunt (rude tentacle, then angry), stun, photo pose, hit, pop.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Health))]
    public class Blob : MonoBehaviour
    {
        public enum Kind { Tourist, Lifeguard, Bouncer, KingGlorp }

        [Header("Type")]
        public Kind kind = Kind.Tourist;
        public bool isHalf;                       // bouncer half after a split

        [Header("Links")]
        public Animator animator;
        public Renderer jellyRenderer;
        public GameObject throwPrefab;            // lifeguard: float/chair (optional)
        public Blob splitPrefab;                  // bouncer: the half-size copy (this prefab)

        [Header("Tuning (set from the type)")]
        public float speed = 1.2f;
        public float sightRange = 18f;
        public float attackRange = 7f;
        public float attackInterval = 2.5f;
        public float damage = 8f;

        public DeathStyle deathStyle = DeathStyle.Pop;
        [HideInInspector] public Vector3 flingDirection;
        public bool IsStunned => Time.time < stunUntil;
        static int alive;
        public static int AliveCount() => alive;
        public static readonly System.Collections.Generic.List<Blob> All = new System.Collections.Generic.List<Blob>();
        public bool IsDead => health != null && health.IsDead;
        void OnEnable() { alive++; All.Add(this); }
        void OnDisable() { alive--; All.Remove(this); }

        /// <summary>Harpoon: pinned (stuck in place, flailing) for a few seconds.</summary>
        public void Pin(float seconds)
        {
            stunUntil = Time.time + seconds;
            Play("Pinned");
        }
        public bool IsAngry => Time.time < angryUntil;

        NavMeshAgent agent;
        Health health;
        Transform rex;
        Health rexHealth;
        float nextAttack, stunUntil, angryUntil, busyUntil;
        bool wasStunned;
        MaterialPropertyBlock mpb;
        Color baseColor;

        static readonly int SpeedHash = Animator.StringToHash("Speed");

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            mpb = new MaterialPropertyBlock();
            ApplyKind();
            if (jellyRenderer != null && jellyRenderer.sharedMaterial != null)
                baseColor = jellyRenderer.sharedMaterial.GetColor("_BaseColor");
        }

        void ApplyKind()
        {
            switch (kind)
            {
                case Kind.Tourist: speed = 1.2f; attackRange = 7f; attackInterval = 2.6f; damage = 8f; health.ResetHealth(40); break;
                case Kind.Lifeguard: speed = 4.2f; attackRange = 11f; attackInterval = 2.0f; damage = 10f; health.ResetHealth(30); break;
                case Kind.Bouncer: speed = 2.0f; attackRange = 2.6f; attackInterval = 1.8f; damage = 18f; health.ResetHealth(isHalf ? 40 : 90); break;
                case Kind.KingGlorp: speed = 1.5f; attackRange = 14f; attackInterval = 1.6f; damage = 15f; health.ResetHealth(1200); break;
            }
            agent.speed = speed;
            agent.angularSpeed = 240f;
            agent.stoppingDistance = Mathf.Min(attackRange * 0.6f, 4f);
        }

        void Start()
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                rex = p.transform;
                rexHealth = p.GetComponent<Health>();
            }
            nextAttack = Time.time + Random.Range(1f, 3f);
        }

        void Update()
        {
            if (!health.IsDead && Random.value < Time.deltaTime * 0.06f) Sfx.Play("blob_idle", transform.position, 0.5f);
            if (health.IsDead) return;
            if (animator != null) animator.SetFloat(SpeedHash, agent.enabled ? agent.velocity.magnitude : 0f);
            if (!agent.enabled) return;            // rising out of the water (BlobSpawner)

            // Angry: darker color + shake (brief); a little faster
            float angry = IsAngry ? 1f : 0f;
            agent.speed = speed * DayNight.BlobSpeed * (IsAngry ? 1.5f : 1f) * (IsStunned ? 0f : 1f);
            if (jellyRenderer != null)
            {
                jellyRenderer.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", Color.Lerp(baseColor, baseColor * 0.45f, angry));
                jellyRenderer.SetPropertyBlock(mpb);
            }

            if (wasStunned && !IsStunned) Play("Locomotion", 0.2f);
            wasStunned = IsStunned;
            if (rex == null || (rexHealth != null && rexHealth.IsDead) || IsStunned || Time.time < busyUntil)
            {
                if (agent.isOnNavMesh) agent.isStopped = true;
                return;
            }
            if (Disguised) { Wander(); return; }
            float dist = Vector3.Distance(transform.position, rex.position);
            if (dist > sightRange && !IsAngry)
            {
                if (agent.isOnNavMesh) agent.isStopped = true;
                return;
            }
            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(rex.position);
            }
            Vector3 to = rex.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f && dist < attackRange * 1.2f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 6f * Time.deltaTime);
            if (dist <= attackRange && Time.time >= nextAttack)
                StartCoroutine(Attack(dist));
        }

        IEnumerator Attack(float dist)
        {
            nextAttack = Time.time + attackInterval * (IsAngry ? 0.6f : 1f);
            bool melee = kind == Kind.Bouncer || (kind == Kind.KingGlorp && dist < 4f) || dist < 2.2f;
            string clip = melee ? "Whip" : kind == Kind.Lifeguard ? "Throw" : "Spit";
            Play(clip);
            if (!melee) Sfx.Play("blob_spit", transform.position, 0.8f);
            busyUntil = Time.time + 0.6f;
            yield return new WaitForSeconds(melee ? 0.4f : 0.45f);
            if (health.IsDead || rex == null) yield break;
            Vector3 mouth = transform.position + Vector3.up * HeightOf() * 0.6f + transform.forward * 0.4f;
            if (melee)
            {
                if (Vector3.Distance(transform.position, rex.position) < attackRange + 0.6f && rexHealth != null)
                    rexHealth.Damage(damage, rex.position + Vector3.up, gameObject);
            }
            else
            {
                Vector3 target = rex.position + Vector3.up * 1.2f;
                SlimeProjectile.Launch(mouth, target, damage, gameObject, BaseColor(),
                    kind == Kind.Lifeguard ? throwPrefab : null);
            }
        }

        float HeightOf() => kind == Kind.Tourist ? 1.25f : kind == Kind.Lifeguard ? 1.9f : kind == Kind.Bouncer ? (isHalf ? 1.1f : 2.15f) : 4.2f;

        public Color BaseColor() => baseColor;

        public void Play(string clip, float fade = 0.08f)
        {
            if (animator != null) animator.CrossFadeInFixedTime(clip, fade);
        }

        // ---------------------------------------------------------------- reactions

        /// <summary>Disguise variant (brief section 4): wanders slowly and does not attack until found out.</summary>
        public bool Disguised { get; set; }
        float nextWander;

        void Wander()
        {
            if (!agent.isOnNavMesh) return;
            agent.isStopped = false;
            agent.speed = speed * 0.5f * DayNight.BlobSpeed;
            if (Time.time < nextWander) return;
            nextWander = Time.time + Random.Range(4f, 8f);
            Vector3 p = transform.position + Random.insideUnitSphere * 6f;
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 3f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
        }

        /// <summary>Found out: drop the act, answer with the rude tentacle and charge.</summary>
        public void Reveal()
        {
            if (!Disguised) return;
            Disguised = false;
            OnTaunted();
        }

        /// <summary>Rex's taunt: answer with the rude tentacle, then get angry and charge.</summary>
        public void OnTaunted()
        {
            if (health.IsDead || IsStunned) return;
            Play("Rude");
            Sfx.Play("blob_angry", transform.position);
            busyUntil = Time.time + 1.5f;
            angryUntil = Time.time + 10f;
        }

        public void Stun(float seconds)
        {
            if (health.IsDead) return;
            stunUntil = Time.time + seconds;
            Play("Stun");
        }

        /// <summary>Rex takes a photo: stunned blobs stay still; others pose confused, then hit back.</summary>
        public bool OnPhoto()
        {
            if (IsStunned) return true;
            Play("Photo");
            busyUntil = Time.time + 1.2f;
            nextAttack = Time.time + 1.2f;
            return false;
        }

        void OnDamaged(float amount, Vector3 point, GameObject attacker)
        {
            if (!health.IsDead && !IsStunned) Play("Hit", 0.03f);
            Sfx.Play("blob_hit", point);
        }

        GameObject lastKiller;

        void OnDied(GameObject killer)
        {
            lastKiller = killer;
            if (killer != null && killer.GetComponent<RexController>() != null)
            {
                Progress.Add("kills");
                var w = killer.GetComponent<RexWeapons>();
                if (w != null) Progress.Add("kills_" + w.Current.id);
                if (kind == Kind.KingGlorp) Progress.Set("glorp", 1);
            }
            agent.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            Play("Pop", 0.02f);
            Sfx.Play("blob_pop", transform.position);
            StartCoroutine(PopRoutine());
        }

        IEnumerator PopRoutine()
        {
            var t = transform;
            switch (deathStyle)
            {
                case DeathStyle.Leak:          // knife: leaks and shrinks like a balloon
                    if (animator != null) animator.CrossFadeInFixedTime("Hit", 0.05f);
                    for (float k = 0; k < 1.2f; k += Time.deltaTime)
                    {
                        t.localScale *= 1f - Time.deltaTime * 0.9f;
                        if (Random.value < 0.3f) SlimePuddle.Spawn(t.position, BaseColor(), 0.25f);
                        t.Rotate(0, 600 * Time.deltaTime, 0);
                        yield return null;
                    }
                    break;
                case DeathStyle.Freeze:        // swordfish: freezes (icy white), then shatters
                    if (jellyRenderer != null)
                    {
                        jellyRenderer.GetPropertyBlock(mpb);
                        mpb.SetColor("_BaseColor", new Color(0.85f, 0.95f, 1f, 0.9f));
                        jellyRenderer.SetPropertyBlock(mpb);
                    }
                    if (animator) animator.speed = 0f;
                    yield return new WaitForSeconds(0.6f);
                    SlimeFX.Splat(t.position + Vector3.up * HeightOf() * 0.5f, Vector3.up, new Color(0.85f, 0.95f, 1f), 24);
                    Destroy(gameObject);
                    yield break;
                case DeathStyle.Fling:         // golf: flies away (into the pool / sea), then pops
                    var rb = gameObject.AddComponent<Rigidbody>();
                    rb.mass = 30f;
                    rb.AddForce(flingDirection.normalized * 14f, ForceMode.VelocityChange);
                    rb.AddTorque(Random.onUnitSphere * 8f, ForceMode.VelocityChange);
                    yield return new WaitForSeconds(1.6f);
                    break;
                case DeathStyle.Pinned:
                    Play("Pinned");
                    yield return new WaitForSeconds(1.5f);
                    break;
                default:
                    yield return new WaitForSeconds(0.4f);
                    break;
            }
            SlimeFX.Burst(transform.position + Vector3.up * HeightOf() * 0.5f, BaseColor(), HeightOf());
            if (kind == Kind.Bouncer && !isHalf && splitPrefab != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    Vector3 side = transform.right * (i == 0 ? -0.9f : 0.9f);
                    var half = Instantiate(splitPrefab, transform.position + side, transform.rotation);
                    half.isHalf = true;
                    half.transform.localScale = transform.localScale * 0.5f;
                    half.ApplyKind();
                }
                if (lastKiller != null && lastKiller.GetComponent<RexController>() != null)
                    RexVoice.Say(lastKiller, "BouncerSplit", true);
            }
            Destroy(gameObject, 0.15f);
        }

        /// <summary>Night: soft glow in its own color.</summary>
        public void SetNightGlow(float amount)
        {
            if (jellyRenderer == null) return;
            jellyRenderer.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", baseColor * amount * 1.5f);
            jellyRenderer.SetPropertyBlock(mpb);
        }
    }
}
