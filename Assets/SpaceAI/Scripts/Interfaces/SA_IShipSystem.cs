using SpaceAI.Ship;
using UnityEngine;

namespace SpaceAI.ShipSystems
{
    public interface SA_IShipSystem
    {
        SA_IShipSystem Init(SA_IShip ship, GameObject gameObject);

        void ShipSystemEvent(Collision collision);
    }
}
