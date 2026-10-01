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

        public static void Take(MonoBehaviour host, string subject)
        {
            host.StartCoroutine(Capture(subject));
        }

        static IEnumerator Capture(string subject)
        {
            yield return new WaitForSeconds(1.1f);          // the pose
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
            else if (!string.IsNullOrEmpty(subject)) Progress.Add("photos_blob");
            RexVoice.Subtitle?.Invoke("PHOTO SAVED: " + caption);
        }
    }
}
