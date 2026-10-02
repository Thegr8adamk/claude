using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace RexMagnum
{
    /// <summary>
    /// Renders the small interior box probes under this object (lobby, corridors, tunnels, room 304) by script:
    /// once on load, then again when DayNight.Night crosses 0.3 or 0.7 (dusk / night / dawn), so the rooms do not
    /// keep reflecting the noon sky all night. One probe per frame: each is six small scene renders.
    /// Added by IslandBuilder to the probes' parent; the probes are set to ViaScripting.
    /// </summary>
    public class InteriorProbes : MonoBehaviour
    {
        ReflectionProbe[] probes;
        int band = -1;
        bool busy, again;

        void Start()
        {
            probes = GetComponentsInChildren<ReflectionProbe>(true);
            foreach (var p in probes) p.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
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
                    p.RenderProbe();
                    yield return null;
                }
            } while (again);
            busy = false;
        }
    }
}
