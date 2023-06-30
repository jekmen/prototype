using SpaceAI.Core;
using SpaceAI.Ship;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace SpaceAI.WeaponSystem
{
    public class SA_DamageSandler : SA_DamageBase
    {
        public ShellType ShellType;
        public bool Explosive;
        public float ExplosionRadius = 20;
        public float ExplosionForce = 1000;
        public float DestryAfterDuration = 10;

        private GameObject storedEffect;
        private Collider[] colliders;

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

            Physics.IgnoreCollision(GetComponent<Collider>(), Owner.GetComponent<Collider>());

            foreach (Collider col in colliders)
            {
                Physics.IgnoreCollision(GetComponent<Collider>(), col);
            }
        }

        public void Active()
        {
            if (explousionEffectPrefab)
            {
                if (!storedEffect)
                {
                    storedEffect = Instantiate(explousionEffectPrefab, transform.position, transform.rotation);
                }
                else
                {
                    storedEffect.transform.SetPositionAndRotation(transform.position, transform.rotation);
                    storedEffect.SetActive(true);
                }

                Deactivate(storedEffect, lifeTimeEffect);
            }

            if (Explosive)
            {
                ExplosionDamage();
            }

            if (explosionSound && !explosionSound.isPlaying)
            {
                explosionSound.Play();
                GetComponent<Renderer>().enabled = false;
                GetComponent<Collider>().enabled = false;
                Deactivate(gameObject, UnityEngine.Random.Range(1, explosionSound.clip.length));
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

            await Task.Delay(TimeSpan.FromSeconds(time));

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

                if (hit.gameObject.GetComponent<IDamage>() is IDamage damagebleComponent)
                {
                    damagebleComponent.ApplyDamage(damage, Owner);
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
            if (!Owner) return;

            if (collision.gameObject.GetComponent<IDamageSendler>() is IDamageSendler dms)
            {
                if (dms.Owner == Owner)
                {
                    foreach (Collider col in Owner.GetComponentsInChildren<Collider>())
                    {
                        Physics.IgnoreCollision(collision.gameObject.GetComponent<Collider>(), col, true);

                        return;
                    }
                }
            }

            if (collision.gameObject.GetComponent<IDamage>() is IDamage damagebleComponent)
            {
                if (!Explosive)
                {
                    damagebleComponent.ApplyDamage(damage, Owner);
                }

                Active();
            }
            else
            {
                Active();
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
