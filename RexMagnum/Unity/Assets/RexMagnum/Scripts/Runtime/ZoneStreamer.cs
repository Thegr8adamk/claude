using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Zone streaming inside the one island scene (CLAUDE.md §3). Each zone (resort, golf, jungle, ruins, cave,
    /// tunnels, ship) is a set of building roots; when it is farther than the tier's radius (Low 180 m, Medium 260 m,
    /// High / Ultra 400 m, +30 m hysteresis) from Rex and from every on-screen camera, its LOD groups are forced to
    /// their lowest level and the higher-level renderers, its lamps and particle systems are switched off. The lowest
    /// level stays: the hotel / ship are landmarks seen from the hills, and a vanishing building would pop. Beyond
    /// 2.5x the radius the lowest level goes too (a forced LOD never distance-culls: small hidden buildings drew from
    /// everywhere); at that range they were below their own cull height anyway.
    /// Colliders, NavMesh, gameplay scripts, blobs, guests, pickups, terrain, ocean and sky are never touched.
    /// Additive scenes were not used: the NavMesh, light / reflection probes and occlusion data are baked across the
    /// whole island, and splitting the scene would split (and break) those bakes.
    /// Added by IslandBuilder to the "Island" root, with the zone roots.
    /// </summary>
    public class ZoneStreamer : MonoBehaviour
    {
        [System.Serializable]
        public class Zone { public string name; public Transform[] roots; public Light[] lights; }

        public Zone[] zones;

        // EnsureShown(box): every zone touching box shown (InteriorProbes: a box probe must capture its zones at full
        // detail, not the forced lowest LOD). Runs out after HoldTimeout (a caller that never released).
        static readonly System.Collections.Generic.List<Bounds> pins = new System.Collections.Generic.List<Bounds>();
        static ZoneStreamer active;
        static float pinUntil;
        const float HoldTimeout = 20f;

        class State
        {
            public Bounds b;
            public Renderer[] detail;     // every LOD level but the lowest
            public bool[] detailOn;
            public LODGroup[] groups;
            public Light[] lights;
            public bool[] lightOn;
            public ParticleSystem[] ps;
            public bool[] psOn;
            public bool hidden;
            public int pending = -1;      // next detail renderer to switch back on (time-sliced), -1 = none
            public Renderer[] low;        // the lowest LOD level (off only beyond FarCull x the radius)
            public bool[] lowOn;
            public bool lowHidden;
            public int lowPending = -1;   // next lowest-level renderer to switch back on, -1 = none
        }

        State[] st;
        QualityAuto quality;
        Transform player;
        Vector3 lastPlayer;
        float nextTick;
        readonly Camera[] camBuf = new Camera[16];
        const float Hysteresis = 30f, JumpDist = 60f;
        const int EnablePerFrame = 150;
        const float FarCull = 2.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { pins.Clear(); active = null; pinUntil = 0f; keepLow = false; }

        static bool keepLow;
        /// <summary>On: very far zones keep their lowest LOD (the 2.4 km island probe must see the whole island).</summary>
        public static void KeepLowAll(bool on)
        {
            if (keepLow == on) return;
            keepLow = on;
            if (active != null) active.Refresh(false);
        }

        /// <summary>Every zone has its lowest LOD shown (no sliced low show still running).</summary>
        public static bool LowReady()
        {
            if (active == null || active.st == null) return true;
            foreach (var s in active.st) if (s.lowHidden || s.lowPending >= 0) return false;
            return true;
        }

        /// <summary>Shows every zone whose box touches box (a probe's world bounds: a tunnel probe can reach into the
        /// ruins zone) at full detail and keeps them shown until ReleaseShown(box) with the same box (HoldTimeout at
        /// most). Time-sliced: a whole resort in one frame hitched on phones.</summary>
        public static void EnsureShown(Bounds box)
        {
            pins.Add(box);
            pinUntil = Time.unscaledTime + HoldTimeout;
            if (active == null || active.st == null) return;      // not culled yet: Refresh respects the pin later
            foreach (var s in active.st)
                if (s.hidden && s.b.Intersects(box)) active.Show(s, false);
        }

        /// <summary>Every zone touching box is fully shown (no time-sliced show still running). True when not streamed.</summary>
        public static bool Ready(Bounds box)
        {
            if (active == null || active.st == null) return true;
            foreach (var s in active.st)
                if (s.b.Intersects(box) && (s.hidden || s.pending >= 0 || s.lowPending >= 0)) return false;
            return true;
        }

        public static void ReleaseShown(Bounds box) => pins.Remove(box);

        bool Pinned(int i)
        {
            foreach (var p in pins) if (st[i].b.Intersects(p)) return true;
            return false;
        }

        void OnEnable() => active = this;

        void Start()
        {
            quality = FindAnyObjectByType<QualityAuto>();
            if (zones == null) zones = new Zone[0];
            st = new State[zones.Length];
            for (int i = 0; i < zones.Length; i++)
            {
                var z = zones[i];
                var s = st[i] = new State();
                var detail = new System.Collections.Generic.List<Renderer>();
                var groups = new System.Collections.Generic.List<LODGroup>();
                var ps = new System.Collections.Generic.List<ParticleSystem>();
                var low = new System.Collections.Generic.List<Renderer>();
                bool any = false;
                if (z.roots != null)
                    foreach (var root in z.roots)
                    {
                        if (root == null) continue;
                        // hidden / empty renderers have empty bounds at the origin: left out of the zone's box
                        foreach (var r in root.GetComponentsInChildren<Renderer>())
                        {
                            if (!r.enabled) continue;
                            if (!any) { s.b = r.bounds; any = true; } else s.b.Encapsulate(r.bounds);
                        }
                        foreach (var g in root.GetComponentsInChildren<LODGroup>(true))
                        {
                            var lods = g.GetLODs();
                            if (lods.Length < 2) continue;
                            groups.Add(g);
                            for (int l = 0; l < lods.Length - 1; l++)
                                foreach (var r in lods[l].renderers)
                                    if (r != null && r.enabled && !detail.Contains(r)) detail.Add(r);
                            foreach (var r in lods[lods.Length - 1].renderers)
                                if (r != null && r.enabled && !low.Contains(r)) low.Add(r);
                        }
                        ps.AddRange(root.GetComponentsInChildren<ParticleSystem>(true));
                    }
                if (!any) s.b = new Bounds(Vector3.zero, Vector3.one * 1e6f);    // nothing to measure: never culled
                s.detail = detail.ToArray();
                s.detailOn = new bool[s.detail.Length];
                low.RemoveAll(r => detail.Contains(r));          // shared with a higher level: the detail pass owns it
                s.low = low.ToArray();
                s.lowOn = new bool[s.low.Length];
                s.groups = groups.ToArray();
                s.lights = z.lights ?? new Light[0];
                s.lightOn = new bool[s.lights.Length];
                s.ps = ps.ToArray();
                s.psOn = new bool[s.ps.Length];
            }
            FindPlayer();
            // the first cull a moment after load: the island reflection probe renders on awake with every zone in it
            nextTick = Time.unscaledTime + 1.5f;
        }

        void FindPlayer()
        {
            var rex = FindAnyObjectByType<RexController>();
            player = rex != null ? rex.transform : null;
            if (player != null) lastPlayer = player.position;
        }

        float Radius() => quality == null ? 260f : quality.Tier == QualityTier.Low ? 180f : quality.Tier == QualityTier.Medium ? 260f : 400f;

        void Update()
        {
            if (st == null) return;
            // a pin that never got released (caller destroyed mid-pass)
            if (pins.Count > 0 && Time.unscaledTime > pinUntil) pins.Clear();
            // fast travel / zone doors / respawn: a jump refreshes now and shows the new zone at once (no slicing)
            bool jump = false;
            if (player != null)
            {
                var p = player.position;
                jump = (p - lastPlayer).sqrMagnitude > JumpDist * JumpDist;
                lastPlayer = p;
            }
            if (jump || Time.unscaledTime >= nextTick)
            {
                nextTick = Time.unscaledTime + 0.5f;
                if (player == null) FindPlayer();
                Refresh(jump);
            }
            // zones coming back: a few renderers per frame (no hitch when a big zone returns); the lowest level
            // first (it is what a forced-lowest zone draws), then the detail
            int budget = EnablePerFrame;
            foreach (var s in st)
            {
                if (s.lowPending >= 0)
                {
                    for (; budget > 0 && s.lowPending < s.low.Length; s.lowPending++, budget--)
                        if (s.low[s.lowPending] != null) s.low[s.lowPending].enabled = s.lowOn[s.lowPending];
                    if (s.lowPending >= s.low.Length) s.lowPending = -1;
                    if (budget <= 0) break;
                }
                if (s.pending < 0) continue;
                for (; budget > 0 && s.pending < s.detail.Length; s.pending++, budget--)
                    if (s.detail[s.pending] != null) s.detail[s.pending].enabled = s.detailOn[s.pending];
                if (s.pending >= s.detail.Length) Finish(s);
                if (budget <= 0) break;
            }
        }

        void Refresh(bool instant)
        {
            if (st == null) return;
            float r = Radius();
            int cams = Camera.allCamerasCount <= camBuf.Length ? Camera.GetAllCameras(camBuf) : 0;
            for (int zi = 0; zi < st.Length; zi++)
            {
                var s = st[zi];
                // nearest viewpoint: Rex, the main / VR camera, and any other on-screen camera (kill cam, photo,
                // title); render-to-texture cameras (mirrors, pool reflection) look at what Rex already sees
                float d = float.MaxValue;
                if (player != null) d = Mathf.Sqrt(s.b.SqrDistance(player.position));
                for (int c = 0; c < cams; c++)
                    if (camBuf[c] != null && camBuf[c].targetTexture == null)
                        d = Mathf.Min(d, Mathf.Sqrt(s.b.SqrDistance(camBuf[c].transform.position)));
                if (cams == 0 && Camera.main != null) d = Mathf.Min(d, Mathf.Sqrt(s.b.SqrDistance(Camera.main.transform.position)));
                bool far = d < float.MaxValue && d > (s.hidden ? r : r + Hysteresis) && !(pins.Count > 0 && Pinned(zi));
                // very far: the lowest level off too (same hysteresis)
                bool veryFar = !keepLow && far && d > (s.lowHidden ? r * FarCull : r * FarCull + Hysteresis);
                if (far && !s.hidden) Hide(s);
                else if (!far && s.hidden) Show(s, instant);
                else if (!far && instant && s.pending >= 0) Finish(s);
                if (veryFar && !s.lowHidden) HideLow(s);
                else if (!veryFar && s.lowHidden) ShowLow(s, instant);
                else if (!veryFar && instant && s.lowPending >= 0) FinishLow(s);
            }
        }

        void Hide(State s)
        {
            foreach (var g in s.groups) if (g != null) g.ForceLOD(g.lodCount - 1);
            // a show still in progress: only the renderers already back hold a fresh state
            int upto = s.pending >= 0 ? s.pending : s.detail.Length;
            for (int k = 0; k < s.detail.Length; k++)
            {
                var r = s.detail[k];
                if (r == null) continue;
                if (k < upto) s.detailOn[k] = r.enabled;
                r.enabled = false;
            }
            // zone lamps stay on: out of range they cost nothing (per-object light culling), and InteriorProbes' box
            // probes (tunnels, ruins) render in any zone at load and at band changes: they must see the lamps lit
            for (int k = 0; k < s.ps.Length; k++)
                if (s.ps[k] != null) { s.psOn[k] = s.ps[k].isPlaying; if (s.psOn[k]) s.ps[k].Pause(true); }
            s.hidden = true;
            s.pending = -1;
        }

        void HideLow(State s)
        {
            // a show still in progress: only the renderers already back hold a fresh state
            int upto = s.lowPending >= 0 ? s.lowPending : s.low.Length;
            for (int k = 0; k < s.low.Length; k++)
            {
                var r = s.low[k];
                if (r == null) continue;
                if (k < upto) s.lowOn[k] = r.enabled;
                r.enabled = false;
            }
            s.lowHidden = true;
            s.lowPending = -1;
        }

        void ShowLow(State s, bool instant)
        {
            s.lowHidden = false;
            s.lowPending = 0;
            if (instant) FinishLow(s);
        }

        void FinishLow(State s)
        {
            for (int k = Mathf.Max(0, s.lowPending); k < s.low.Length; k++)
                if (s.low[k] != null) s.low[k].enabled = s.lowOn[k];
            s.lowPending = -1;
        }

        void Show(State s, bool instant)
        {
            s.hidden = false;
            if (s.lowHidden) ShowLow(s, instant);
            for (int k = 0; k < s.ps.Length; k++) if (s.ps[k] != null && s.psOn[k]) s.ps[k].Play(true);
            s.pending = 0;          // still forced to the lowest LOD until every renderer is back (no holes)
            if (instant) Finish(s);
        }

        void Finish(State s)
        {
            for (int k = Mathf.Max(0, s.pending); k < s.detail.Length; k++)
                if (s.detail[k] != null) s.detail[k].enabled = s.detailOn[k];
            s.pending = -1;
            if (s.lowPending >= 0) FinishLow(s);          // no holes once the groups pick their own level again
            foreach (var g in s.groups) if (g != null) g.ForceLOD(-1);
        }

        void OnDisable()
        {
            // switched off (or the scene unloading): everything back as built
            if (st != null)
                foreach (var s in st)
                {
                    if (s.hidden) Show(s, true);
                    else if (s.pending >= 0) Finish(s);
                    else if (s.lowPending >= 0) FinishLow(s);
                }
            if (active == this) active = null;
        }

        void OnDestroy()
        {
            if (active == this) active = null;
            pins.Clear();
            keepLow = false;
        }
    }
}
