namespace SpaceAI.Events
{
    using SpaceAI.Ship;
    using SpaceAI.WeaponSystem;
    using UnityEngine;

    public struct SA_ShipTargetRequesEvent : SA_IEvent
    {
        public SA_IShip Ship;
        public int ScanRange;

        public SA_ShipTargetRequesEvent(SA_IShip owner, int scanRange)
        {
            Ship = owner;
            ScanRange = scanRange;
        }
    }

    public struct SA_TurretTargetRequestEvent : SA_IEvent
    {
        public SA_WeaponBase Owner;
        public float Range;
        public GroupType[] RequestedTargets;
        public Transform TurretPosition;

        public SA_TurretTargetRequestEvent(SA_WeaponBase owner, Transform turretPosition, GroupType[] requestedTargets, float range)
        {
            Owner = owner;
            Range = range;
            RequestedTargets = requestedTargets;
            TurretPosition = turretPosition;
        }
    }
}