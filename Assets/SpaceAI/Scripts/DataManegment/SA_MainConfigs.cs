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
        public float MoveSpeedIncrease;
        public float RotationSpeed;
        public float ShipMass;
        [Header("Damage Settings")]
        public float HP;
        public float Prediction;
        public float DurableForce;
        public float CollisionDamage;                
        [Header("Fly Settings")]
        public Vector3 patrolPoint;
        public float flyDistance;

        public SA_MainConfigs() { }

        public SA_MainConfigs(float speed, float moveSpeedIncrease, float rotationSpeed, float shipMass, float hP, float prediction, float durableForce, float collisionDamage, float flyDistance)
        {
            Speed = speed;
            MoveSpeedIncrease = moveSpeedIncrease;
            RotationSpeed = rotationSpeed;
            ShipMass = shipMass;
            HP = hP;
            Prediction = prediction;
            DurableForce = durableForce;
            CollisionDamage = collisionDamage;
            this.flyDistance = flyDistance;
        }

        public Quaternion MainRot { get; set; }
        public float HPmax { set; get; }
        public float MoveSpeed { get; set; }
    }
}