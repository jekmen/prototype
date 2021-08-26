using System;
using System.Xml.Serialization;
using UnityEngine;

namespace SpaceAI.DataManagment
{    
    [XmlRoot]
    [Serializable]
    public class SA_MainConfigs
    {
        [Header("Movment Settings")]
        public float Speed;
        [HideInInspector] public float SpeedMax;
        [HideInInspector] public float SpeedMin;
        public float RotationSpeed;
        public float PichSens;
        public float YawSens;
        public float ShipMass;
        [Header("Damage Settings")]
        public float HP;
        public float DurableForce;
        public float CollisionDamage;
        [Header("Shield Settings")]
        public bool EnableShields;
        [Header("Fly Settings")]
        public Vector3 patrolPoint;
        public float flyDistance;


        public SA_MainConfigs() { }

        public SA_MainConfigs(float Speed, float RotationSpeed, float PichSens, float YawSens, float ShipMass, float HP, float DurableForce, float CollisionDamage, float flyDistance, bool EnableShields = false)
        {
            this.Speed = Speed;
            this.RotationSpeed = RotationSpeed;
            this.PichSens = PichSens;
            this.YawSens = YawSens;
            this.ShipMass = ShipMass;
            this.HP = HP;
            this.DurableForce = DurableForce;
            this.CollisionDamage = CollisionDamage;
            this.EnableShields = EnableShields;
            this.flyDistance = flyDistance;
        }

        public Quaternion MainRot { get; set; }
        public float HPmax { set; get; }
        public float MoveSpeed { get; set; }
    }
}