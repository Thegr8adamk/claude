using System.Collections;
using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// First playable flow (brief section 2 "Opening"):
    ///  1. Title screen over a slow island shot: title, content warning, "press any key", title music.
    ///  2. Chaos sounds, fade in on Rex face-down on the bed in room 304, wake-up line.
    ///  3. Rex stands next to the bed; controls are taught one hint at a time (look/walk, punch the stuck door,
    ///     pick up the beer, taunt at the mirror, camera switch, towel on the balcony rail). Day music loops.
    /// Music and sounds load from Resources/Music and Resources/Sfx when the ElevenLabs files are added
    /// (title_theme, day_resort, chaos); without them the flow still works, silently.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        public Camera titleCamera;
        public Camera playerCamera;
        public RexController rex;
        public Transform bedPoint, wakePoint;
        public Behaviour[] disableDuringTitle;
        public bool skipTitle;

        enum State { Title, Intro, Play }
        State state = State.Title;
        float fade = 1f, fadeTarget;
        AudioSource music, fight;
        const float DayVolume = 0.35f, FightVolume = 0.55f;
        int hint;
        float hintTime;
        Vector3 titleOrbitCenter;
        float titleAngle;
        GUIStyle big, mid, small, box;

        static readonly string[] Hints =
        {
            "Mouse / right stick: look.   W A S D / left stick: walk.   Shift: run.",
            "The door is stuck. Left click / RT: punch it.",
            "F / X: pick up the full beer bottle on the table. It is your first weapon.",
            "T / D-pad up: taunt. Try it at the mirror.",
            "V / R-stick click: first person or third person.   C: other shoulder.",
            "The beach towel is on the balcony rail. Look at the pool. Something is wrong.",
        };

        public static bool Busy { get; private set; }

        void Start()
        {
            Busy = !skipTitle;
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            music.spatialBlend = 0f;
            music.volume = 0.7f;
            // fight music (80s hair metal) fades in when 2+ living blobs are near Rex, day music ducks under it
            fight = gameObject.AddComponent<AudioSource>();
            fight.clip = Resources.Load<AudioClip>("Music/fight");
            fight.loop = true;
            fight.spatialBlend = 0f;
            fight.volume = 0f;
            if (titleCamera != null) titleOrbitCenter = titleCamera.transform.position;
            if (skipTitle) { StartCoroutine(Intro()); return; }
            SetPlayerActive(false);
            Play("Music/title_theme");
        }

        void SetPlayerActive(bool on)
        {
            if (rex != null) rex.InputEnabled = on;
            foreach (var b in disableDuringTitle) if (b != null) b.enabled = on;
            var pr = playerCamera ? playerCamera.GetComponent<RexCameraRig>() : null;
            if (pr) pr.InputEnabled = on;
            if (titleCamera != null) titleCamera.enabled = !on && state == State.Title;
            if (playerCamera != null) playerCamera.enabled = on || state != State.Title;
        }

        void Play(string res)
        {
            var clip = Resources.Load<AudioClip>(res);
            if (clip == null) return;
            music.clip = clip;
            music.Play();
        }

        void Update()
        {
            fade = Mathf.MoveTowards(fade, fadeTarget, Time.unscaledDeltaTime * 0.9f);
            if (state == State.Title)
            {
                if (titleCamera != null)
                {
                    titleAngle += Time.deltaTime * 1.5f;
                    titleCamera.transform.rotation = Quaternion.Euler(8f, -16f + Mathf.Sin(titleAngle * 0.05f) * 12f, 0f);
                }
                if (RMInput.AnyKey && fade < 0.05f) StartCoroutine(Intro());
            }
            else if (state == State.Play)
            {
                if (hint < Hints.Length && Time.time - hintTime > 9f)
                {
                    hint++;
                    hintTime = Time.time;
                }
                FightMusic();
            }
        }

        void FightMusic()
        {
            if (fight == null || fight.clip == null || rex == null) return;
            bool battle = !KingGlorpBoss.Fighting && Blob.LivingNear(rex.transform.position, 20f) >= 2;
            float target = battle ? FightVolume : 0f;
            // in fast (1 s), out slowly (4 s) so short pauses between blobs do not cut the song
            fight.volume = Mathf.MoveTowards(fight.volume, target, Time.deltaTime * (battle ? FightVolume : FightVolume / 4f));
            if (fight.volume > 0f && !fight.isPlaying) fight.Play();
            else if (fight.volume <= 0f && fight.isPlaying) fight.Pause();
            music.volume = KingGlorpBoss.Fighting ? 0f : DayVolume * (1f - fight.volume / FightVolume);
        }

        IEnumerator Intro()
        {
            state = State.Intro;
            fadeTarget = 1f;
            while (fade < 0.99f) yield return null;
            var chaos = Resources.Load<AudioClip>("Sfx/chaos");
            if (chaos != null) AudioSource.PlayClipAtPoint(chaos, bedPoint != null ? bedPoint.position : Vector3.zero, 1f);
            music.Stop();
            if (titleCamera != null) titleCamera.enabled = false;
            if (playerCamera != null) playerCamera.enabled = true;
            if (rex != null && bedPoint != null)
            {
                rex.Teleport(bedPoint.position, bedPoint.eulerAngles.y);
                rex.Die();                       // face-down pose
                var rig = playerCamera ? playerCamera.GetComponent<RexCameraRig>() : null;
                if (rig) { rig.InputEnabled = false; rig.enabled = true; }
            }
            yield return new WaitForSeconds(0.6f);
            fadeTarget = 0f;
            yield return new WaitForSeconds(1.2f);
            if (rex != null) RexVoice.Say(rex.gameObject, "Wake", true);
            yield return new WaitForSeconds(2.6f);
            fadeTarget = 1f;
            while (fade < 0.99f) yield return null;
            if (rex != null && wakePoint != null)
            {
                rex.Revive();
                rex.Teleport(wakePoint.position, wakePoint.eulerAngles.y);
                if (rex.bodyAnimator) rex.bodyAnimator.Play("Locomotion", 0, 0f);
                if (rex.armsAnimator) rex.armsAnimator.Play("Locomotion", 0, 0f);
            }
            state = State.Play;
            Busy = false;
            SetPlayerActive(true);
            Play("Music/day_resort");
            music.volume = DayVolume;
            fadeTarget = 0f;
            hintTime = Time.time;
        }

        void Styles()
        {
            if (big != null) return;
            big = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            big.normal.textColor = new Color(1f, 0.8f, 0.25f);
            mid = new GUIStyle(big) { fontSize = 30 };
            mid.normal.textColor = new Color(1f, 0.92f, 0.8f);
            small = new GUIStyle(big) { fontSize = 18, fontStyle = FontStyle.Normal, wordWrap = true };
            small.normal.textColor = Color.white;
            box = new GUIStyle(GUI.skin.box) { fontSize = 20, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            box.normal.textColor = Color.white;
        }

        void OnGUI()
        {
            Styles();
            float w = Screen.width, h = Screen.height, s = h / 1080f;
            if (state == State.Title)
            {
                var sh = new GUIStyle(big);
                sh.normal.textColor = new Color(0, 0, 0, 0.6f);
                big.fontSize = (int)(84 * s); sh.fontSize = big.fontSize;
                GUI.Label(new Rect(4 * s, h * 0.22f + 4 * s, w, 120 * s), "REX MAGNUM", sh);
                GUI.Label(new Rect(0, h * 0.22f, w, 120 * s), "REX MAGNUM", big);
                mid.fontSize = (int)(40 * s);
                GUI.Label(new Rect(0, h * 0.33f, w, 60 * s), "VACATION VIOLENCE", mid);
                small.fontSize = (int)(20 * s);
                GUI.Label(new Rect(w * 0.2f, h * 0.72f, w * 0.6f, 80 * s),
                    "CONTENT WARNING: crude adult humor, rude gestures, strong language, cartoon slime violence.", small);
                if (Mathf.Repeat(Time.unscaledTime, 1.2f) < 0.8f)
                    GUI.Label(new Rect(0, h * 0.82f, w, 50 * s), "PRESS ANY KEY", mid);
            }
            else if (state == State.Play && hint < Hints.Length)
            {
                box.fontSize = (int)(22 * s);
                GUI.Box(new Rect(w * 0.25f, h * 0.06f, w * 0.5f, 70 * s), Hints[hint], box);
            }
            if (fade > 0.001f)
            {
                GUI.color = new Color(0, 0, 0, fade);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }
    }
}
