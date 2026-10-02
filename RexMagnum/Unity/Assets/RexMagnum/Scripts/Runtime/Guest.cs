using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace RexMagnum
{
    /// <summary>
    /// Resort guest (brief section 3): runs from blobs and hides; can be rescued (no blob near, Rex close, press Use)
    /// and then gives ammo / health and a line; talks only when Rex's hands are down (Action = Talk: Rex flirts, the
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

        void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            anim = GetComponentInChildren<Animator>();
            if (Blob.Loot == null && rewardPrefabs != null && rewardPrefabs.Length > 0) Blob.Loot = rewardPrefabs;   // ammo pickups for blob drops
            if (!TryGetComponent(out voice)) voice = gameObject.AddComponent<AudioSource>();
            voice.spatialBlend = 1f;
            var rc = FindAnyObjectByType<RexController>();
            if (rc != null) { rex = rc.transform; rexCtrl = rc; }
            home = transform.position;
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

        void Update()
        {
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
            if (state == State.Hiding && !lockedIn && rexDist < 3f && agent.isOnNavMesh && RMInput.Use &&
                WeaponPickup.Nearby == null && FoodPickup.Nearby == null)
            {
                NearestBlob(out float safeDist);
                if (safeDist > panicRadius * 1.5f) Rescue();
            }
            if (Time.time < nextThink || !agent.isOnNavMesh) return;
            nextThink = Time.time + 0.4f;
            NearestBlob(out float blobDist);
            // brief: at night guests lock themselves in their rooms. They walk back to their spot, then go inside
            // (hidden); knocking (Use / Action near the spot) gets a muffled answer through the door. At dawn they
            // come out again.
            if (!security && DayNight.Night > 0.6f && state != State.Rescued && state != State.Panic)
            {
                if (!lockedIn)
                {
                    if (Vector3.Distance(transform.position, home) > 1.2f) agent.SetDestination(home);
                    else SetLockedIn(true);
                }
                return;
            }
            if (lockedIn) SetLockedIn(false);
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
                    if (!agent.pathPending && agent.remainingDistance < 0.8f) gameObject.SetActive(false);   // safe inside
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

        void Panic()
        {
            state = State.Panic;
            agent.speed = 4.5f;
            Vector3 target = transform.position + (transform.position - (rex != null ? rex.position : transform.position)).normalized * 15f;
            if (hideSpots != null && hideSpots.Length > 0)
            {
                Transform best = null;
                float bd = float.MaxValue;
                foreach (var h in hideSpots)
                {
                    if (h == null) continue;
                    float d = Vector3.Distance(h.position, transform.position);
                    if (d < bd) { bd = d; best = h; }
                }
                if (best != null) target = best.position;
            }
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
            float roll = Random.value;
            if (roll < 0.3f)                                              // brief: ammo / health / clue
                RexVoice.Subtitle?.Invoke(guestName.ToUpper() + ": " + Clues[Random.Range(0, Clues.Length)]);
            else if (roll < 0.6f || rewardPrefabs == null || rewardPrefabs.Length == 0)
                FoodPickup.Spawn((FoodPickup.Kind)Random.Range(0, 3), drop, 0f);
            else
                Instantiate(rewardPrefabs[Random.Range(0, rewardPrefabs.Length)], drop - Vector3.up * 0.3f, Quaternion.identity);
            agent.SetDestination(home);
        }

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
