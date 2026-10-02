using UnityEngine;

namespace RexMagnum
{
    /// <summary>Shows a toast when an accomplishment unlocks; tracks zone visits and nights survived.</summary>
    public class ProgressTracker : MonoBehaviour
    {
        string toast; float toastUntil;
        Transform rex;
        bool wasNight;

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
                foreach (var z in GameGuide.Zones)        // zone centres: GameGuide (also the HUD compass and the map)
                    if (GameGuide.InZone(z, rex.position)) Progress.Visit(z.id);
            bool night = DayNight.Night > 0.8f;
            if (wasNight && !night) Progress.Add("nights");
            wasNight = night;
        }

        void OnGUI()
        {
            if (Time.unscaledTime > toastUntil) return;
            float w = Screen.width, h = Screen.height, s = h / 1080f;
            MenuSkin.Use(s);                 // shared menu look, no new style per event
            var r = new Rect(w - 520 * s, h * 0.25f, 480 * s, 90 * s);
            MenuSkin.DrawPanel(r);
            GUI.Label(r, toast, MenuSkin.Toast);
        }
    }
}
