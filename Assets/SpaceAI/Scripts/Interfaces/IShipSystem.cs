using SpaceAI.Ship;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI.ShipSystems
{
    public interface IShipSystem
    {
        IShipSystem Init(IShip ship, GameObject gameObject);

        void ShipSystemEvent(Collision collision);
    }
}
