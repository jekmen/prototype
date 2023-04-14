using SpaceAI.Ship;
using System;
using UnityEngine;

namespace SpaceAI.ShipSystems
{
    [Serializable]
    public abstract class SA_ShipSystem : IShipSystem
    {
        protected IShip ship;

        public SA_ShipSystem(IShip ship)
        {
            this.ship = ship;
        }

        public SA_ShipSystem() { }

        ~SA_ShipSystem() { }

        public abstract IShipSystem Init(IShip ship, GameObject gameObject);

        public virtual void ShipSystemEvent(Collision obj)
        {
            
        }
    }
}