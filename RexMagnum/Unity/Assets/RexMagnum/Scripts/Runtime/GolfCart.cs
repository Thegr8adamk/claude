using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Golf cart (brief section 6). Use (F / X) next to it: get in / out. Drive: W/S or left stick, steer A/D.
    /// Battery (HUD): drains with distance; more when driving fast, uphill, and with the headlights on
    /// (Hands key while driving). At 0 the cart only crawls (Rex pushes it) until it reaches a charge station.
    /// Arcade physics: a Rigidbody that stays upright and follows the ground.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class GolfCart : MonoBehaviour
    {
        public Transform seat;
        public Light[] headlights;
        public float maxSpeed = 9f, accel = 5f, turnRate = 70f;
        public float battery = 100f;
        public static GolfCart Driven { get; private set; }
        static readonly System.Collections.Generic.List<Transform> chargers = new System.Collections.Generic.List<Transform>();
        Rigidbody rb;
        RexController rex;
        RexCameraRig rig;
        float speed, nextUse, nextLine;
        bool lights;

        public static void RegisterCharger(Transform t) { if (!chargers.Contains(t)) chargers.Add(t); }

        void Start()
        {
            rb = GetComponent<Rigidbody>();
            rb.mass = 400f;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rex = FindFirstObjectByType<RexController>();
            rig = FindFirstObjectByType<RexCameraRig>();
            SetLights(false);
        }

        void SetLights(bool on)
        {
            lights = on;
            if (headlights != null) foreach (var l in headlights) if (l != null) l.enabled = on;
        }

        void Update()
        {
            if (rex == null) return;
            bool driving = Driven == this;
            if (RMInput.Use && Time.time > nextUse)
            {
                if (driving) { Exit(); nextUse = Time.time + 0.5f; }
                else if (Driven == null && Vector3.Distance(rex.transform.position, transform.position) < 3f)
                { Enter(); nextUse = Time.time + 0.5f; }
            }
            if (!driving) return;
            if (RMInput.Hands) SetLights(!lights);
            rex.transform.SetPositionAndRotation(seat.position, seat.rotation);
            // charging
            foreach (var c in chargers)
                if (c != null && Vector3.Distance(c.position, transform.position) < 3.5f)
                    battery = Mathf.Min(100f, battery + 8f * Time.deltaTime);
        }

        void FixedUpdate()
        {
            bool driving = Driven == this;
            Vector2 mv = driving ? RMInput.Move : Vector2.zero;
            float top = battery > 0f ? maxSpeed : maxSpeed * 0.15f;
            speed = Mathf.MoveTowards(speed, mv.y * top, accel * Time.fixedDeltaTime);
            if (Mathf.Abs(mv.y) < 0.05f) speed = Mathf.MoveTowards(speed, 0f, accel * 1.5f * Time.fixedDeltaTime);
            float turn = mv.x * turnRate * Mathf.Clamp01(Mathf.Abs(speed) / 2f) * Mathf.Sign(speed == 0 ? 1 : speed);
            rb.MoveRotation(Quaternion.Euler(0f, rb.rotation.eulerAngles.y + turn * Time.fixedDeltaTime, 0f));
            Vector3 v = transform.forward * speed;
            v.y = rb.linearVelocity.y;
            rb.linearVelocity = v;
            if (driving && Mathf.Abs(speed) > 0.1f)
            {
                float dist = Mathf.Abs(speed) * Time.fixedDeltaTime;
                float uphill = Mathf.Max(0f, Vector3.Dot(transform.forward * Mathf.Sign(speed), Vector3.up)) * 3f;
                float drain = 0.05f * dist * (1f + (Mathf.Abs(speed) > 6f ? 0.6f : 0f) + uphill);
                if (lights) drain += 0.6f / 60f * Time.fixedDeltaTime;
                Progress.Add("cart_m_frac", Mathf.RoundToInt(dist * 100f));
                if (Progress.Value("cart_m_frac") >= 100) { Progress.Add("cart_m", Progress.Value("cart_m_frac") / 100); Progress.Set("cart_m_frac", Progress.Value("cart_m_frac") % 100); }
                float before = battery;
                battery = Mathf.Max(0f, battery - drain);
                if (before > 0f && battery <= 0f) { RexVoice.Say(rex.gameObject, "CartDead", true); Progress.Set("cart_dead", 1); }
            }
            if (driving && battery <= 0f && Mathf.Abs(speed) > 0.3f && Time.time > nextLine)
            {
                nextLine = Time.time + 20f;
                RexVoice.Say(rex.gameObject, "CartPush", true);
            }
        }

        void Enter()
        {
            Driven = this;
            rex.InputEnabled = false;
            var cc = rex.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            if (rex.bodyAnimator != null) rex.bodyAnimator.SetBool("Drive", true);   // sit at the wheel
        }

        void Exit()
        {
            Driven = null;
            Vector3 p = transform.position - transform.right * 1.3f + Vector3.up * 0.2f;
            rex.transform.position = p;
            var cc = rex.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;
            rex.InputEnabled = true;
            if (rex.bodyAnimator != null) rex.bodyAnimator.SetBool("Drive", false);
            speed = 0f;
        }

        void OnGUI()
        {
            if (Driven != this) return;
            float w = Screen.width, h = Screen.height, s = h / 1080f;
            var r = new Rect(w - 300 * s, h - 80 * s, 260 * s, 26 * s);
            GUI.color = new Color(0, 0, 0, 0.5f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = battery > 25f ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.3f, 0.2f);
            GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, (r.width - 6) * battery / 100f, r.height - 6), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x, r.y - 22 * s, r.width, 22 * s), $"BATTERY {battery:0}%   {(lights ? "LIGHTS ON" : "")}");
        }
    }
}
