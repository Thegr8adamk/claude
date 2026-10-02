using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RexMagnum
{
    /// <summary>
    /// Planar reflection for the main pool (the PlanarMirror approach on a horizontal plane): one camera renders the
    /// view mirrored at the water surface into a half-res texture (quarter on Medium phones), and RexMagnum/Water
    /// blends it in by Fresnel (_PlanarRefl + _UsePlanar, set per renderer). Only when Rex is within ~40 m of the
    /// pool, the camera is above the water and the pool is on screen; off on Low and in VR (the reflection probe
    /// alone then, as before). Added by IslandBuilder to "Game"; finds the pool water (A_PoolWater) under
    /// Island/PoolArea by itself.
    /// </summary>
    public class PoolReflection : MonoBehaviour
    {
        public float activeDistance = 40f;
        public float farClip = 150f;               // hotel, palms, slide tower: farther things are the skybox
        Renderer[] water;                          // PoolArea renderers holding the pool water sub-mesh
        Bounds pool;
        float planeY;
        Camera reflCam;
        RenderTexture rt;
        Transform player;
        QualityAuto quality;
        MaterialPropertyBlock mpb;
        bool on;
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
        }

        /// <summary>RT at half the screen size, a quarter on Medium phones; remade when the tier or the window changes.</summary>
        void EnsureRT()
        {
            int div = Application.isMobilePlatform && quality != null && quality.Tier <= QualityTier.Medium ? 4 : 2;
            int w = Mathf.Max(256, Screen.width / div), h = Mathf.Max(256, Screen.height / div);
            if (rt != null && rt.width == w && rt.height == h) return;
            if (rt != null) { rt.Release(); Destroy(rt); }
            var fmt = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR) ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
            rt = new RenderTexture(w, h, 16, fmt) { name = "PoolReflectionRT" };
            reflCam.targetTexture = rt;
        }

        void SetUse(bool use)
        {
            if (use == on) return;
            on = use;
            foreach (var r in water)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetFloat(UseId, use ? 1f : 0f);
                if (use && rt != null) mpb.SetTexture(PlanarId, rt);
                r.SetPropertyBlock(mpb);
            }
        }

        bool Wanted(Camera cam)
        {
            if (reflCam == null || VRMode.Active) return false;
            if (quality != null && quality.Tier == QualityTier.Low) return false;
            if (player == null || pool.SqrDistance(player.position) > activeDistance * activeDistance) return false;
            if (cam.transform.position.y < planeY + 0.05f) return false;     // under water / at the surface
            bool seen = false;
            foreach (var r in water) if (r != null && r.isVisible) { seen = true; break; }
            if (!seen) return false;
            GeometryUtility.CalculateFrustumPlanes(cam, frustum);
            return GeometryUtility.TestPlanesAABB(frustum, pool);
        }

        void OnDestroy()
        {
            if (rt != null) { rt.Release(); Destroy(rt); }
            if (reflCam != null) Destroy(reflCam.gameObject);
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || water == null) return;
            if (!Wanted(cam)) { SetUse(false); return; }
            var oldRt = rt;
            EnsureRT();
            if (rt != oldRt) on = false;            // a new texture: hand it to the water again
            Vector3 n = Vector3.up;
            Vector3 p = new Vector3(0f, planeY, 0f);
            float d = -planeY;
            // reflection about the plane y = planeY
            var refl = Matrix4x4.identity;
            refl.m11 = -1f; refl.m13 = -2f * d;
            reflCam.CopyFrom(cam);
            reflCam.targetTexture = rt;
            reflCam.useOcclusionCulling = false;    // the game camera's occlusion data is for its own view
            // no cloud dome: it sits at 90% of the far plane, and the oblique near plane bends the far plane
            reflCam.cullingMask = cam.cullingMask & ~(1 << DayNight.SkyLayer);
            reflCam.worldToCameraMatrix = cam.worldToCameraMatrix * refl;
            reflCam.farClipPlane = Mathf.Min(cam.farClipPlane, farClip);
            reflCam.aspect = cam.aspect;
            reflCam.ResetProjectionMatrix();
            Matrix4x4 m = reflCam.worldToCameraMatrix;
            Vector3 cp = m.MultiplyPoint(p + n * 0.02f);
            Vector3 cn = m.MultiplyVector(n).normalized;
            reflCam.projectionMatrix = reflCam.CalculateObliqueMatrix(new Vector4(cn.x, cn.y, cn.z, -Vector3.Dot(cp, cn)));
            var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (!RenderPipeline.SupportsRenderRequest(reflCam, req)) { SetUse(false); return; }
            // the water itself stays out (it would read the texture it is drawn into); first person: the body is
            // shadow-only and the arms float at the eye, so the reflection shows the body (as in PlanarMirror)
            foreach (var r in water) if (r != null) r.enabled = false;
            shown.Clear(); armsOff.Clear();
            if (player != null)
                foreach (var r in player.GetComponentsInChildren<Renderer>())
                    if (r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) { shown.Add(r); r.shadowCastingMode = ShadowCastingMode.On; }
            var rig = cam.GetComponent<RexCameraRig>();
            if (rig != null && rig.fpArms != null && rig.fpArms.activeInHierarchy)
                foreach (var r in rig.fpArms.GetComponentsInChildren<Renderer>())
                    if (r.enabled) { armsOff.Add(r); r.enabled = false; }
            GL.invertCulling = true;
            RenderPipeline.SubmitRenderRequest(reflCam, req);
            GL.invertCulling = false;
            foreach (var r in shown) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            foreach (var r in armsOff) r.enabled = true;
            foreach (var r in water) if (r != null) r.enabled = true;
            SetUse(true);
        }
    }
}
