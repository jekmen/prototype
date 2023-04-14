using SpaceAI.Ship;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI.WeaponSystem
{
    public class SA_TurretRotation : SA_WeaponLunchManager
    {
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
            IShip ship = GetComponentInParent<IShip>();

            if (independent && ship != null)
            {
                if (weaponController == null) weaponController = ship.WeaponControll;

                if (Target)
                {
                    SetAimpointFromShip();
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

        public void SetAimpointFromShip()
        {
            int c = 20;
            Vector3 dir = (aimPoint - transform.position).normalized;

            foreach (var item in turretBarrels)
            {
                float dot = Mathf.Round(Vector3.Dot(dir, item.transform.forward * turnRate * c));
                float attackDirection = Mathf.Round(turnRate * c) - 0.5F;

                if (dot > attackDirection)
                {
                    weaponController.LaunchWeapons(shellOut);
                }
            }
        }

        public void SetIdle(bool idle)
        {
            aiming = !idle;

            if (aiming)
                AtRest = false;
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
            foreach (var turrBase in turretBase)
            {
                // Note, the local conversion has to come from the parent.
                Vector3 localTargetPos = turrBase.parent.InverseTransformPoint(aimPoint);
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
            foreach (var turretBarr in turretBarrels)
            {
                // Note, the local conversion has to come from the parent.
                Vector3 localTargetPos = turretBarr.InverseTransformPoint(aimPoint);
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

        #region EDITOR
#if UNITY_EDITOR

        public void AutoPopulateBaseAndBarrels()
        {
            /// Don't allow this while ingame.
            if (!Application.isPlaying)
            {

            }
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