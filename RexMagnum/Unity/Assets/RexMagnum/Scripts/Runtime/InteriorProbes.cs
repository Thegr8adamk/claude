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
        Vector3? pinned;                       // ZoneStreamer.EnsureShown position of the probe rendering now

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
                    // the probe's zone at full detail while it renders (a streamed-out zone sits at its lowest LOD)
                    pinned = p.transform.position;
                    ZoneStreamer.EnsureShown(pinned.Value);
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
