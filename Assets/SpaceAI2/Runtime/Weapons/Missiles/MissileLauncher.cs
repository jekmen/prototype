using System;
using UnityEngine;

namespace SpaceAI2
{
    /// <summary>
    /// Missile rails with a seeker lock: guided missiles launch only after the lock target has stayed in the lock cone
    /// and range for the profile's lock time. Unguided ones launch straight down the nose.
    /// </summary>
    [RequireComponent(typeof(SpaceShip))]
    public class MissileLauncher : MonoBehaviour
    {
        [SerializeField] private MissileProfile missile;
        [Tooltip("Launch points used in turn. Empty means two rails under the wings.")]
        [SerializeField] private Transform[] hardpoints;
        [Tooltip("Missiles carried; negative is unlimited.")]
        [SerializeField] private int ammo = -1;

        private SpaceShip ship;
        private Vector3[] localHardpoints;
        private SpaceShip lockTarget;
        private float cooldown;
        private float lockTime;
        private int rail;

        public event Action<MissileLauncher, int> Launched;

        public MissileProfile Missile
        {
            get => missile;
            set
            {
                if (missile == value) return;
                missile = value;
                lockTime = 0f;
            }
        }

        public int Ammo
        {
            get => ammo;
            set => ammo = value;
        }

        /// <summary>Launch rails, used in turn; empty uses two under the hull. Read in Awake.</summary>
        public Transform[] Hardpoints
        {
            get => hardpoints;
            set => hardpoints = value;
        }

        public SpaceShip Ship => ship;

        /// <summary>The ship the seeker tries to lock; changing it restarts the lock.</summary>
        public SpaceShip LockTarget
        {
            get => lockTarget;
            set
            {
                if (lockTarget == value) return;
                lockTarget = value;
                lockTime = 0f;
            }
        }

        public float LockProgress => missile == null || missile.LockTime <= 0f ? (IsInLockCone ? 1f : 0f) : Mathf.Clamp01(lockTime / missile.LockTime);
        public bool IsLocked => missile != null && (!missile.IsGuided || IsInLockCone && lockTime >= missile.LockTime);
        public bool IsReady => missile != null && cooldown <= 0f && ammo != 0;
        public bool CanLaunch => IsReady && IsLocked;

        private bool IsInLockCone
        {
            get
            {
                if (lockTarget == null || !lockTarget.gameObject.activeInHierarchy) return false;
                Vector3 offset = lockTarget.Position - ship.Position;
                return offset.sqrMagnitude <= missile.LockRange * missile.LockRange && Vector3.Angle(ship.Forward, offset) <= missile.LockAngle;
            }
        }

        public bool Launch()
        {
            if (!CanLaunch) return false;
            Rigidbody body = ship.Motor.Body;
            SpaceShip target = missile.IsGuided ? lockTarget : null;
            for (int i = 0; i < missile.Salvo; i++)
            {
                Vector3 origin = body.position + body.rotation * localHardpoints[rail];
                rail = (rail + 1) % localHardpoints.Length;

                Vector3 direction = body.rotation * Vector3.forward;
                if (missile.Salvo > 1)
                {
                    // Fan the swarm out evenly around the nose so the missiles converge from different sides.
                    float around = (i + 0.5f) / missile.Salvo * 360f;
                    Vector3 side = Quaternion.AngleAxis(around, Vector3.forward) * Vector3.up;
                    direction = body.rotation * Quaternion.AngleAxis(missile.SalvoSpread, Vector3.Cross(Vector3.forward, side)) * Vector3.forward;
                }
                MissileSystem.Instance.Launch(missile, ship, origin, direction, target);
            }

            cooldown = missile.Reload;
            if (ammo > 0) ammo--;
            Launched?.Invoke(this, missile.Salvo);
            return true;
        }

        private void Awake()
        {
            ship = GetComponent<SpaceShip>();
            if (hardpoints == null || hardpoints.Length == 0)
            {
                float r = ship.Radius;
                localHardpoints = new[] { new Vector3(r * 0.6f, -r * 0.4f, 0f), new Vector3(-r * 0.6f, -r * 0.4f, 0f) };
                return;
            }
            localHardpoints = new Vector3[hardpoints.Length];
            for (int i = 0; i < hardpoints.Length; i++) localHardpoints[i] = transform.InverseTransformPoint(hardpoints[i].position);
        }

        private void OnDisable()
        {
            lockTarget = null;
            lockTime = 0f;
        }

        private void FixedUpdate()
        {
            if (cooldown > 0f) cooldown -= Time.fixedDeltaTime;
            if (missile == null || !missile.IsGuided) return;
            // Leaving the cone drains the lock faster than it builds, so a brief slip does not restart it.
            lockTime = IsInLockCone ? lockTime + Time.fixedDeltaTime : Mathf.Max(0f, lockTime - Time.fixedDeltaTime * 2f);
        }
    }
}
