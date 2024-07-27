namespace SpaceAI.WeaponSystem
{
    using SpaceAI.Core;
    using System.Collections;
    using UnityEngine;

    public class SA_DamageSandler : SA_DamageBase
    {
        public ShellType ShellType;
        public bool Explosive;
        public float ExplosionRadius = 20;
        public float ExplosionForce = 1000;
        public float DestryAfterDuration = 10;

        private GameObject storedEffect;
        private Collider[] colliders;
        private Coroutine deactivateCoroutine;

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

            ReActivate(gameObject, DestryAfterDuration);

            if (storedEffect)
            {
                storedEffect.SetActive(false);
            }

            if (!Owner || !Owner.GetComponent<Collider>()) return;

            colliders = Owner.GetComponentsInChildren<Collider>();

            Physics.IgnoreCollision(GetComponent<Collider>(), Owner.GetComponent<Collider>());

            foreach (Collider col in colliders)
            {
                Physics.IgnoreCollision(GetComponent<Collider>(), col);
            }
        }

        public void Active()
        {
            if (!isActiveAndEnabled) return;

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

                ReActivate(storedEffect);
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
                ReActivate(gameObject, UnityEngine.Random.Range(1, explosionSound.clip.length));
            }
            else
            {
                Deactivate(gameObject);
            }
        }

        public void Deactivate(GameObject any)
        {
            any.SetActive(false);
        }

        public IEnumerator DeactivateAfterTime(GameObject any, float time)
        {
            yield return new WaitForSeconds(time);

            any.SetActive(false);
        }

        private void ExplosionDamage()
        {
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, ExplosionRadius);

            for (int i = 0; i < hitColliders.Length; i++)
            {
                Collider hit = hitColliders[i];

                if (!hit)
                    continue;

                if (hit.gameObject.GetComponent<SA_IDamage>() is SA_IDamage damagebleComponent)
                {
                    damagebleComponent.ApplyDamage(damage, Owner);
                }

                if (hit.GetComponent<Rigidbody>())
                    hit.GetComponent<Rigidbody>().AddExplosionForce(ExplosionForce, transform.position, ExplosionRadius, 3.0f);
            }
        }

        private void ReActivate(GameObject gameObject, float time = 1)
        {
            if (deactivateCoroutine != null)
            {
                StopCoroutine(deactivateCoroutine);
                deactivateCoroutine = null;
            }

            deactivateCoroutine = StartCoroutine(DeactivateAfterTime(gameObject, time));
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!Owner) return;

            if (collision.gameObject.GetComponent<SA_IDamageSendler>() is SA_IDamageSendler dms)
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

            if (collision.gameObject.GetComponent<SA_IDamage>() is SA_IDamage damagebleComponent)
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
    }
}
