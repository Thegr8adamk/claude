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
            if (!TryGetComponent(out voice)) voice = gameObject.AddComponent<AudioSource>();
            voice.spatialBlend = 1f;
            var rc = FindFirstObjectByType<RexController>();
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
            if (RMInput.Taunt && rexDist < 6f && Time.time > nextTalk)
            {
                nextTalk = Time.time + 3f;
                if (Random.value < 0.5f) Say("(laughs)");
                else { Say("(throws a drink)"); SlimeFX.Splat(rex.position + Vector3.up * 1.6f, Vector3.up, new Color(0.8f, 0.6f, 0.2f), 6); }
            }
            // Talk: only when Rex's hands are down
            if (rexCtrl != null && rexCtrl.HandsDown && RMInput.ActionDown && rexDist < 2.5f && Time.time > nextTalk)
            {
                nextTalk = Time.time + 5f;
                StartCoroutine(Flirt());
            }
            if (Time.time < nextThink || !agent.isOnNavMesh) return;
            nextThink = Time.time + 0.4f;
            NearestBlob(out float blobDist);
            // brief: at night guests lock themselves in (they walk back to their spot and stay out of sight)
            if (!security && DayNight.Night > 0.6f && state != State.Rescued && state != State.Panic)
            {
                if (Vector3.Distance(transform.position, home) > 1f) agent.SetDestination(home);
                return;
            }
            switch (state)
            {
                case State.Idle:
                case State.Hiding:
                    if (!security && blobDist < panicRadius) Panic();
                    else if (state == State.Hiding && blobDist > panicRadius * 1.5f && rexDist < 3f && WeaponPickup.Nearby == null && FoodPickup.Nearby == null && RMInput.Use)
                        Rescue();
                    break;
                case State.Panic:
                    if (!agent.pathPending && agent.remainingDistance < 0.6f) { state = State.Hiding; agent.speed = 1.4f; }
                    break;
                case State.Rescued:
                    if (!agent.pathPending && agent.remainingDistance < 0.8f) gameObject.SetActive(false);   // safe inside
                    break;
            }
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
            Say(GuestLines.Scream());
        }

        void Rescue()
        {
            state = State.Rescued;
            RMInput.ConsumeUse();
            agent.speed = 2.2f;
            Progress.Add("rescues");
            if (rex != null) RexVoice.Say(rex.gameObject, "Rescue", true);
            Vector3 drop = transform.position + transform.forward * 0.8f + Vector3.up * 0.3f;
            if (Random.value < 0.4f || rewardPrefabs == null || rewardPrefabs.Length == 0)   // brief: ammo / health / clue
                FoodPickup.Spawn((FoodPickup.Kind)Random.Range(0, 3), drop, 0f);
            else
                Instantiate(rewardPrefabs[Random.Range(0, rewardPrefabs.Length)], drop - Vector3.up * 0.3f, Quaternion.identity);
            agent.SetDestination(home);
        }

        IEnumerator Flirt()
        {
            var pair = security ? GuestLines.Carmen() : GuestLines.FlirtFor(guestName);
            if (security) Progress.Add("carmen_flirts");
            RexVoice.Subtitle?.Invoke("REX: " + pair.rex);
            yield return new WaitForSeconds(2.2f);
            Say(pair.answer, pair.index);
            yield return new WaitForSeconds(1.8f);
            if (!security && rex != null) RexVoice.Say(rex.gameObject, "FlirtFail", true);
        }

        void Say(string line, int index = -1)
        {
            RexVoice.Subtitle?.Invoke($"{guestName.ToUpper()}: {line}");
            if (index >= 0)
            {
                var clip = Resources.Load<AudioClip>($"Voice/Guests/{guestName}/answer_{index + 1:00}");
                if (clip != null) voice.PlayOneShot(clip);
            }
        }
    }

    /// <summary>Lines from the brief (section 9): Rex's flirt take -> the guest's answer; Carmen's answers.</summary>
    public static class GuestLines
    {
        public struct Pair { public string rex, answer; public int index; }
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
            return new Pair { rex = Flirts[pick].rex, answer = Flirts[pick].answer, index = mine.IndexOf(pick) };
        }

        public static Pair Carmen()
        {
            int i = (carmenLast + 1) % CarmenLines.Length;
            carmenLast = i;
            return new Pair { rex = CarmenLines[i].rex, answer = CarmenLines[i].answer, index = i };
        }

        static readonly string[] Screams = { "(screams)", "Help!", "It's in the pool!", "Not the face!", "Run!" };
        public static string Scream() => Screams[Random.Range(0, Screams.Length)];
    }
}
