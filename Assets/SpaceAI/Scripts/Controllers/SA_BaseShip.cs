namespace SpaceAI.Ship
{
    using SpaceAI.Core;
    using SpaceAI.DataManagment;
    using SpaceAI.Events;
    using SpaceAI.FSM;
    using SpaceAI.ShipSystems;
    using SpaceAI.WeaponSystem;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    [RequireComponent(typeof(Rigidbody))]
    public abstract class SA_BaseShip : MonoBehaviour, SA_IShip, SA_IDamage
    {
        [HideInInspector] public string currFile;
        [HideInInspector] public string configFileName;

        protected SA_AIProvider aIProvider;
        protected Vector3 velocityTarget = Vector3.zero;
        protected bool followTarget;
        protected Rigidbody rb;

        protected Vector3 SetVelocityTarget(Vector3 vector) { velocityTarget = vector; return velocityTarget; }

        protected SA_ObstacleSystem obstacleSystem;
        private ShipSystemFactory shipSystemFactory;
        private SA_ShipConfigurationManager shipConfiguration;
        private SA_WeaponController weaponController;
        private SA_HealthProvider healthProvider;
        private Vector3 positionTarget;
        private float shipSize;
        private Mesh meshObj;
        private SA_Shield shield = null;
        private ParticleSystem onFire;

        private SA_ShipRegistryEvent shipRegistryEvent;
        private SA_ShipSystemsInitedEvent shipSystemsInitedEvent;

        private readonly HashSet<SA_IShipSystem> shipSystems = new();

        public SA_ShipConfigurationManager ShipConfiguration => shipConfiguration;

        public SA_WeaponController WeaponControll => weaponController;

        public SA_AIProvider CurrentAIProvider => aIProvider;

        public GameObject CurrentEnemy { get; set; }

        public Vector3 GetCurrentTargetPosition => positionTarget;

        public float CurrentShipSize => shipSize;

        public Transform CurrentShipTransform => transform;

        public Mesh CurrentMesh => meshObj;

        public event Action<Collision> CollisionEvent;

        protected virtual IEnumerator Start()
        {
            yield return StartCoroutine(InitShipComponents());

            while (true)
            {
                yield return StartCoroutine(RunBuildInSystems());
            }
        }

        protected virtual void OnEnable()
        {
            SA_EventsBus.AddEventListener<SA_ShipSystemsInitedEvent>(OnSystemsReady);
        }

        protected virtual void OnDisable()
        {
            SA_EventsBus.RemoveEventListener<SA_ShipSystemsInitedEvent>(OnSystemsReady);

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

        private IEnumerator RunBuildInSystems()
        {
            yield return new WaitForEndOfFrame();

           // obstacleSystem.OvoideObstacles();

            if (shield != null && !ShipConfiguration.ShieldsConfiguration.EnableShields)
            {
                shield = null;//TODO: fix it in next update
            }

            if (shield != null) shield.UpdateFade();
        }

        private IEnumerator InitShipComponents()
        {
            shipRegistryEvent = new SA_ShipRegistryEvent(this);

            yield return null;

#if UNITY_EDITOR
            LoadShipDataUnityOnly();
#else
            yield return StartCoroutine(LoadShipData());
#endif
            GetComponents();

            InitBuildInSystems();

            SA_EventsBus.Publish(shipRegistryEvent);

            SA_EventsBus.Publish(shipSystemsInitedEvent);
        }

        private void GetComponents()
        {
            var mfilter = GetComponent<MeshFilter>();

            if (!mfilter)
            {
                meshObj = GetComponentInChildren<MeshFilter>().mesh;
                
            }
            else
            {
                meshObj = GetComponent<MeshFilter>().mesh;
            }

            rb = GetComponent<Rigidbody>();
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

        /// <summary>
        /// Method for builds
        /// </summary>
        /// <returns></returns>
        private IEnumerator LoadShipData()
        {
            yield return StartCoroutine(SA_FileManager.CopyShipTemplateFile(configFileName));
            shipConfiguration = SA_FileManager.LoadXml<SA_ShipConfigurationManager>(configFileName);
            shipConfiguration.Items = Resources.Load<SA_ItemsStaf>(shipConfiguration.ItemDataPath);
        }

        /// <summary>
        /// Editor only method
        /// </summary>
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

#if UNITY_EDITOR
        public void Validate()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.drag = 1.3F;
            rb.angularDrag = 0.5F;
        }
#endif

        protected abstract void Move();

        protected abstract void OnSystemsReady(SA_ShipSystemsInitedEvent e);

        private void OnDeathCome()
        {
            SA_EventsBus.Publish(new SA_ShipUnRegisterEvent(this));
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
            return obstacleSystem.M_ObstacleState switch
            {
                SA_ObstacleSystem.ObstacleState.Scan => true,
                SA_ObstacleSystem.ObstacleState.GoToEscapeDirection => false,
                _ => false,
            };
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