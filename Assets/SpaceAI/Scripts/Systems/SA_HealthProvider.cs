namespace SpaceAI.ShipSystems
{
    using SpaceAI.Core;
    using SpaceAI.Ship;
    using UnityEngine;
    using System;

    [Serializable]
    public class SA_HealthProvider
    {
        private SA_IShip ship;
        private ParticleSystem onFire;
        private float currentHP;
        private float maxHP;
        private float dmgByCollision;
        private SA_Shield shield;

        public SA_Shield Shield => shield;

        public SA_HealthProvider(SA_IShip ship, ParticleSystem onFire, ShipSystemFactory shipSystemFactory)
        {
            this.ship = ship;
            this.onFire = onFire;
            currentHP = ship.ShipConfiguration.MainConfig.HP;
            maxHP = ship.ShipConfiguration.MainConfig.HPmax;
            dmgByCollision = ship.ShipConfiguration.MainConfig.CollisionDamage;

            if (ship.ShipConfiguration.ShieldsConfiguration.EnableShields)
            {
                shield = shipSystemFactory.CreateSystem<SA_Shield>(ship, ship.ShipConfiguration.Items.ShieldPrefab);
            }

            ship.SubscribeEvent(ShipHit);
        }

        public void ResetHP()
        {
            currentHP = maxHP;
        }

        public void ApplyDamage(float damage, GameObject killer, Action callback)
        {
            if (currentHP < 0)
                return;

            if (ship.ShipConfiguration.Items.HitSounds != null && ship.ShipConfiguration.Items.HitSounds.Length > 0)
            {
                AudioSource.PlayClipAtPoint(ship.ShipConfiguration.Items.HitSounds[UnityEngine.Random.Range(0, ship.ShipConfiguration.Items.HitSounds.Length)], ship.CurrentShipTransform.position);
            }

            if (ship.ShipConfiguration.ShieldsConfiguration.EnableShields)
            {
                if (shield != null)
                {
                    if (shield.ShieldPower > 0) shield.ShieldPower -= damage;

                    if (shield.ShieldPower <= 0)
                    {
                        shield = null;
                    }
                }
                else
                {
                    currentHP -= damage;
                }
            }
            else
            {
                currentHP -= damage;
            }

            if (onFire)
            {
                if (currentHP < (int)(maxHP / 2))
                {
                    onFire.Play();
                }
            }
            if (currentHP <= 0)
            {
                Dead();

                callback.Invoke();
            }
        }

        public virtual void Dead()
        {
            if (ship.ShipConfiguration.Items.ExplousionEffect)
            {
                UnityEngine.Object.Instantiate(ship.ShipConfiguration.Items.ExplousionEffect, ship.CurrentShipTransform.position, ship.CurrentShipTransform.rotation);
            }
        }

        public void ShipHit(Collision collision)
        {
            bool isDethCome = false;

            if (collision.gameObject.GetComponent(typeof(SA_IDamageSendler))) return;

            if (ship.ShipConfiguration.Items.HitSounds != null && ship.ShipConfiguration.Items.HitSounds.Length > 0)
            {
                AudioSource.PlayClipAtPoint(ship.ShipConfiguration.Items.HitSounds[UnityEngine.Random.Range(0, ship.ShipConfiguration.Items.HitSounds.Length)], ship.CurrentShipTransform.position);
            }

            var owner = ship as SA_BaseShip;

            if (!owner.GetComponent<Collider>().isTrigger)
            {
                if (collision.relativeVelocity.magnitude > ship.ShipConfiguration.MainConfig.DurableForce)
                {
                    var hp = Mathf.Clamp(currentHP - ship.ShipConfiguration.MainConfig.CollisionDamage, 0f, maxHP);

                    if (ship.ShipConfiguration.ShieldsConfiguration.EnableShields)
                    {
                        if (shield == null)
                        {
                            isDethCome = hp <= 0;
                        }
                        else
                        {
                            shield.ShieldPower -= ship.ShipConfiguration.MainConfig.CollisionDamage;
                        }
                    }
                    else
                    {
                        isDethCome = hp <= 0;
                    }

                    if (isDethCome) Dead();
                }
            }
        }

        public bool IsDead()
        {
            if (currentHP <= 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}