using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace RexMagnum
{
    /// <summary>
    /// Renders the small interior box probes under this object (lobby, corridors, tunnels, room 304) by script:
    /// once on load, then again when DayNight.Night crosses 0.3 or 0.7 (dusk / night / dawn), so the rooms do not
    /// keep reflecting the noon sky all night. One probe at a time, time-sliced (IndividualFaces: one cube face per
    /// frame, not six scene renders in one frame: no hitch at dusk / dawn).
    /// Added by IslandBuilder to the probes' parent; the probes are set to ViaScripting.
    /// </summary>
    public class InteriorProbes : MonoBehaviour
    {
        ReflectionProbe[] probes;
        int band = -1;
        bool busy, again;
        Bounds? pinned;                        // ZoneStreamer.EnsureShown box of the probe rendering now (same key to release)

        void Start()
        {
            probes = GetComponentsInChildren<ReflectionProbe>(true);
            foreach (var p in probes)
            {
                p.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                p.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
                // no cloud dome in the rooms / tunnels (it is drawn at the probe's far plane), except room 304's probe:
                // its windows look out at the sky, and the glass reflected a cloudless one
                if (p.name == "RoomProbe") p.cullingMask |= 1 << DayNight.SkyLayer;
                else p.cullingMask &= ~(1 << DayNight.SkyLayer);
            }
            RoomFill();
        }

        /// <summary>Room 304: the floor read dark blue (the sky ambient from the windows, the only warm light a shadowed
        /// ceiling lamp). A soft warm fill under the ceiling, no shadows (the shadow-light budget stays as it was).
        /// Added here at run time, so a scene built before it gets it too.</summary>
        void RoomFill()
        {
            if (!name.StartsWith("Room304")) return;
            var ceiling = transform.Find("CeilingLight");
            if (ceiling == null || transform.Find("WarmFill") != null) return;
            var l = new GameObject("WarmFill").AddComponent<Light>();
            l.transform.SetParent(transform, false);
            l.transform.position = ceiling.position + Vector3.down * 0.5f;
            l.type = LightType.Point;
            l.color = new Color(1f, 0.74f, 0.5f);
            l.intensity = 0.55f;
            l.range = 6.5f;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.Auto;
        }

        void Update()
        {
            if (probes == null || probes.Length == 0) return;
            float n = DayNight.Night;
            int b = n < 0.3f ? 0 : n < 0.7f ? 1 : 2;
            if (b == band) return;
            band = b;
            if (busy) again = true;            // a crossing during a pass: one more pass when it ends
            else StartCoroutine(RenderAll());
        }

        IEnumerator RenderAll()
        {
            busy = true;
            do
            {
                again = false;
                foreach (var p in probes)
                {
                    if (p == null || !p.isActiveAndEnabled) continue;
                    // every zone the probe's box reaches at full detail while it renders (a streamed-out zone sits at
                    // its lowest LOD); a tunnel box can reach into the ruins zone, not only the zone at its centre
                    pinned = p.bounds;
                    ZoneStreamer.EnsureShown(pinned.Value);
                    for (int f = 0; f < 60 && !ZoneStreamer.Ready(pinned.Value); f++) yield return null;   // sliced show
                    if (p == null || !p.isActiveAndEnabled) { Unpin(); continue; }
                    int id = p.RenderProbe();
                    // wait for the slices (about 9 frames); cap it so a probe disabled mid-render cannot stall the pass
                    for (int f = 0; f < 30 && p != null && !p.IsFinishedRendering(id); f++) yield return null;
                    Unpin();
                }
            } while (again);
            busy = false;
        }

        void Unpin()
        {
            if (pinned.HasValue) ZoneStreamer.ReleaseShown(pinned.Value);
            pinned = null;
        }

        void OnDisable()
        {
            // the pass coroutine stops with the object: release its zone and render again when enabled
            Unpin();
            busy = again = false;
            band = -1;
        }
    }
}
