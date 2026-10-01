using UnityEngine;

namespace RexMagnum
{
    /// <summary>Golf-cart charge station (resort, golf course, ruins): carts parked within 3.5 m charge up.</summary>
    public class ChargeStation : MonoBehaviour
    {
        void OnEnable() => GolfCart.RegisterCharger(transform);
    }
}
