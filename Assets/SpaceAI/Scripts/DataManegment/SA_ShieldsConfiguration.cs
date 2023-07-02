namespace SpaceAI.DataManagment
{
    using System;
    using System.Xml.Serialization;
    using UnityEngine;

    [Serializable]
    [XmlRoot]
    public class SA_ShieldsConfiguration
    {
        public bool EnableShields;
        [Header("Collision events:")]
        public bool CollisionEnter;
        [Header("Shield settings:")]
        public float DecaySpeed = 2.0f;
        public float ReactSpeed = 0.1f;
        public bool FixNonUniformScale;
        public float ShieldPower;

        public SA_ShieldsConfiguration() { }

        public SA_ShieldsConfiguration(bool enableShields, bool collisionEnter, float decaySpeed, float reactSpeed, bool fixNonUniformScale, float shieldPower)
        {
            EnableShields = enableShields;
            CollisionEnter = collisionEnter;
            DecaySpeed = decaySpeed;
            ReactSpeed = reactSpeed;
            FixNonUniformScale = fixNonUniformScale;
            ShieldPower = shieldPower;
        }
    }
}