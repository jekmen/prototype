using SpaceAI.WeaponSystem;
using System;
using UnityEngine;

namespace SpaceAI.Weapons
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(SA_DamageSandler))]
    [RequireComponent(typeof(AudioSource))]
    public class SA_Missile : SA_DamageSandler
    {
        [Serializable]
        public class MissileSettings
        {
            public float Speed;
            public float Damping;
            public float Noise;
            public float NoiseAmplitude;
        }

        public MissileSettings missileSettings;

        private float perlinTime;

        private void FixedUpdate()
        {
            // Calculate the noise values based on Perlin noise
            float xNoise = Mathf.PerlinNoise(missileSettings.NoiseAmplitude * perlinTime, 0f);
            float yNoise = Mathf.PerlinNoise(0f, missileSettings.NoiseAmplitude * perlinTime);
            float zNoise = Mathf.PerlinNoise(missileSettings.NoiseAmplitude * perlinTime, missileSettings.NoiseAmplitude * perlinTime);
            Vector3 noise = new Vector3(xNoise, yNoise, zNoise) * missileSettings.Noise;

            // Calculate the velocity vector with added noise
            Vector3 velocity = transform.forward * missileSettings.Speed + noise;

            // Apply the velocity to the rigidbody
            Rb.velocity = velocity;

            if (Target)
            {
                Quaternion rotation = Quaternion.LookRotation(Target.transform.position - transform.transform.position);
                transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * missileSettings.Damping);
            }

            // Increment the Perlin noise time
            perlinTime += Time.deltaTime;
        }
    }
}