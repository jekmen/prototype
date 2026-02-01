namespace SpaceAI.WeaponSystem
{
    using SpaceAI.Events;
    using SpaceAI.Ship;
    using System;
    using UnityEngine;

    public class SA_Turret : SA_WeaponLaunchManager
    {
        private const float _attackDirection = 0.98F;
        private const float _leadTimeEpsilon = 0.0001f;
        private const float _maxLeadTime = 6.0f;
        private const float _maxTargetAccel = 200.0f;
        private const float _maxTurnRate = 120.0f; // deg/sec
        private const float _accelLeadFactor = 0.5f;

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
        [Tooltip("Extra lead multiplier for fast angular targets.")]
        [SerializeField] private float leadMultiplier = 1.15f;
        [Tooltip("Minimum lead scale when target is turning fast.")]
        [SerializeField] private float minLeadScale = 0.7f;
        public Vector3 aimPoint = new Vector3(0, 0, 100);
        [SerializeField] private float outOfRange = 550;

        private bool aiming = false;
        private SA_WeaponController weaponController;
        private bool hasLastTargetVelocity = false;
        private Vector3 lastTargetVelocity = Vector3.zero;
        private int lastTargetInstanceId = -1;

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

        private void FixedUpdate()
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

#if UNITY_EDITOR
            if (showDebugRay) DrawDebugRays();
#endif
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
            Rigidbody targetRb = Target.GetComponentInParent<Rigidbody>();
            if (!targetRb)
            {
                SetAimpoint(Target.transform.position);
                return;
            }

            Rigidbody ownerRb = Owner ? Owner.GetComponent<Rigidbody>() : null;
            Vector3 shipVelocity = Vector3.zero;
            Vector3 targetVelocity = targetRb.velocity;

            Vector3 fireOrigin = GetFireOrigin();
            if (ownerRb)
            {
                Vector3 angularVel = ownerRb.angularVelocity;
                Vector3 r = fireOrigin - ownerRb.worldCenterOfMass;
                shipVelocity = ownerRb.velocity + Vector3.Cross(angularVel, r);
            }
            Vector3 targetPos = Target.transform.position;
            Vector3 displacement = targetPos - fireOrigin;
            Vector3 relativeVelocity = targetVelocity - shipVelocity;

            float a = Vector3.Dot(relativeVelocity, relativeVelocity) - BulletSpeed * BulletSpeed;
            float b = 2f * Vector3.Dot(relativeVelocity, displacement);
            float c = Vector3.Dot(displacement, displacement);

            float distance = displacement.magnitude;
            float t = SolveInterceptTime(a, b, c, distance);
            Vector3 targetAccel = Vector3.zero;
            float leadScale = 1f;

            int currentTargetId = Target.GetInstanceID();
            if (currentTargetId != lastTargetInstanceId)
            {
                hasLastTargetVelocity = false;
                lastTargetInstanceId = currentTargetId;
            }

            if (hasLastTargetVelocity && Time.fixedDeltaTime > 0f)
            {
                targetAccel = (targetVelocity - lastTargetVelocity) / Time.fixedDeltaTime;
                if (targetAccel.sqrMagnitude > _maxTargetAccel * _maxTargetAccel)
                {
                    targetAccel = targetAccel.normalized * _maxTargetAccel;
                }

                float turnRate = Vector3.Angle(lastTargetVelocity, targetVelocity) / Time.fixedDeltaTime;
                leadScale = Mathf.Clamp01(1f - (turnRate / _maxTurnRate));
                leadScale = Mathf.Max(leadScale, minLeadScale);
            }

            lastTargetVelocity = targetVelocity;
            hasLastTargetVelocity = true;

            if (t <= 0f && BulletSpeed > 0.01f)
            {
                t = Mathf.Min(distance / BulletSpeed, _maxLeadTime);
            }

            if (t > 0f)
            {
                t *= leadScale * leadMultiplier;
            }

            Vector3 aimPoint = t > 0f
                ? targetPos + targetVelocity * t + 0.5f * targetAccel * t * t * _accelLeadFactor
                : targetPos;

            SetAimpoint(aimPoint);

            foreach (var barrel in turretBarrels)
            {
                if (!barrel) continue;

                Vector3 dir = (aimPoint - barrel.position).normalized;
                float dot = Vector3.Dot(dir, barrel.transform.forward);

                if (dot > _attackDirection)
                {
                    Shoot(WeaponLaunchManagerSettings.shellOuter, aimPoint);
                }
            }
        }

        private Vector3 GetFireOrigin()
        {
            if (WeaponLaunchManagerSettings != null && WeaponLaunchManagerSettings.shellOuter != null && WeaponLaunchManagerSettings.shellOuter.Length > 0)
            {
                Vector3 sum = Vector3.zero;
                int count = 0;

                foreach (var shell in WeaponLaunchManagerSettings.shellOuter)
                {
                    if (!shell) continue;
                    sum += shell.position;
                    count++;
                }

                if (count > 0)
                {
                    return sum / count;
                }
            }

            if (turretBarrels != null && turretBarrels.Length > 0)
            {
                Vector3 sum = Vector3.zero;
                int count = 0;

                foreach (var barrel in turretBarrels)
                {
                    if (!barrel) continue;
                    sum += barrel.position;
                    count++;
                }

                if (count > 0)
                {
                    return sum / count;
                }
            }

            return transform.position;
        }

        private float SolveInterceptTime(float a, float b, float c, float distance)
        {
            float t;

            if (Mathf.Abs(a) < _leadTimeEpsilon)
            {
                if (Mathf.Abs(b) < _leadTimeEpsilon)
                {
                    return 0f;
                }

                t = -c / b;
            }
            else
            {
                float d = b * b - 4f * a * c;

                if (d < 0f)
                {
                    return 0f;
                }

                float sqrtD = Mathf.Sqrt(d);
                float t1 = (-b - sqrtD) / (2f * a);
                float t2 = (-b + sqrtD) / (2f * a);

                t = SelectPositiveTime(t1, t2);
            }

            if (t <= 0f)
            {
                return 0f;
            }

            float maxLeadTime = Mathf.Min(distance / BulletSpeed * 1.5f, _maxLeadTime);
            return Mathf.Clamp(t, 0f, maxLeadTime);
        }

        private float SelectPositiveTime(float t1, float t2)
        {
            bool t1Valid = t1 > 0f;
            bool t2Valid = t2 > 0f;

            if (t1Valid && t2Valid) return Mathf.Min(t1, t2);
            if (t1Valid) return t1;
            if (t2Valid) return t2;

            return 0f;
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
