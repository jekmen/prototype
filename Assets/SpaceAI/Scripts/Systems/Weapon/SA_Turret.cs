namespace SpaceAI.WeaponSystem
{
    using SpaceAI.Ship;
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
        public Transform[] shellOut;
        public Vector3 aimPoint = new Vector3(0, 0, 100);

        private bool aiming = false;
        private SA_WeaponController weaponController;

        public bool Idle { get { return !aiming; } }
        public bool AtRest { get; private set; } = false;

        private void Update()
        {
            if (!runRotationsInFixed)
            {
                IndependentShipTurret();
                RotateTurret();
            }

            if (showDebugRay) DrawDebugRays();
        }

        private void IndependentShipTurret()
        {
            if (!isActiveAndEnabled) return;

            SA_IShip ship = GetComponentInParent<SA_IShip>();

            if (independent && ship != null)
            {
                if (weaponController == null) weaponController = ship.WeaponControll;

                if (Target)
                {
                    SetAimpointFromShip(Target.transform);
                }
            }
        }

        private void FixedUpdate()
        {
            if (runRotationsInFixed)
            {
                IndependentShipTurret();
                RotateTurret();
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
            // TODO: Turret needs to rotate the long way around if the aimpoint gets behind
            // it and traversal limits prevent it from taking the shortest rotation.
            foreach (var turrBase in turretBase)
            {
                // Note, the local conversion has to come from the parent.
                Vector3 localTargetPos = transform.InverseTransformPoint(aimPoint);
                localTargetPos.y = 0.0f;

                // Clamp target rotation by creating a limited rotation to the target.
                // Use different clamps depending if the target is to the left or right of the turret.
                Vector3 clampedLocalVec2Target = localTargetPos;
                if (limitTraverse)
                {
                    if (localTargetPos.x >= 0.0f)
                        clampedLocalVec2Target = Vector3.RotateTowards(Vector3.forward, localTargetPos, Mathf.Deg2Rad * rightTraverse, float.MaxValue);
                    else
                        clampedLocalVec2Target = Vector3.RotateTowards(Vector3.forward, localTargetPos, Mathf.Deg2Rad * leftTraverse, float.MaxValue);
                }

                // Create new rotation towards the target in local space.
                Quaternion rotationGoal = Quaternion.LookRotation(clampedLocalVec2Target);
                Quaternion newRotation = Quaternion.RotateTowards(turrBase.localRotation, rotationGoal, turnRate * Time.deltaTime);

                // Set the new rotation of the base.
                turrBase.localRotation = newRotation;
            }
        }

        private void RotateBarrels()
        {
            // TODO: A target position directly to the turret's right will cause the turret
            // to attempt to aim straight up. This looks silly and on slow moving turrets can
            // cause delays on targeting. This is why barrels have a boosted rotation speed.
            Vector3 localTargetPos = Vector3.zero;

            foreach (var turrBase in turretBase)
            {
                localTargetPos = turrBase.InverseTransformPoint(aimPoint);
            }

            // Note, the local conversion has to come from the parent.
            localTargetPos.x = 0.0f;

            // Clamp target rotation by creating a limited rotation to the target.
            // Use different clamps depending if the target is above or below the turret.
            Vector3 clampedLocalVec2Target = localTargetPos;

            if (localTargetPos.y >= 0.0f)
                clampedLocalVec2Target = Vector3.RotateTowards(Vector3.forward, localTargetPos, elevation, float.MaxValue);
            else
                clampedLocalVec2Target = Vector3.RotateTowards(Vector3.forward, localTargetPos, Mathf.Deg2Rad * depression, float.MaxValue);

            // Create new rotation towards the target in local space.
            Quaternion rotationGoal = Quaternion.LookRotation(clampedLocalVec2Target);

            foreach (var turretBarr in turretBarrels)
            {
                Quaternion newRotation = Quaternion.RotateTowards(turretBarr.localRotation, rotationGoal, 2.0f * turnRate * Time.deltaTime);

                // Set the new rotation of the barrels.
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

        #region EDITOR
#if UNITY_EDITOR
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