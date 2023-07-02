namespace SpaceAI.Weapons
{
    using SpaceAI.WeaponSystem;
    using System;
    using UnityEngine;

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
            public float velocity;
            public float alignSpeed;

            public MissileSettings(MissileType type, float velocity, float alignSpeed)
            {
                missileType = type;
                this.velocity = velocity;
                this.alignSpeed = alignSpeed;
            }
        }

        public MissileSettings missaleSettings;

        private Transform m_transform;
        private Vector3 targetLastPos;
        private Vector3 step;

        void Awake()
        {
            m_transform = transform;
        }

        void FixedUpdate()
        {
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

            step = m_transform.forward * Time.deltaTime * missaleSettings.velocity;

            m_transform.position += step;
        }

        public static Vector3 Predict(Vector3 sPos, Vector3 tPos, Vector3 tLastPos, float pSpeed)
        {
            Vector3 tVel = (tPos - tLastPos) / Time.deltaTime;

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
