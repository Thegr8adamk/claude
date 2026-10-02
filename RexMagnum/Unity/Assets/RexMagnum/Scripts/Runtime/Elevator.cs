using System.Collections.Generic;
using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Hotel elevator (brief: stairs + elevator, soft music). Use (F / X) inside the cab: go to the next floor
    /// (up, then back down). Use near a closed landing door: call the cab. Landing doors open only when the cab
    /// stands at that floor, so nobody falls into the shaft. Rex rides along (moved with the cab).
    /// Music: Resources/Music/elevator (optional).
    /// </summary>
    public class Elevator : MonoBehaviour
    {
        public Transform cab;                 // origin = cab floor centre
        public float[] floorHeights;          // world Y of each floor top
        public Transform[] landingDoors;      // one per floor (slides sideways to open)
        public Vector3 doorOpenOffset = new Vector3(0f, 0f, 1.25f);
        public float speed = 1.6f;
        public Vector3 cabHalfSize = new Vector3(1.45f, 1.3f, 1.55f);
        int target, current;
        CharacterController rex;
        AudioSource music;
        Vector3[] doorClosed;
        float nextUse;

        void Start()
        {
            var rc = FindAnyObjectByType<RexController>();
            if (rc != null) rex = rc.GetComponent<CharacterController>();
            doorClosed = new Vector3[landingDoors.Length];
            for (int i = 0; i < landingDoors.Length; i++) doorClosed[i] = landingDoors[i].localPosition;
            music = cab.gameObject.AddComponent<AudioSource>();
            music.clip = Resources.Load<AudioClip>("Music/elevator");
            music.loop = true;
            music.spatialBlend = 1f;
            music.maxDistance = 6f;
            music.volume = 0.5f * AudioMix.Music;
            if (music.clip != null) music.Play();
        }

        bool RexInCab()
        {
            if (rex == null) return false;
            Vector3 p = cab.InverseTransformPoint(rex.transform.position + Vector3.up * 0.5f);
            return Mathf.Abs(p.x) < cabHalfSize.x && p.y > -0.3f && p.y < cabHalfSize.y * 2f && Mathf.Abs(p.z) < cabHalfSize.z;
        }

        int NearestFloor(float y)
        {
            int best = 0;
            for (int i = 1; i < floorHeights.Length; i++)
                if (Mathf.Abs(floorHeights[i] - y) < Mathf.Abs(floorHeights[best] - y)) best = i;
            return best;
        }

        [Range(0f, 1f)] public float slimeChance = 0.2f;   // brief: the elevator is sometimes slime-filled

        /// <summary>The cab stops: sometimes its floor is covered in slime (puddles ride along; Rex can slip).</summary>
        void Arrived()
        {
            if (Random.value >= slimeChance) return;
            var green = new Color(0.35f, 0.95f, 0.3f);
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = cab.position + cab.right * Random.Range(-0.9f, 0.9f) + cab.forward * Random.Range(-1f, 1f);
                var puddle = SlimePuddle.Spawn(p, green, Random.Range(0.6f, 1.0f));
                puddle.transform.SetParent(cab, true);
            }
            SlimeFX.Splat(cab.position + Vector3.up * 2.2f, Vector3.down, green, 6);   // dripping from the ceiling
        }

        void Update()
        {
            bool moving = Mathf.Abs(cab.position.y - floorHeights[target]) > 0.01f;
            if (RMInput.Use && Time.time > nextUse && rex != null)
            {
                nextUse = Time.time + 0.5f;
                if (RexInCab() && !moving)
                    target = current + 1 < floorHeights.Length ? current + 1 : 0;
                else if (Vector3.Distance(rex.transform.position, landingDoors[NearestFloor(rex.transform.position.y)].position) < 3f)
                    target = NearestFloor(rex.transform.position.y);
            }
            if (moving)
            {
                float y = Mathf.MoveTowards(cab.position.y, floorHeights[target], speed * Time.deltaTime);
                float dy = y - cab.position.y;
                bool ride = RexInCab();
                cab.position = new Vector3(cab.position.x, y, cab.position.z);
                if (ride) rex.Move(new Vector3(0f, dy, 0f));
            }
            else
            {
                if (current != target) Arrived();
                current = target;
            }
            // landing doors: open only at the floor where the cab stands still
            for (int i = 0; i < landingDoors.Length; i++)
            {
                bool open = !moving && i == current;
                Vector3 goal = doorClosed[i] + (open ? doorOpenOffset : Vector3.zero);
                landingDoors[i].localPosition = Vector3.MoveTowards(landingDoors[i].localPosition, goal, 1.5f * Time.deltaTime);
            }
        }
    }
}
