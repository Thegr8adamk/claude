using UnityEngine;

namespace RexMagnum
{
    /// <summary>The ending (brief section 2), about 26 s, skippable after 3 s:
    ///  1. a guest's phone video: Rex slips on slime (portrait phone frame, REC, the view count climbs to 41 million);
    ///  2. Marty calls: the floor-cleaner TV ad;
    ///  3. the resort's bar-tab receipt with the damage on it;
    ///  4. Rex in his hero pose: "I'm back."
    /// Then free play continues.</summary>
    public class EndingSequence : MonoBehaviour
    {
        float t;
        Camera cam;
        RexController rex;
        bool slipped, heroed, hadInput;
        int lastCall = -1;

        static readonly string[] Marty =
        {
            "MARTY: Rex, babe! You're trending!",
            "MARTY: Floor cleaner. National TV ad. They want the slip. The SLIP, Rex.",
            "REX: Do I get a trailer?",
            "MARTY: You get a mop. Love ya, babe!",
        };
        static readonly (string item, string price)[] Tab =
        {
            ("Bar tab (carried over)", "$11,480.00"), ("Lobby glass, 3 panes", "$2,950.00"), ("Vending machine (face)", "$1,200.00"),
            ("Golf cart, battery abuse", "$640.00"), ("Pool cleaning (slime, King-size)", "$8,800.00"),
            ("Frozen swordfish, used", "$310.00"), ("Mini-bar, room 304", "$497.50"),
        };

        void Start()
        {
            rex = FindFirstObjectByType<RexController>();
            if (rex != null) { hadInput = rex.InputEnabled; rex.InputEnabled = false; }
            var main = Camera.main;
            cam = new GameObject("EndingCam").AddComponent<Camera>();
            if (main != null) { cam.CopyFrom(main); cam.depth = main.depth + 20f; }
            cam.fieldOfView = 50f;
        }

        void OnDestroy()
        {
            if (cam != null) Destroy(cam.gameObject);
            if (rex != null) rex.InputEnabled = hadInput;     // still off if he was at the golf cart's wheel
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            if (t > 3f && RMInput.AnyKey && t < 22f) t = 22f;                 // skip to "I'm back."
            if (rex == null) { if (t > 26f) Destroy(gameObject); return; }
            Vector3 p = rex.transform.position, f = rex.transform.forward, r = rex.transform.right;
            if (t < 7f)
            {
                // the phone video: hand-held, low, a little shaky, zooming in
                if (!slipped && t > 1.2f) { slipped = true; rex.SetTrigger(Animator.StringToHash("Slip")); }
                float zoom = Mathf.Lerp(5f, 3f, t / 7f);
                cam.transform.position = p + f * zoom + r * 1.2f + Vector3.up * 1.1f + Random.insideUnitSphere * 0.015f;
                cam.transform.LookAt(p + Vector3.up * 0.8f);
            }
            else if (t < 22f)
            {
                // the call and the receipt: slow push-in on Rex
                float k = (t - 7f) / 15f;
                cam.transform.position = p + f * Mathf.Lerp(3f, 2f, k) + r * 0.6f + Vector3.up * 1.6f;
                cam.transform.LookAt(p + Vector3.up * 1.5f);
            }
            else
            {
                // "I'm back.": low hero angle, hero pose
                if (!heroed) { heroed = true; if (rex.bodyAnimator != null) rex.bodyAnimator.Play("Locomotion", 0, 0f); RexVoice.Subtitle?.Invoke("REX: I'm back."); PlayLine("Voice/Ending/imback_01"); }
                cam.transform.position = p + f * 3.2f - r * 0.8f + Vector3.up * 0.5f;
                cam.transform.LookAt(p + Vector3.up * 1.4f);
            }
            if (t > 26f) Destroy(gameObject);
        }

        void PlayLine(string path)
        {
            var clip = Resources.Load<AudioClip>(path);
            if (clip != null) AudioSource.PlayClipAtPoint(clip, cam != null ? cam.transform.position : Vector3.zero, 1f);
        }

