using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Rex's weapons (brief section 5): inventory, equip in the right hand (third-person body AND
    /// first-person arms), one Action button (tap = light, hold = heavy / aim), ammo, special effects.
    /// </summary>
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
        float pressTime = -1f, busyUntil, nextShot;
        int bottleHits;
        LineRenderer aimLine;

        static readonly WeaponId[] Order =
        {
            WeaponId.Fists, WeaponId.Bottle, WeaponId.BottleBroken, WeaponId.Towel, WeaponId.Knife, WeaponId.GolfClub,
            WeaponId.Swordfish, WeaponId.Pistol, WeaponId.Harpoon, WeaponId.Sniper, WeaponId.Coconut,
        };

        void Start() => Equip(WeaponId.Fists);

        // ---------------------------------------------------------------- inventory

        public void Give(WeaponId id)
        {
            var d = WeaponDef.Get(id);
            if (!owned.Contains(id))
            {
                owned.Add(id);
                if (d.isGun) ammo[id] = d.startAmmo;
                Equip(id);
                RexVoice.Say(gameObject, "WeaponFound", true);
            }
            else if (d.isGun) AddAmmo(id, d.startAmmo);
        }

        public void AddAmmo(WeaponId id, int n)
        {
            var d = WeaponDef.Get(id);
            ammo[id] = Mathf.Min(d.maxAmmo, Ammo(id) + n);
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
            if (heldBody) Destroy(heldBody);
            if (heldArms) Destroy(heldArms);
            var prefab = prefabs != null && (int)id < prefabs.Length ? prefabs[(int)id] : null;
            if (prefab == null) return;
            heldBody = Attach(prefab, controller.bodyAnimator);
            heldArms = Attach(prefab, controller.armsAnimator);
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

        void UpdateVisibility()
        {
            bool show = !controller.HandsDown;
            if (heldBody) heldBody.SetActive(show);
            if (heldArms) heldArms.SetActive(show);
        }

        // ---------------------------------------------------------------- input

        void Update()
        {
            if (!controller.InputEnabled) return;
            if (RMInput.NextWeapon) Cycle(1);
            if (RMInput.PrevWeapon) Cycle(-1);
            UpdateVisibility();
            var d = Current;
            Aiming = !controller.HandsDown && d.isGun && RMInput.Aim;
            Scoped = Aiming && current == WeaponId.Sniper;
            if (controller.HandsDown) return;
            bool busy = Time.time < busyUntil;

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
                if (RMInput.ActionDown && Time.time >= nextShot) Shoot(d);
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
            busyUntil = Time.time + (heavy ? 0.9f : 0.5f);
            controller.FaceAimFor(heavy ? 0.9f : 0.5f);
            StartCoroutine(MeleeHit(d, heavy, heavy ? 0.45f : 0.2f));
        }

        IEnumerator MeleeHit(WeaponDef d, bool heavy, float delay)
        {
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
                    if (d.id == WeaponId.Towel && heavy) blob.Stun(2f);          // brief: wind-up snap stuns 2 s
                }
                h.Damage(heavy ? d.holdDamage : d.tapDamage, col.ClosestPoint(c), gameObject);
                if (h.IsDead)
                {
                    RexVoice.Say(gameObject, d.id == WeaponId.Fists ? "Fists" : d.id.ToString());
                    if (heavy && d.killCamOnHold) KillCam.Show(h.transform, transform.position, false);
                }
                if (d.id == WeaponId.Bottle && ++bottleHits >= 3)               // brief: breaks after 3 hits
                {
                    owned.Remove(WeaponId.Bottle);
                    Give(WeaponId.BottleBroken);
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
                return;
            }
            ammo[d.id]--;
            nextShot = Time.time + (d.id == WeaponId.Sniper ? 1.2f : d.id == WeaponId.Harpoon ? 1.0f : 0.25f);
            controller.FaceAimFor(0.6f);
            Vector3 target = cameraRig.AimPoint(d.reach);
            Transform muzzle = MuzzleOf(cameraRig.Mode == RexCameraRig.CameraMode.FirstPerson ? heldArms : heldBody);
            Vector3 from = muzzle != null ? muzzle.position : cameraRig.transform.position;
            if (d.id == WeaponId.Harpoon)
            {
                HarpoonBolt.Fire(from, (target - from).normalized, d.tapDamage, gameObject);
                return;
            }
            // hitscan from the camera (crosshair), tracer from the muzzle
            Ray ray = new Ray(cameraRig.transform.position, (target - cameraRig.transform.position).normalized);
            if (Physics.Raycast(ray, out RaycastHit hit, d.reach, ~0, QueryTriggerInteraction.Ignore) &&
                !hit.transform.IsChildOf(transform))
            {
                var h = hit.collider.GetComponentInParent<Health>();
                if (h != null && !h.IsDead)
                {
                    var blob = h.GetComponent<Blob>();
                    bool brain = hit.collider.name.Contains("Brain");
                    h.Damage(d.tapDamage * (brain ? 2f : 1f), hit.point, gameObject);
                    if (h.IsDead)
                    {
                        RexVoice.Say(gameObject, d.id.ToString());
                        if (d.id == WeaponId.Sniper) KillCam.Show(h.transform, from, true);   // full bullet cam
                        else if (blob != null && Blob.AliveCount() == 0) KillCam.Show(h.transform, from, true);
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

        static void Tracer(Vector3 a, Vector3 b)
        {
            var go = new GameObject("Tracer");
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.startWidth = 0.015f;
            lr.endWidth = 0.004f;
            lr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            lr.material.SetColor("_BaseColor", new Color(1f, 0.85f, 0.5f));
            Destroy(go, 0.05f);
        }

        // ---------------------------------------------------------------- coconut

        void ThrowAimLine(bool show)
        {
            if (aimLine == null)
            {
                aimLine = new GameObject("CoconutAim").AddComponent<LineRenderer>();
                aimLine.startWidth = aimLine.endWidth = 0.03f;
                aimLine.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
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

        void ThrowCoconut(bool longThrow)
        {
            if (Ammo(WeaponId.Coconut) <= 0) return;
            ammo[WeaponId.Coconut]--;
            controller.SetTrigger(Animator.StringToHash("Swing"));
            controller.FaceAimFor(0.6f);
            busyUntil = Time.time + 0.7f;
            var prefab = prefabs[(int)WeaponId.Coconut];
            CoconutBomb.Throw(prefab, transform.position + Vector3.up * 1.6f + transform.forward * 0.4f,
                ThrowVelocity(longThrow), WeaponDef.Get(WeaponId.Coconut).tapDamage, gameObject);
            if (aimLine) aimLine.enabled = false;
        }
    }
}
