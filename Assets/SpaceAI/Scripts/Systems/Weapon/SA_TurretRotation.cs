using SpaceAI.Ship;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI.WeaponSystem
{
    public class SA_TurretRotation : MonoBehaviour
    {
        [Tooltip("Should turret rotate in the FixedUpdate rather than Update?")]
        public bool runRotationsInFixed = false;

        [Header("Objects")]
        [Tooltip("Transform used to provide the horizontal rotation of the turret.")]
        public Transform turretBase;
        [Tooltip("Transform used to provide the vertical rotation of the barrels. Must be a child of the TurretBase.")]
        public Transform turretBarrels;

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
        public Vector3 aimPoint;

        private bool aiming = false;
        private SA_WeaponController weaponController;

        public Transform target { get; set; }
        public bool Idle { get { return !aiming; } }
        public bool AtRest { get; private set; } = false;

        private void Start()
        {
            if (aiming == false) aimPoint = transform.TransformPoint(Vector3.forward * 100.0f);
        }

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
            if (independent && GetComponentInParent(typeof(IShip)))
            {
                if (weaponController == null) weaponController = GetComponentInParent<SA_BaseShip>().WeaponController;

                if (target && target.GetComponent(typeof(IShip)))
                {
                    SetAimpointFromShip(aimPoint);
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

        /// <summary>
        /// Give the turret a position to aim at. If not idle, it will rotate to aim at this point.
        /// </summary>
        public void SetAimpoint(Vector3 position)
        {
            aiming = true;
            aimPoint = position + Vector3.forward * 10;
        }

        public void SetAimpointFromShip(Vector3 position)
        {
            aiming = true;
            aimPoint = position;

            int c = 20;
            Vector3 dir = (aimPoint - transform.position).normalized;
            float dot = Mathf.Round(Vector3.Dot(dir, turretBarrels.transform.forward * turnRate * c));
            float attackDirection = Mathf.Round(turnRate * c) - 0.5F;

            if (dot > attackDirection)
            {
                weaponController.LaunchWeapon(shellOut);
            }
        }

        /// <summary>
        /// When idle, turret returns to resting position, will not track an aimpoint, and rotations stop updating.
        /// </summary>
        public void SetIdle(bool idle)
        {
            aiming = !idle;

            if (aiming)
                AtRest = false;
        }

        /// <summary>
        /// Attempts to automatically assign the turretBase and turretBarrels transforms. Will search for a transform
        /// named "Base" for turretBase and a transform named "Barrels" for the turretBarrels.
        /// </summary>
        public void AutoPopulateBaseAndBarrels()
        {
            // Don't allow this while ingame.
            if (!Application.isPlaying)
            {
                turretBase = transform.Find("Base");
                if (turretBase != null)
                    turretBarrels = turretBase.Find("Barrels");
            }
            else
            {
                Debug.LogWarning(name + ": Turret cannot auto-populate transforms while game is playing.");
            }
        }

        /// <summary>
        /// Sets the turretBase and turretBarrels transforms to null.
        /// </summary>
        public void ClearTransforms()
        {
            // Don't allow this while ingame.
            if (!Application.isPlaying)
            {
                turretBase = null;
                turretBarrels = null;
            }
            else
            {
                Debug.LogWarning(name + ": Turret cannot clear transforms while game is playing.");
            }
        }

        private void RotateTurret()
        {
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
            if (turretBase != null)
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
                Quaternion newRotation = Quaternion.RotateTowards(turretBase.localRotation, rotationGoal, turnRate * Time.deltaTime);

                // Set the new rotation of the base.
                turretBase.localRotation = newRotation;
            }
        }

        private void RotateBarrels()
        {
            // TODO: A target position directly to the turret's right will cause the turret
            // to attempt to aim straight up. This looks silly and on slow moving turrets can
            // cause delays on targeting. This is why barrels have a boosted rotation speed.
            if (turretBase != null && turretBarrels != null)
            {
                // Note, the local conversion has to come from the parent.
                Vector3 localTargetPos = turretBase.InverseTransformPoint(aimPoint);
                localTargetPos.x = 0.0f;

                // Clamp target rotation by creating a limited rotation to the target.
                // Use different clamps depending if the target is above or below the turret.
                Vector3 clampedLocalVec2Target = localTargetPos;
                if (localTargetPos.y >= 0.0f)
                    clampedLocalVec2Target = Vector3.RotateTowards(Vector3.forward, localTargetPos, Mathf.Deg2Rad * elevation, float.MaxValue);
                else
                    clampedLocalVec2Target = Vector3.RotateTowards(Vector3.forward, localTargetPos, Mathf.Deg2Rad * depression, float.MaxValue);

                // Create new rotation towards the target in local space.
                Quaternion rotationGoal = Quaternion.LookRotation(clampedLocalVec2Target);
                Quaternion newRotation = Quaternion.RotateTowards(turretBarrels.localRotation, rotationGoal, 2.0f * turnRate * Time.deltaTime);

                // Set the new rotation of the barrels.
                turretBarrels.localRotation = newRotation;
            }
        }

        /// <summary>
        /// Rotates the turret to resting position.
        /// </summary>
        /// <returns>True when turret has finished rotating to resting positing.</returns>
        private bool RotateToIdle()
        {
            bool baseFinished = false;
            bool barrelsFinished = false;

            if (turretBase != null)
            {
                Quaternion newRotation = Quaternion.RotateTowards(turretBase.localRotation, Quaternion.identity, turnRate * Time.deltaTime);
                turretBase.localRotation = newRotation;

                if (turretBase.localRotation == Quaternion.identity)
                    baseFinished = true;
            }

            if (turretBarrels != null)
            {
                Quaternion newRotation = Quaternion.RotateTowards(turretBarrels.localRotation, Quaternion.identity, 2.0f * turnRate * Time.deltaTime);
                turretBarrels.localRotation = newRotation;

                if (turretBarrels.localRotation == Quaternion.identity)
                    barrelsFinished = true;
            }

            return (baseFinished && barrelsFinished);
        }

        private void DrawDebugRays()
        {
            if (turretBarrels != null)
                Debug.DrawRay(turretBarrels.position, turretBarrels.forward * 100.0f, Color.red);
            else if (turretBase != null)
                Debug.DrawRay(turretBase.position, turretBase.forward * 100.0f, Color.red);
        }
    }
}