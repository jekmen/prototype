using SpaceAI.ScaneTools;
using SpaceAI.Ship;
using System;
using UnityEngine;

namespace SpaceAI.WeaponSystem
{
    [RequireComponent(typeof(AudioSource))]
    public class SA_WeaponLunchManager : SA_WeaponBase
    {
        [Serializable]
        public class Settings
        {
            public bool OnActive;
            public bool InfinityAmmo = false;
            public Transform[] shellOuter;
            public GameObject ShellPrefab;
            public GameObject Muzzle;
            public float FireRate = 0.1f;
            public float Spread = 1;
            public float ForceShoot = 1000;
            public float ReloadTime = 1;
            public float MuzzleLifeTime = 2;
            public int NumBullet = 1;
            public int Ammo = 10;
            public int AmmoMax = 10;
            public AudioClip[] SoundGun;
            public AudioClip SoundReloading;
            public AudioClip SoundReloaded;
        }

        public Settings settings;

        private AudioSource m_audio;
        private Vector3 torqueTemp;
        private int currentOuter = 0;
        private float nextFireTime = 0;
        private float reloadTimeTemp;
        private bool Reloading;
        private GameObject muzzle;

        private int poolID;

        private void Start()
        {
            if (!Owner)
                Owner = transform.root.gameObject;

            m_audio = GetComponent<AudioSource>();

            if (!m_audio)
            {
                m_audio = gameObject.AddComponent<AudioSource>();
            }

            SA_Manager.Pool pool = new SA_Manager.Pool
            {
                id = poolID = UnityEngine.Random.Range(1000, 10000)
            };

            for (int i = 0; i < settings.AmmoMax; i++)
            {
                GameObject bullet = Instantiate(settings.ShellPrefab);
                bullet.SetActive(false);
                pool.bullet.Add(bullet);
            }

            SA_Manager.instance.BulletPool.Add(pool);
        }

