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
        [HideInInspector] public float flingSpeed;          // golf: set to reach the water; 0 = default 14 m/s
        public bool IsStunned => Time.time < stunUntil;
        static int alive;
        public static int AliveCount() => alive;
        /// <summary>Blobs still alive within radius of p (a dead blob stays in the scene ~1 s while it pops).</summary>
        public static int LivingNear(Vector3 p, float radius)
        {
            int n = 0;
            float r2 = radius * radius;
            foreach (var b in All)
                if (b != null && !b.IsDead && (b.transform.position - p).sqrMagnitude < r2) n++;
            return n;
        }
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
        /// <summary>Top chase speed: below Rex's run speed (6.0), so running away always works.</summary>
        public const float MaxChaseSpeed = 5.7f;

        /// <summary>Hit reaction: slides the blob back along dir (flat) by metres over 0.15 s, on the NavMesh.
        /// Not for the boss (too big to shove) or a blob that is already popping.</summary>
        public void Knockback(Vector3 dir, float metres)
        {
            if (health.IsDead || kind == Kind.KingGlorp || !agent.enabled || !agent.isOnNavMesh) return;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            if (knockRoutine != null) StopCoroutine(knockRoutine);
            knockRoutine = StartCoroutine(KnockSlide(dir.normalized * metres, 0.15f));
        }
        Coroutine knockRoutine;

        IEnumerator KnockSlide(Vector3 total, float time)
        {
            busyUntil = Mathf.Max(busyUntil, Time.time + time + 0.1f);      // stops chasing while it slides
            float done = 0f;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                if (health.IsDead || !agent.enabled || !agent.isOnNavMesh) yield break;
                float k = Mathf.Clamp01((t + Time.deltaTime) / time);
                float eased = 1f - (1f - k) * (1f - k);                       // fast first, then settles
                agent.Move(total * (eased - done));                          // Move keeps it on the NavMesh (no wall clipping)
                done = eased;
                yield return null;
            }
            knockRoutine = null;
        }

        NavMeshAgent agent;
        float nextSlideSfx;
        Health health;
        Transform rex;
        Health rexHealth;
        float nextAttack, stunUntil, angryUntil, busyUntil, alertUntil;
        Vector3 startScale;
        bool wasStunned;
        MaterialPropertyBlock mpb;
        Color baseColor;

        static readonly int SpeedHash = Animator.StringToHash("Speed");

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            startScale = transform.localScale;
            health = GetComponent<Health>();
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            mpb = new MaterialPropertyBlock();
            ApplyKind();
            if (jellyRenderer != null && jellyRenderer.sharedMaterial != null)
                baseColor = jellyRenderer.sharedMaterial.GetColor("_BaseColor");
            if (jellyRenderer != null) jellyBaseScale = jellyRenderer.transform.localScale;
        }

        // hit reaction (every hit): a short white flash and a squash of the jelly
        const float FlashTime = 0.08f, SquashTime = 0.12f;
        float flashUntil, squashUntil;
        Vector3 jellyBaseScale = Vector3.one;

        void Squash()
        {
            if (jellyRenderer == null) return;
            float k = squashUntil > Time.time ? (squashUntil - Time.time) / SquashTime : 0f;
            var t = jellyRenderer.transform;
            if (k <= 0f) { if (t.localScale != jellyBaseScale) t.localScale = jellyBaseScale; return; }
            float s = Mathf.Sin(k * Mathf.PI) * 0.22f;                     // flat and wide, then back
            t.localScale = Vector3.Scale(jellyBaseScale, new Vector3(1f + s * 0.5f, 1f - s, 1f + s * 0.5f));
        }

        // far-away blobs nobody sees go away quietly (keeps the blob count and the agents down)
        const float CullDistance = 90f, CullAfter = 20f;
        float hiddenSince = -1f, nextCull;

        void CullCheck()
        {
            if (Time.time < nextCull) return;
            nextCull = Time.time + 1f;
            if (kind == Kind.KingGlorp || KingGlorpBoss.Fighting || rex == null) { hiddenSince = -1f; return; }
            bool far = (transform.position - rex.position).sqrMagnitude > CullDistance * CullDistance;
            var cam = Camera.main;
            bool seen = false;
            if (cam != null)
            {
                Vector3 v = cam.WorldToViewportPoint(transform.position + Vector3.up * HeightOf() * 0.5f);
                seen = v.z > 0f && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f;
            }
            if (!far || seen) { hiddenSince = -1f; return; }
            if (hiddenSince < 0f) hiddenSince = Time.time;
            else if (Time.time - hiddenSince > CullAfter) Destroy(gameObject);
        }

        void ApplyKind()
        {
            switch (kind)
            {
                case Kind.Tourist: speed = 1.2f; attackRange = 7f; attackInterval = 2.6f; damage = 8f; health.ResetHealth(40); break;
                case Kind.Lifeguard: speed = 3.6f; attackRange = 11f; attackInterval = 2.0f; damage = 10f; health.ResetHealth(30); break;
                case Kind.Bouncer: speed = 2.0f; attackRange = 2.6f; attackInterval = 1.8f; damage = isHalf ? 9f : 18f; health.ResetHealth(isHalf ? 40 : 90); break;
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
            nextCull = Time.time + Random.Range(0f, 1f);                   // spread the checks over the frames
            SetNightGlow(DayNight.Night);         // DayNight only writes the glow when Night changes: a new blob gets it here
        }

        void Update()
        {
            Squash();                                                      // also restores the jelly when it dies mid-squash
            if (!health.IsDead && Random.value < Time.deltaTime * 0.06f) Sfx.Play("blob_idle", transform.position, 0.5f);
            if (health.IsDead) return;
            CullCheck();
            if (animator != null) animator.SetFloat(SpeedHash, agent.enabled ? agent.velocity.magnitude : 0f);
            // wet squelch now and then while sliding fast (not every frame: one blob every ~1.5-3 s)
            if (agent.enabled && agent.velocity.sqrMagnitude > 2.25f && Time.time > nextSlideSfx)
            {
                nextSlideSfx = Time.time + Random.Range(1.5f, 3f);
                Sfx.Play("blob_slide", transform.position, 0.45f);
            }
            if (!agent.enabled) return;            // rising out of the water (BlobSpawner)

            // Angry: darker color + shake (brief); a little faster
            float angry = IsAngry ? 1f : 0f;
            agent.speed = speed * DayNight.BlobSpeed * (IsAngry ? 1.5f : 1f) * (IsStunned ? 0f : 1f);
            // fairness: night + angry must never outrun Rex's run (6 m/s); the lifeguard's 3.6 is 5.76 at full night
            if (kind != Kind.KingGlorp) agent.speed = Mathf.Min(agent.speed, MaxChaseSpeed);
            if (jellyRenderer != null)
            {
                jellyRenderer.GetPropertyBlock(mpb);
                var dark = baseColor * 0.45f;
                dark.a = baseColor.a;                          // darker, not more see-through
                var col = Color.Lerp(baseColor, dark, angry);
                // hit flash: toward white for 0.08 s
                if (Time.time < flashUntil)
                {
                    float f = (flashUntil - Time.time) / FlashTime;
                    col = Color.Lerp(col, new Color(1f, 1f, 1f, col.a), f * 0.85f);
                }
                mpb.SetColor("_BaseColor", col);
                jellyRenderer.SetPropertyBlock(mpb);
            }

            if (wasStunned && !IsStunned) Play("Locomotion", 0.2f);
            wasStunned = IsStunned;
            if (rex == null || (rexHealth != null && rexHealth.IsDead) || IsStunned || Time.time < busyUntil || GameFlow.Busy)   // Busy: title + intro
            {
                if (agent.isOnNavMesh) agent.isStopped = true;
                return;
            }
            if (Disguised) { Wander(); return; }
            float dist = Vector3.Distance(transform.position, rex.position);
            if (dist > sightRange && !IsAngry && Time.time >= alertUntil)
            {
                if (agent.isOnNavMesh) agent.isStopped = true;
                return;
            }
            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
                // a new path only when Rex has moved or every 0.25 s (path search for every blob every frame is costly)
                if (Time.time > nextPath || (rex.position - lastTarget).sqrMagnitude > 0.25f)
                {
                    agent.SetDestination(rex.position);
                    lastTarget = rex.position;
                    nextPath = Time.time + 0.25f;
                }
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
            Sfx.Play(melee ? "blob_whip" : "blob_spit", transform.position, 0.8f);
            busyUntil = Time.time + 0.6f;
            yield return new WaitForSeconds(melee ? 0.4f : 0.45f);
            if (health.IsDead || rex == null) yield break;
            Vector3 mouth = transform.position + Vector3.up * HeightOf() * 0.6f + transform.forward * 0.4f;
            if (melee)
            {
                // the whip reaches a few metres (attackRange is the distance to start an attack, also for spitting)
                float reach = kind == Kind.Bouncer ? attackRange : kind == Kind.KingGlorp ? 4.5f : 2.4f;
                if (Vector3.Distance(transform.position, rex.position) < reach + 0.6f && rexHealth != null)
                    rexHealth.Damage(damage, rex.position + Vector3.up, gameObject);
            }
            else
            {
                Vector3 target = rex.position + Vector3.up * 1.2f;
                SlimeProjectile.Launch(mouth, target, damage, gameObject, BaseColor(),
                    kind == Kind.Lifeguard ? (throwPrefab != null ? throwPrefab : ThrowProps.Random01()) : null);
            }
        }

        /// <summary>The pink brain: inside the upper body (sniper head shot, X-ray).</summary>
        public Vector3 BrainPoint => transform.position + Vector3.up * HeightOf() * 0.62f;
        public float BrainRadius => HeightOf() * 0.14f;

        float nextPath;
        Vector3 lastTarget;
        /// <summary>Set by thrown / fired weapons just around their Damage call: the weapon that gets the kill.</summary>
        [System.NonSerialized] public WeaponId? creditWeapon;

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
            // hit from far away (sniper): it comes for Rex instead of sitting still outside its sight range
            if (attacker != null && attacker.CompareTag("Player")) alertUntil = Mathf.Max(alertUntil, Time.time + 10f);
            // a hit that does not kill: the weapon's death style (golf fling, knife leak) must not stay for a later kill
            if (health.Current > 0f) { deathStyle = DeathStyle.Pop; flingSpeed = 0f; }
            if (!health.IsDead && !IsStunned) Play("Hit", 0.03f);
            flashUntil = Time.time + FlashTime;                            // every hit, light ones too
            squashUntil = Time.time + SquashTime;
            Sfx.Play("blob_hit", point);
        }

        GameObject lastKiller;
        /// <summary>Ammo pickups blobs can drop (set from the guests' reward list).</summary>
        public static GameObject[] Loot;

        void DropLoot()
        {
            // brief section 8: taunted (angry) blobs drop more loot
            float chance = IsAngry ? 0.6f : 0.15f;
            if (kind == Kind.KingGlorp || Random.value > chance) return;
            Vector3 p = transform.position + Vector3.up * 0.4f;
            if (Loot != null && Loot.Length > 0 && Random.value < 0.6f)
            {
                var drop = Instantiate(Loot[Random.Range(0, Loot.Length)], p - Vector3.up * 0.3f, Quaternion.identity);
                if (drop.TryGetComponent(out WeaponPickup wp)) wp.respawnSeconds = 0f;     // loot is one-time (world ammo comes back)
            }
            else
                FoodPickup.Spawn((FoodPickup.Kind)Random.Range(0, 3), p, 0f);
        }

        void OnDied(GameObject killer)
        {
            lastKiller = killer;
            if (killer != null && killer.GetComponent<RexController>() != null)
            {
                Progress.Add("kills");
                var w = killer.GetComponent<RexWeapons>();
                if (w != null) Progress.Add("kills_" + (creditWeapon ?? w.Current.id));   // a coconut / harpoon kill after a switch counts for it
                if (kind == Kind.KingGlorp) Progress.Set("glorp", 1);
            }
            agent.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            Play("Pop", 0.02f);
            Sfx.Play("blob_pop", transform.position);
            if (killer != null && killer.GetComponent<RexController>() != null) DropLoot();
            StartCoroutine(PopRoutine());
        }

        IEnumerator PopRoutine()
        {
            var t = transform;
            switch (deathStyle)
            {
                case DeathStyle.Leak:          // knife: leaks and shrinks like a balloon
                    if (animator != null) animator.CrossFadeInFixedTime("Hit", 0.05f);
                    float nextDrip = 0f;
                    for (float k = 0; k < 1.2f; k += Time.deltaTime)
                    {
                        t.localScale *= 1f - Time.deltaTime * 0.9f;
                        // a drip every 0.15 s (not a chance per frame: at high frame rates that was dozens of puddles)
                        if (k >= nextDrip) { nextDrip = k + 0.15f; SlimePuddle.Spawn(t.position, BaseColor(), 0.25f); }
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
                    break;                                     // shatters (no slime burst below); a bouncer still splits
                case DeathStyle.Fling:         // golf: flies away (into the pool / sea), then pops
                    var rb = gameObject.AddComponent<Rigidbody>();
                    rb.mass = 30f;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    rb.AddForce(flingDirection.normalized * (flingSpeed > 0f ? flingSpeed : 14f), ForceMode.VelocityChange);
                    rb.AddTorque(Random.onUnitSphere * 8f, ForceMode.VelocityChange);
                    // collide again (all colliders went off at death) so it lands instead of falling through the ground;
                    // pop when it lands or hits water, at the latest after 3 s
                    var body = GetComponent<CapsuleCollider>();
                    if (body != null) body.enabled = true;
                    for (float k = 0f; k < 3f; k += Time.deltaTime)
                    {
                        if (k > 0.4f && (rb.linearVelocity.sqrMagnitude < 1.5f || WaterVolume.Depth(t.position) > 0.3f)) break;
                        yield return null;
                    }
                    break;
                case DeathStyle.Pinned:
                    Play("Pinned");
                    yield return new WaitForSeconds(1.5f);
                    break;
                default:
                    yield return new WaitForSeconds(0.4f);
                    break;
            }
            if (deathStyle != DeathStyle.Freeze) SlimeFX.Burst(transform.position + Vector3.up * HeightOf() * 0.5f, BaseColor(), HeightOf());
            if (deathStyle == DeathStyle.Freeze && jellyRenderer != null) jellyRenderer.enabled = false;      // gone in the shatter
            if (kind == Kind.Bouncer && !isHalf && splitPrefab != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    Vector3 side = transform.right * (i == 0 ? -0.9f : 0.9f);
                    // on the NavMesh (a golf-flung bouncer pops away from it); full-size base scale (a knife kill shrank it)
                    Vector3 at = transform.position + side;
                    if (NavMesh.SamplePosition(at, out NavMeshHit nh, 6f, NavMesh.AllAreas)) at = nh.position;
                    var half = Instantiate(splitPrefab, at, transform.rotation);
                    half.isHalf = true;
                    half.transform.localScale = startScale * 0.5f;
                    half.ApplyKind();
                }
                if (lastKiller != null && lastKiller.GetComponent<RexController>() != null)
                {
                    RexVoice.Say(lastKiller, "BouncerSplit", true);
                    Progress.Add("bouncer_splits");
                }
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
