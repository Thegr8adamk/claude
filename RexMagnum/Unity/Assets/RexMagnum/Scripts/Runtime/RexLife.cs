using System.Collections;
using UnityEngine;

namespace RexMagnum
{
    /// <summary>Rex's health reactions: hurt animation + line; at 0 he falls, says a line, and wakes up again
    /// (brief: wakes in room 304 and keeps his weapons; in the test scene: at the spawn point). Far zones (cave, ruins,
    /// ship) and the boss fight set a checkpoint: after a death there Rex wakes near it, not a 2-minute walk away.</summary>
    [RequireComponent(typeof(Health))]
    public class RexLife : MonoBehaviour
    {
        /// <summary>Low health (under 25 %): Rex limps (animator bool Hurt) when no blob is within 15 m; in a fight he
        /// only slows (walk 3, run 5 m/s) so he can still get away. Checked on damage and twice a second.</summary>
        void UpdateLimp()
        {
            if (ctrl == null || health == null) return;
            bool low = !health.IsDead && health.Fraction < 0.25f;
            bool fight = low && Blob.LivingNear(transform.position, 15f) > 0;
            bool limp = low && !fight;
            ctrl.Limping = limp;                       // speed cap 2 m/s, no run (RexController)
            ctrl.HurtInFight = fight;
            if (ctrl.bodyAnimator != null) ctrl.bodyAnimator.SetBool("Hurt", limp);
        }

        void OnEnable() => InvokeRepeating(nameof(UpdateLimp), 1f, 0.5f);
        void OnDisable() => CancelInvoke(nameof(UpdateLimp));

        public Transform respawnPoint;
        Health health;
        RexController ctrl;
        Vector3 startPos;
        float startYaw;
        bool lowSaid;      // the low-health line once per drop below 25 %

        // ---- checkpoint (this session only): where Rex stood (on the ground, not in water) when he entered the cave,
        // the ruins or the ship, or when the King Glorp fight started
        static readonly string[] CheckpointZones = { "cave", "ruins", "ship" };
        bool hasCheckpoint;
        Vector3 checkpointPos;
        float checkpointYaw, nextCheckpointCheck;
        string checkpointKey;
        string checkpointZone;     // zone of the checkpoint Rex wakes at (survives leaving the zone, unlike checkpointKey)
        static RexLife instance;

        void UpdateCheckpoint()
        {
            if (health.IsDead || ctrl == null || GameFlow.TutorialActive || !ctrl.InputEnabled) return;
            if (!ctrl.Grounded || ctrl.IsSwimming || ctrl.WaterDepth > 0f || ctrl.Locked) return;   // not mid-jump, in water or on the cart
            string key = KingGlorpBoss.Fighting ? "boss" : null;
            if (key == null)
                foreach (var z in GameGuide.Zones)
                    if (System.Array.IndexOf(CheckpointZones, z.id) >= 0 && GameGuide.InZone(z, transform.position)) { key = z.id; break; }
            if (key == null) { checkpointKey = null; return; }       // left the zone: the next entry sets a new one
            if (key == checkpointKey) return;                         // set once per entry, not every step
            // walking along a zone edge (out and in again) near the checkpoint: keep it, no new notice
            if (hasCheckpoint && key != "boss" && (transform.position - checkpointPos).sqrMagnitude < 900f) { checkpointKey = key; return; }
            // not next to a running blob spawner (the factory core hall): waking up among fresh blobs is a death loop.
            // Keep the old checkpoint; the next check (Rex moved on) tries again.
            if (key != "boss" && NearActiveSpawner(transform.position)) return;
            checkpointKey = key;
            checkpointZone = key;
            hasCheckpoint = true;
            checkpointPos = transform.position;
            checkpointYaw = transform.eulerAngles.y;
            RexTestHUD.Notice("CHECKPOINT", 1.2f);
            // kept across sessions (GameFlow wakes a returning player here), but never the boss one: a returning
            // player must not wake beside King Glorp. Nor one whose goal is already done.
            if (key != "boss" && !Stale(key)) WriteSaved(checkpointPos, checkpointYaw, key);
        }

