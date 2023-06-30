using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI.SceneEnvironment
{
    public class SA_MoonOrbit : MonoBehaviour
    {

        // Orbit speed of moon around its parent planet
        public float orbitSpeed = 0.0f;
        // Rotational speed of moon around its own acis
        public float rotationSpeed = 0.0f;

        // Private Variables
        private Transform _cacheTransform;
        private Transform _cacheMeshTransform;

        void Start()
        {
            // Cache transforms to increase performance
            _cacheTransform = transform;
            _cacheMeshTransform = transform.Find("MoonObject");
        }

        void Update()
        {
            // Orbit around the planet at orbitSpeed
            if (_cacheTransform != null)
            {
                _cacheTransform.Rotate(Vector3.up * orbitSpeed * Time.deltaTime);
            }

            // Rotate around own axis
            if (_cacheMeshTransform != null)
            {
                _cacheMeshTransform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime);
            }
        }
    }
}
