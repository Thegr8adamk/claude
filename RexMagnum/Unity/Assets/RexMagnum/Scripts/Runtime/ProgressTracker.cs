using UnityEngine;

namespace RexMagnum
{
    /// <summary>Shows a toast when an accomplishment unlocks; tracks zone visits and nights survived.</summary>
    public class ProgressTracker : MonoBehaviour
    {
        string toast; float toastUntil;
        Transform rex;
        bool wasNight;
        static readonly (string zone, Vector3 at, float r)[] Zones =
        {
            ("hotel", new Vector3(40, 0, -300), 60), ("pool", new Vector3(40, 0, -255), 60), ("beach", new Vector3(40, 0, -345), 80),
            ("golf", new Vector3(-280, 0, -40), 110), ("jungle", new Vector3(60, 0, 60), 90), ("ruins", new Vector3(300, 0, 70), 45),
            ("cave", new Vector3(-160, 0, 230), 25), ("ship", new Vector3(40, 0, 300), 60),
        };

        void OnEnable() => Progress.Unlocked += Show;
        void OnDisable() => Progress.Unlocked -= Show;
        void Show(Progress.Def d) { toast = $"ACCOMPLISHMENT: {d.name}\n{d.desc}"; toastUntil = Time.unscaledTime + 4f; }

        void Start() { var r = FindAnyObjectByType<RexController>(); if (r != null) rex = r.transform; }

        float nextFlush;
        void OnApplicationPause(bool p) { if (p) Progress.Flush(); }
        void OnApplicationQuit() => Progress.Flush();

        void Update()
        {
            if (Time.unscaledTime > nextFlush) { nextFlush = Time.unscaledTime + 2f; Progress.Flush(); }
            if (rex != null && Time.frameCount % 30 == 0)
                foreach (var z in Zones)
                {
                    var p = rex.position; p.y = 0;
                    if (Vector3.Distance(p, z.at) < z.r) Progress.Visit(z.zone);
                }
            bool night = DayNight.Night > 0.8f;
            if (wasNight && !night) Progress.Add("nights");
            wasNight = night;
        }

        void OnGUI()
        {
            if (Time.unscaledTime > toastUntil) return;
            float w = Screen.width, h = Screen.height, s = h / 1080f;
            var r = new Rect(w - 520 * s, h * 0.25f, 480 * s, 90 * s);
            GUI.color = new Color(0, 0, 0, 0.7f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            var st = new GUIStyle(GUI.skin.label) { fontSize = (int)(20 * s), wordWrap = true, alignment = TextAnchor.MiddleCenter };
            st.normal.textColor = new Color(1f, 0.85f, 0.35f);
            GUI.Label(r, toast, st);
        }
    }
}
