using SpaceAI.DataManagment;
using SpaceAI.FSM;
using SpaceAI.WeaponSystem;
using System;
using UnityEngine;

namespace SpaceAI.Ship
{
    /// <summary>
    /// Target identifier
    /// </summary>
    public enum GroupType
    {
        Enemy,
        Player
        // Add more here
    }

    public interface IShip
    {
        GroupType Ship();

        SA_ShipConfigurationManager ShipConfiguration { get; }

        SA_WeaponController WeaponControll { get; }

        SA_AIProvider CurrentAIProvider { get; }

        GameObject CurrentEnemy { get; set; }

        Mesh CurrentMesh { get;}

        Transform CurrentShipTransform { get; }

        Vector3 GetCurrentTargetPosition { get; }

        float CurrentShipSize { get; }

        bool WayIsFree();

        void SubscribeEvent(Action<Collision> collisionEvent);

        void SetTarget(Vector3 target);

        void SetTarget(Transform target);

        void SetTarget(GameObject target);

        bool CanFollowTarget(bool followTarget);

        bool ToFar();

        void SetCurrentEnemy(GameObject newTarget);
    }
}