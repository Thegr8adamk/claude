using System.Collections;
using System.IO;
using UnityEngine;

namespace RexMagnum
{
    /// <summary>Photo button (brief section 8): saves a real screenshot to the album with a caption from the brief.</summary>
    public static class PhotoAlbum
    {
        /// <summary>A photo is being taken this frame: HUD, hints and touch buttons stay out of the picture.</summary>
        public static bool Capturing { get; private set; }

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
            // VR: a selfie camera would take over the headset view (sickness): the photo is what the head camera sees,
            // the subject is pulled in front of Rex instead of beside him
            bool vr = VRMode.Active;
            if (rex != null)
            {
                // pull the subject to Rex's side (a stunned blob or a guest comes along; NavMesh agents warp)
                if (target != null)
                {
                    Vector3 spot = vr ? rex.position + rex.forward * 1.8f : rex.position + rex.right * 0.95f + rex.forward * 0.1f;
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
            }
            if (rex != null && vr) yield return new WaitForSeconds(0.8f);     // the pose, seen from the head camera
            else if (rex != null)
            {
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
                if (main != null)
                {
                    selfie.CopyFrom(main);
                    selfie.depth = main.depth + 10f;
                    // CopyFrom does not copy URP's camera data: same post-processing and anti-aliasing as the game
                    var md = main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                    if (md != null)
                    {
                        if (!go.TryGetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(out var sd))
                            sd = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                        sd.renderPostProcessing = md.renderPostProcessing;
                        sd.antialiasing = md.antialiasing;
                    }
                }
                selfie.fieldOfView = 70f;
                Vector3 head = rex.position + Vector3.up * 1.65f;
                Vector3 look = target != null ? Vector3.Lerp(head, target.position + Vector3.up * 1.2f, 0.4f) : head;
                go.transform.position = head + rex.forward * 0.85f + rex.right * 0.25f + Vector3.up * 0.12f;
                go.transform.rotation = Quaternion.LookRotation(look - go.transform.position, Vector3.up);
                yield return new WaitForSeconds(0.8f);      // the pose
            }
            else yield return new WaitForSeconds(1.1f);
            Capturing = true;                       // HUD, hints and touch buttons hide for the picture
            yield return null;
            yield return new WaitForEndOfFrame();
            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            Capturing = false;
            // at most 1920x1080 (a 4K or ultrawide screen is scaled down): a short encode, a small file
            float k = Mathf.Min(1f, Mathf.Min(MaxW / (float)shot.width, MaxH / (float)shot.height));
            var tex = k < 1f ? Shrink(shot, Mathf.RoundToInt(shot.width * k), Mathf.RoundToInt(shot.height * k)) : shot;
            if (tex != shot) Object.Destroy(shot);
            Directory.CreateDirectory(Progress.AlbumDir);
            string file = $"photo_{System.DateTime.Now:yyyyMMdd_HHmmss}.jpg";
            WriteLater(Path.Combine(Progress.AlbumDir, file), tex.EncodeToJPG(85));     // encode here, write on a worker thread
            SaveThumb(tex, file);                   // the ALBUM tab loads only these (small: phone memory)
            Object.Destroy(tex);
            string caption;
            if (subject == "KingGlorp") caption = GlorpCaption;
            else { int i = Random.Range(0, Captions.Length); if (i == last) i = (i + 1) % Captions.Length; last = i; caption = Captions[i]; }
            Progress.AddPhoto(new Progress.Photo { file = file, caption = caption, subject = subject, time = System.DateTime.Now.Ticks });
            if (subject == "KingGlorp") Progress.Add("photos_glorp");
            else if (!string.IsNullOrEmpty(subject) && subject != "Guest") Progress.Add("photos_blob");
            RexTestHUD.Notice("PHOTO SAVED: " + caption, 4f);        // a game message: shows with subtitles off too
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

        // ------------------------------------------------------------ thumbnails (album)

        public const int ThumbW = 384, ThumbH = 216;
        const int MaxW = 1920, MaxH = 1080;          // biggest saved photo
        static string ThumbPath(string file) => Path.Combine(Progress.AlbumDir, "thumb_" + file);

        /// <summary>Writes the encoded bytes on a worker thread (no frame hitch on a slow phone card).</summary>
        static void WriteLater(string path, byte[] bytes)
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                try { File.WriteAllBytes(path, bytes); }
                catch (System.Exception e) { Debug.LogWarning(e); }
            });
        }

        /// <summary>Writes thumb_&lt;file&gt; (384x216 JPG) from the full picture.</summary>
        static void SaveThumb(Texture src, string file)
        {
            var t = Downscale(src);
            var bytes = t.EncodeToJPG(80);
            // the newest thumbnail stays in memory: the ALBUM tab may open before the worker thread finished the file
            newestFile = file; newestThumb = bytes;
            WriteLater(ThumbPath(file), bytes);
            Object.Destroy(t);
        }

        static string newestFile;
        static byte[] newestThumb;

        /// <summary>
        /// The album picture for the ALBUM tab: thumb_&lt;file&gt;. Older photos without one: the full JPG is loaded once,
        /// shrunk, saved as the thumbnail and freed. Null if the photo is gone. The caller destroys the texture.
        /// </summary>
        public static Texture2D LoadThumb(string file)
        {
            string tp = ThumbPath(file);
            try
            {
                if (file == newestFile && newestThumb != null)
                {
                    // never read a file the worker thread may still be writing
                    var n = new Texture2D(2, 2, TextureFormat.RGB24, false);
                    n.LoadImage(newestThumb, true);
                    return n;
                }
                if (File.Exists(tp))
                {
                    var t = new Texture2D(2, 2, TextureFormat.RGB24, false);
                    t.LoadImage(File.ReadAllBytes(tp), true);          // no CPU copy
                    return t;
                }
                string full = Path.Combine(Progress.AlbumDir, file);
                if (!File.Exists(full)) return null;
                var big = new Texture2D(2, 2, TextureFormat.RGB24, false);
                big.LoadImage(File.ReadAllBytes(full), true);
                var small = Downscale(big);
                Object.Destroy(big);
                File.WriteAllBytes(tp, small.EncodeToJPG(80));
                small.Apply(false, true);                            // keep only the GPU copy
                return small;
            }
            catch (System.Exception e) { Debug.LogWarning(e); return null; }
        }

        /// <summary>GPU shrink into 384x216 in halving steps (one big bilinear step would shimmer). The picture keeps its
        /// shape: a 4:3 or ultrawide screen gets black bars (letterbox). Readable result.</summary>
        static Texture2D Downscale(Texture src)
        {
            float k = Mathf.Min(ThumbW / (float)src.width, ThumbH / (float)src.height);
            int w = Mathf.Clamp(Mathf.RoundToInt(src.width * k), 1, ThumbW), h = Mathf.Clamp(Mathf.RoundToInt(src.height * k), 1, ThumbH);
            return Shrink(src, w, h, ThumbW, ThumbH);
        }

        /// <summary>GPU shrink to tw x th in halving steps. Readable result (the caller destroys it). With outW / outH
        /// bigger than tw / th: the picture sits in the middle of a black outW x outH texture.</summary>
        static Texture2D Shrink(Texture src, int tw, int th, int outW = 0, int outH = 0)
        {
            if (outW < tw) outW = tw;
            if (outH < th) outH = th;
            int w = src.width, h = src.height;
            RenderTexture cur = null;
            while (w / 2 >= tw && h / 2 >= th)
            {
                w /= 2; h /= 2;
                var next = RenderTexture.GetTemporary(w, h, 0);
                if (cur == null) Graphics.Blit(src, next); else Graphics.Blit(cur, next);
                if (cur != null) RenderTexture.ReleaseTemporary(cur);
                cur = next;
            }
            var dst = RenderTexture.GetTemporary(tw, th, 0);
            if (cur == null) Graphics.Blit(src, dst); else Graphics.Blit(cur, dst);
            if (cur != null) RenderTexture.ReleaseTemporary(cur);
            var prev = RenderTexture.active;
            RenderTexture.active = dst;
            var t = new Texture2D(outW, outH, TextureFormat.RGB24, false);
            if (outW != tw || outH != th) t.SetPixels(new Color[outW * outH]);      // black bars (new Color[] is all black)
            t.ReadPixels(new Rect(0, 0, tw, th), (outW - tw) / 2, (outH - th) / 2);
            t.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(dst);
            return t;
        }
    }
}
