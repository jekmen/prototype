using SpaceAI.Core;
using System.Threading.Tasks;
using UnityEngine;

namespace SpaceAI.WeaponSystem
{
    public class SA_DamageSandler : SA_DamageBase, IDamageSendler
    {
        public bool Explosive;
        public float ExplosionRadius = 20;
        public float ExplosionForce = 1000;
        public float TimeActive = 0;
        public bool RandomTimeActive;
        public float DestryAfterDuration = 10;
        public AudioSource explosion;

        private GameObject storedEffect;
        private Collider[] colliders;
        private float timetemp = 0;

        private void OnEnable()
        {
            if (GetComponent<TrailRenderer>())
            {
                GetComponent<TrailRenderer>().Clear();
                GetComponent<TrailRenderer>().enabled = true;
            }

            if (GetComponent<Collider>())
            {
                GetComponent<Collider>().enabled = true;
            }

            if (!Owner || !Owner.GetComponent<Collider>()) return;

            colliders = Owner.GetComponentsInChildren<Collider>();

            Deactivate(gameObject, DestryAfterDuration);

            timetemp = Time.time;

            if (RandomTimeActive)
                TimeActive = Random.Range(TimeActive / 2f, TimeActive);

            Physics.IgnoreCollision(GetComponent<Collider>(), Owner.GetComponent<Collider>());

            foreach (Collider col in colliders)
            {
                Physics.IgnoreCollision(GetComponent<Collider>(), col);
            }
        }

        private void Update()
        {
            if (TimeActive > 0)
            {
                if (Time.time >= (timetemp + TimeActive))
                {
                    Active();
                }
            }
        }

        public void Active()
        {            
            if (Effect)
            {
                if (!storedEffect)
                {
                    storedEffect = Instantiate(Effect, transform.position, transform.rotation);
                }
                else
                {
                    storedEffect.transform.position = transform.position;
                    storedEffect.transform.rotation = transform.rotation;
                    storedEffect.SetActive(true);
                }

                Deactivate(storedEffect, LifeTimeEffect);
            }

            if (Explosive)
            {
                ExplosionDamage();
            }

            if (explosion && !explosion.isPlaying)
            {
                explosion.Play();
                GetComponent<Renderer>().enabled = false;
                GetComponent<Collider>().enabled = false;
                Deactivate(gameObject, Random.Range(1, explosion.clip.length));
            }
            else
            {
                Deactivate(gameObject);
            }
        }

        public void Deactivate(GameObject gameObject)
        {
            gameObject.SetActive(false);
        }

        public async void Deactivate(GameObject gameObject, float time)
        {
            if (!Application.isPlaying)
                return;

            await Task.Delay((int)time * 1000);

            if (!Application.isPlaying)
                return;
            gameObject.SetActive(false);
        }

        private void ExplosionDamage()
        {
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, ExplosionRadius);

            for (int i = 0; i < hitColliders.Length; i++)
            {
                Collider hit = hitColliders[i];

                if (!hit)
                    continue;

                if (hit.gameObject.GetComponent(typeof(IDamage)) is IDamage damagebleComponent)
                {
                    damagebleComponent.ApplyDamage(Damage, Owner);
                }

                if (hit.GetComponent<Rigidbody>())
                    hit.GetComponent<Rigidbody>().AddExplosionForce(ExplosionForce, transform.position, ExplosionRadius, 3.0f);
            }
        }

        /// <summary>
        /// Aply damage 
        /// </summary>
        /// <param name="collision"></param>
        private void OnCollisionEnter(Collision collision)
        {
            if (Owner && collision.gameObject == Owner.transform.root.gameObject)
            {
                foreach (Collider col in Owner.GetComponentsInChildren<Collider>())
                {
                    Physics.IgnoreCollision(collision.gameObject.GetComponent<Collider>(), col);
                }
            }
            else
            {
                if (collision.gameObject.GetComponentInParent(typeof(IDamage)) is IDamage damagebleComponent)
                {
                    if (!Explosive)
                    {
                        damagebleComponent.ApplyDamage(Damage, Owner);
                    }

                    Active();
                }
                else
                {
                    Active();
                }
            }
        }

        /// <summary>
        /// Make pasible to aply damage to child gameobjects 
        /// </summary>
        /// <param name="other"></param>
        //private void OnTriggerEnter(Collider other)
        //{
        //    if (Owner && other.gameObject == Owner.transform.root.gameObject)
        //    {
        //        foreach (Collider col in Owner.GetComponentsInChildren<Collider>())
        //        {
        //            Physics.IgnoreCollision(other.gameObject.GetComponent<Collider>(), col);
        //        }
        //    }
        //    else
        //    {
        //        var damagebleComponent = other.gameObject.GetComponentInParent(typeof(IDamage)) as IDamage;

        //        if (damagebleComponent != null)
        //        {
        //            if (!Explosive)
        //            {
        //                damagebleComponent.ApplyDamage(Damage, Owner);
        //            }
        //            Active();
        //        }
        //    }
        //}
    }
}
