namespace SpaceAI.WeaponSystem
{
    using SpaceAI.SceneTools;
    using SpaceAI.Ship;
    using System;
    using System.Collections;
    using UnityEngine;

    [RequireComponent(typeof(AudioSource))]
    public class SA_WeaponLaunchManager : SA_WeaponBase, SA_IWeapon
    {
        private readonly string _poolName = "BulletPool";

        [Serializable]
        public class Settings
        {
            public ShellType ShellType;
            public Transform[] shellOuter;
            public GameObject Muzzle;
            public float FireRate = 0.1f;
            public float Spread = 1;
            public float ForceShoot = 1000;
            public float ReloadTime = 1;
            public float MuzzleLifeTime = 2;
            public int NumBullet = 1;
            public int Ammo = 10;
            public int AmmoMax = 10;
            public bool InfinityAmmo = false;
            public AudioClip[] SoundGun;
            public AudioClip SoundReloading;
            public AudioClip SoundReloaded;

        }

        [SerializeField] private Settings settings;

        private SA_DamageSandler shellPrefab;
        private AudioSource m_audio;
        private int currentOuter = 0;
        private float nextFireTime = 0;
        private bool Reloading;
        private GameObject muzzle;

        private int poolID;
        private SA_IShip owner;

        Settings SA_IWeapon.Settings => settings;

        private void InitGuns()
        {
            if (!Owner) Owner = transform.root.gameObject;

            m_audio = GetComponent<AudioSource>();

            if (!m_audio)
            {
                m_audio = gameObject.AddComponent<AudioSource>();
            }

            SA_Manager.Pool pool = new()
            {
                id = poolID = UnityEngine.Random.Range(1000, 10000)
            };

            GameObject scenePool = new(_poolName);

            for (int i = 0; i < settings.AmmoMax; i++)
            {
                SA_DamageSandler bullet = Instantiate(shellPrefab, scenePool.transform);
                bullet.gameObject.SetActive(false);
                pool.bullet.Add(bullet);
            }

            SA_Manager.BulletPool.Add(pool);
        }

        IEnumerator Reload()
        {
            if (!Reloading && settings.Ammo <= 0)
            {
                Reloading = true;

                if (settings.SoundReloading && m_audio)
                {
                    m_audio.PlayOneShot(settings.SoundReloading);
                }
            }
            else if (Reloading)
            {
                yield return new WaitForSeconds(settings.ReloadTime);

                if (settings.SoundReloaded && m_audio)
                {
                    m_audio.PlayOneShot(settings.SoundReloaded);
                    settings.Ammo = settings.AmmoMax;
                }
                else
                {
                    settings.Ammo = settings.AmmoMax;
                }

                Reloading = false;
            }
        }

        public virtual void Shoot(Transform[] outShell)
        {
            if (outShell == null || outShell.Length == 0)
            {
                outShell = settings.shellOuter;
            }

            Target = owner.CurrentEnemy;

            if (settings.InfinityAmmo)
            {
                settings.Ammo = 1;
            }

            if (settings.Ammo > 0 && Time.time > nextFireTime + settings.FireRate)
            {
                Vector3 spread = UnityEngine.Random.insideUnitSphere * settings.Spread / 100;
                nextFireTime = Time.time;
                settings.Ammo--;
                Vector3 direction;

                //set bullets at transform of out shell
                Vector3 shellPosition = transform.position;
                Quaternion shellRotate = transform.rotation;

                if (outShell != null && outShell.Length > 0)
                {                    
                    Transform shell = outShell[currentOuter++ % outShell.Length];
                    direction = shell.forward + spread;
                    shellPosition = shell.position;
                    shellRotate = shell.rotation;
                }
                else
                {
                    direction = transform.forward + spread;
                    shellPosition = transform.position;
                    shellRotate = transform.rotation;
                }

                //bullet shot effekt
                if (settings.Muzzle)
                {
                    muzzle = muzzle ?? Instantiate(settings.Muzzle, shellPosition, shellRotate, transform);
                    muzzle.transform.parent = outShell != null && outShell.Length > 0 ? outShell[currentOuter - 1].transform : transform;
                    muzzle.SetActive(true);
                }

                for (int i = 0; i < settings.NumBullet; i++)
                {
                    if (shellPrefab)
                    {
                        SA_DamageSandler bullet = SA_Manager.BulletPool.Find(p => p.id == poolID).bullet.Find(b => !b.gameObject.activeSelf);

                        if (bullet)
                        {
                            bullet.SetOwner(Owner);
                            bullet.SetTarget(Target);
                            bullet.transform.SetPositionAndRotation(shellPosition, shellRotate);
                            bullet.gameObject.SetActive(true);

                            if (bullet.Rb)
                            {
                                bullet.Rb.velocity = Vector3.zero;

                                if (Owner?.GetComponent<Rigidbody>() is Rigidbody rb)
                                {
                                    bullet.Rb.velocity = rb.velocity;
                                }

                                bullet.Rb.AddForce(direction * settings.ForceShoot);
                            }
                        }
                    }
                }

                if (settings.SoundGun.Length > 0 && m_audio)
                {
                    m_audio.PlayOneShot(settings.SoundGun[UnityEngine.Random.Range(0, settings.SoundGun.Length)]);
                }

                nextFireTime += settings.FireRate;
            }
            else
            {
                StartCoroutine(Reload());
            }
        }

        public void SetOwner(SA_IShip ownerShip)
        {
            owner = ownerShip;
        }

        public void SetFireShells(SA_DamageSandler[] shellPrefab)
        {
            foreach (var shell in shellPrefab)
            {
                if (shell.ShellType == settings.ShellType)
                {
                    this.shellPrefab = shell;

                    InitGuns();
                }
            }
        }
    }
}