        void OnGUI()
        {
            float w = Screen.width, h = Screen.height, s = h / 1080f;
            var big = new GUIStyle(GUI.skin.label) { fontSize = (int)(44 * s), alignment = TextAnchor.MiddleCenter, wordWrap = true, fontStyle = FontStyle.Bold };
            var mid = new GUIStyle(big) { fontSize = (int)(30 * s) };
            var mono = new GUIStyle(GUI.skin.label) { fontSize = (int)(26 * s), font = null };
            if (t < 7f)
            {
                // portrait phone frame around the middle of the screen
                float pw = h * 0.5f, ph = h * 0.9f, px = (w - pw) / 2f, py = h * 0.05f;
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0, 0, px, h), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(px + pw, 0, w - px - pw, h), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(px, 0, pw, py), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(px, py + ph, pw, h - py - ph), Texture2D.whiteTexture);
                GUI.color = Mathf.Repeat(t, 1f) < 0.6f ? Color.red : new Color(1, 0, 0, 0.3f);
                GUI.DrawTexture(new Rect(px + 20 * s, py + 20 * s, 18 * s, 18 * s), Texture2D.whiteTexture);
                GUI.color = Color.white;
                mono.normal.textColor = Color.white;
                GUI.Label(new Rect(px + 46 * s, py + 12 * s, pw, 34 * s), "REC", mono);
                long views = (long)(41_000_000 * Mathf.Pow(Mathf.Clamp01((t - 2f) / 5f), 3f));
                big.normal.textColor = Color.white;
                GUI.Label(new Rect(px, py + ph - 200 * s, pw, 60 * s), $"{views:N0} views", big);
                mid.normal.textColor = new Color(0.8f, 0.9f, 1f);
                GUI.Label(new Rect(px, py + ph - 140 * s, pw, 120 * s), "washed-up action star vs. slime #maximumjustice #fail", mid);
                return;
            }
            GUI.color = new Color(0, 0, 0, 0.7f);
            if (t < 14f)
            {
                GUI.DrawTexture(new Rect(0, h * 0.72f, w, h * 0.2f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                int i = Mathf.Clamp((int)((t - 7f) / 1.75f), 0, Marty.Length - 1);
                if (i != lastCall) { lastCall = i; PlayLine($"Voice/Ending/call_{i + 1:00}"); }
                mid.normal.textColor = new Color(1f, 0.85f, 0.4f);
                GUI.Label(new Rect(w * 0.1f, h * 0.72f, w * 0.8f, h * 0.2f), (t < 7.8f ? "(phone rings) " : "") + Marty[i], mid);
                return;
            }
            if (t < 22f)
            {
                // the receipt
                float rw = h * 0.55f, rh = h * 0.8f, rx = w * 0.62f, ry = h * 0.1f;
                GUI.color = new Color(0.97f, 0.96f, 0.9f);
                GUI.DrawTexture(new Rect(rx, ry, rw, rh), Texture2D.whiteTexture);
                GUI.color = Color.white;
                mono.normal.textColor = new Color(0.1f, 0.1f, 0.1f);
                var head = new GUIStyle(mono) { fontSize = (int)(30 * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(rx, ry + 20 * s, rw, 40 * s), "ISLA GRANDE RESORT", head);
                GUI.Label(new Rect(rx, ry + 60 * s, rw, 30 * s), "Guest: R. MAGNUM, room 304", new GUIStyle(mono) { alignment = TextAnchor.MiddleCenter });
                int shown = Mathf.Clamp((int)((t - 14f) / 0.6f), 0, Tab.Length);
                for (int i = 0; i < shown; i++)
                {
                    GUI.Label(new Rect(rx + 24 * s, ry + (120 + i * 46) * s, rw * 0.65f, 40 * s), Tab[i].item, mono);
                    GUI.Label(new Rect(rx + rw * 0.68f, ry + (120 + i * 46) * s, rw * 0.3f, 40 * s), Tab[i].price, mono);
                }
                if (shown == Tab.Length)
                {
                    var tot = new GUIStyle(head) { alignment = TextAnchor.MiddleLeft };
                    GUI.Label(new Rect(rx + 24 * s, ry + rh - 90 * s, rw, 40 * s), "TOTAL DUE:  $25,877.50", tot);
                    GUI.Label(new Rect(rx + 24 * s, ry + rh - 50 * s, rw, 34 * s), "\"The damage has been added to your tab.\"", mono);
                }
                return;
            }
            GUI.color = Color.white;
            big.fontSize = (int)(110 * s);
            big.normal.textColor = new Color(1f, 0.8f, 0.25f);
            GUI.Label(new Rect(0, h * 0.62f, w, h * 0.3f), "I'M BACK.", big);
        }
    }
}
