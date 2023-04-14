using SpaceAI.Core;
using SpaceAI.DataManagment;
using SpaceAI.Events;
using SpaceAI.FSM;
using SpaceAI.ScaneTools;
using SpaceAI.ShipSystems;
using SpaceAI.WeaponSystem;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace SpaceAI.Ship
{
    [RequireComponent(typeof(Rigidbody))]
    public abstract class SA_BaseShip : MonoBehaviour, IShip, IDamage
    {
        [HideInInspector] public string currFile;
        [HideInInspector] public string configFileName;

        protected SA_AIProvider aIProvider;
        protected SA_ObstacleSystem obstacleSystem;
        protected Vector3 velocityTarget = Vector3.zero;
        protected bool followTarget;
        protected Rigidbody rb;

        protected Vector3 SetVelocityTarget(Vector3 vector) { velocityTarget = vector; return velocityTarget; }

        private ShipSystemFactory shipSystemFactory;
        private SA_ShipConfigurationManager shipConfiguration;
        private SA_WeaponController weaponController;
        private SA_HealthProvider healthProvider;
        private Vector3 positionTarget;
        private float shipSize;
        private Mesh meshObj;
        private SA_Shield shield = null;
        private ParticleSystem onFire;

        private readonly HashSet<IShipSystem> shipSystems = new();

        public SA_ShipConfigurationManager ShipConfiguration => shipConfiguration;

        public SA_WeaponController WeaponControll => weaponController;

        public SA_AIProvider CurrentAIProvider => aIProvider;

        public GameObject CurrentEnemy { get; set; }

        public Vector3 GetCurrentTargetPosition => positionTarget;

        public float CurrentShipSize => shipSize;

        public Transform CurrentShipTransform => transform;

        public event Action<Collision> CollisionEvent;

        protected virtual void Start()
        {            
            InitShipComponents();
        }

        private void Update()
        {
            if (shield != null && !ShipConfiguration.ShieldsConfiguration.EnableShields)
            {
                shield = null;//TODO: fix it in next update
            }

            if (shield != null) shield.UpdateFade();            
        }

        protected virtual void OnDisable()
        {
            foreach (var system in shipSystems)
            {
                if (system != null)
                {
                    CollisionEvent -= system.ShipSystemEvent;
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            CollisionEvent?.Invoke(collision);
        }

        private void InitShipComponents()
        {
            LoadShipDataUnityOnly();

            GetComponents();
            
            InitBuildInSystems();

            EventsBus.Publish(new ShipRegistryEvent(this));
        }

        private void GetComponents()
        {
            rb = GetComponent<Rigidbody>();
            meshObj = GetComponent<MeshFilter>().mesh;
            rb.mass = shipConfiguration.MainConfig.ShipMass;
            shipSize = meshObj.bounds.size.x + meshObj.bounds.size.y + meshObj.bounds.size.z;

            if (shipConfiguration.Items.OnFireParticle)
            {
                GameObject obj = Instantiate(shipConfiguration.Items.OnFireParticle.gameObject, transform);
                onFire = obj.GetComponent<ParticleSystem>();
                onFire.transform.localPosition = Vector3.zero;
                onFire.Stop();
            }
        }

        private void LoadShipDataUnityOnly()
        {
            shipConfiguration = SA_FileManager.LoadXmlConfigUnityOnly<SA_ShipConfigurationManager>(configFileName);
            shipConfiguration.Items = Resources.Load<SA_ItemsStaf>(shipConfiguration.ItemDataPath);
        }

        private void InitBuildInSystems()
        {
            shipSystemFactory = new ShipSystemFactory(shipConfiguration.ShipSystems.CreateAll());
            obstacleSystem = shipSystemFactory.CreateSystem<SA_ObstacleSystem>(this);
            weaponController = shipSystemFactory.CreateSystem<SA_WeaponController>(this);
            healthProvider = new SA_HealthProvider(this, onFire, shipSystemFactory);

            shipSystems.Add(obstacleSystem);
            shipSystems.Add(weaponController);

            if (healthProvider.Shield != null)
            {
                shield = healthProvider.Shield;
                shipSystems.Add(healthProvider.Shield);
            }
        }

        public void Validate()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.drag = 0.8F;
            rb.angularDrag = 0.5F;
        }

        protected abstract void Move();

        private void OnDeathCome()
        {
            EventsBus.Publish(new ShipUnRegisterEvent(this));
        }

        #region Interface
        public void SubscribeEvent(Action<Collision> collisionEvent)
        {
            CollisionEvent += collisionEvent;
        }

        public void SetTarget(Vector3 target)
        {
            positionTarget = target;
        }

        public void SetTarget(Transform target)
        {
            positionTarget = target.position;
        }

        public void SetTarget(GameObject target)
        {
            positionTarget = target.transform.position;
        }

        public GroupType Ship()
        {
            return shipConfiguration.AIConfig.GroupType;
        }

        public bool WayIsFree()
        {
            switch (obstacleSystem.M_ObstacleState)
            {
                case SA_ObstacleSystem.ObstacleState.Scan: return true;
                case SA_ObstacleSystem.ObstacleState.GoToEscapeDirection: return false;
                default: return false;
            }
        }

        public bool CanFollowTarget(bool followTarget)
        {
            return this.followTarget = followTarget;
        }

        public virtual bool ToFar()
        {
            return false;
        }

        public virtual void SetCurrentEnemy(GameObject newTarget)
        {

        }

        public void ApplyDamage(float damage, GameObject killer)
        {
            healthProvider.ApplyDamage(damage, killer, OnDeathCome);
        }

        #endregion
    }
}