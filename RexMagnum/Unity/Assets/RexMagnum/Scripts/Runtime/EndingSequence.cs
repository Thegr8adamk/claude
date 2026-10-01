using UnityEngine;

namespace RexMagnum
{
    /// <summary>The ending (brief section 2): the viral slip video, Marty's call with the floor-cleaner ad, the resort
    /// adds the damage to the bar tab, "I'm back." Then free play continues.</summary>
    public class EndingSequence : MonoBehaviour
    {
        float t;
        static readonly (float at, string text)[] Cards =
        {
            (0f, "A guest's phone video: Rex Magnum slips on slime. 41 million views."),
            (5f, "MARTY (phone): \"Rex, babe! You're trending! Floor cleaner. National TV ad. They want the slip.\""),
            (11f, "RESORT: \"Mr. Magnum, the damage has been added to your bar tab.\""),
            (16f, "REX: \"I'm back.\""),
            (21f, ""),
        };

        void Update()
        {
            t += Time.unscaledDeltaTime;
            if (t > 22f) Destroy(gameObject);
        }

        void OnGUI()
        {
            float w = Screen.width, h = Screen.height, s = h / 1080f;
            GUI.color = new Color(0, 0, 0, 0.75f);
            GUI.DrawTexture(new Rect(0, h * 0.35f, w, h * 0.3f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            string line = "";
            foreach (var c in Cards) if (t >= c.at) line = c.text;
            var st = new GUIStyle(GUI.skin.label) { fontSize = (int)(40 * s), alignment = TextAnchor.MiddleCenter, wordWrap = true, fontStyle = FontStyle.Bold };
            st.normal.textColor = new Color(1f, 0.85f, 0.4f);
            GUI.Label(new Rect(w * 0.1f, h * 0.35f, w * 0.8f, h * 0.3f), line, st);
        }
    }
}
