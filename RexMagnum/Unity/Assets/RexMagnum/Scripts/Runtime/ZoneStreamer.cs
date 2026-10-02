using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Zone streaming inside the one island scene (CLAUDE.md §3). Each zone (resort, golf, jungle, ruins, cave,
    /// tunnels, ship) is a set of building roots; when it is farther than the tier's radius (Low 180 m, Medium 260 m,
    /// High / Ultra 400 m, +30 m hysteresis) from Rex and from every on-screen camera, its LOD groups are forced to
    /// their lowest level and the higher-level renderers, its lamps and particle systems are switched off. The lowest
    /// level stays: the hotel / ship are landmarks seen from the hills, and a vanishing building would pop.
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

        // Hold(): every zone shown (time-sliced) until the matching Release(); EnsureShown(pos): the zone at pos shown at
        // once (InteriorProbes: a box probe must capture its zone at full detail, not the forced lowest LOD).
        // Both run out after HoldTimeout (a caller that never released).
        static int holds;
        static readonly System.Collections.Generic.List<Vector3> pins = new System.Collections.Generic.List<Vector3>();
        static ZoneStreamer active;
        static float holdUntil, pinUntil;
        const float HoldTimeout = 20f;

        /// <summary>True while a Hold() is active.</summary>
        public static bool Held => holds > 0;

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
        }

        State[] st;
        QualityAuto quality;
        Transform player;
        Vector3 lastPlayer;
        float nextTick;
        readonly Camera[] camBuf = new Camera[16];
        const float Hysteresis = 30f, JumpDist = 60f;
        const int EnablePerFrame = 150;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { holds = 0; pins.Clear(); active = null; holdUntil = pinUntil = 0f; }

        /// <summary>Shows every zone and keeps them shown until the matching Release() (HoldTimeout at most). Hidden
        /// zones come back a few renderers per frame (an instant show of thousands of renderers hitched on phones).</summary>
        public static void Hold()
        {
            holds++;
            holdUntil = Time.unscaledTime + HoldTimeout;
            if (active != null) active.Refresh(false);
        }

        public static void Release() { if (holds > 0) holds--; }

        /// <summary>Shows the zone at pos (the smallest zone box holding it) at once, full detail, and keeps it shown
        /// until ReleaseShown(pos) (HoldTimeout at most). One zone: cheap enough to do in one frame.</summary>
        public static void EnsureShown(Vector3 pos)
        {
            pins.Add(pos);
            pinUntil = Time.unscaledTime + HoldTimeout;
            if (active == null || active.st == null) return;      // not culled yet: Refresh respects the pin later
            int i = active.ZoneAt(pos);
            if (i < 0) return;
            var s = active.st[i];
            if (s.hidden) active.Show(s, false);            // time-sliced: a whole resort in one frame hitched on phones
        }

        /// <summary>The zone at pos is fully shown (no time-sliced show still running). True when not streamed.</summary>
        public static bool Ready(Vector3 pos)
        {
            if (active == null || active.st == null) return true;
            int i = active.ZoneAt(pos);
            return i < 0 || (!active.st[i].hidden && active.st[i].pending < 0);
        }

        public static void ReleaseShown(Vector3 pos) => pins.Remove(pos);

        /// <summary>Index of the smallest zone box that holds pos, -1 if none.</summary>
        int ZoneAt(Vector3 pos)
        {
            int best = -1;
            float bestVol = float.MaxValue;
            for (int i = 0; i < st.Length; i++)
            {
                var b = st[i].b;
                if (!b.Contains(pos)) continue;
                float vol = b.size.x * b.size.y * b.size.z;
                if (vol < bestVol) { bestVol = vol; best = i; }
            }
            return best;
        }

        bool Pinned(int i)
        {
            foreach (var p in pins) if (ZoneAt(p) == i) return true;
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
                        }
                        ps.AddRange(root.GetComponentsInChildren<ParticleSystem>(true));
                    }
                if (!any) s.b = new Bounds(Vector3.zero, Vector3.one * 1e6f);    // nothing to measure: never culled
                s.detail = detail.ToArray();
                s.detailOn = new bool[s.detail.Length];
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
            // a hold / pin that never got released (caller destroyed mid-pass)
            if (holds > 0 && Time.unscaledTime > holdUntil) holds = 0;
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
            // zones coming back: a few renderers per frame (no hitch when a big zone returns)
            int budget = EnablePerFrame;
            foreach (var s in st)
            {
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
                bool far = holds == 0 && d < float.MaxValue && d > (s.hidden ? r : r + Hysteresis) && !(pins.Count > 0 && Pinned(zi));
                if (far && !s.hidden) Hide(s);
                else if (!far && s.hidden) Show(s, instant);
                else if (!far && instant && s.pending >= 0) Finish(s);
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

        void Show(State s, bool instant)
        {
            s.hidden = false;
            for (int k = 0; k < s.ps.Length; k++) if (s.ps[k] != null && s.psOn[k]) s.ps[k].Play(true);
            s.pending = 0;          // still forced to the lowest LOD until every renderer is back (no holes)
            if (instant) Finish(s);
        }

        void Finish(State s)
        {
            for (int k = Mathf.Max(0, s.pending); k < s.detail.Length; k++)
                if (s.detail[k] != null) s.detail[k].enabled = s.detailOn[k];
            s.pending = -1;
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
                }
            if (active == this) active = null;
        }

        void OnDestroy()
        {
            if (active == this) active = null;
            holds = 0;
            pins.Clear();
        }
    }
}
