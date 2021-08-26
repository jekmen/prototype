using SpaceAI.WeaponSystem;
using System;
using UnityEngine;

namespace SpaceAI.Weapons
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(SA_DamageSandler))]
    [RequireComponent(typeof(AudioSource))]
    public class SA_Missile : SA_WeaponBase
    {
        [Serializable]
        public class MissileSettings
        {
            public float Damping;
            public float Speed;
            public float SpeedMax;
            public float SpeedMult;
            public Vector3 Noise;
            public float TargetLockDirection;
            public int DistanceLock;
            public int DurationLock;
            public bool Seeker;
            public float LifeTime;

            public MissileSettings(float damping, float speed, float speedMax, float speedMult, Vector3 noise, float targetLockDirection, int distanceLock, int durationLock, bool seeker, float lifeTime)
            {
                Damping = damping;
                Speed = speed;
                SpeedMax = speedMax;
                SpeedMult = speedMult;
                Noise = noise;
                TargetLockDirection = targetLockDirection;
                DistanceLock = distanceLock;
                DurationLock = durationLock;
                Seeker = seeker;
                LifeTime = lifeTime;
            }
        }

        public MissileSettings missileSettings;

        private bool locked;
        private int timetorock;
        private float timeCount = 0;
        private Rigidbody rb;

        private GameObject[] objs;

        private void Start()
        {
            rb = GetComponent<Rigidbody>();

            timeCount = Time.time;
            Destroy(gameObject, missileSettings.LifeTime);

            if (missileSettings.Seeker)
            {
                //objs = GameObject.FindGameObjectsWithTag(SA_AIController.SHIPS_SERCH_TAG1);
            }
        }

        private void FixedUpdate()
        {
            rb.velocity = new Vector3(transform.forward.x * missileSettings.Speed * Time.fixedDeltaTime, transform.forward.y * missileSettings.Speed * Time.fixedDeltaTime, transform.forward.z * missileSettings.Speed * Time.fixedDeltaTime);
            rb.velocity += new Vector3(UnityEngine.Random.Range(-missileSettings.Noise.x, missileSettings.Noise.x), UnityEngine.Random.Range(-missileSettings.Noise.y, missileSettings.Noise.y), UnityEngine.Random.Range(-missileSettings.Noise.z, missileSettings.Noise.z));

            if (missileSettings.Speed < missileSettings.SpeedMax)
            {
                missileSettings.Speed += missileSettings.SpeedMult * Time.fixedDeltaTime;
            }
        }

        private void Update()
        {
            if (Time.time >= (timeCount + missileSettings.LifeTime) - 0.5f)
            {
                if (GetComponent<SA_DamageSandler>())
                {
                    GetComponent<SA_DamageSandler>().Active();
                }
            }

            if (Target)
            {
                Quaternion rotation = Quaternion.LookRotation(Target.transform.position - transform.transform.position);
                transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * missileSettings.Damping);
                Vector3 dir = (Target.transform.position - transform.position).normalized;
                float direction = Vector3.Dot(dir, transform.forward);
                if (direction < missileSettings.TargetLockDirection)
                {
                    Target = null;
                }
            }

            FindTargets();
        }

        void FindTargets()
        {
            if (missileSettings.Seeker)
            {
                if (timetorock > missileSettings.DurationLock)
                {
                    if (!locked && !Target)
                    {
                        float distance = int.MaxValue;
                        if (objs.Length > 0)
                        {
                            for (int i = 0; i < objs.Length; i++)
                            {
                                if (objs[i])
                                {
                                    Vector3 dir = (objs[i].transform.position - transform.position).normalized;
                                    float direction = Vector3.Dot(dir, transform.forward);
                                    float dis = Vector3.Distance(objs[i].transform.position, transform.position);
                                    if (direction >= missileSettings.TargetLockDirection)
                                    {
                                        if (missileSettings.DistanceLock > dis)
                                        {
                                            if (distance > dis)
                                            {
                                                distance = dis;

                                                //if (Owner && Owner.GetComponent<SA_FlightSystem>().m_MainControls.groupType != objs[i].gameObject.GetComponent<SA_FlightSystem>().m_MainControls.groupType)
                                                //{
                                                //    Target = objs[i].gameObject;
                                                //}
                                            }
                                            locked = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    timetorock += 1;
                }

                if (Target)
                {

                }
                else
                {
                    locked = false;
                }
            }
        }
    }
}

