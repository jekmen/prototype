using SpaceAI.Ship;
using System;
using UnityEngine;

namespace SpaceAI.ShipSystems
{
    [Serializable]
    public abstract class SA_ShipSystem : SA_IShipSystem
    {
        protected SA_IShip ship;

        public SA_ShipSystem(SA_IShip ship)
        {
            this.ship = ship;
        }

        public SA_ShipSystem() { }

        ~SA_ShipSystem() { }

        public abstract SA_IShipSystem Init(SA_IShip ship, GameObject gameObject);

        public virtual void ShipSystemEvent(Collision obj)
        {
            
        }
    }
}