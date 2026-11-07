using System;
using UnityEngine;

namespace SpaceAI2
{
    [RequireComponent(typeof(SpaceShip))]
    public class ShipGun : MonoBehaviour
    {
        [SerializeField] private WeaponProfile weapon;
        [Tooltip("Barrels fire in turn. Empty means a single barrel just ahead of the nose.")]
        [SerializeField] private Transform[] muzzles;
        [Tooltip("Cone around the nose the barrels can swivel within to meet the aim direction, degrees.")]
        [SerializeField, Range(0f, 30f)] private float gimbalAngle = 2f;

        private SpaceShip ship;
        private Vector3[] localMuzzles;
        private float cooldown;
        private int barrel;

        public event Action<ShipGun> Fired;

        public WeaponProfile Weapon
        {
            get => weapon;
            set => weapon = value;
        }

        public float GimbalAngle
        {
            get => gimbalAngle;
            set => gimbalAngle = Mathf.Clamp(value, 0f, 30f);
        }

        /// <summary>Barrel points, fired in turn; empty fires from the nose. The list is read in Awake; positions are read per shot.</summary>
        public Transform[] Muzzles
        {
            get => muzzles;
            set => muzzles = value;
        }

        public SpaceShip Ship => ship;
        public bool Trigger { get; set; }
        /// <summary>World direction the gun should fire along; zero means straight along the nose.</summary>
        public Vector3 AimDirection { get; set; }
        /// <summary>Flak fuse in seconds; zero detonates at full range.</summary>
        public float FuseTime { get; set; }

        public Vector3 FireDirection
        {
            get
            {
                Vector3 nose = ship.Motor.Body.rotation * Vector3.forward;
                if (AimDirection.sqrMagnitude < 0.0001f) return nose;
                return Vector3.RotateTowards(nose, AimDirection.normalized, gimbalAngle * Mathf.Deg2Rad, 0f);
            }
        }

        private void Awake()
        {
            ship = GetComponent<SpaceShip>();
            if (muzzles == null || muzzles.Length == 0)
            {
                localMuzzles = new[] { Vector3.forward * (ship.Radius + 1f) };
                return;
            }
            localMuzzles = new Vector3[muzzles.Length];
            for (int i = 0; i < muzzles.Length; i++) localMuzzles[i] = transform.InverseTransformPoint(muzzles[i].position);
        }

        private void OnDisable() => Trigger = false;

        private void FixedUpdate()
        {
            if (cooldown > 0f) cooldown -= Time.fixedDeltaTime;
            if (!Trigger || weapon == null) return;

            while (cooldown <= 0f)
            {
                Fire();
                cooldown += 1f / weapon.FireRate;
            }
        }

        private void Fire()
        {
            Rigidbody body = ship.Motor.Body;
            // Muzzles may sit on the banking model, so their offset is read per shot rather than fixed at Awake.
            Transform muzzle = muzzles != null && barrel < muzzles.Length ? muzzles[barrel] : null;
            Vector3 local = muzzle != null ? transform.InverseTransformPoint(muzzle.position) : localMuzzles[barrel];
            Vector3 origin = body.position + body.rotation * local;
            barrel = (barrel + 1) % localMuzzles.Length;

            Vector3 direction = FireDirection;
            if (weapon.Spread > 0f)
            {
                Vector2 jitter = UnityEngine.Random.insideUnitCircle * Mathf.Tan(weapon.Spread * Mathf.Deg2Rad);
                direction = (direction + body.rotation * new Vector3(jitter.x, jitter.y, 0f)).normalized;
            }

            ProjectileSystem.Instance.Fire(weapon, ship, origin, direction, FuseTime);
            Fired?.Invoke(this);
        }
    }
}
