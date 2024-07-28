namespace SpaceAI.WeaponSystem
{
    using SpaceAI.Events;
    using SpaceAI.Ship;
    using System;
    using UnityEngine;

    public class SA_Turret : SA_WeaponLaunchManager
    {
        private const float _attackDirection = 0.98F;

        [Tooltip("Should turret rotate in the FixedUpdate rather than Update?")]
        public bool runRotationsInFixed = false;

        [Header("Objects")]
        [Tooltip("Transform used to provide the horizontal rotation of the turret.")]
        public Transform[] turretBase;
        [Tooltip("Transform used to provide the vertical rotation of the barrels. Must be a child of the TurretBase.")]
        public Transform[] turretBarrels;

        [Header("Rotation Limits")]
        [Tooltip("Turn rate of the turret's base and barrels in degrees per second.")]
        public float turnRate = 30.0f;
        [Tooltip("When true, turret rotates according to left/right traverse limits. When false, turret can rotate freely.")]
        public bool limitTraverse = false;
        [Tooltip("When traverse is limited, how many degrees to the left the turret can turn.")]
        [Range(0.0f, 180.0f)]
        public float leftTraverse = 60.0f;
        [Tooltip("When traverse is limited, how many degrees to the right the turret can turn.")]
        [Range(0.0f, 180.0f)]
        public float rightTraverse = 60.0f;
        [Tooltip("How far up the barrel(s) can rotate.")]
        [Range(0.0f, 90.0f)]
        public float elevation = 60.0f;
        [Tooltip("How far down the barrel(s) can rotate.")]
        [Range(0.0f, 90.0f)]
        public float depression = 5.0f;

        [Header("Utilities")]
        [Tooltip("Show the arcs that the turret can aim through.\n\nRed: Left/Right Traverse\nGreen: Elevation\nBlue: Depression")]
        public bool showArcs = false;
        [Tooltip("When game is running in editor, draws a debug ray to show where the turret is aiming.")]
        public bool showDebugRay = true;
        [Tooltip("If true, will aim for targets automatically")]
        public bool independent;
        public Vector3 aimPoint = new Vector3(0, 0, 100);
        [SerializeField] private float outOfRange = 550;

        private bool aiming = false;
        private SA_WeaponController weaponController;

        [Header("Debug")]
        public bool debug = false;
        public GameObject DebugTarget;
        public float fireRate = 1f;
        public SA_DamageSandler bullet;

        public bool Idle { get { return !aiming; } }
        public bool AtRest { get; private set; } = false;

        private void Start()
        {
            if (debug)
            {
                Target = DebugTarget;
                SetFireShells(new SA_DamageSandler[] { bullet }, 0);
            }
        }

        private void Update()
        {
            if (!runRotationsInFixed)
            {
                if (debug)
                {
                    DebugTargeting();
                }
                else
                {
                    IndependentShipTurret();
                }

                RotateTurret();
            }

#if UNITY_EDITOR
            if (showDebugRay) DrawDebugRays();
#endif
        }

        private void FixedUpdate()
        {
            if (runRotationsInFixed)
            {
                if (debug)
                {
                    DebugTargeting();
                }
                else
                {
                    IndependentShipTurret();
                }

                RotateTurret();
            }
        }

        private void DebugTargeting()
        {
            if (!isActiveAndEnabled) return;

            if (Target)
            {
                Predict();
            }
            else
            {
                RotateToIdle();
            }
        }

        private void Predict()
        {
            Vector3 displacement = Target.transform.position - transform.position;
            Vector3 targetVelocity = Target.GetComponent<Rigidbody>().velocity;

            // Get the velocity of the ship that the turret is mounted on
            Vector3 shipVelocity = Owner.GetComponent<Rigidbody>().velocity;

            // Adjust the target velocity to consider the relative velocity between the ship and the target
            Vector3 relativeVelocity = targetVelocity - shipVelocity;

            float a = Vector3.Dot(relativeVelocity, relativeVelocity) - BulletSpeed * BulletSpeed;
            float b = 2f * Vector3.Dot(relativeVelocity, displacement);
            float c = Vector3.Dot(displacement, displacement);

            float d = b * b - 4f * a * c;

            if (d >= 0)
            {
                float t1 = (-b - Mathf.Sqrt(d)) / (2f * a);
                float t2 = (-b + Mathf.Sqrt(d)) / (2f * a);
                float t;

                // Use the smallest positive time
                if (t1 > 0 && t2 > 0)
                    t = Mathf.Min(t1, t2);
                else
                    t = Mathf.Max(t1, t2);

                if (t > 0)
                {
                    Vector3 predictedPosition = Target.transform.position + targetVelocity * t;
                    Vector3 aimPoint = predictedPosition;

                    SetAimpoint(aimPoint);

                    Vector3 dir = (aimPoint - transform.position).normalized;

                    foreach (var barrel in turretBarrels)
                    {
                        float dot = Vector3.Dot(dir, barrel.transform.forward);

                        if (dot > _attackDirection)
                        {
                            Shoot(WeaponLaunchManagerSettings.shellOuter, aimPoint);
                        }
                    }
                }
            }
        }

        private void IndependentShipTurret()
        {
            if (!isActiveAndEnabled) return;

            SA_IShip ship = GetComponentInParent<SA_IShip>();

            if (ship == null) return;

            if (weaponController == null) weaponController = ship.WeaponControll;

            if (independent)
            {
                if (Target && Vector3.Distance(transform.position, Target.transform.position) > outOfRange)
                {
                    Target = null;
                }

                if (Target)
                {
                    Predict();
                }
                else
                {
                    SA_EventsBus.Publish(new SA_TurretTargetRequestEvent(this, transform, ship.ShipConfiguration.AIConfig.GroupTypesToAction, outOfRange));

                    RotateToIdle();
                }
            }
            else
            {
                if (Target)
                {
                    Predict();
                }
                else
                {
                    RotateToIdle();
                }
            }
        }

        public void SetAimpoint(Vector3 position)
        {
            aiming = true;
            aimPoint = position;
        }

        public void SetAimpointFromShip(Transform aimpoint)
        {
            Vector3 predictendTarget = PredictTargetPosition(aimpoint, weaponController.GetCurrentWeapon().BulletInitPos, weaponController.GetCurrentWeapon().BulletSpeed);

            SetAimpoint(predictendTarget);

            Vector3 dir = (predictendTarget - transform.position).normalized;

            foreach (var barrel in turretBarrels)
            {
                float dot = Vector3.Dot(dir, barrel.transform.forward);

                if (dot > _attackDirection)
                {
                    weaponController.LaunchWeapon(WeaponId);
                }
            }
        }

        private Vector3 PredictTargetPosition(Transform target, Vector3 bulletStartPosition, float bulletSpeed)
        {
            Rigidbody targetRigidbody = target.GetComponent<Rigidbody>();

            if (targetRigidbody == null)
            {
                return target.transform.position; // Return current position if no Rigidbody
            }

            // Calculate the direction vector from the bullet's start position to the predicted target position
            Vector3 direction = (PredictedPosition(target) - bulletStartPosition).normalized;

            // Calculate the bullet's velocity vector by multiplying the direction vector with the bullet's speed
            Vector3 bulletVelocity = direction * bulletSpeed;

            // Calculate the time to intercept using the distance between the bullet's start position and the predicted target position
            float distanceToIntercept = Vector3.Distance(bulletStartPosition, PredictedPosition(target));
            float timeToIntercept = distanceToIntercept / bulletSpeed;

            // Predict the future position based on the bullet's velocity and time to intercept
            Vector3 predictedPosition = bulletStartPosition + bulletVelocity * timeToIntercept;

            return predictedPosition;
        }

        private Vector3 PredictedPosition(Transform target)
        {
            Rigidbody targetRigidbody = target.GetComponent<Rigidbody>();

            if (targetRigidbody == null)
            {
                return target.transform.position; // Return current position if no Rigidbody
            }

            Vector3 targetVelocity = targetRigidbody.velocity; // Adjust for more accurate prediction

            // Predict the future position based on current position, velocity, and time to intercept
            Vector3 predictedPosition = target.transform.position + targetVelocity;

            return predictedPosition;
        }

        public void SetIdle(bool idle)
        {
            aiming = !idle;

            if (aiming)
                AtRest = false;
        }

        private void RotateTurret()
        {
            if (!isActiveAndEnabled) return;

            if (aiming)
            {
                RotateBase();
                RotateBarrels();
            }
            else if (!AtRest)
            {
                AtRest = RotateToIdle();
            }
        }

        private void RotateBase()
        {
            foreach (var turrBase in turretBase)
            {
                Vector3 localTargetPos = transform.InverseTransformPoint(aimPoint);
                localTargetPos.y = 0.0f;

                Vector3 clampedLocalVec2Target = localTargetPos;
                if (limitTraverse)
                {
                    if (localTargetPos.x >= 0.0f)
                        clampedLocalVec2Target = Vector3.RotateTowards(Vector3.forward, localTargetPos, Mathf.Deg2Rad * rightTraverse, float.MaxValue);
                    else
                        clampedLocalVec2Target = Vector3.RotateTowards(Vector3.forward, localTargetPos, Mathf.Deg2Rad * leftTraverse, float.MaxValue);
                }

                Quaternion rotationGoal = Quaternion.LookRotation(clampedLocalVec2Target);
                Quaternion newRotation = Quaternion.RotateTowards(turrBase.localRotation, rotationGoal, turnRate * Time.deltaTime);

                turrBase.localRotation = newRotation;
            }
        }

        private void RotateBarrels()
        {
            Vector3 localTargetPos = Vector3.zero;

            foreach (var turrBase in turretBase)
            {
                localTargetPos = turrBase.InverseTransformPoint(aimPoint);
            }

            localTargetPos.x = 0.0f;

            Vector3 clampedLocalVec2Target = localTargetPos;

            if (localTargetPos.y >= 0.0f)
                clampedLocalVec2Target = Vector3.RotateTowards(Vector3.forward, localTargetPos, Mathf.Deg2Rad * elevation, float.MaxValue);
            else
                clampedLocalVec2Target = Vector3.RotateTowards(Vector3.forward, localTargetPos, Mathf.Deg2Rad * depression, float.MaxValue);

            Quaternion rotationGoal = Quaternion.LookRotation(clampedLocalVec2Target);

            foreach (var turretBarr in turretBarrels)
            {
                Quaternion newRotation = Quaternion.RotateTowards(turretBarr.localRotation, rotationGoal, 2.0f * turnRate * Time.deltaTime);
                turretBarr.localRotation = newRotation;
            }
        }

        private bool RotateToIdle()
        {
            bool baseFinished = false;
            bool barrelsFinished = false;

            foreach (var turrBase in turretBase)
            {
                Quaternion newRotation = Quaternion.RotateTowards(turrBase.localRotation, Quaternion.identity, turnRate * Time.deltaTime);
                turrBase.localRotation = newRotation;

                if (turrBase.localRotation == Quaternion.identity)
                    baseFinished = true;
            }

            foreach (var turrBarr in turretBarrels)
            {
                Quaternion newRotation = Quaternion.RotateTowards(turrBarr.localRotation, Quaternion.identity, 2.0f * turnRate * Time.deltaTime);
                turrBarr.localRotation = newRotation;

                if (turrBarr.localRotation == Quaternion.identity)
                    barrelsFinished = true;
            }

            return (baseFinished && barrelsFinished);
        }

        #region EDITOR
#if UNITY_EDITOR
        private void DrawDebugRays()
        {
            foreach (var item in turretBarrels)
            {
                Debug.DrawRay(item.position, item.forward * 100.0f, Color.red);
            }

            foreach (var item in turretBase)
            {
                Debug.DrawRay(item.position, item.forward * 100.0f, Color.red);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(aimPoint, 2);
        }

        public void ClearTransforms()
        {
            /// Don't allow this while ingame.
            if (!Application.isPlaying)
            {
                turretBase = null;
                turretBarrels = null;
            }
        }
#endif
        #endregion
    }
}