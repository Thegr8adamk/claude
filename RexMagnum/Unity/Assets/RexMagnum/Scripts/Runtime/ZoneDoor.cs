using System.Collections;
using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Door between two places (cave mouth -> underground tunnels -> ruins back door / ship). When Rex walks in,
    /// the screen fades out, Rex moves to the target point, and the screen fades in. Pairs of doors point at each
    /// other's exit point (a little in front of the other door, so Rex does not bounce back).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class ZoneDoor : MonoBehaviour
    {
        public Transform exit;
        public string label = "";
        static float busyUntil;
        static float fade;

        void Reset() { GetComponent<BoxCollider>().isTrigger = true; }

        void OnTriggerEnter(Collider other)
        {
            var rex = other.GetComponentInParent<RexController>();
            if (rex == null || exit == null || Time.time < busyUntil || GolfCart.Driven != null) return;
            busyUntil = Time.time + 2f;
            rex.StartCoroutine(Go(rex));
        }

        IEnumerator Go(RexController rex)
        {
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.35f) { fade = t; yield return null; }
            fade = 1f;
            rex.Teleport(exit.position, exit.eulerAngles.y);
            yield return new WaitForSeconds(0.25f);
            for (float t = 1f; t > 0f; t -= Time.deltaTime / 0.5f) { fade = t; yield return null; }
            fade = 0f;
        }

        void OnGUI()
        {
            if (fade <= 0.001f) return;
            GUI.color = new Color(0, 0, 0, fade);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
