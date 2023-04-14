using SpaceAI.WeaponSystem;
using System;
using UnityEngine;

namespace SpaceAI.Weapons
{
    public class SA_MissileTypes : SA_DamageSandler
    {
        [Serializable]
        public class MissileSettings
        {
            public enum MissileType
            {
                Unguided,
                Guided,
                Predictive
            }

            public MissileType missileType;
            public float detonationDistance;
            public float lifeTime; // Missile life time
            public float velocity; // Missile velocity
            public float alignSpeed;
            public float RaycastAdvance; // Raycast advance multiplier

            public MissileSettings(MissileType type, float detonationDis, float lifeTime, float velocity, float alignSpeed, float RaycastAdvance)
            {
                missileType = type;
                detonationDistance = detonationDis;
                this.lifeTime = lifeTime;
                this.velocity = velocity;
                this.alignSpeed = alignSpeed;
                this.RaycastAdvance = RaycastAdvance;
            }
        }

        public MissileSettings missaleSettings;

        private Transform m_transform; // Cached transform
        private bool isHit = false; // Missile hit flag
        private float timer = 0f; // Missile timer
        private Vector3 targetLastPos;
        private Vector3 step;

        void Awake()
        {
            m_transform = transform;
        }

        void OnHit()
        {
        }

        void FixedUpdate()
        {
            // If something was hit
            if (!isHit)
            {
                // Navigate
                if (Target)
                {
                    if (missaleSettings.missileType == MissileSettings.MissileType.Predictive)
                    {
                        Vector3 hitPos = Predict(m_transform.position, Target.transform.position, targetLastPos, missaleSettings.velocity);
                        targetLastPos = Target.transform.position;

                        m_transform.rotation = Quaternion.Lerp(m_transform.rotation,
                            Quaternion.LookRotation(hitPos - m_transform.position), Time.deltaTime * missaleSettings.alignSpeed);
                    }
                    else if (missaleSettings.missileType == MissileSettings.MissileType.Guided)
                    {
                        m_transform.rotation = Quaternion.Lerp(m_transform.rotation,
                            Quaternion.LookRotation(Target.transform.position - m_transform.position), Time.deltaTime * missaleSettings.alignSpeed);
                    }
                }
                // Missile step per frame based on velocity and time
                step = m_transform.forward * Time.deltaTime * missaleSettings.velocity;

                if (Target != null && missaleSettings.missileType != MissileSettings.MissileType.Unguided &&
                    Vector3.SqrMagnitude(m_transform.position - Target.transform.position) <= missaleSettings.detonationDistance)
                {
                    OnHit();
                }
                else
                {
                    // Despawn missile at the end of life cycle
                    if (timer >= missaleSettings.lifeTime)
                    {
                        OnHit();
                    }
                }
                // Advances missile forward
                m_transform.position += step;
            }
            // Updates missile timer
            timer += Time.deltaTime;
        }

        public static Vector3 Predict(Vector3 sPos, Vector3 tPos, Vector3 tLastPos, float pSpeed)
        {
            // Target velocity
            Vector3 tVel = (tPos - tLastPos) / Time.deltaTime;

            // Time to reach the target
            float flyTime = GetProjFlightTime(tPos - sPos, tVel, pSpeed);

            if (flyTime > 0)
            {
                return tPos + flyTime * tVel;
            }

            return tPos;
        }

        static float GetProjFlightTime(Vector3 dist, Vector3 tVel, float pSpeed)
        {
            float a = Vector3.Dot(tVel, tVel) - pSpeed * pSpeed;
            float b = 2.0f * Vector3.Dot(tVel, dist);
            float c = Vector3.Dot(dist, dist);

            float det = b * b - 4 * a * c;

            if (det > 0)
            {
                return 2 * c / (Mathf.Sqrt(det) - b);
            }

            return -1;
        }
    }
}
