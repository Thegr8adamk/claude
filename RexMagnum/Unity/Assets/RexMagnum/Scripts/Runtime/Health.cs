using System;
using UnityEngine;

namespace RexMagnum
{
    /// <summary>Hit points for Rex and the blobs.</summary>
    public class Health : MonoBehaviour
    {
        public float maxHealth = 100f;
        public float Current { get; private set; }
        public bool IsDead { get; private set; }
        public float Fraction => maxHealth > 0 ? Current / maxHealth : 0f;

        public event Action<float, Vector3, GameObject> Damaged;   // amount, point, attacker
        public event Action<GameObject> Died;                      // killer

        void Awake() => Current = maxHealth;

        public void ResetHealth(float max = -1f)
        {
            if (max > 0) maxHealth = max;
            Current = maxHealth;
            IsDead = false;
        }

        public void Heal(float amount)
        {
            if (!IsDead) Current = Mathf.Min(maxHealth, Current + amount);
        }

        public void Damage(float amount, Vector3 point, GameObject attacker = null)
        {
            if (IsDead || amount <= 0f) return;
            Current = Mathf.Max(0f, Current - amount);
            Damaged?.Invoke(amount, point, attacker);
            if (Current <= 0f)
            {
                IsDead = true;
                Died?.Invoke(attacker);
            }
        }
    }
}
