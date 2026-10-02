using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace RexMagnum
{
    /// <summary>
    /// Resort guest (brief section 3): runs from blobs and hides; can be rescued (no blob within 10 m, Rex close, press Use)
    /// and then gives a clue (saved for the pause map) plus ammo / health; talks only when Rex's hands are down (Action = Talk: Rex flirts, the
    /// guest answers, brief section 9); reacts to the taunt (laughs or throws a drink). Carmen (security) never runs:
    /// she stays at her post and always rejects Rex. Uses Rex's animator (Humanoid): Speed, Hit.
    /// Voice: Resources/Voice/Guests/&lt;Name&gt;/answer_NN (optional; subtitles always).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class Guest : MonoBehaviour
    {
        public string guestName = "Guest";
        public bool security;
        public float panicRadius = 12f;
        public Transform[] hideSpots;
        public GameObject[] rewardPrefabs;      // ammo / food pickups dropped on rescue

        enum State { Idle, Panic, Hiding, Rescued }
        State state;
        NavMeshAgent agent;
        Animator anim;
        Transform rex;
        RexController rexCtrl;
        AudioSource voice;
        Vector3 home;
        float nextThink, nextTalk;
        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int HitHash = Animator.StringToHash("Hit");
        /// <summary>No living blob this close: the guest dares to come out (rescue).</summary>
        public const float SafeRadius = 10f;
        /// <summary>Active guests (HUD Use prompts, objective marker).</summary>
        public static readonly List<Guest> All = new List<Guest>();
        /// <summary>Hiding and reachable: Use near the guest rescues (when no blob is near).</summary>
        public bool CanRescue => !lockedIn && !security &&
                                 (state == State.Hiding || (state == State.Idle && lastBlobDist < BlobsAroundRadius));
        /// <summary>An idle guest with blobs around (but none within SafeRadius) can be rescued too.</summary>
        public const float BlobsAroundRadius = 30f;
        float lastBlobDist = float.MaxValue;
        Health rexHealth;
        bool indoors, sheltering;      // home inside the hotel (locks in at night); outdoors: walks to a hide spot
        public bool Rescued => state == State.Rescued;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            anim = GetComponentInChildren<Animator>();
            if (Blob.Loot == null && rewardPrefabs != null && rewardPrefabs.Length > 0) Blob.Loot = rewardPrefabs;   // ammo pickups for blob drops
            if (!TryGetComponent(out voice)) voice = gameObject.AddComponent<AudioSource>();
            voice.spatialBlend = 1f;
            var rc = FindAnyObjectByType<RexController>();
            if (rc != null) { rex = rc.transform; rexCtrl = rc; rexHealth = rc.GetComponent<Health>(); }
            home = transform.position;
            indoors = HomeIndoors();
            agent.speed = 1.4f;
        }

        Blob NearestBlob(out float dist)
        {
            Blob best = null;
            dist = float.MaxValue;
            foreach (var b in Blob.All)
            {
                if (b == null || b.IsDead) continue;
                float d = Vector3.Distance(b.transform.position, transform.position);
                if (d < dist) { dist = d; best = b; }
            }
            return best;
        }

        // far away and off screen: Animator and NavMeshAgent off (dozens of guests over the island cost CPU for nothing)
        const float SleepDistance = 70f;
        bool dormant, hadPath;
        float nextSleepCheck;
        Vector3 savedDestination;

        void SleepCheck()
        {
            if (Time.time < nextSleepCheck) return;
            nextSleepCheck = Time.time + Random.Range(0.4f, 0.6f);          // spread over the frames
            bool far = (rex.position - transform.position).sqrMagnitude > SleepDistance * SleepDistance;
            bool seen = false;
            var cam = Camera.main;
            if (far && cam != null)
            {
                Vector3 v = cam.WorldToViewportPoint(transform.position + Vector3.up);
                seen = v.z > 0f && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f;
            }
            bool sleep = far && !seen && state != State.Rescued && !lockedIn;     // rescued: let them walk off and go
            if (sleep == dormant) return;
            dormant = sleep;
            if (anim != null) anim.enabled = !sleep;
            if (sleep)
            {
                hadPath = agent.enabled && agent.isOnNavMesh && agent.hasPath;
                if (hadPath) savedDestination = agent.destination;
                agent.enabled = false;
            }
            else
            {
                agent.enabled = true;
                if (hadPath && agent.isOnNavMesh) agent.SetDestination(savedDestination);   // where it was going
            }
        }

        void Update()
        {
            if (rex != null) SleepCheck();
            if (dormant) return;
            if (anim != null) anim.SetFloat(SpeedHash, agent.velocity.magnitude, 0.1f, Time.deltaTime);
            if (rex == null) return;
            float rexDist = Vector3.Distance(rex.position, transform.position);

            // Rex's taunt near a guest: laugh or a drink in the face
            if (!lockedIn && RMInput.Taunt && rexDist < 6f && Time.time > nextTalk)
            {
                nextTalk = Time.time + 3f;
                if (Random.value < 0.5f) Say("(laughs)");
                else { Say("(throws a drink)"); SlimeFX.Splat(rex.position + Vector3.up * 1.6f, Vector3.up, new Color(0.8f, 0.6f, 0.2f), 6); }
            }
            // Talk: only when Rex's hands are down
            if (!lockedIn && rexCtrl != null && rexCtrl.HandsDown && RMInput.ActionDown && rexDist < 2.5f && Time.time > nextTalk)
            {
                nextTalk = Time.time + 5f;
                StartCoroutine(Flirt());
            }
            if (lockedIn)
            {
                // knocking: checked every frame (a button press lasts one frame)
                if (rexDist < 3f && (RMInput.Use || RMInput.ActionDown) && Time.time > nextTalk)
                {
                    nextTalk = Time.time + 4f;
                    RexVoice.Subtitle?.Invoke(guestName.ToUpper() + " (behind a locked door): " + LockedLines[Random.Range(0, LockedLines.Length)]);
                }
            }
            // rescue: checked every frame (a button press lasts one frame)
            // food nearby only blocks when Rex could eat it (at full health Use does not pick food up)
            if (CanRescue && rexDist < 3f && agent.isOnNavMesh && RMInput.Use && WeaponPickup.Nearby == null &&
                (FoodPickup.Nearby == null || (rexHealth != null && rexHealth.Fraction >= 1f)))
            {
                NearestBlob(out float safeDist);
                if (safeDist > SafeRadius) Rescue();
                else { RMInput.ConsumeUse(); RexTestHUD.Notice("Not safe yet: blobs nearby", 2f); }   // say why nothing happens
            }
            if (Time.time < nextThink || !agent.isOnNavMesh) return;
            nextThink = Time.time + 0.4f;
            NearestBlob(out float blobDist);
            lastBlobDist = blobDist;
            // brief: at night guests lock themselves in their rooms. They walk back to their spot, then go inside
            // (hidden); knocking (Use / Action near the spot) gets a muffled answer through the door. At dawn they
            // come out again.
            // Only guests whose home is inside the hotel have a room to lock; outdoor guests (pool, beach) walk to
            // the nearest hotel hide spot and wait there (visible), and go back home at dawn.
            if (!security && DayNight.Night > 0.6f && state != State.Rescued && state != State.Panic)
            {
                if (indoors)
                {
                    if (!lockedIn)
                    {
                        if (Vector3.Distance(transform.position, home) > 1.2f) agent.SetDestination(home);
                        else SetLockedIn(true);
                    }
                }
                else if (!sheltering)
                {
                    sheltering = true;
                    var spot = NearestHideSpot();
                    if (spot != null && NavMesh.SamplePosition(spot.position, out NavMeshHit sh, 6f, NavMesh.AllAreas)) agent.SetDestination(sh.position);
                }
                return;
            }
            if (lockedIn) SetLockedIn(false);
            if (sheltering)
            {
                sheltering = false;
                if (state != State.Rescued && state != State.Panic) agent.SetDestination(home);
            }
            switch (state)
            {
                case State.Idle:
                case State.Hiding:
                    // already hiding: stay put (panicking again only picks the same spot and screams every 0.8 s)
                    if (!security && state == State.Idle && blobDist < panicRadius) Panic();
                    break;
                case State.Panic:
                    if (!agent.pathPending && agent.remainingDistance < 0.6f) { state = State.Hiding; agent.speed = 1.4f; }
                    break;
                case State.Rescued:
                    // safe inside: not before 2 s, so nobody blinks out right in front of Rex
                    if (Time.time - rescuedAt > 2f && !agent.pathPending && agent.remainingDistance < 0.8f) gameObject.SetActive(false);
                    break;
            }
        }

        bool lockedIn;
        /// <summary>Inside the room for the night (hidden): no photos, no rescue.</summary>
        public bool LockedIn => lockedIn;
        static readonly string[] LockedLines =
        {
            "We are NOT opening this door!", "Go away! Come back when it's light!", "Is that Rex? Definitely not opening.",
            "There's a chair under the handle. Two chairs.", "Room service? ...No. Nice try, slime.",
        };

        void SetLockedIn(bool on)
        {
            lockedIn = on;
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = !on;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = !on;
            if (agent.isOnNavMesh) agent.isStopped = on;
        }

        Transform NearestHideSpot()
        {
            Transform best = null;
            if (hideSpots == null) return null;
            float bd = float.MaxValue;
            foreach (var h in hideSpots)
            {
                if (h == null) continue;
                float d = Vector3.Distance(h.position, transform.position);
                if (d < bd) { bd = d; best = h; }
            }
            return best;
        }

        /// <summary>Home inside the hotel: within the area of the hide spots (all inside the hotel) plus 5 m.</summary>
        bool HomeIndoors()
        {
            if (hideSpots == null) return false;
            bool any = false;
            var b = new Bounds();
            foreach (var h in hideSpots)
            {
                if (h == null) continue;
                if (!any) { b = new Bounds(h.position, Vector3.zero); any = true; }
                else b.Encapsulate(h.position);
            }
            if (!any) return false;
            b.Expand(new Vector3(10f, 200f, 10f));         // size: 5 m on each side, any floor
            return b.Contains(home);
        }

        void Panic()
        {
            state = State.Panic;
            agent.speed = 4.5f;
            Vector3 target = transform.position + (transform.position - (rex != null ? rex.position : transform.position)).normalized * 15f;
            var best = NearestHideSpot();
            if (best != null) target = best.position;
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 6f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
            Say(GuestLines.Scream(), -1, true);      // crowd line: does not push other subtitles off the screen
        }

        /// <summary>Rescue clues (brief section 3): where weapons and big goals are.</summary>
        static readonly string[] Clues =
        {
            "There's a shiny gun in the casino lounge. Downstairs.",
            "The dive shop on the beach has a harpoon gun!",
            "I saw a rifle up on the hotel roof.",
            "The buffet freezer has a fish the size of a man. Just saying.",
            "The steak knives are at the buffet. Don't ask why I know.",
            "Golf clubs are in the pro shop. West side.",
            "The slime comes from the old temple ruins. Something glows in there.",
            "There's a ship on top of the big waterfall. A big gold one lives in it.",
            "The cave behind the waterfall goes all the way to the ruins.",
            "Coconuts at the tiki bar. They go boom.",
        };

        void Rescue()
        {
            state = State.Rescued;
            RMInput.ConsumeUse();
            agent.speed = 2.2f;
            Progress.Add("rescues");
            if (rex != null) RexVoice.Say(rex.gameObject, "Rescue", true);
            Vector3 drop = transform.position + transform.forward * 0.8f + Vector3.up * 0.3f;
            // brief: ammo / health / clue. Every rescue gives a clue (a new one while any are left, kept in the pause
            // map), and also ammo or food
            int start = Random.Range(0, Clues.Length);
            string clue = Clues[start];
            for (int i = 0; i < Clues.Length; i++)
                if (Progress.AddClue(Clues[(start + i) % Clues.Length])) { clue = Clues[(start + i) % Clues.Length]; break; }
            // own HUD line (not a subtitle): Rex's rescue line does not push it off, and it shows with subtitles off
            RexTestHUD.Notice($"CLUE from {guestName}: \"{clue}\"  (Pause > Map keeps it)", 6f);
            if (Random.value < 0.5f || rewardPrefabs == null || rewardPrefabs.Length == 0)
                FoodPickup.Spawn((FoodPickup.Kind)Random.Range(0, 3), drop, 0f);
            else
                Instantiate(rewardPrefabs[Random.Range(0, rewardPrefabs.Length)], drop - Vector3.up * 0.3f, Quaternion.identity);
            rescuedAt = Time.time;
            Vector3 goal = home;
            // rescued at the home spot: go somewhere first (the nearest hide spot, or 6 m away from Rex), not vanish on the spot
            if (Vector3.Distance(transform.position, home) < 3f)
            {
                var spot = NearestHideSpot();
                if (spot != null && Vector3.Distance(spot.position, transform.position) > 3f) goal = spot.position;
                else
                {
                    Vector3 away = transform.position - (rex != null ? rex.position : transform.position - transform.forward);
                    away.y = 0f;
                    goal = transform.position + (away.sqrMagnitude > 0.01f ? away.normalized : transform.forward) * 6f;
                }
                if (NavMesh.SamplePosition(goal, out NavMeshHit gh, 4f, NavMesh.AllAreas)) goal = gh.position;
            }
            agent.SetDestination(goal);
        }
        float rescuedAt;

        IEnumerator Flirt()
        {
            var pair = security ? GuestLines.Carmen() : GuestLines.FlirtFor(guestName);
            if (security) Progress.Add("carmen_flirts");
            // Rex's flirt take, voiced (Resources/Voice/Rex/flirt_NN or carmen_NN)
            float t = 2.2f;
            if (rex != null) t = RexVoice.SayLine(rex.gameObject, "REX", pair.rex, security ? $"Voice/Rex/carmen_{pair.global + 1:00}" : $"Voice/Rex/flirt_{pair.global + 1:00}");
            else RexVoice.Subtitle?.Invoke("REX: " + pair.rex);
            yield return new WaitForSeconds(t + 0.25f);          // the answer starts when Rex's line ends
            t = Say(pair.answer, pair.index);
            yield return new WaitForSeconds(t + 0.25f);
            if (!security && rex != null) RexVoice.Say(rex.gameObject, "FlirtFail", true);
        }

        /// <summary>Show and play a line; returns its length in seconds (clip, or reading time).</summary>
        float Say(string line, int index = -1, bool crowd = false)
        {
            AudioClip clip = index >= 0 ? Resources.Load<AudioClip>($"Voice/Guests/{guestName}/answer_{index + 1:00}") : null;
            if (clip != null) voice.PlayOneShot(clip, AudioMix.Voice);
            float length = clip != null ? clip.length : Mathf.Clamp(line.Length * 0.06f, 1.5f, 6f);
            string text = $"{guestName.ToUpper()}: {line}";
            if (crowd && RexVoice.SubtitleCrowd != null) RexVoice.SubtitleCrowd(text);
            else RexVoice.ShowSubtitle(text, length + 0.4f);
            return length;
        }
    }

    /// <summary>Lines from the brief (section 9): Rex's flirt take -> the guest's answer; Carmen's answers.</summary>
    public static class GuestLines
    {
        public struct Pair { public string rex, answer; public int index, global; }
        static readonly (string who, string rex, string answer)[] Flirts =
        {
            ("Callie", "I'm Rex Magnum.", "Who?"),
            ("Heather", "You might know me from the movies.", "My grandma does."),
            ("Lucy", "I just saved your life. Dinner?", "Put me back in the closet with the slime."),
            ("Lucy", "Is it hot out here, or is it me?", "It's the building. It's on fire."),
            ("Justin", "Room 304. The door's broken, so come right in.", "No."),
            ("Callie", "I've got a gold chain and a bar tab. What have you got?", "Standards."),
            ("Justin", "I do all my own stunts.", "Is breathing that hard one of them?"),
            ("Fin", "At my age, all night long means I fell asleep during.", "Why would you tell me that?"),
            ("Gerald", "Want to see my award? I don't have one. Want to see my room?", "I want to see a manager."),
            ("Gerald", "I was huge in the eighties.", "You're pretty big now. Around the middle."),
            ("Heather", "My ex-wives said I was unforgettable.", "In court?"),
            ("Fin", "Stick with me, baby. I know this island.", "You got lost in the lobby."),
        };
        static readonly (string rex, string answer)[] CarmenLines =
        {
            ("We make a good team.", "You broke a vending machine with your face."),
            ("After this, you, me, a bottle of wine?", "After this, you, me, your bar bill."),
            ("Admit it. You're impressed.", "I'm not impressed. I'm surprised. It's different."),
        };
        static readonly Dictionary<string, int> last = new Dictionary<string, int>();
        static int carmenLast = -1;

        public static Pair FlirtFor(string who)
        {
            var mine = new List<int>();
            for (int i = 0; i < Flirts.Length; i++) if (Flirts[i].who == who) mine.Add(i);
            if (mine.Count == 0) for (int i = 0; i < Flirts.Length; i++) mine.Add(i);
            int pick = mine[Random.Range(0, mine.Count)];
            if (mine.Count > 1 && last.TryGetValue(who, out int prev) && prev == pick) pick = mine[(mine.IndexOf(pick) + 1) % mine.Count];
            last[who] = pick;
            return new Pair { rex = Flirts[pick].rex, answer = Flirts[pick].answer, index = mine.IndexOf(pick), global = pick };
        }

        public static Pair Carmen()
        {
            int i = (carmenLast + 1) % CarmenLines.Length;
            carmenLast = i;
            return new Pair { rex = CarmenLines[i].rex, answer = CarmenLines[i].answer, index = i, global = i };
        }

        static readonly string[] Screams = { "(screams)", "Help!", "It's in the pool!", "Not the face!", "Run!" };
        public static string Scream() => Screams[Random.Range(0, Screams.Length)];
    }
}
