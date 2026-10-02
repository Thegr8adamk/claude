using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RexMagnum
{
    /// <summary>
    /// Planar reflection for the main pool (the PlanarMirror approach on a horizontal plane): one camera renders the
    /// view mirrored at the water surface into a half-res texture (quarter on Medium), and RexMagnum/Water
    /// blends it in by Fresnel (_PlanarRefl + _UsePlanar, set per renderer). Only when Rex is within ~40 m of the
    /// pool, the camera is above the water and the pool is on screen; off on Low, on phones, in VR and while another
    /// camera shows the scene (kill cam, ending, selfie: they would sample a texture made for the game camera's view;
    /// the reflection probe alone then, as before). Every 2nd frame on Medium, never in a frame a mirror rendered.
    /// Added by IslandBuilder to "Game"; finds the pool water (A_PoolWater) under Island/PoolArea by itself.
    /// </summary>
    [DefaultExecutionOrder(100)]                   // LateUpdate after the mirrors: their frame stamp is set by then
    public class PoolReflection : MonoBehaviour
    {
        static readonly int NoDepthId = Shader.PropertyToID("_RMNoDepth");
        /// <summary>Unity's built-in "Water" layer: IslandBuilder puts the pool water renderer on it, so the reflection
        /// camera culls just the water (the glass rails / leaves of the same model stay in the reflection).</summary>
        public const int WaterLayer = 4;
        public float activeDistance = 40f;
        public float farClip = 80f;                // hotel front, palms, slide tower: farther things are sky / skipped
        Renderer[] water;                          // PoolArea renderers holding the pool water sub-mesh
        Bounds pool;
        float planeY;
        Camera reflCam;
        RenderTexture rt;
        Transform player;
        QualityAuto quality;
        MaterialPropertyBlock mpb;
        // _UsePlanar fades over FadeTime (a hard 1/0 switch popped the water at 40 m / on and off screen);
        // the weight and texture last written to the renderers (written only when they change)
        const float FadeTime = 0.2f;
        float weight, shownWeight = -1f;
        Texture shownTex;
        static readonly Plane[] frustum = new Plane[6];
        static readonly System.Collections.Generic.List<Renderer> shown = new System.Collections.Generic.List<Renderer>(), armsOff = new System.Collections.Generic.List<Renderer>();
        static readonly int PlanarId = Shader.PropertyToID("_PlanarRefl"), UseId = Shader.PropertyToID("_UsePlanar");

        void Start()
        {
            quality = FindAnyObjectByType<QualityAuto>();
            var rex = FindAnyObjectByType<RexController>();
            if (rex != null) player = rex.transform;
            var area = GameObject.Find("Island/PoolArea");
            if (area == null) { enabled = false; return; }
            // the water sub-mesh's own bounds (the renderer also holds glass / other see-through parts)
            var found = new System.Collections.Generic.List<Renderer>();
            bool any = false;
            foreach (var r in area.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mats = r.sharedMaterials;
                for (int s = 0; s < mats.Length && s < mf.sharedMesh.subMeshCount; s++)
                {
                    if (mats[s] == null || !mats[s].name.StartsWith("A_PoolWater")) continue;
                    var lb = mf.sharedMesh.GetSubMesh(s).bounds;
                    var wb = new Bounds(r.transform.TransformPoint(lb.center), Vector3.zero);
                    for (int c = 0; c < 8; c++)             // all eight corners (the model may be rotated)
                        wb.Encapsulate(r.transform.TransformPoint(new Vector3(
                            (c & 1) == 0 ? lb.min.x : lb.max.x, (c & 2) == 0 ? lb.min.y : lb.max.y, (c & 4) == 0 ? lb.min.z : lb.max.z)));
                    if (!any) { pool = wb; any = true; } else pool.Encapsulate(wb);
                    if (!found.Contains(r)) found.Add(r);
                }
            }
            if (!any) { enabled = false; return; }
            water = found.ToArray();
            planeY = pool.max.y;                    // the water top (the hot tubs' tops are within a few cm of it)
            mpb = new MaterialPropertyBlock();
            var go = new GameObject("PoolReflectionCamera") { hideFlags = HideFlags.HideAndDontSave };
            reflCam = go.AddComponent<Camera>();
            reflCam.enabled = false;
            // a cheap render like the mirror's: no shadow maps, depth / opaque copies, post or AA of its own
            var cd = go.AddComponent<UniversalAdditionalCameraData>();
            foreach (var pn in new[] { "renderShadows", "requiresDepthTexture", "requiresColorTexture", "renderPostProcessing" })
            {
                var pi = typeof(UniversalAdditionalCameraData).GetProperty(pn);
                if (pi != null && pi.CanWrite && pi.PropertyType == typeof(bool)) pi.SetValue(cd, false);
            }
            cd.antialiasing = AntialiasingMode.None;
            QualityAuto.UseAuxRenderer(cd);                // the renderer without SSAO, when ProjectSetup made it
        }

        /// <summary>RT at half the screen size (quarter on Medium); remade when the window or tier changes.</summary>
        void EnsureRT()
        {
            int div = quality != null && quality.Tier == QualityTier.Medium ? 4 : 2;      // Medium: quarter size, every frame
            int w = Mathf.Max(256, Screen.width / div), h = Mathf.Max(256, Screen.height / div);
            if (rt != null && rt.width == w && rt.height == h) return;
            if (rt != null) { rt.Release(); Destroy(rt); }
            var fmt = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR) ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
            rt = new RenderTexture(w, h, 16, fmt) { name = "PoolReflectionRT" };
            reflCam.targetTexture = rt;
        }

        /// <summary>Writes _UsePlanar (and the texture) to the water renderers when either changed.</summary>
        void Push(float w)
        {
            if (water == null || mpb == null) return;
            bool texChanged = w > 0f && shownTex != rt;
            if (w == shownWeight && !texChanged) return;
            shownWeight = w;
            foreach (var r in water)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetFloat(UseId, w);
                if (w > 0f && rt != null) mpb.SetTexture(PlanarId, rt);
                r.SetPropertyBlock(mpb);
            }
            if (w > 0f) shownTex = rt;
        }

        /// <summary>Probe only, at once (the planar texture would be wrong for this view).</summary>
        void Off() { weight = 0f; Push(0f); }

        /// <summary>One step of the fade toward planar on / off (unscaled: kill cam slow motion does not stall it).</summary>
        int mirrorSkips;
        void Fade(bool use)
        {
            weight = Mathf.MoveTowards(weight, use ? 1f : 0f, Time.unscaledDeltaTime / FadeTime);
            Push(weight);
        }

        /// <summary>Planar reflection allowed at all for this view; when not, it goes off at once (another camera shows
        /// the scene, VR, Low, phones, under water).</summary>
        bool Allowed(Camera cam)
        {
            if (reflCam == null || VRMode.Active || Application.isMobilePlatform) return false;   // phones: a whole-hub render
            if (KillCam.Active || EndingSequence.Playing || PhotoAlbum.Capturing) return false;
            if (quality != null && quality.Tier == QualityTier.Low) return false;
            return cam.transform.position.y >= planeY + 0.05f;               // not under water / at the surface
        }

        /// <summary>Near the pool and the pool on screen (out of range / off screen fades out).</summary>
        bool Wanted(Camera cam)
        {
            if (player == null || pool.SqrDistance(player.position) > activeDistance * activeDistance) return false;
            bool seen = false;
            foreach (var r in water) if (r != null && r.isVisible) { seen = true; break; }
            if (!seen) return false;
            GeometryUtility.CalculateFrustumPlanes(cam, frustum);
            return GeometryUtility.TestPlanesAABB(frustum, pool);
        }

        void OnDisable() => Off();

        void OnDestroy()
        {
            Off();
            if (rt != null) { rt.Release(); Destroy(rt); }
            if (reflCam != null) Destroy(reflCam.gameObject);
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || water == null) return;
            if (!Allowed(cam)) { Off(); return; }
            if (!Wanted(cam)) { Fade(false); return; }       // the last texture shows while it fades (0.2 s)
            // never render in a frame a mirror rendered: the water keeps last frame's texture and weight (one frame
            // old, barely visible; dropping to the probe for that frame flickered)
            if (PlanarMirror.RenderedFrame == Time.frameCount)
            {
                // two mirror frames in a row (a mirror rendering every frame): the old texture would slide, so fade out
                if (++mirrorSkips >= 2) Fade(false);
                return;
            }
            mirrorSkips = 0;
            EnsureRT();                             // a new texture is handed to the water by Push
            Vector3 n = Vector3.up;
            Vector3 p = new Vector3(0f, planeY, 0f);
            float d = -planeY;
            // reflection about the plane y = planeY
            var refl = Matrix4x4.identity;
            refl.m11 = -1f; refl.m13 = -2f * d;
            reflCam.CopyFrom(cam);
            reflCam.targetTexture = rt;
            reflCam.useOcclusionCulling = false;    // the game camera's occlusion data is for its own view
            // the water itself stays out (it would read the texture it is drawn into); the cloud dome stays in (at 90% of
            // the 80 m far plane, behind the hub's buildings: the reflection had a cloudless sky)
            bool waterLayered = true;
            foreach (var r in water) if (r != null && r.gameObject.layer != WaterLayer) waterLayered = false;
            reflCam.cullingMask = cam.cullingMask & ~(1 << WaterLayer) & ~(1 << DayNight.SkyLayer);   // no cloud dome: the oblique far plane cuts it into patches
            reflCam.worldToCameraMatrix = cam.worldToCameraMatrix * refl;
            reflCam.farClipPlane = Mathf.Min(cam.farClipPlane, farClip);
            reflCam.aspect = cam.aspect;
            reflCam.ResetProjectionMatrix();
            Matrix4x4 m = reflCam.worldToCameraMatrix;
            Vector3 cp = m.MultiplyPoint(p + n * 0.02f);
            Vector3 cn = m.MultiplyVector(n).normalized;
            reflCam.projectionMatrix = reflCam.CalculateObliqueMatrix(new Vector4(cn.x, cn.y, cn.z, -Vector3.Dot(cp, cn)));
            var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (!RenderPipeline.SupportsRenderRequest(reflCam, req)) { Off(); return; }
            // an island built before the Water layer: the water renderers off instead (their glass / leaves with them).
            // First person: the body is shadow-only and the arms float at the eye, so the reflection shows the body
            if (!waterLayered) foreach (var r in water) if (r != null) r.enabled = false;
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
            RenderPipeline.SubmitRenderRequest(reflCam, req);
            Shader.SetGlobalFloat(NoDepthId, 0f);
            GL.invertCulling = false;
            foreach (var r in shown) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            foreach (var r in armsOff) r.enabled = true;
            if (!waterLayered) foreach (var r in water) if (r != null) r.enabled = true;
            Fade(true);
        }
    }
}
