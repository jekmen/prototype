using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

namespace SpaceAI.DataManagment
{
    [Serializable]
    [XmlRoot]
    public class SA_ShipConfigurationManager : ScriptableObject
    {
        public SA_MainConfigs MainConfig;
        public SA_AIConfifuration AIConfig;
        public SA_ShieldsConfiguration ShieldsConfiguration;
        public SA_Options Options;
        public SA_ShipSystems ShipSystems;
        [XmlIgnore] public SA_ItemsStaf Items;

        public string ItemDataPath;

        public void Save(string fileName)
        {
            if (Items)
            {
                ItemDataPath = "Items/" + Items.name;
            }
            else
            {
                Debug.LogError("Add Items");
                ItemDataPath = null;
                return;
            }

            MainConfig.HPmax = MainConfig.HP;
            MainConfig.SpeedMax = MainConfig.Speed;
            MainConfig.SpeedMin = -10;

            SA_FileManager.SaveXmlConfigUnityOnly(this, fileName);
        }

        public SA_ShipConfigurationManager Load(string fileName)
        {
            return SA_FileManager.LoadXmlConfigUnityOnly<SA_ShipConfigurationManager>(fileName);
        }

        public void SetAsDefault()
        {
            MainConfig = new SA_MainConfigs
            {
                Speed = 187,
                RotationSpeed = 1.2F,
                ShipMass = 3987,
                HP = 200,
                Prediction = 800,
                DurableForce = 17,
                CollisionDamage = 3,
                flyDistance = 1200
            };
        }
    }
}
