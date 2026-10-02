using System.Collections;
using System.IO;
using UnityEngine;

namespace RexMagnum
{
    /// <summary>Photo button (brief section 8): saves a real screenshot to the album with a caption from the brief.</summary>
    public static class PhotoAlbum
    {
        static readonly string[] Captions =
        {
            "Me and a fan. #blessed #hireme", "New co-star. Less slimy than my last agent.",
            "Still got it. (Not sure what it is. Seeing a doctor.)", "Available for weddings, funerals, and car lot openings.",
            "Working vacation. #stunts #ownhip", "He wanted a selfie. He really didn't.", "58 and wet. #goals",
            "Swipe up to hire me. Please. Seriously.", "My plus-one for the premiere. If there is one.",
        };
        const string GlorpCaption = "Me and royalty. He's the one on the left. I think.";
        static int last = -1;

        /// <summary>Rex cannot attack until this time (the 2 s photo pose).</summary>
        public static float PoseUntil { get; private set; }

        public static void Take(MonoBehaviour host, string subject) => Take(host, subject, null, null);

        /// <summary>Brief section 8: Rex pulls the nearest blob or guest close, a front-camera selfie view shows for
        /// about 2 s (Rex cannot attack, can be hit), and that view is saved to the album.</summary>
        public static void Take(MonoBehaviour host, string subject, Transform rex, Transform target)
        {
            PoseUntil = Time.time + 2f;
            host.StartCoroutine(Capture(subject, rex, target));
        }

        static IEnumerator Capture(string subject, Transform rex, Transform target)
        {
            Camera selfie = null;
            var hidden = new System.Collections.Generic.List<Renderer>();
            var arms = new System.Collections.Generic.List<Renderer>();
            var rig = Object.FindAnyObjectByType<RexCameraRig>();
            if (rex != null)
            {
                // pull the subject to Rex's side (a stunned blob or a guest comes along; NavMesh agents warp)
                if (target != null)
                {
                    Vector3 spot = rex.position + rex.right * 0.95f + rex.forward * 0.1f;
                    var ag = target.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    Vector3 from = target.position;
                    for (float t = 0; t < 1f; t += Time.deltaTime / 0.3f)
                    {
                        if (target == null) break;
                        Vector3 p = Vector3.Lerp(from, spot, t * t * (3f - 2f * t));
                        if (ag != null && ag.enabled && ag.isOnNavMesh) ag.Warp(p); else target.position = p;
                        yield return null;
                    }
                    if (target != null) target.rotation = Quaternion.LookRotation(-rex.forward, Vector3.up);
                }
                // first person hides Rex's body from the camera (shadows only): show it for the selfie
                foreach (var r in rex.GetComponentsInChildren<Renderer>())
                    if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly)
                    {
                        hidden.Add(r);
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    }
                // the first-person arms float at the eye camera, right in front of the selfie camera: hide them
                if (rig != null && rig.fpArms != null)
                    foreach (var r in rig.fpArms.GetComponentsInChildren<Renderer>())
                        if (r.enabled) { arms.Add(r); r.enabled = false; }
                // front-camera selfie: held out in front of Rex, looking back at him (and the subject)
                var go = new GameObject("SelfieCam");
                selfie = go.AddComponent<Camera>();
                var main = Camera.main;
                if (main != null) { selfie.CopyFrom(main); selfie.depth = main.depth + 10f; }
                selfie.fieldOfView = 70f;
                Vector3 head = rex.position + Vector3.up * 1.65f;
                Vector3 look = target != null ? Vector3.Lerp(head, target.position + Vector3.up * 1.2f, 0.4f) : head;
                go.transform.position = head + rex.forward * 0.85f + rex.right * 0.25f + Vector3.up * 0.12f;
                go.transform.rotation = Quaternion.LookRotation(look - go.transform.position, Vector3.up);
                yield return new WaitForSeconds(0.8f);      // the pose
            }
            else yield return new WaitForSeconds(1.1f);
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory(Progress.AlbumDir);
            string file = $"photo_{System.DateTime.Now:yyyyMMdd_HHmmss}.jpg";
            File.WriteAllBytes(Path.Combine(Progress.AlbumDir, file), tex.EncodeToJPG(85));
            Object.Destroy(tex);
            string caption;
            if (subject == "KingGlorp") caption = GlorpCaption;
            else { int i = Random.Range(0, Captions.Length); if (i == last) i = (i + 1) % Captions.Length; last = i; caption = Captions[i]; }
            Progress.AddPhoto(new Progress.Photo { file = file, caption = caption, subject = subject, time = System.DateTime.Now.Ticks });
            if (subject == "KingGlorp") Progress.Add("photos_glorp");
            else if (!string.IsNullOrEmpty(subject) && subject != "Guest") Progress.Add("photos_blob");
            RexVoice.Subtitle?.Invoke("PHOTO SAVED: " + caption);
            if (selfie != null)
            {
                yield return new WaitForSeconds(0.6f);
                Object.Destroy(selfie.gameObject);
            }
            // back to shadows only, unless the player switched to third person meanwhile (the rig set the body back)
            if (rig == null || rig.Mode == RexCameraRig.CameraMode.FirstPerson)
                foreach (var r in hidden) if (r != null) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            foreach (var r in arms) if (r != null) r.enabled = true;
        }
    }
}
