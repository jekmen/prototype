using SpaceAI.Core;
using SpaceAI.DataManagment;
using SpaceAI.ScaneTools;
using SpaceAI.ShipSystems;
using SpaceAI.WeaponSystem;
using UnityEngine;

namespace SpaceAI.Ship
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(AudioSource))]
    public abstract class SA_BaseShip : MonoBehaviour, IShip, IDamage
    {
        public string currFile;
        public string configFileName;
        private Vector3 PositionTarget { get; set; }
        public Rigidbody Rb { get; set; }
        public AudioSource Audio { get; set; }
        public SA_WeaponController WeaponController { get; private set; }
        public SA_ShipConfigurationManager Configuration { get; set; }
        public SA_ObstacleSystem ObstacleSystem { get; set; }
        public bool FollowTarget { get; set; }
        public Mesh meshObj { get; private set; }

        public float ShipSize { get; private set; }

        private SA_Shield shield;
        private ParticleSystem onFire;

        public delegate void CollisionObserver(Collision collision);
        public event CollisionObserver CollisionEvent;

        protected virtual void Start()
        {
            InitShipComponents();
        }

        private void InitShipComponents()
        {
            Configuration = new SA_ShipConfigurationManager();
            Configuration = SA_FileManager.LoadXmlConfig<SA_ShipConfigurationManager>(configFileName);
            Configuration.Items = Resources.Load<SA_ItemsStaf>(Configuration.ItemDataPath);
            meshObj = GetComponent<MeshFilter>().mesh;

            ShipSize = meshObj.bounds.size.x + meshObj.bounds.size.y + meshObj.bounds.size.z;
            Rb = GetComponent<Rigidbody>();
            Audio = GetComponent<AudioSource>();
            Rb.mass = Configuration.MainConfig.ShipMass;

            ObstacleSystem = new SA_ObstacleSystem(this, true);//Build in component
            WeaponController = new SA_WeaponController(this);// Build in component

            SubscribeEvent(ShipHit);

            if (SA_Manager.instance != null)
            {
                SA_Manager.instance.Subscribe(gameObject);
            }

            if (Configuration.Items.OnFireParticle)
            {
                GameObject obj = Instantiate(Configuration.Items.OnFireParticle.gameObject, transform);
                onFire = obj.GetComponent<ParticleSystem>();
                onFire.transform.localPosition = Vector3.zero;
                onFire.Stop();
            }

            if (Configuration.MainConfig.EnableShields)
            {
                if (Configuration.Items.ShipSystems == null) return;

                foreach (var item in Configuration.Items.ShipSystems)
                {
                    if (item.GetType() == typeof(SA_Shield))
                    {
                        shield = new SA_Shield(this, true);
                    }
                }
            }
        }

        public void SubscribeEvent(CollisionObserver collisionObserver)
        {
            CollisionEvent += collisionObserver;
        }

        public virtual void Dead()
        {
            if (Configuration.Items.ExplousionEffect)
            {
                GameObject obj = Instantiate(Configuration.Items.ExplousionEffect, transform.position, transform.rotation);

                if (obj.GetComponent<Rigidbody>())
                {
                    obj.GetComponent<Rigidbody>().velocity = Rb.velocity;
                    obj.GetComponent<Rigidbody>().AddTorque(UnityEngine.Random.rotation.eulerAngles * UnityEngine.Random.Range(100, 2000));
                }
            }

            SA_Manager.instance.UnSubscribe(gameObject);
        }

        /// <summary>
        /// Reset HitPoints
        /// </summary>
        public virtual void ResetHP()
        {
            Configuration.MainConfig.HP = Configuration.MainConfig.HPmax;
        }

        private void ShipHit(Collision collision)
        {
            if (collision.gameObject.GetComponentInParent(typeof(IDamageSendler))) return;

            if (Configuration.Items.HitSounds != null && Configuration.Items.HitSounds.Length > 0)
                AudioSource.PlayClipAtPoint(Configuration.Items.HitSounds[UnityEngine.Random.Range(0, Configuration.Items.HitSounds.Length)], transform.position);

            if (!gameObject.GetComponent<Collider>().isTrigger)
            {
                if (collision.relativeVelocity.magnitude > Configuration.MainConfig.DurableForce)
                {
                    Configuration.MainConfig.HP = Mathf.Clamp(Configuration.MainConfig.HP - Configuration.MainConfig.CollisionDamage, 0f, Configuration.MainConfig.HPmax);
                }
            }

            bool isDethCome = Configuration.MainConfig.HP - Configuration.MainConfig.CollisionDamage <= 0;

            if (isDethCome)
                Dead();
        }

        public void Validate()
        {
            Rb = GetComponent<Rigidbody>();
            Rb.useGravity = false;
            Rb.drag = 0.8F;
            Rb.angularDrag = 0.5F;

            Audio = GetComponent<AudioSource>();
            Audio.playOnAwake = true;
            Audio.loop = true;
            Audio.spatialBlend = 1;
            Audio.minDistance = 25;
        }

        protected abstract void Move();

        public void ApplyDamage(float damage, GameObject killer)
        {
            if (Configuration.MainConfig.HP < 0)
                return;

            if (Configuration.Items.HitSounds != null && Configuration.Items.HitSounds.Length > 0)
            {
                AudioSource.PlayClipAtPoint(Configuration.Items.HitSounds[UnityEngine.Random.Range(0, Configuration.Items.HitSounds.Length)], transform.position);
            }

            if (Configuration.MainConfig.EnableShields)
            {
                if (shield)
                {
                    if (shield.shieldPower > 0) shield.shieldPower -= damage;

                    if (shield.shieldPower <= 0)
                    {
                        Configuration.MainConfig.HP -= damage;
                    }
                }
            }
            else
            {
                Configuration.MainConfig.HP -= damage;
            }

            if (onFire)
            {
                if (Configuration.MainConfig.HP < (int)(Configuration.MainConfig.HPmax / 2.0f))
                {
                    onFire.Play();
                }
            }
            if (Configuration.MainConfig.HP <= 0)
            {
                Dead();
            }
        }

        /// <summary>
        /// Make damage by contact force
        /// </summary>
        /// <param name="collision"></param>
        private void OnCollisionEnter(Collision collision)
        {
            CollisionEvent?.Invoke(collision);
        }

        public bool IsDead()
        {
            if (Configuration.MainConfig.HP <= 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void SetTarget(Vector3 target)
        {
            PositionTarget = target;
        }

        public void SetTarget(Transform target)
        {
            PositionTarget = target.position;
        }

        public void SetTarget(GameObject target)
        {
            PositionTarget = target.transform.position;
        }

        public Vector3 GetTarget()
        {
            return PositionTarget;
        }

        public GroupType Ship()
        {
            return Configuration.AIConfig.groupType;
        }

        protected virtual void OnDisable()
        {
            CollisionEvent -= ShipHit;

            if (!shield) return;

            CollisionEvent -= shield.CollisionEvent;
        }
    }
}