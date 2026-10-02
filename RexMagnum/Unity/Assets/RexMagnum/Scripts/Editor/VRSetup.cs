using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace RexMagnum.EditorTools
{
    /// <summary>
    /// Menu "Rex Magnum > Add VR Support": installs Unity's OpenXR package (it brings XR Plug-in Management).
    /// Then one setting is needed: Edit > Project Settings > XR Plug-in Management > Windows tab > tick OpenXR,
    /// and leave "Initialize XR on Startup" OFF (the game starts VR itself when the player turns VR mode on).
    /// </summary>
    public static class VRSetup
    {
        static AddRequest req;

        [MenuItem("Rex Magnum/Add VR Support", priority = 30)]
        public static void Add()
        {
            if (req != null && !req.IsCompleted) return;
            req = Client.Add("com.unity.xr.openxr");
            EditorApplication.update += Wait;
            EditorUtility.DisplayProgressBar("Rex Magnum", "Adding OpenXR (VR)...", 0.5f);
        }

        static void Wait()
        {
            if (!req.IsCompleted) return;
            EditorApplication.update -= Wait;
            EditorUtility.ClearProgressBar();
            if (req.Status == StatusCode.Success)
                EditorUtility.DisplayDialog("Rex Magnum - VR",
                    "OpenXR is installed.\n\nOne more step:\n" +
                    "1. Edit > Project Settings > XR Plug-in Management.\n" +
                    "2. Windows tab (monitor icon): tick OpenXR.\n" +
                    "3. Untick \"Initialize XR on Startup\".\n" +
                    "4. If a yellow warning shows, click Fix All.\n\n" +
                    "In the game: Settings > CAMERA & VR > VR mode ON.", "OK");
            else
                EditorUtility.DisplayDialog("Rex Magnum - VR", "Could not add OpenXR: " + req.Error?.message, "OK");
        }
    }
}
