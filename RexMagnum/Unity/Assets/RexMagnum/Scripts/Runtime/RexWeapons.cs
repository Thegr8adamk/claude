using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Rex's weapons (brief section 5): inventory, equip in the right hand (third-person body AND
    /// first-person arms), one Action button (tap = light, hold = heavy / aim), ammo, special effects.
    /// </summary>
    [RequireComponent(typeof(WeaponWheel))]
    public class RexWeapons : MonoBehaviour
    {
        public RexController controller;
        public RexCameraRig cameraRig;
        public float holdTime = 0.3f;
        /// <summary>Weapon prefabs by id (set by the builder). Index = (int)WeaponId.</summary>
        public GameObject[] prefabs = new GameObject[11];

        public WeaponDef Current => WeaponDef.Get(current);
        public int Ammo(WeaponId id) => ammo.TryGetValue(id, out int a) ? a : 0;
        public bool Has(WeaponId id) => owned.Contains(id);
        public bool Aiming { get; private set; }
        public bool Scoped { get; private set; }
        public string ActionWord => controller.HandsDown ? "Talk" : Current.word;
        public Color ActionColor => controller.HandsDown ? new Color(0.5f, 0.9f, 1f) : Current.color;

        WeaponId current = WeaponId.Fists;
        readonly List<WeaponId> owned = new List<WeaponId> { WeaponId.Fists };
        readonly Dictionary<WeaponId, int> ammo = new Dictionary<WeaponId, int>();
        GameObject heldBody, heldArms;
        float pressTime = -1f, busyUntil, nextShot, nextEmptyNotice;
        int bottleHits;
        LineRenderer aimLine;

        static readonly WeaponId[] Order =
        {
            WeaponId.Fists, WeaponId.Bottle, WeaponId.BottleBroken, WeaponId.Towel, WeaponId.Knife, WeaponId.GolfClub,
            WeaponId.Swordfish, WeaponId.Pistol, WeaponId.Harpoon, WeaponId.Sniper, WeaponId.Coconut,
        };

        void Start()
        {
            Progress.LoadWeapons(owned, ammo);             // weapons + ammo from the save (keeps them after a restart)
            Equip(WeaponId.Fists);
        }

        void SaveInventory() => Progress.SaveWeapons(owned, Ammo);

        /// <summary>Owned weapons in the fixed wheel / cycle order.</summary>
        public List<WeaponId> OwnedInOrder()
        {
            var list = new List<WeaponId>();
            foreach (var id in Order) if (owned.Contains(id)) list.Add(id);
            return list;
        }

        // ---------------------------------------------------------------- wet towel (brief section 5)

        /// <summary>Dip the towel in water: darker, drips, hits harder. Dries after a while.</summary>
        public bool TowelWet => Time.time < towelWetUntil;
        public float wetTowelDamage = 1.5f;
        public float wetTowelSeconds = 90f;
        float towelWetUntil, nextDrip;
        bool towelShownWet;
        MaterialPropertyBlock wetBlock;

        void UpdateTowel()
        {
            if (current == WeaponId.Towel && controller.WaterDepth > 0.9f)   // the hand is under the surface
            {
                if (!TowelWet) Progress.Add("wet_towels");
                towelWetUntil = Time.time + wetTowelSeconds;
            }
            bool wet = TowelWet && current == WeaponId.Towel;
            if (wet != towelShownWet) { towelShownWet = wet; TintTowel(wet); }
            if (wet && !controller.HandsDown && Time.time > nextDrip && heldBody != null && heldBody.activeInHierarchy)
            {
                nextDrip = Time.time + Random.Range(0.6f, 1.2f);
                SlimeFX.Splat(heldBody.transform.position + Vector3.down * 0.3f, Vector3.down, new Color(0.7f, 0.85f, 1f), 1);
            }
        }

        void TintTowel(bool wet)
        {
            if (wetBlock == null) wetBlock = new MaterialPropertyBlock();
            foreach (var held in new[] { heldBody, heldArms })
            {
                if (held == null) continue;
                foreach (var r in held.GetComponentsInChildren<Renderer>())
                {
                    if (!wet) { r.SetPropertyBlock(null); continue; }      // dry: the towel's own material again
                    r.GetPropertyBlock(wetBlock);
                    wetBlock.SetColor("_BaseColor", new Color(0.55f, 0.55f, 0.6f));
                    wetBlock.SetFloat("_Smoothness", 0.75f);
                    r.SetPropertyBlock(wetBlock);
                }
            }
        }

        /// <summary>A line after a kill: the weapon's line, or sometimes one about the blob type or the fight.
        /// Only ~40% of plain kills get one (a line every pop wears thin); kill cams and special kills always do.</summary>
        void KillLine(WeaponDef d, Blob blob, bool always = false)
        {
            if (!always && Random.value > 0.4f) return;
            string group = d.id == WeaponId.Fists ? "Fists" : d.id.ToString();
            float roll = Random.value;
            if (blob != null && roll < 0.3f)
                group = blob.kind == Blob.Kind.Lifeguard ? "Lifeguard" : blob.kind == Blob.Kind.Bouncer ? "Bouncer" :
                        blob.kind == Blob.Kind.Tourist ? "Tourist" : group;
            else if (roll < 0.45f) group = "Fight";
            RexVoice.Say(gameObject, group);
        }

        // ---------------------------------------------------------------- inventory

        public void Give(WeaponId id)
        {
            var d = WeaponDef.Get(id);
            if (!owned.Contains(id))
            {
                owned.Add(id);
                if (d.isGun) ammo[id] = Mathf.Min(d.maxAmmo, Ammo(id) + d.startAmmo);   // keep ammo found before the weapon
                Equip(id);
                RexVoice.Say(gameObject, "WeaponFound", true);
                SaveInventory();
            }
            else if (d.isGun) AddAmmo(id, d.startAmmo);
        }

        public void AddAmmo(WeaponId id, int n)
        {
            var d = WeaponDef.Get(id);
            ammo[id] = Mathf.Min(d.maxAmmo, Ammo(id) + n);
            SaveInventory();
        }

        void Cycle(int dir)
        {
            int i = System.Array.IndexOf(Order, current);
            for (int k = 1; k <= Order.Length; k++)
            {
                var id = Order[((i + dir * k) % Order.Length + Order.Length) % Order.Length];
                if (owned.Contains(id)) { Equip(id); return; }
            }
        }

        public void Equip(WeaponId id)
        {
            current = id;
            bottleHits = 0;
            towelShownWet = false;
            if (heldBody) Destroy(heldBody);
            if (heldArms) Destroy(heldArms);
            bodyWeaponHidden = false;
            if (aimLine) aimLine.enabled = false;
            var prefab = prefabs != null && (int)id < prefabs.Length ? prefabs[(int)id] : null;
            if (prefab == null) return;
            heldBody = Attach(prefab, controller.bodyAnimator);
            heldArms = Attach(prefab, controller.armsAnimator);
            bodyScale = heldBody ? heldBody.transform.localScale : Vector3.one;
            armsScale = heldArms ? heldArms.transform.localScale : Vector3.one;
            drawT = 0f;                                        // take out: grows into the hand
            UpdateVisibility();
        }

        GameObject Attach(GameObject prefab, Animator anim)
        {
            if (anim == null || !anim.isHuman) return null;
            var hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            var index = anim.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            var little = anim.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            var middle = anim.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            if (hand == null || index == null || little == null || middle == null) return null;
            var go = Instantiate(prefab);
            var w = go.GetComponent<Weapon>();
            // Hand frame: d = toward the fingers, s = index -> little finger (the handle runs along s)
            Vector3 d = (middle.position - hand.position).normalized;
            Vector3 s = Vector3.ProjectOnPlane(little.position - index.position, d).normalized;
            Vector3 palm = Vector3.Cross(d, s);
            Vector3 fwd, up;
            switch (WeaponDef.Get(current).grip)
            {
                case Grip.Gun: fwd = d; up = -s; break;          // barrel along the knuckles, grip down to the little finger
                default: fwd = -s; up = d; break;                // melee: the long part leaves the fist on the thumb side
            }
            go.transform.rotation = Quaternion.LookRotation(fwd, up) * Quaternion.Inverse(w.LocalFrame());
            go.transform.position = hand.position + d * 0.075f + palm * 0.025f;
            go.transform.position += go.transform.position - w.GripPoint.position;   // grip point into the palm
            go.transform.SetParent(hand, true);
            if (anim == controller.armsAnimator)
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // brief section 5: take out / put away. The weapon grows into the hand in 0.2 s and shrinks away when the
        // hands go down (a short procedural move; the body animations stay as they are).
        float drawT = 1f;
        Vector3 bodyScale = Vector3.one, armsScale = Vector3.one;
        const float DrawTime = 0.2f;

        void UpdateVisibility()
        {
            bool show = !controller.HandsDown;
            drawT = Mathf.MoveTowards(drawT, show ? 1f : 0f, Time.deltaTime / DrawTime);
            float k = drawT * drawT * (3f - 2f * drawT);
            bool on = drawT > 0.01f;
            if (heldBody) { heldBody.SetActive(on); heldBody.transform.localScale = bodyScale * Mathf.Max(0.01f, k); }
            if (heldArms) { heldArms.SetActive(on); heldArms.transform.localScale = armsScale * Mathf.Max(0.01f, k); }
            // first person: the body's copy of the weapon only casts a shadow (the arms hold the one you see)
            bool hide = cameraRig != null && (cameraRig.Mode == RexCameraRig.CameraMode.FirstPerson || Scoped);
            if (heldBody && hide != bodyWeaponHidden)
            {
                bodyWeaponHidden = hide;
                foreach (var r in heldBody.GetComponentsInChildren<Renderer>(true))
                    r.shadowCastingMode = hide ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }
        bool bodyWeaponHidden;

        /// <summary>The arms are hidden in third person, so their hand bone may not be found when a weapon is taken
        /// then: put the weapon in the arms' hand when they show.</summary>
        void AttachArmsIfMissing()
        {
            if (heldArms || controller.armsAnimator == null || !controller.armsAnimator.isActiveAndEnabled) return;
            var prefab = prefabs != null && (int)current < prefabs.Length ? prefabs[(int)current] : null;
            if (prefab == null) return;
            heldArms = Attach(prefab, controller.armsAnimator);
            armsScale = heldArms ? heldArms.transform.localScale : Vector3.one;
            towelShownWet = false;                             // tint the new copy too
        }

        // ---------------------------------------------------------------- input

        WeaponWheel wheel;

        void Update()
        {
            if (!controller.InputEnabled) { Aiming = Scoped = false; return; }    // cart, ending: no scope left on
            if (wheel == null) wheel = GetComponent<WeaponWheel>();
            if (wheel != null && wheel.IsOpen) { Aiming = Scoped = false; return; }   // choosing a weapon: no attacks
            if (RMInput.NextWeapon) Cycle(1);
            if (RMInput.PrevWeapon) Cycle(-1);
            AttachArmsIfMissing();
            UpdateVisibility();
            UpdateTowel();
            var d = Current;
            Aiming = !controller.HandsDown && !controller.Locked && d.isGun && RMInput.Aim;
            Scoped = Aiming && current == WeaponId.Sniper;
            if (aimLine && aimLine.enabled && (controller.HandsDown || controller.Locked)) aimLine.enabled = false;
            if (controller.HandsDown || controller.Locked) { pressTime = -1f; return; }   // dead or slipped: no attacks
            bool busy = Time.time < busyUntil || Time.time < PhotoAlbum.PoseUntil;   // no attacks during a photo

            if (d.grip == Grip.Throw)
            {
                ThrowAimLine(RMInput.ActionHeld && pressTime >= 0 && Time.time - pressTime > holdTime);
                if (RMInput.ActionDown) pressTime = Time.time;
                if (RMInput.ActionUp && pressTime >= 0 && !busy)
                {
                    ThrowCoconut(Time.time - pressTime >= holdTime);
                    pressTime = -1f;
                }
                return;
            }
            if (d.isGun)
            {
                // touch: holding Action is the aim (scope), so the shot goes on release; a short tap = hip shot
                bool fire = TouchState.Active ? RMInput.ActionUp : RMInput.ActionDown;
                if (fire && Time.time >= nextShot) Shoot(d);
                return;
            }
            if (RMInput.ActionDown) pressTime = Time.time;
            if (pressTime >= 0f && !busy)
            {
                if (RMInput.ActionUp && Time.time - pressTime < holdTime) { Melee(d, false); pressTime = -1f; }
                else if (RMInput.ActionHeld && Time.time - pressTime >= holdTime) { Melee(d, true); pressTime = -1f; }
            }
            if (RMInput.ActionUp) pressTime = -1f;
        }

        // ---------------------------------------------------------------- melee

        void Melee(WeaponDef d, bool heavy)
        {
            controller.SetTrigger(Animator.StringToHash(heavy ? d.holdTrigger : d.tapTrigger));
            // tap = a quick jab (0.42 s); heavy = 0.75 s but ~3x the damage, knockback, hit-stop and shake
            busyUntil = Time.time + (heavy ? 0.75f : 0.42f);
            controller.FaceAimFor(heavy ? 0.75f : 0.42f);
            // tap combo: the 3rd tap within 0.8 s of the one before is a finisher (2x damage, heavy knockback + hit-stop)
            bool combo = false;
            if (!heavy)
            {
                tapCount = Time.time - lastTapTime <= ComboWindow ? tapCount + 1 : 1;
                lastTapTime = Time.time;
                if (tapCount >= 3) { combo = true; tapCount = 0; }
            }
            else tapCount = 0;
            StartCoroutine(MeleeHit(d, heavy, heavy ? 0.45f : 0.18f, combo));
            // a whoosh on every swing: the towel and golf club have their own sound, the rest a plain swing
            // (lower and louder for a heavy attack)
            bool own = d.id == WeaponId.Towel || d.id == WeaponId.GolfClub;
            string swing = d.id == WeaponId.Towel ? "towel_crack" : d.id == WeaponId.GolfClub ? "golf_swing" : "swing";
            float pitch = own ? 1f : heavy ? 0.85f : 1.1f;
            float vol = own ? 1f : heavy ? 0.8f : 0.55f;
            StartCoroutine(PlayLater(swing, heavy ? 0.4f : 0.12f, vol, pitch));
        }

        /// <summary>A hit lands: the game freezes for a moment (real time; heavy 60 ms, light 25 ms). Skipped while the
        /// pause menu, kill cam or weapon wheel owns the time scale, and only undone if nobody took it over meanwhile.</summary>
        IEnumerator HitStop(float seconds)
        {
            const float stopScale = 0.02f;
            if (KillCam.Active || PauseMenu.Open || !Mathf.Approximately(Time.timeScale, 1f)) yield break;
            Time.timeScale = stopScale;
            yield return new WaitForSecondsRealtime(seconds);
            if (Mathf.Approximately(Time.timeScale, stopScale)) Time.timeScale = 1f;
        }

        const float ComboWindow = 0.8f;
        int tapCount;
        float lastTapTime = -10f;

        IEnumerator MeleeHit(WeaponDef d, bool heavy, float delay, bool combo = false)
        {
            bool hard = heavy || combo;            // the combo finisher hits like a heavy (feel only; death style stays the tap one)
            yield return new WaitForSeconds(delay);
            Vector3 c = transform.position + Vector3.up * 1.1f + transform.forward * (d.reach * 0.6f);
            foreach (var col in Physics.OverlapSphere(c, d.reach * 0.55f, ~0, QueryTriggerInteraction.Ignore))
            {
                var h = col.GetComponentInParent<Health>();
                if (h == null || h.gameObject == gameObject || h.IsDead) continue;
                var blob = h.GetComponent<Blob>();
                if (blob != null)
                {
                    blob.deathStyle = heavy ? d.holdDeath : d.tapDeath;
                    blob.flingDirection = transform.forward + Vector3.up * 0.6f;
                    // golf (brief: the blob flies into the pool / sea): aim the fling at water nearby, if any
                    if (d.id == WeaponId.GolfClub && heavy && WaterVolume.RandomSurfacePoint(blob.transform.position, 35f, out Vector3 wet))
                    {
                        Vector3 flat = Vector3.ProjectOnPlane(wet - blob.transform.position, Vector3.up);
                        if (Vector3.Dot(flat.normalized, transform.forward) > -0.2f)      // not straight back at Rex
                        {
                            blob.flingDirection = flat.normalized + Vector3.up;            // 45 degrees
                            blob.flingSpeed = Mathf.Clamp(Mathf.Sqrt(flat.magnitude * 9.81f), 8f, 20f);
                        }
                    }
                    if (d.id == WeaponId.Towel && heavy) { blob.Stun(2f); Progress.Add("towel_stuns"); }          // brief: wind-up snap stuns 2 s
                }
                float dmg = (heavy ? d.holdDamage : d.tapDamage * (combo ? 2f : 1f)) * (d.id == WeaponId.Towel && TowelWet ? wetTowelDamage : 1f);
                h.Damage(dmg, col.ClosestPoint(c), gameObject);
                if (blob != null) RexTestHUD.HitMarker();
                if (combo && blob != null) RexTestHUD.Notice("COMBO", 0.7f);      // once: the loop stops after the first hit
                if (d.id == WeaponId.Knife || d.id == WeaponId.BottleBroken) Sfx.Play("knife_stab", c);
                if (d.id == WeaponId.Swordfish) Sfx.Play("swordfish", c);
                if (blob != null && !h.IsDead) blob.Knockback(transform.forward, hard ? 1.5f : 0.4f);
                if (h.IsDead && blob != null)       // blobs only: the stuck door also has Health (no kill cam for a door)
                {
                    // brief section 8: kill cam on a hold-attack kill, or on the last blob of the group
                    if ((heavy && d.killCamOnHold) || Blob.LivingNear(transform.position, 30f) == 0)
                        KillCam.Show(h.transform, transform.position,
                            d.id == WeaponId.GolfClub && heavy ? KillCam.Style.Follow :
                            d.id == WeaponId.Towel ? KillCam.Style.Towel :
                            d.id == WeaponId.Fists ? KillCam.Style.Fist : KillCam.Style.Orbit);
                    KillLine(d, blob, KillCam.Active || (heavy && d.id == WeaponId.GolfClub));   // golf fling: always a line
                }
                if (blob != null)          // light hits get a smaller kick too (the jab should not feel like air)
                {
                    if (cameraRig != null) cameraRig.Shake(hard ? 0.18f : 0.06f);
                    StartCoroutine(HitStop(hard ? 0.06f : 0.025f));
                    if (hard) RMInput.Rumble(0.4f, 0.6f, 0.12f); else RMInput.Rumble(0.15f, 0.25f, 0.06f);
                }
                if (d.id == WeaponId.Bottle && ++bottleHits >= 3)               // brief: breaks after 3 hits
                {
                    Sfx.Play("bottle_break", c);
                    Progress.Add("bottle_breaks");
                    owned.Remove(WeaponId.Bottle);
                    SaveInventory();                       // the intact bottle must not come back after a reload
                    if (Has(WeaponId.BottleBroken)) Equip(WeaponId.BottleBroken);   // Give only equips a new weapon
                    else Give(WeaponId.BottleBroken);
                    SlimeFX.Splat(heldBody ? heldBody.transform.position : c, Vector3.up, new Color(0.3f, 0.12f, 0.03f), 10);
                }
                break;
            }
        }

        // ---------------------------------------------------------------- guns

        void Shoot(WeaponDef d)
        {
            if (Ammo(d.id) <= 0)
            {
                nextShot = Time.time + 0.4f;
                Sfx.Play("dryfire", transform.position, 0.6f);      // empty: click
                if (Time.time >= nextEmptyNotice)                   // plus a message (the click alone is easy to miss)
                {
                    nextEmptyNotice = Time.time + 2f;
                    RexTestHUD.Notice("OUT OF AMMO", 0.8f);
                }
                return;
            }
            ammo[d.id]--;
            SaveInventory();
            nextShot = Time.time + (d.id == WeaponId.Sniper ? 1.2f : d.id == WeaponId.Harpoon ? 1.0f : 0.25f);
            controller.FaceAimFor(0.6f);
            RMInput.Rumble(0.2f, 0.4f, 0.08f);
            Sfx.Play(d.id == WeaponId.Sniper ? "sniper" : d.id == WeaponId.Harpoon ? "harpoon" : "pistol", transform.position);
            Vector3 target = cameraRig.AimPoint(d.reach);
            Transform muzzle = MuzzleOf(cameraRig.Mode == RexCameraRig.CameraMode.FirstPerson ? heldArms : heldBody);
            Vector3 from = muzzle != null ? muzzle.position : cameraRig.transform.position;
            if (d.id == WeaponId.Harpoon)
            {
                HarpoonBolt.Fire(from, (target - from).normalized, d.tapDamage, gameObject);
                return;
            }
            // hitscan from the camera (crosshair), tracer from the muzzle
            Vector3 dir = (target - cameraRig.transform.position).normalized;
            // start past Rex: in tight rooms the third-person camera is close and the line passes through his capsule
            Vector3 origin = cameraRig.transform.position;
            origin += dir * Mathf.Max(0f, Vector3.Dot(transform.position + Vector3.up * 1.2f - origin, dir));
            Ray ray = new Ray(origin, dir);
            if (Physics.Raycast(ray, out RaycastHit hit, d.reach, ~0, QueryTriggerInteraction.Ignore) &&
                !hit.transform.IsChildOf(transform))
            {
                var h = hit.collider.GetComponentInParent<Health>();
                if (h != null && !h.IsDead)
                {
                    var blob = h.GetComponent<Blob>();
                    // brain: the shot line passes through the brain inside the jelly (the blob has one body collider)
                    bool brain = blob != null && Vector3.Cross(ray.direction, blob.BrainPoint - ray.origin).magnitude < blob.BrainRadius;
                    if (brain && d.id == WeaponId.Sniper) Progress.Add("sniper_brains");
                    h.Damage(d.tapDamage * (brain ? 2f : 1f), hit.point, gameObject);
                    if (blob != null) RexTestHUD.HitMarker();
                    if (h.IsDead)
                    {
                        bool longShot = d.id == WeaponId.Sniper && Vector3.Distance(from, hit.point) >= 150f;
                        if (longShot) Progress.Add("sniper_long");
                        if (d.id == WeaponId.Sniper) KillCam.Show(h.transform, from, true);   // full bullet cam
                        else if (blob != null && Blob.LivingNear(transform.position, 30f) == 0) KillCam.Show(h.transform, from, true);
                        KillLine(d, blob, KillCam.Active || brain || longShot);
                    }
                }
                SlimeFX.Splat(hit.point, hit.normal, new Color(1f, 0.9f, 0.6f), 4);
                Tracer(from, hit.point);
            }
            else Tracer(from, target);
        }

        static Transform MuzzleOf(GameObject held)
        {
            if (held == null) return null;
            var w = held.GetComponent<Weapon>();
            return w != null ? w.muzzle : null;
        }

        static Material tracerMat;

        static void Tracer(Vector3 a, Vector3 b)
        {
            var go = new GameObject("Tracer");
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.startWidth = 0.015f;
            lr.endWidth = 0.004f;
            if (tracerMat == null)          // one shared material (a new one per shot was never freed)
            {
                tracerMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
                tracerMat.SetColor("_BaseColor", new Color(1f, 0.85f, 0.5f));
            }
            lr.sharedMaterial = tracerMat;
            Destroy(go, 0.05f);
        }

        // ---------------------------------------------------------------- coconut

        void ThrowAimLine(bool show)
        {
            if (aimLine == null)
            {
                aimLine = new GameObject("CoconutAim").AddComponent<LineRenderer>();
                aimLine.startWidth = aimLine.endWidth = 0.03f;
                aimLine.material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
                aimLine.material.SetColor("_BaseColor", new Color(1f, 0.6f, 0.2f));
            }
            aimLine.enabled = show;
            if (!show) return;
            Vector3 p = transform.position + Vector3.up * 1.6f, v = ThrowVelocity(true);
            var pts = new List<Vector3>();
            for (int i = 0; i < 40 && p.y > transform.position.y - 0.5f; i++)
            {
                pts.Add(p);
                p += v * 0.05f;
                v += Physics.gravity * 0.05f;
            }
            aimLine.positionCount = pts.Count;
            aimLine.SetPositions(pts.ToArray());
        }

        Vector3 ThrowVelocity(bool longThrow)
        {
            Vector3 dir = cameraRig.transform.forward;
            dir.y = Mathf.Max(dir.y, -0.2f) + 0.35f;
            return dir.normalized * (longThrow ? 15f : 8f);
        }

        System.Collections.IEnumerator PlayLater(string sfx, float t, float volume = 1f, float pitch = 1f)
        {
            yield return new WaitForSeconds(t);
            Sfx.Play(sfx, transform.position, volume, pitch);
        }

        void ThrowCoconut(bool longThrow)
        {
            if (Ammo(WeaponId.Coconut) <= 0) return;
            Sfx.Play("coconut", transform.position);
            ammo[WeaponId.Coconut]--;
            SaveInventory();
            controller.SetTrigger(Animator.StringToHash("Throw"));       // overhand coconut throw
            controller.FaceAimFor(0.6f);
            busyUntil = Time.time + 0.7f;
            var prefab = prefabs[(int)WeaponId.Coconut];
            Vector3 v = ThrowVelocity(longThrow);           // starts on the side Rex throws to (he may still face the other way)
            CoconutBomb.Throw(prefab, transform.position + Vector3.up * 1.6f + Vector3.ProjectOnPlane(v, Vector3.up).normalized * 0.4f,
                v, WeaponDef.Get(WeaponId.Coconut).tapDamage, gameObject);
            if (aimLine) aimLine.enabled = false;
        }
    }
}
