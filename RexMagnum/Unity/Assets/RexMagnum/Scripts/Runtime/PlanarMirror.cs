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
        static readonly int NoDepthId = Shader.PropertyToID("_RMNoDepth");
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
        /// <summary>Frame a mirror last rendered (PoolReflection skips its own render that frame: one extra scene render at most).</summary>
        public static int RenderedFrame { get; private set; } = -1;
        static readonly System.Collections.Generic.List<Renderer> shown = new System.Collections.Generic.List<Renderer>(), armsOff = new System.Collections.Generic.List<Renderer>();
        Transform player;
        float nextLine;

        static readonly Plane[] frustum = new Plane[6];
        // one mirror render per frame at most: the nearest mirror that is in range, faced and on screen
        static int rankFrame = -1;
        static PlanarMirror nearest;
        QualityAuto quality;
        // VR: one mono render sampled in screen space is wrong in each eye (swims, double image): a plain shiny
        // surface instead (reflection probe only), back to the real mirror when VR is off.
        // The same shiny surface while the mirror does not render (not the nearest, out of range, off screen, before
        // its first render): the screen-space texture is then another frame's view and slid with the camera
        Material mirrorMat, vrMat;
        bool vrLook, showingMirror;
        int idleFrames;
        const int IdleSwap = 4;                        // frames without a render before the swap (no flicker at the edges)

        void EnsureVRMat()
        {
            if (vrMat != null) return;
            vrMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            vrMat.SetColor("_BaseColor", new Color(0.55f, 0.6f, 0.65f));
            vrMat.SetFloat("_Metallic", 1f);
            vrMat.SetFloat("_Smoothness", 0.95f);
        }

        void ShowMirror(bool on)
        {
            showingMirror = on;
            if (!on) EnsureVRMat();
            rend.sharedMaterial = on ? mirrorMat : vrMat;
        }

        /// <summary>VR on: the shiny surface; VR off: still the shiny surface until the mirror renders again.</summary>
        void SetVRLook(bool on)
        {
            vrLook = on;
            ShowMirror(false);
        }

        /// <summary>A frame this mirror did not render: the shiny surface after IdleSwap such frames.</summary>
        void Idle()
        {
            if (showingMirror && ++idleFrames >= IdleSwap) ShowMirror(false);
        }

        void Start()
        {
            rend = GetComponent<Renderer>();
            rend.shadowCastingMode = ShadowCastingMode.Off;
            quality = FindAnyObjectByType<QualityAuto>();
            var go = new GameObject("MirrorCamera") { hideFlags = HideFlags.HideAndDontSave };
            mirrorCam = go.AddComponent<Camera>();
            mirrorCam.enabled = false;
            // a cheap render: no shadow maps, depth / opaque copies, post or AA of its own (the reflection is small and
            // seen through the screen-space mirror shader)
            // (by reflection: not every one of these setters is public in every URP version)
            var cd = go.AddComponent<UniversalAdditionalCameraData>();
            foreach (var pn in new[] { "renderShadows", "requiresDepthTexture", "requiresColorTexture", "renderPostProcessing" })
            {
                var pi = typeof(UniversalAdditionalCameraData).GetProperty(pn);
                if (pi != null && pi.CanWrite && pi.PropertyType == typeof(bool)) pi.SetValue(cd, false);
            }
            cd.antialiasing = AntialiasingMode.None;
            QualityAuto.UseAuxRenderer(cd);                // the renderer without SSAO, when ProjectSetup made it
            mirrorMat = new Material(Shader.Find("RexMagnum/MirrorScreen"));      // samples the RT in screen space
            ShowMirror(false);                             // the shiny surface until the first render (the RT is empty)
            EnsureRT();
            var rex = FindAnyObjectByType<RexController>();
            if (rex != null) player = rex.transform;
        }

        /// <summary>RT at half the screen size, a quarter on the Low tier; remade when the tier or the window changes.</summary>
        void EnsureRT()
        {
            int div = quality != null && quality.Tier == QualityTier.Low ? 4 : 2;
            int w = Mathf.Max(256, Screen.width / div), h = Mathf.Max(256, Screen.height / div);
            if (rt != null && rt.width == w && rt.height == h) return;
            if (rt != null) { rt.Release(); Destroy(rt); }
            // HDR like the main view: an LDR target clipped bright lamps / sky in the reflection (and banded at night)
            var fmt = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR) ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
            rt = new RenderTexture(w, h, 16, fmt) { name = "MirrorRT" };
            if (mirrorCam != null) mirrorCam.targetTexture = rt;
            if (mirrorMat != null) mirrorMat.SetTexture("_BaseMap", rt);
        }

        /// <summary>In range, the camera in front of it and its bounds in the camera frustum (frustum computed per frame
        /// by Rank). isVisible alone is also true from the shadow pass and from behind.</summary>
        bool Eligible(Camera cam, out float dist)
        {
            dist = float.MaxValue;
            if (rend == null || mirrorCam == null || vrLook || !isActiveAndEnabled || !rend.isVisible) return false;
            dist = Vector3.Distance(cam.transform.position, transform.position);
            if (dist > activeDistance) return false;
            Vector3 n = transform.TransformDirection(localNormal).normalized;
            if (Vector3.Dot(cam.transform.position - transform.position, n) <= 0f) return false;
            return GeometryUtility.TestPlanesAABB(frustum, rend.bounds);
        }

        /// <summary>Once per frame: the nearest eligible mirror is the only one rendered (two mirrors in one room were
        /// two extra scene renders).</summary>
        static void Rank(Camera cam)
        {
            if (rankFrame == Time.frameCount) return;
            rankFrame = Time.frameCount;
            nearest = null;
            GeometryUtility.CalculateFrustumPlanes(cam, frustum);
            float best = float.MaxValue;
            foreach (var m in all)
                if (m != null && m.Eligible(cam, out float md) && md < best) { best = md; nearest = m; }
        }

        void OnDestroy()
        {
            if (rt != null) { rt.Release(); Destroy(rt); }
            if (mirrorMat != null) Destroy(mirrorMat);
            if (mirrorCam != null) Destroy(mirrorCam.gameObject);
            if (vrMat != null) Destroy(vrMat);
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
            if (VRMode.Active != vrLook) SetVRLook(VRMode.Active);
            if (vrLook) return;
            // the selfie / kill cam / ending cameras would sample the main camera's reflection: shiny surface instead
            if (KillCam.Active || EndingSequence.Playing || PhotoAlbum.Capturing || Time.time < PhotoAlbum.PoseUntil) { if (showingMirror) ShowMirror(false); return; }
            // a full scene render is costly: only the nearest mirror in front of the player and on screen
            Rank(cam);
            if (nearest != this) { Idle(); return; }
            EnsureRT();
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
            mirrorCam.useOcclusionCulling = false;          // the game camera's occlusion data is for its own view, not the reflected one
            // no cloud dome: it sits at 90% of the far plane, which is 27 m here (clouds floated in the room)
            mirrorCam.cullingMask = cam.cullingMask & ~(1 << DayNight.SkyLayer);
            mirrorCam.worldToCameraMatrix = cam.worldToCameraMatrix * refl;
            // rooms and corridors: ~30 m of reflected view is enough (fewer objects culled in and drawn)
            mirrorCam.farClipPlane = Mathf.Min(cam.farClipPlane, 30f);
            mirrorCam.aspect = cam.aspect;                 // the RT size is rounded / clamped: keep the screen's shape
            mirrorCam.ResetProjectionMatrix();
            Vector4 plane = CameraSpacePlane(mirrorCam, p, n);
            mirrorCam.projectionMatrix = mirrorCam.CalculateObliqueMatrix(plane);
            // Unity 6 / URP 17: render the mirror camera with a render request (synchronous)
            var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (!RenderPipeline.SupportsRenderRequest(mirrorCam, req)) { Idle(); return; }
            rendering = true;
            RenderedFrame = Time.frameCount;
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
            Shader.SetGlobalFloat(NoDepthId, 1f);          // this camera has no depth texture: water must not read another view's
            RenderPipeline.SubmitRenderRequest(mirrorCam, req);
            Shader.SetGlobalFloat(NoDepthId, 0f);
            GL.invertCulling = false;
            foreach (var r in shown) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            foreach (var r in armsOff) r.enabled = true;
            rendering = false;
            idleFrames = 0;
            if (!showingMirror) ShowMirror(true);          // the RT holds this frame's view now
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
