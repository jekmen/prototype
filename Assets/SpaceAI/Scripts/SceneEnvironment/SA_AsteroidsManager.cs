using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI.SceneEnvironment
{
    public class SA_AsteroidsManager : MonoBehaviour
    {
        // Poly Count (quality) of the asteroids in the field
        public SA_Asteroid.PolyCount polyCount = SA_Asteroid.PolyCount.HIGH;
        // Poly Count (quality) of the asteroid colliders (LOW = fast, HIGH = slow)
        public SA_Asteroid.PolyCount polyCountCollider = SA_Asteroid.PolyCount.LOW;


        // Array of prefabs that the asteroid fields should consist of
        public Transform[] prefabAsteroids;

        // Arrays of materials of asteroids in the field
        public Material[] materialVeryCommon;       // 50%
        public Material[] materialCommon;           // 30%
        public Material[] materialRare;             // 15%
        public Material[] materialVeryRare;         // 5%

        // Range of asteroid field sphere (when asteroids are beyond this range from the game object  
        // they will respawn (relocate) to within range at distanceSpawn of range.
        public float range = 20000.0f;
        // Maximum number of asteroids in the sphere (configure to your needs for look and performance)
        public int maxAsteroids;
        // Respawn destroyed asteroids true/false
        public bool respawnDestroyedAsteroids = true;
        // Respawn if out of range (must be true for infinite/endless asteroid fields
        public bool respawnIfOutOfRange = true;
        // Distance percentile of range to relocate/spawn asteroids to
        public float distanceSpawn = 0.95f;
        // Minimum scale of asteroid	
        public float minAsteroidScale = 0.1f;
        // Maximum scale of asteroid
        public float maxAsteroidScale = 1.0f;
        // Multiplier of scale
        public float scaleMultiplier = 1.0f;

        public float minAsteroidResources = 0.1f;
        public float maxAsteroidResources = 1.0f;
        public float ResourcesMultiplier = 1.0f;

        // Is rigid body or not
        public bool isRigidbody = false;

        // NON-RIGIDBODY ASTEROIDS ---
        // Minimum rotational speed of asteroid
        public float minAsteroidRotationSpeed = 0.0f;
        // Maximum rotational speed of asteroid
        public float maxAsteroidRotationSpeed = 1.0f;
        // Rotation speed multiplier
        public float rotationSpeedMultiplier = 1.0f;
        // Minimum drift/movement speed of asteroid
        public float minAsteroidDriftSpeed = 0.0f;
        // Maximum drift/movement speed of asteroid
        public float maxAsteroidDriftSpeed = 1.0f;
        // Multiplier of driftSpeed
        public float driftSpeedMultiplier = 1.0f;
        // ---------------------------

        // RIGIDBODY ASTEROIDS -------
        // Mass of asteroid (scaled between minAsterodiScale/maxAsteroidScale)
        public float mass = 1.0f;
        // Minimum angular velocity of asteroid (rotational speed)
        public float minAsteroidAngularVelocity = 0.0f;
        // Maximum angular velocity of asteroid (rotational speed)
        public float maxAsteroidAngularVelocity = 1.0f;
        // Angular velocity (rotational speed) multiplier
        public float angularVelocityMultiplier = 1.0f;
        // Minimum velocity of asteroid (drift/movement speed)
        public float minAsteroidVelocity = 0.0f;
        // Maximum velocity of asteroid (drift/movement speed)
        public float maxAsteroidVelocity = 1.0f;
        // Velocity (drift/movement speed) multiplier
        public float velocityMultiplier = 1.0f;
        // ----------------------------

        // Fade asteroids in/out of range
        public bool fadeAsteroids = true;
        // Distance percentile of spawn distance to start fading asteroids
        // Alpha = 1.0 at distanceFade*distanceSpawn*range, and 0.0 at distanceSpawn*range
        public float distanceFade = 0.95f;

        // Private variables
        private float _distanceToSpawn;
        private float _distanceToFade;
        private Transform _cacheTransform;
        private List<Transform> _asteroids = new List<Transform>();
        private List<Material> _asteroidsMaterials = new List<Material>();
        private List<bool> _asteroidsFading = new List<bool>();
        private Hashtable _materialsTransparent = new Hashtable();
        private SortedList<int, string> _materialList = new SortedList<int, string>(4);


        void OnEnable()
        {
            // Cache reference to transform to increase performance
            _cacheTransform = transform;

            // Calculate the actual spawn and fade distances
            _distanceToSpawn = range * distanceSpawn;
            _distanceToFade = range * distanceSpawn * distanceFade;

            // If fading is enabled...
            if (fadeAsteroids && _materialsTransparent.Count == 0)
            {
                // Create transparent materials to be used when in fading range
                CreateTransparentMaterial(materialVeryCommon, _materialsTransparent);
                CreateTransparentMaterial(materialCommon, _materialsTransparent);
                CreateTransparentMaterial(materialRare, _materialsTransparent);
                CreateTransparentMaterial(materialVeryRare, _materialsTransparent);
            }

            // Populate the material list based on probabilty of materials
            if (_materialList.Count == 0)
            {
                if (materialVeryRare.Length > 0) _materialList.Add(5, "VeryRare");
                if (materialRare.Length > 0) _materialList.Add(5 + 15, "Rare");
                if (materialCommon.Length > 0) _materialList.Add(5 + 15 + 30, "Common");
                if (materialVeryCommon.Length != 0)
                {
                    _materialList.Add(5 + 15 + 30 + 50, "VeryCommon");
                }
                else
                {
                    Debug.LogError("Asteroid Field must have at least one Material in the 'Material Very Common' Array.");
                }
            }

            // Check if there are any asteroid objects that was spawned prior to this script being disabled
            // If there are asteroids in the list, activate the gameObject again.
            for (int i = 0; i < _asteroids.Count; i++)
            {
                _asteroids[i].gameObject.SetActive(true);
            }

            // Spawn new asteroids in the entire sphere (not just at spawn range, hence the "false" parameter)
            SpawnAsteroids(false);
        }

        void OnDisable()
        {
            //// Asteroid field game object has been disabled, disable all the asteroids as well
            //for (int i = 0; i < _asteroids.Count; i++)
            //{
            //    // If the transform of the asteroid exists (it won't be upon application exit for example)...			
            //    if (_asteroids[i] != null)
            //    {
            //        // deactivate the asteroid gameObject
            //        _asteroids[i].gameObject.SetActive(false);
            //    }
            //}
        }

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(.5f);
            gameObject.SetActive(false);
        }

        void OnDrawGizmosSelected()
        {
            // Draw a yellow wire gizmo sphere at the transform's position with the size of the asteroid field
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, range);
        }

        void Update()
        {
            // Iterate through asteroids and relocate them as parent object moves		
            for (int i = 0; i < _asteroids.Count; i++)
            {
                // Cache the reference to the Transform of the asteroid in the list
                Transform _asteroid = _asteroids[i];

                // If the asteroid in the list has a Transform...
                if (_asteroid != null)
                {
                    // Calculate the distance of the asteroid to the center of the asteroid field
                    float _distance = Vector3.Distance(_asteroid.position, _cacheTransform.position);

                    // If the distance is greater than the range variable...
                    if (_distance > range && respawnIfOutOfRange)
                    {
                        // Relocate ("respawn") the asteroid to a new position at spawning distance					
                        _asteroid.position = Random.onUnitSphere * _distanceToSpawn + _cacheTransform.position;
                        // Give the asteroid a new scale within the min/max scale range
                        float _newScale = Random.Range(minAsteroidScale, maxAsteroidScale) * scaleMultiplier;
                        _asteroid.localScale = new Vector3(_newScale, _newScale, _newScale);
                        // Give the asteroid a new random rotation
                        Vector3 _newRotation = new Vector3(Random.Range(0, 360), Random.Range(0, 360), Random.Range(0, 360));
                        _asteroid.eulerAngles = _newRotation;
                        // Recalculate the distance since it has been relocated
                        //_distance = Vector3.Distance(_asteroid.position, _cacheTransform.position);					
                    }

                    // If fading of asteroids is enabled...
                    if (fadeAsteroids)
                    {
                        // ...and if distance of asteroid is greater than distance to fade...
                        if (_distance > _distanceToFade)
                        {
                            // ...and if the bool flag has been not been set...
                            if (!_asteroidsFading[i])
                            {
                                // Change the material of the asteroid to a transparent material that supports alpha fading
                                _asteroid.GetComponent<Renderer>().sharedMaterial = (Material)_materialsTransparent[_asteroidsMaterials[i]];
                                // Set the fading flag to true (we compare a Bool list instead of costly checking the material in each frame
                                _asteroidsFading[i] = true;
                            }
                            // Cache the color of the material

                            Color _col = _asteroid.GetComponent<Renderer>().material.color;
                            // Calculate the new alpha value between _distanceToFade (1.0) and _distanceToSpawn (0.0)
                            float _alpha = Mathf.Clamp01(1.0f - ((_distance - _distanceToFade) / (_distanceToSpawn - _distanceToFade)));
                            // Set the same color with the new alpha value to the asteroid
                            _asteroid.GetComponent<Renderer>().material.color = new Color(_col.r, _col.g, _col.b, _alpha);
                        }
                        else
                        {
                            // The asteroid is within range, and if the fading flag is still set...
                            if (_asteroidsFading[i])
                            {
                                // Change the material back to the original non-transparent material
                                _asteroid.GetComponent<Renderer>().material = (Material)_asteroidsMaterials[i];
                                // Set the fading falg to false to prevent material changes each frame
                                _asteroidsFading[i] = false;
                            }
                        }
                    }
                }
                else
                {
                    // Asteroid transform must have been destroyed for some reason (from another scriåt?), remove it from the lists
                    _asteroids.RemoveAt(i);
                    _asteroidsMaterials.RemoveAt(i);
                    _asteroidsFading.RemoveAt(i);
                }

                // 	If respawning is enabled and asteroid count is lower than Max Asteroids...		
                if (respawnDestroyedAsteroids && _asteroids.Count < maxAsteroids)
                {
                    // Spawn new asteroids (where true states that they are to be spawned at spawn distance rather than anywhere in range)
                    SpawnAsteroids(true);
                }
            }
        }

        /// <summary>
        /// Spawns the number of asteroids needed to reach maxAsteroids
        /// </summary>
        /// <param name='atSpawnDistance'>
        /// true = spawn on sphere at distanceSpawn * range (used for respawning asteroids)
        /// false = spawn in sphere within distanceSpawn * range (used for brand new asteroid fields)
        /// </param>
        void SpawnAsteroids(bool atSpawnDistance)
        {
            // Spawn new asteroids at a distance if count is below maxAsteroids (e.g. asteroids were destroyed outside of this script)
            while (_asteroids.Count < maxAsteroids)
            {
                // Select a random asteroid from the prefab array			
                Transform _newAsteroidPrefab = prefabAsteroids[Random.Range(0, prefabAsteroids.Length)];

                Vector3 _newPosition = Vector3.zero;
                if (atSpawnDistance)
                {
                    // Spawn asteroid at spawn distance (this is used for existing asteroid fields so it spawns out of visible range)
                    _newPosition = _cacheTransform.position + Random.onUnitSphere * _distanceToSpawn;
                }
                else
                {
                    // Spawn asteroid anywhere within range (this is used for new asteroid fields before it becomes visible)
                    _newPosition = _cacheTransform.position + Random.insideUnitSphere * _distanceToSpawn;
                }

                // Instantiate the new asteroid at a random location
                Transform _newAsteroid = (Transform)Instantiate(_newAsteroidPrefab, _newPosition, _cacheTransform.rotation);

                // Set a random material of the asteroid based on the weighted probabilty list
                switch (WeightedRandom(_materialList))
                {
                    case "VeryCommon":
                        _newAsteroid.GetComponent<Renderer>().sharedMaterial = materialVeryCommon[Random.Range(0, materialVeryCommon.Length)];
                        break;
                    case "Common":
                        _newAsteroid.GetComponent<Renderer>().sharedMaterial = materialCommon[Random.Range(0, materialCommon.Length)];
                        _newAsteroid.GetComponent<SA_Asteroid>().resourceCapacity = Random.Range(minAsteroidResources, maxAsteroidResources) * ResourcesMultiplier;
                        break;
                    case "Rare":
                        _newAsteroid.GetComponent<Renderer>().sharedMaterial = materialRare[Random.Range(0, materialRare.Length)];
                        _newAsteroid.GetComponent<SA_Asteroid>().resourceCapacity = Random.Range(minAsteroidResources, maxAsteroidResources) * ResourcesMultiplier / 2;
                        break;
                    case "VeryRare":
                        _newAsteroid.GetComponent<Renderer>().sharedMaterial = materialVeryRare[Random.Range(0, materialVeryRare.Length)];
                        _newAsteroid.GetComponent<SA_Asteroid>().resourceCapacity = Random.Range(minAsteroidResources, maxAsteroidResources) * ResourcesMultiplier / 4;
                        break;
                }

                // Add the asteroid to a list used to keep track of them
                _asteroids.Add(_newAsteroid);
                // A list to store which material each asteroid has
                _asteroidsMaterials.Add(_newAsteroid.GetComponent<Renderer>().sharedMaterial);
                // Improve performance by having a list of bool values whether or not asteroids are fading
                _asteroidsFading.Add(false);

                // If the asteroid has the Asteroid script attached to it...
                if (_newAsteroid.GetComponent<SA_Asteroid>() != null)
                {
                    // Set the mesh of the asteroid based on chosen polycount
                    _newAsteroid.GetComponent<SA_Asteroid>().SetPolyCount(polyCount);
                    // If the asteroid has a collider...
                    if (_newAsteroid.GetComponent<Collider>() != null)
                    {
                        _newAsteroid.GetComponent<SA_Asteroid>().SetPolyCount(polyCountCollider, true);
                    }
                }

                // Set scale of asteroid within min/max scale * scaleMultiplier
                float _newScale = Random.Range(minAsteroidScale, maxAsteroidScale) * scaleMultiplier;
                _newAsteroid.localScale = new Vector3(_newScale, _newScale, _newScale);

                // Set a random orientation of the asteroid			
                _newAsteroid.eulerAngles = new Vector3(Random.Range(0, 360), Random.Range(0, 360), Random.Range(0, 360));

                if (isRigidbody)
                {
                    // RIGIDBODY ASTEROIDS
                    // If the asteroid prefab has a rigidbody...
                    if (_newAsteroid.GetComponent<Rigidbody>() != null)
                    {
                        // Set the mass to mass specified in AsteroidField mutiplied by scale
                        _newAsteroid.GetComponent<Rigidbody>().mass = mass * _newScale;
                        // Set the velocity (speed) of the rigidbody to within the min/max velocity range multiplier by velocityMultiplier
                        _newAsteroid.GetComponent<Rigidbody>().velocity = _newAsteroid.transform.forward * Random.Range(minAsteroidVelocity, maxAsteroidVelocity) * velocityMultiplier;
                        // Set the angular velocity (rotational speed) of the rigidbody to within the min/max velocity range multiplier by velocityMultiplier
                        _newAsteroid.GetComponent<Rigidbody>().angularVelocity = new Vector3(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f)) * Random.Range(minAsteroidAngularVelocity, maxAsteroidAngularVelocity) * angularVelocityMultiplier;
                    }
                    else
                    {
                        Debug.LogWarning("AsteroidField is set to spawn rigidbody asterodids but one or more asteroid prefabs do not have rigidbody component attached.");
                    }
                }
                else
                {
                    // NON-RIGIDBODY ASTEROIDS

                    // If the asteroid prefab has a rigidbody...
                    if (_newAsteroid.GetComponent<Rigidbody>() != null)
                    {
                        // Destroy the rigidbody since the asteroid field is spawning non-rigidbody asteroids
                        Destroy(_newAsteroid.GetComponent<Rigidbody>());
                    }
                    // If the asteroid has the Asteroid script attached to it...
                    if (_newAsteroid.GetComponent<SA_Asteroid>() != null)
                    {
                        // Set rotation and drift axis and speed				
                        _newAsteroid.GetComponent<SA_Asteroid>().rotationSpeed = Random.Range(minAsteroidRotationSpeed, maxAsteroidRotationSpeed);
                        _newAsteroid.GetComponent<SA_Asteroid>().rotationalAxis = new Vector3(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f));
                        _newAsteroid.GetComponent<SA_Asteroid>().driftSpeed = Random.Range(minAsteroidDriftSpeed, maxAsteroidDriftSpeed);
                        _newAsteroid.GetComponent<SA_Asteroid>().driftAxis = new Vector3(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f));
                    }

                }
            }

        }

        // Internal function to create transparent material counterparts for fading
        void CreateTransparentMaterial(Material[] _sourceMaterials, Hashtable _ht)
        {
            foreach (Material _mat in _sourceMaterials)
            {
                _ht.Add(_mat, new Material(Shader.Find("Transparent/Diffuse")));
                ((Material)_ht[_mat]).SetTexture("_MainTex", _mat.GetTexture("_MainTex"));
                ((Material)_ht[_mat]).color = _mat.color;
            }
        }

        // Internal function to allow weighted random selection of materials
        static T WeightedRandom<T>(SortedList<int, T> _list)
        {
            int _max = _list.Keys[_list.Keys.Count - 1];
            int _random = Random.Range(0, _max);
            foreach (int _key in _list.Keys)
            {
                if (_random <= _key)
                {
                    return _list[_key];
                }
            }
            return default(T);
        }
    }
}
