using System;
using UnityEngine;

namespace SpaceAI2
{
    /// <summary>Shield absorbs damage first and regenerates after a pause; the ship is disabled when the hull runs out.</summary>
    [RequireComponent(typeof(SpaceShip))]
    public class ShipHealth : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float shieldRegenDelay = 3f;
        [Tooltip("Shield points per second.")]
        [SerializeField, Min(0f)] private float shieldRegenRate = 10f;

        private SpaceShip ship;
        private float lastHitTime = float.NegativeInfinity;

        public event Action<ShipHealth, HitInfo> Damaged;
        public event Action<ShipHealth, HitInfo> Destroyed;

        public float Hull { get; private set; }
        public float Shield { get; private set; }
        public float LastShieldDamage { get; private set; }
        public float MaxHull => ship.Motor.Profile != null ? ship.Motor.Profile.Hull : 100f;
        public float MaxShield => ship.Motor.Profile != null ? ship.Motor.Profile.Shield : 0f;
        public bool IsAlive => Hull > 0f;

        public void Restore()
        {
            Hull = MaxHull;
            Shield = MaxShield;
            LastShieldDamage = 0f;
        }

        private void Awake()
        {
            ship = GetComponent<SpaceShip>();
            Restore();
        }

        private void OnEnable() => ship.Hit += OnHit;

        private void OnDisable() => ship.Hit -= OnHit;

        private void Update()
        {
            if (Shield < MaxShield && Time.time - lastHitTime > shieldRegenDelay)
                Shield = Mathf.Min(MaxShield, Shield + shieldRegenRate * Time.deltaTime);
        }

        private void OnHit(HitInfo hit)
        {
            if (!IsAlive) return;
            lastHitTime = Time.time;
            float absorbed = Mathf.Min(Shield, hit.Damage);
            LastShieldDamage = absorbed;
            Shield -= absorbed;
            Hull = Mathf.Max(0f, Hull - (hit.Damage - absorbed));
            Damaged?.Invoke(this, hit);

            if (IsAlive) return;
            Destroyed?.Invoke(this, hit);
            gameObject.SetActive(false);
        }
    }
}
