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
        static readonly System.Collections.Generic.List<PlanarMirror> all = new System.Collections.Generic.List<PlanarMirror>();
        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        /// <summary>Rex is close to a mirror (Taunt there = comb the side hair over the bald spot).</summary>
        public static bool Near(Vector3 p)
        {
            foreach (var m in all) if (m != null && Vector3.Distance(m.transform.position, p) < 3f) return true;
            return false;
        }

        public Vector3 localNormal = Vector3.forward;
        public float activeDistance = 8f;
        public int resolution = 512;
        Camera mirrorCam;
        RenderTexture rt;
        Renderer rend;
        static bool rendering;
        static readonly System.Collections.Generic.List<Renderer> shown = new System.Collections.Generic.List<Renderer>(), armsOff = new System.Collections.Generic.List<Renderer>();
        Transform player;
        float nextLine;

        readonly Plane[] frustum = new Plane[6];

        void Start()
        {
            rend = GetComponent<Renderer>();
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rt = new RenderTexture(Mathf.Max(256, Screen.width / 2), Mathf.Max(256, Screen.height / 2), 16) { name = "MirrorRT" };
            var go = new GameObject("MirrorCamera") { hideFlags = HideFlags.HideAndDontSave };
            mirrorCam = go.AddComponent<Camera>();
            mirrorCam.enabled = false;
            mirrorCam.targetTexture = rt;
            var mat = new Material(Shader.Find("RexMagnum/MirrorScreen"));      // samples the RT in screen space
            mat.SetTexture("_BaseMap", rt);
            rend.material = mat;
            var rex = FindAnyObjectByType<RexController>();
            if (rex != null) player = rex.transform;
        }

        void OnDestroy()
        {
            if (rt != null) rt.Release();
            if (mirrorCam != null) Destroy(mirrorCam.gameObject);
        }

        void Update()
        {
            if (player == null || !RMInput.Taunt || Time.time < nextLine || PauseMenu.Open || GameFlow.Busy) return;
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
            // a full scene render is costly: only when the player is in front of the mirror and it is on screen
            // (isVisible is also true from the shadow pass and from behind)
            if (Vector3.Dot(cam.transform.position - p, n) <= 0f) return;
            GeometryUtility.CalculateFrustumPlanes(cam, frustum);
            if (!GeometryUtility.TestPlanesAABB(frustum, rend.bounds)) return;
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
            // first person hides Rex's body (shadows only) and floats the arms at the eye: the mirror shows the body
            shown.Clear(); armsOff.Clear();
            if (player != null)
                foreach (var r in player.GetComponentsInChildren<Renderer>())
                    if (r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) { shown.Add(r); r.shadowCastingMode = ShadowCastingMode.On; }
            var rig = cam.GetComponent<RexCameraRig>();
            if (rig != null && rig.fpArms != null && rig.fpArms.activeInHierarchy)
                foreach (var r in rig.fpArms.GetComponentsInChildren<Renderer>())
                    if (r.enabled) { armsOff.Add(r); r.enabled = false; }
            GL.invertCulling = true;
            RenderPipeline.SubmitRenderRequest(mirrorCam, req);
            GL.invertCulling = false;
            foreach (var r in shown) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            foreach (var r in armsOff) r.enabled = true;
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
