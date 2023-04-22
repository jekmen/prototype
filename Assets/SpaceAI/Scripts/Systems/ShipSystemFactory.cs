using SpaceAI.Ship;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaceAI.ShipSystems
{
    [Serializable]
    public class ShipSystemFactory
    {
        private readonly IDictionary<string, IShipSystem> systemTypes;
        private readonly HashSet<IShipSystem> systemInstances;

        public HashSet<IShipSystem> ShipSystemInstances => systemInstances;

        public ShipSystemFactory(IDictionary<string, IShipSystem> systemTypes)
        {
            this.systemTypes = systemTypes;
            systemInstances = new HashSet<IShipSystem>();
        }

        public T CreateSystem<T>(IShip ship, GameObject asset = null) where T : IShipSystem
        {
            var systemType = typeof(T);

            if (systemTypes.TryGetValue(systemType.FullName, out IShipSystem systemInstance) && systemInstance is T instanceOfTypeT)
            {
                var i = (T)instanceOfTypeT.Init(ship, asset);

                systemInstances.Add(systemInstance);

                return i;
            }

            throw new ArgumentException($"System type {systemType.FullName} is not registered or is not of type {typeof(T).Name}.");
        }

        public void RemoveSystem(Type systemType)
        {
            var instancesToRemove = systemInstances.Where(s => s.GetType() == systemType).ToList();
            foreach (var instance in instancesToRemove)
            {
                systemInstances.Remove(instance);
            }
        }
    }
}