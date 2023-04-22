using SpaceAI.ShipSystems;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEditor;
using UnityEngine;

namespace SpaceAI.DataManagment
{
    [Serializable]
    [XmlRoot]
    public class SA_ShipSystems
    {
#if UNITY_EDITOR
        [XmlIgnore] public List<MonoScript> shipSystems;
#endif
        [HideInInspector] public List<string> shipSystemsScripts;

        public IDictionary<string, IShipSystem> CreateAll()
        {
            IDictionary<string, IShipSystem> valuePairs = new Dictionary<string, IShipSystem>();

            foreach (var item in shipSystemsScripts)
            {
                string scriptName = item;                

                Type scriptType = Type.GetType(scriptName);

                if (scriptType != null)
                {
                    object obj = Activator.CreateInstance(scriptType);

                    if (scriptType.GetInterface(nameof(IShipSystem)) != null)
                    {
                        IShipSystem scriptObject = obj as IShipSystem;
                        // Use scriptObject
                        valuePairs.Add(scriptName, scriptObject);
                    }
                    else
                    {
                        Debug.LogError("Failed to instantiate script: " + scriptName);
                    }
                }
                else
                {
                    Debug.LogError("Script not found: " + scriptName);
                }
            }

            return valuePairs;
        }

#if UNITY_EDITOR
        public void OnValidate()
        {
            if (shipSystems != null)
            {
                shipSystemsScripts = new List<string>();

                foreach (var file in shipSystems)
                {
                    if (file != null)
                    {
                        shipSystemsScripts.Add(file.GetClass().FullName);
                    }
                }
            }
        }
#endif
    }
}