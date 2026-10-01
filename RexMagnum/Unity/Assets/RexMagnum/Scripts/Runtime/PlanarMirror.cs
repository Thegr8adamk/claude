using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RexMagnum
{
    /// <summary>
    /// Real mirror for URP: a second camera renders the reflected view into a texture (only when the player is near
    /// and the mirror is visible). The quad's forward (+Z of the quad's local space, or "normal") faces the room.
    /// Taunt next to a mirror: Rex combs his side hair and says a mirror line (brief: mirrors).
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class PlanarMirror : MonoBehaviour
    {
        public Vector3 localNormal = Vector3.forward;
        public float activeDistance = 8f;
        public int resolution = 512;
        Camera mirrorCam;
        RenderTexture rt;
        Renderer rend;
        static bool rendering;
        Transform player;
        float nextLine;

        void Start()
        {
            rend = GetComponent<Renderer>();
            rt = new RenderTexture(Mathf.Max(256, Screen.width / 2), Mathf.Max(256, Screen.height / 2), 16) { name = "MirrorRT" };
            var go = new GameObject("MirrorCamera") { hideFlags = HideFlags.HideAndDontSave };
            mirrorCam = go.AddComponent<Camera>();
            mirrorCam.enabled = false;
            mirrorCam.targetTexture = rt;
            var mat = new Material(Shader.Find("RexMagnum/MirrorScreen"));      // samples the RT in screen space
            mat.SetTexture("_BaseMap", rt);
            rend.material = mat;
            var rex = FindFirstObjectByType<RexController>();
            if (rex != null) player = rex.transform;
        }

        void OnDestroy()
        {
            if (rt != null) rt.Release();
            if (mirrorCam != null) Destroy(mirrorCam.gameObject);
        }

        void Update()
        {
            if (player == null || !RMInput.Taunt || Time.time < nextLine) return;
            if (Vector3.Distance(player.position, transform.position) < 3f)
            {
                nextLine = Time.time + 4f;
                RexVoice.Say(player.gameObject, "Mirror", true);
                Progress.Set("mirror", 1);
            }
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || rendering) return;
            if (Vector3.Distance(cam.transform.position, transform.position) > activeDistance || !rend.isVisible) return;
            Vector3 n = transform.TransformDirection(localNormal).normalized;
            Vector3 p = transform.position;
            float d = -Vector3.Dot(n, p);
            var refl = new Matrix4x4();
            refl.m00 = 1 - 2 * n.x * n.x; refl.m01 = -2 * n.x * n.y; refl.m02 = -2 * n.x * n.z; refl.m03 = -2 * d * n.x;
            refl.m10 = -2 * n.y * n.x; refl.m11 = 1 - 2 * n.y * n.y; refl.m12 = -2 * n.y * n.z; refl.m13 = -2 * d * n.y;
            refl.m20 = -2 * n.z * n.x; refl.m21 = -2 * n.z * n.y; refl.m22 = 1 - 2 * n.z * n.z; refl.m23 = -2 * d * n.z;
            refl.m33 = 1;
            mirrorCam.CopyFrom(cam);
            mirrorCam.targetTexture = rt;
            mirrorCam.worldToCameraMatrix = cam.worldToCameraMatrix * refl;
            Vector4 plane = CameraSpacePlane(mirrorCam, p, n);
            mirrorCam.projectionMatrix = cam.CalculateObliqueMatrix(plane);
            // Unity 6 / URP 17: render the mirror camera with a render request (synchronous)
            var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (!RenderPipeline.SupportsRenderRequest(mirrorCam, req)) return;
            rendering = true;
            GL.invertCulling = true;
            RenderPipeline.SubmitRenderRequest(mirrorCam, req);
            GL.invertCulling = false;
            rendering = false;
        }

        static Vector4 CameraSpacePlane(Camera c, Vector3 pos, Vector3 normal)
        {
            Matrix4x4 m = c.worldToCameraMatrix;
            Vector3 cp = m.MultiplyPoint(pos + normal * 0.02f);
            Vector3 cn = m.MultiplyVector(normal).normalized;
            return new Vector4(cn.x, cn.y, cn.z, -Vector3.Dot(cp, cn));
        }
    }
}