        /// <summary>A checkpoint whose zone no longer matters: the ruins once the factory is stopped, the cave, ship
        /// and boss arena once King Glorp is popped.</summary>
        static bool Stale(string zone)
        {
            if (zone == "ruins") return FactoryCore.Stopped;
            if (zone == "cave" || zone == "ship" || zone == "boss") return PlayerPrefs.GetInt(KingGlorpBoss.SaveKey, 0) == 1;
            return false;       // "spot" (where Rex was when the game quit): always fine
        }

        /// <summary>"x,y,z,yaw,zone" in the invariant culture.</summary>
        static void WriteSaved(Vector3 p, float yaw, string zone)
        {
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            PlayerPrefs.SetString(SavedCheckpointKey, string.Format(ic, "{0:0.###},{1:0.###},{2:0.###},{3:0.#},{4}",
                p.x, p.y, p.z, yaw, zone));
            PlayerPrefs.Save();
        }

        /// <summary>A running blob spawner whose spawn circle (its radius + 5 m) holds p: waking up among fresh
        /// blobs is a death loop.</summary>
        static bool NearActiveSpawner(Vector3 p)
        {
            foreach (var sp in FindObjectsByType<BlobSpawner>(FindObjectsSortMode.None))
            {
                float r = sp.radius + 5f;
                if (sp.isActiveAndEnabled && (sp.transform.position - p).sqrMagnitude < r * r) return true;
            }
            return false;
        }

        /// <summary>Drop checkpoints made stale by a big goal (on Start and whenever a big goal is done): the saved
        /// one is deleted, the one of this session forgotten.</summary>
        void DropStaleCheckpoints()
        {
            if (hasCheckpoint && Stale(checkpointZone)) { hasCheckpoint = false; checkpointKey = null; }
            if (ParseSaved(out Vector3 sp, out float sy, out string sz) && (sz == "boss" || Stale(sz))) ClearSavedCheckpoint();
        }

        /// <summary>QUIT TO TITLE / app quit: remember where Rex is, so a returning player wakes there. Only a calm
        /// spot: playing (not at the title), alive, on the ground, not in water or the cart, not in the tutorial,
        /// no boss fight and no living blob within 20 m. Otherwise the last saved checkpoint stays.</summary>
        public static void SaveQuitSpot()
        {
            var r = instance;
            if (r == null || r.ctrl == null || r.health == null || r.health.IsDead) return;
            if (GameFlow.Busy || GameFlow.TutorialActive || KingGlorpBoss.Fighting || GolfCart.Driven != null) return;
            if (!r.ctrl.Grounded || r.ctrl.IsSwimming || r.ctrl.WaterDepth > 0f || r.ctrl.Locked) return;
            Vector3 p = r.transform.position;
            if (Blob.LivingNear(p, 20f) > 0) return;
            if (!CalmGround(r.transform, p) || NearActiveSpawner(p)) return;
            WriteSaved(p, r.transform.eulerAngles.y, "spot");
        }