        private void Update()
        {
            if (!Target && GetComponentInParent<SA_ShipController>())
            {
                Target = GetComponentInParent<SA_ShipController>().EnemyTarget;
            }

            if (settings.OnActive)
            {
                if (TorqueObject)
                {
                    TorqueObject.transform.Rotate(torqueTemp * Time.deltaTime);
                    torqueTemp = Vector3.Lerp(torqueTemp, Vector3.zero, Time.deltaTime);
                }

                if (Reloading)
                {
                    if (Time.time >= reloadTimeTemp + settings.ReloadTime)
                    {
                        Reloading = false;

                        if (settings.SoundReloaded)
                        {
                            if (m_audio)
                            {
                                m_audio.PlayOneShot(settings.SoundReloaded);
                                settings.Ammo = settings.AmmoMax;
                            }
                        }
                        else
                        {
                            settings.Ammo = settings.AmmoMax;
                        }
                    }
                }
                else
                {
                    if (settings.Ammo <= 0)
                    {
                        Reloading = true;
                        reloadTimeTemp = Time.time;

                        if (settings.SoundReloading)
                        {
                            if (m_audio)
                            {
                                m_audio.PlayOneShot(settings.SoundReloading);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Main shot function
        /// </summary>
        public void Shoot()
        {
            //if true it will shot always 
            if (settings.InfinityAmmo)
            {
                settings.Ammo = 1;
            }

            if (settings.Ammo > 0)
            {
                //set next shot
                if (Time.time > nextFireTime + settings.FireRate)
                {
                    Vector3 spread = new Vector3(UnityEngine.Random.Range(-settings.Spread, settings.Spread), UnityEngine.Random.Range(-settings.Spread, settings.Spread), UnityEngine.Random.Range(-settings.Spread, settings.Spread)) / 100;
                    nextFireTime = Time.time;
                    torqueTemp = TorqueSpeedAxis;
                    Vector3 direction;
                    settings.Ammo -= 1;

                    //set bullets at transform of out shell
                    Vector3 shellPosition = transform.position;
                    Quaternion shellRotate = transform.rotation;

                    if (settings.shellOuter.Length > 0)
                    {
                        direction = settings.shellOuter[currentOuter].transform.forward + spread;
                        shellRotate = settings.shellOuter[currentOuter].transform.rotation;
                        shellPosition = settings.shellOuter[currentOuter].transform.position;
                    }
                    else
                    {
                        direction = transform.forward + spread;
                    }

                    if (settings.shellOuter.Length > 0)
                    {
                        currentOuter = (currentOuter + 1) % settings.shellOuter.Length;
                    }

                    //bullet shot effekt
                    if (settings.Muzzle)
                    {
                        if (!muzzle)
                        {
                            muzzle = Instantiate(settings.Muzzle, shellPosition, shellRotate);
                        }

                        muzzle.transform.parent = transform;
                        muzzle.SetActive(true);

                        if (settings.shellOuter.Length > 0)
                        {
                            muzzle.transform.parent = settings.shellOuter[currentOuter].transform;
                        }
                    }

                    for (int i = 0; i < settings.NumBullet; i++)
                    {
                        if (settings.ShellPrefab)
                        {
                            GameObject bullet = null;

                            if (!bullet)
                            {
                                for (int b = 0; b < SA_Manager.instance.BulletPool.Count; b++)
                                {
                                    if (SA_Manager.instance.BulletPool[b].id == poolID)
                                    {
                                        bullet = SA_Manager.instance.BulletPool[b].bullet.Find(x => x == !x.activeSelf);

                                        if (!bullet) return;

                                        SA_DamageBase damangeBase = bullet.GetComponent<SA_DamageBase>();

                                        if (damangeBase)
                                        {
                                            damangeBase.Owner = Owner;
                                        }

                                        SA_WeaponBase weaponBase = bullet.GetComponent<SA_WeaponBase>();

                                        if (weaponBase)
                                        {
                                            weaponBase.Owner = Owner;
                                            weaponBase.Target = Target;
                                        }

                                        bullet.transform.position = shellPosition;
                                        bullet.transform.rotation = shellRotate;
                                        bullet.transform.forward = direction;

                                        bullet.SetActive(true);

                                        if (bullet.GetComponent<Rigidbody>())
                                        {
                                            bullet.GetComponent<Rigidbody>().velocity = Vector3.zero;

                                            if (Owner != null && Owner.GetComponent<Rigidbody>())
                                            {
                                                bullet.GetComponent<Rigidbody>().velocity = Owner.GetComponent<Rigidbody>().velocity;
                                            }

                                            bullet.GetComponent<Rigidbody>().AddForce(direction * settings.ForceShoot);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (settings.SoundGun.Length > 0)
                    {
                        if (m_audio)
                        {
                            m_audio.PlayOneShot(settings.SoundGun[UnityEngine.Random.Range(0, settings.SoundGun.Length)]);
                        }
                    }

                    nextFireTime += settings.FireRate;
                }
            }
        }

        public void Shoot(Transform[] outShell)
        {
            //if true it will shot always 
            if (settings.InfinityAmmo)
            {
                settings.Ammo = 1;
            }

            if (settings.Ammo > 0)
            {
                //set next shot
                if (Time.time > nextFireTime + settings.FireRate)
                {
                    Vector3 spread = new Vector3(UnityEngine.Random.Range(-settings.Spread, settings.Spread), UnityEngine.Random.Range(-settings.Spread, settings.Spread), UnityEngine.Random.Range(-settings.Spread, settings.Spread)) / 100;
                    nextFireTime = Time.time;
                    torqueTemp = TorqueSpeedAxis;
                    Vector3 direction;
                    settings.Ammo -= 1;

                    //set bullets at transform of out shell
                    Vector3 shellPosition = transform.position;
                    Quaternion shellRotate = transform.rotation;

                    if (outShell.Length > 0)
                    {
                        direction = outShell[currentOuter].transform.forward + spread;
                        shellRotate = outShell[currentOuter].transform.rotation;
                        shellPosition = outShell[currentOuter].transform.position;
                    }
                    else
                    {
                        direction = transform.forward + spread;
                    }

                    if (outShell.Length > 0)
                    {
                        currentOuter = (currentOuter + 1) % outShell.Length;
                    }

                    //bullet shot effekt
                    if (settings.Muzzle)
                    {
                        if (!muzzle)
                        {
                            muzzle = Instantiate(settings.Muzzle, shellPosition, shellRotate);
                        }

                        muzzle.transform.parent = transform;
                        muzzle.SetActive(true);

                        if (outShell.Length > 0)
                        {
                            muzzle.transform.parent = outShell[currentOuter].transform;
                        }
                    }

                    for (int i = 0; i < settings.NumBullet; i++)
                    {
                        if (settings.ShellPrefab)
                        {
                            GameObject bullet = null;

                            if (!bullet)
                            {
                                for (int b = 0; b < SA_Manager.instance.BulletPool.Count; b++)
                                {
                                    if (SA_Manager.instance.BulletPool[b].id == poolID)
                                    {
                                        bullet = SA_Manager.instance.BulletPool[b].bullet.Find(x => x == !x.activeSelf);

                                        if (!bullet)
                                            return;

                                        SA_DamageBase damangeBase = bullet.GetComponent<SA_DamageBase>();

                                        if (damangeBase)
                                        {
                                            damangeBase.Owner = Owner;
                                        }

                                        SA_WeaponBase weaponBase = bullet.GetComponent<SA_WeaponBase>();

                                        if (weaponBase)
                                        {
                                            weaponBase.Owner = Owner;
                                            weaponBase.Target = Target;
                                        }

                                        bullet.transform.position = shellPosition;
                                        bullet.transform.rotation = shellRotate;
                                        bullet.transform.forward = direction;

                                        bullet.SetActive(true);

                                        if (bullet.GetComponent<Rigidbody>())
                                        {
                                            bullet.GetComponent<Rigidbody>().velocity = Vector3.zero;

                                            if (Owner != null && Owner.GetComponent<Rigidbody>())
                                            {
                                                bullet.GetComponent<Rigidbody>().velocity = Owner.GetComponent<Rigidbody>().velocity;
                                            }

                                            bullet.GetComponent<Rigidbody>().AddForce(direction * settings.ForceShoot);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (settings.SoundGun.Length > 0)
                    {
                        if (m_audio)
                        {
                            m_audio.PlayOneShot(settings.SoundGun[UnityEngine.Random.Range(0, settings.SoundGun.Length)]);
                        }
                    }

                    nextFireTime += settings.FireRate;
                }
            }
        }
    }
}
