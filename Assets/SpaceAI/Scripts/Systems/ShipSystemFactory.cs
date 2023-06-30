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
        private readonly IDictionary<string, SA_IShipSystem> systemTypes;
        private readonly HashSet<SA_IShipSystem> systemInstances;

        public HashSet<SA_IShipSystem> ShipSystemInstances => systemInstances;

        public ShipSystemFactory(IDictionary<string, SA_IShipSystem> systemTypes)
        {
            this.systemTypes = systemTypes;
            systemInstances = new HashSet<SA_IShipSystem>();
        }

        public T CreateSystem<T>(SA_IShip ship, GameObject asset = null) where T : SA_IShipSystem
        {
            var systemType = typeof(T);

            if (systemTypes.TryGetValue(systemType.FullName, out SA_IShipSystem systemInstance) && systemInstance is T instanceOfTypeT)
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