        /// <summary>A quit spot Rex can wake at again: not on something that moves (an elevator cab wakes him in the
        /// shaft, a rigidbody or the cart moves away) and not in / next to a zone door trigger (wakes him mid-load).</summary>
        static bool CalmGround(Transform rex, Vector3 p)
        {
            foreach (var g in Physics.RaycastAll(p + Vector3.up * 0.5f, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (g.transform.IsChildOf(rex)) continue;                     // Rex's own capsule
                if (g.rigidbody != null || g.collider.GetComponentInParent<Elevator>() != null ||
                    g.collider.GetComponentInParent<GolfCart>() != null) return false;
            }
            foreach (var c in Physics.OverlapSphere(p + Vector3.up, 1.5f, ~0, QueryTriggerInteraction.Collide))
                if (c.GetComponentInParent<ZoneDoor>() != null) return false;
            return true;
        }

        void OnApplicationQuit() => SaveQuitSpot();
        void OnApplicationPause(bool paused) { if (paused) SaveQuitSpot(); }     // mobile: backgrounded apps get killed

        /// <summary>Wake grace: Rex takes no damage for s seconds (a blob near a wake-up spot gets no free hit).</summary>
        public static void WakeGrace(float s)
        {
            if (instance != null && instance.health != null) instance.health.Invulnerable(s);
        }

        /// <summary>PlayerPrefs key of the last saved checkpoint (cave, ruins, ship, or the quit spot):
        /// "x,y,z,yaw,zone" (older saves: "x,y,z,yaw").</summary>
        public const string SavedCheckpointKey = "rm_cp";
        /// <summary>NEW GAME: forget the saved checkpoint.</summary>
        public static void ClearSavedCheckpoint()
        {
            if (!PlayerPrefs.HasKey(SavedCheckpointKey)) return;
            PlayerPrefs.DeleteKey(SavedCheckpointKey);
            PlayerPrefs.Save();
        }

        /// <summary>The saved checkpoint to wake a returning player at, if any: stale ones (goal done, old boss saves)
        /// are deleted, one within a running blob spawner's radius + 5 m is skipped. A hit gives Rex 3 s of wake grace
        /// (GameFlow calls this right before the wake teleport).</summary>
        public static bool TryLoadSavedCheckpoint(out Vector3 pos, out float yaw)
        {
            if (!ParseSaved(out pos, out yaw, out string zone)) return false;
            if (zone == "boss" || Stale(zone)) { ClearSavedCheckpoint(); return false; }
            if (NearActiveSpawner(pos)) return false;
            WakeGrace(3f);
            return true;
        }

        /// <summary>Parse the saved checkpoint (invariant culture). Older 4-part saves get their zone from the
        /// position; one outside every checkpoint zone was the boss checkpoint.</summary>
        static bool ParseSaved(out Vector3 pos, out float yaw, out string zone)
        {
            pos = Vector3.zero; yaw = 0f; zone = null;
            var parts = PlayerPrefs.GetString(SavedCheckpointKey, "").Split(',');
            if (parts.Length != 4 && parts.Length != 5) return false;
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var f = new float[4];
            for (int i = 0; i < 4; i++)
                if (!float.TryParse(parts[i], System.Globalization.NumberStyles.Float, ic, out f[i])) return false;
            pos = new Vector3(f[0], f[1], f[2]); yaw = f[3];
            if (parts.Length == 5) { zone = parts[4]; return true; }
            zone = "boss";
            foreach (var z in GameGuide.Zones)
                if (System.Array.IndexOf(CheckpointZones, z.id) >= 0 && GameGuide.InZone(z, pos)) { zone = z.id; break; }
            return true;
        }

        // ---- big-goal reward: max health 100 + 25 per big goal done (factory stopped, King Glorp popped)
        int bigGoals = -1;
        float nextGoalCheck;

        void UpdateMaxHealth()
        {
            int n = (FactoryCore.Stopped ? 1 : 0) + (PlayerPrefs.GetInt(KingGlorpBoss.SaveKey, 0) == 1 ? 1 : 0);
            if (n == bigGoals) return;
            bool rose = bigGoals >= 0 && n > bigGoals;
            bigGoals = n;
            float oldMax = health.maxHealth, max = 100f + 25f * n;
            health.maxHealth = max;
            if (max > oldMax) health.Heal(max - oldMax);          // the new headroom comes filled (Heal clamps, not when dead)
            else if (health.Current > max) health.Heal(0f);       // Heal clamps Current down to the lower max
            if (rose) RexTestHUD.Notice("MAX HEALTH UP!", 2f);
            DropStaleCheckpoints();          // first call (Start) and each big goal done: ruins / ship / cave / boss go
        }

        void Start()
        {
            UpdateMaxHealth();
            // a returning player: a death near the saved checkpoint wakes there too (not only in this session).
            // Not the quit "spot": that is only for the one wake after PLAY (GameFlow); deaths use cave / ruins / ship or 304.
            if (!hasCheckpoint && ParseSaved(out Vector3 cp, out float cpYaw, out string cpZone) && cpZone != "boss"
                && cpZone != "spot" && !Stale(cpZone) && !NearActiveSpawner(cp))
            { hasCheckpoint = true; checkpointPos = cp; checkpointYaw = cpYaw; checkpointZone = cpZone; }
        }

        void Update()
        {
            if (Time.time > nextCheckpointCheck) { nextCheckpointCheck = Time.time + 0.5f; UpdateCheckpoint(); }
            if (Time.time > nextGoalCheck) { nextGoalCheck = Time.time + 1f; UpdateMaxHealth(); }    // two PlayerPrefs reads a second
            if (lowSaid && health.Fraction > 0.5f) lowSaid = false;
            // slow regen out of the fight: +2 HP/s after 6 s without damage and no living blob within 15 m
            // (food still matters in a fight; a long walk does not leave Rex at 10 HP). Blob check twice a second.
            if (health.IsDead || health.Fraction >= 1f || Time.time - health.LastDamageTime < 6f) return;
            if (Time.time > nextCalmCheck) { nextCalmCheck = Time.time + 0.5f; calm = Blob.LivingNear(transform.position, 15f) == 0; }
            if (calm) health.Heal(2f * Time.deltaTime);       // Heal clamps at max
        }
        float nextCalmCheck;
        bool calm;

        void Awake()
        {
            instance = this;
            health = GetComponent<Health>();
            ctrl = GetComponent<RexController>();
            startPos = transform.position;
            startYaw = transform.eulerAngles.y;
            health.Damaged += (a, p, by) =>
            {
                if (health.IsDead || health.Current <= 0f) return;    // the killing blow: no flinch saved for the respawn
                ctrl.Hurt();
                if (health.Fraction < 0.25f && !lowSaid) { lowSaid = true; RexVoice.Say(gameObject, "LowHealth", true); }
                else RexVoice.Say(gameObject, "Hurt");
            };
            health.Died += by => StartCoroutine(DieRoutine());
            health.Damaged += (a, p, by) => UpdateLimp();
        }

        IEnumerator DieRoutine()
        {
            ctrl.Die();
            Progress.Add("deaths");
            RexVoice.Say(gameObject, "Death", true);
            yield return new WaitForSeconds(3.5f);
            Vector3 p = respawnPoint != null ? respawnPoint.position : startPos;
            float y = respawnPoint != null ? respawnPoint.eulerAngles.y : startYaw;
            // the checkpoint only when Rex died nearer to it than to his room (a death back at the resort wakes in 304)
            // and not into a crowd: 3+ living blobs within 20 m of it (a siege or wave still there) wakes him in 304.
            // The boss checkpoint stays (the king and his adds are always there; the fight resets around it).
            if (hasCheckpoint && (transform.position - checkpointPos).sqrMagnitude < (transform.position - p).sqrMagnitude
                && (checkpointZone == "boss" || Blob.LivingNear(checkpointPos, 20f) < 3))
            { p = checkpointPos; y = checkpointYaw; }
            ctrl.Teleport(p, y);
            if (ctrl.cameraRig != null) ctrl.cameraRig.SetYaw(y);      // the view faces the way Rex wakes up
            if (ctrl.bodyAnimator) ctrl.bodyAnimator.Play("Locomotion", 0, 0f);
            if (ctrl.armsAnimator && ctrl.armsAnimator.gameObject.activeInHierarchy) ctrl.armsAnimator.Play("Locomotion", 0, 0f);
            health.ResetHealth();
            ctrl.Revive();
            health.Invulnerable(2f);                   // respawn grace: a blob waiting at the checkpoint gets no free hit
            RexVoice.Say(gameObject, "Respawn", true);
            UpdateLimp();                              // full health again: the limp / hurt-in-fight flags clear now
        }
    }
}
