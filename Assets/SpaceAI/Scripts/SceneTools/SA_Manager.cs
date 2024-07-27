namespace SpaceAI.SceneTools
{
    using SpaceAI.Events;
    using SpaceAI.Ship;
    using SpaceAI.WeaponSystem;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    public class SA_Manager : MonoBehaviour
    {
        [Serializable]
        public class Pool
        {
            public int id;
            public List<SA_DamageSandler> bullet = new();
        }

        public GameObject[] shipPrefabs;
        private List<GameObject> storedObjs = new();
        public int shipCount;
        public float initTime;
        private int m_count;
        private float t;
        private int i = 0;

        public List<SA_IShip> SharedTargets { get; set; } = new List<SA_IShip>();
        public static List<Pool> BulletPool { get; set; } = new List<Pool>();

        private void Awake()
        {
            m_count = shipCount;
        }

        private void Start()
        {
            t = Time.time;
        }

        private void OnEnable()
        {
            SA_EventsBus.AddEventListener<SA_TurretTargetRequestEvent>(OnTurretTargetRequestEvent);
            SA_EventsBus.AddEventListener<SA_ShipRegistryEvent>(OnShipRegistryUpdate);
            SA_EventsBus.AddEventListener<SA_ShipUnRegisterEvent>(OnShipUnRegisterUpdate);
            SA_EventsBus.AddEventListener<SA_ShipTargetRequesEvent>(OnShipTargetUpdate);
        }

        private void OnDisable()
        {
            SA_EventsBus.RemoveEventListener<SA_TurretTargetRequestEvent>(OnTurretTargetRequestEvent);
            SA_EventsBus.RemoveEventListener<SA_ShipRegistryEvent>(OnShipRegistryUpdate);
            SA_EventsBus.RemoveEventListener<SA_ShipUnRegisterEvent>(OnShipUnRegisterUpdate);
            SA_EventsBus.RemoveEventListener<SA_ShipTargetRequesEvent>(OnShipTargetUpdate);
        }

        private void OnTurretTargetRequestEvent(SA_TurretTargetRequestEvent e)
        {
            e.Owner.Target = GetTarget(e.TurretPosition, e.RequestedTargets, e.Range);
        }

        private void OnShipTargetUpdate(SA_ShipTargetRequesEvent e)
        {
            GetTarget(e.Ship, e.ScanRange);
        }

        private void OnShipUnRegisterUpdate(SA_ShipUnRegisterEvent e)
        {
            UnSubscribe(e.Ship);
        }

        private void OnShipRegistryUpdate(SA_ShipRegistryEvent e)
        {
            Subscribe(e.Ship);
        }

        private void Update()
        {
            if (m_count == storedObjs.Count) return;

            if (storedObjs != null && shipPrefabs.Length > 0)
            {
                if (Time.time > t + initTime)
                {
                    t = Time.time;

                    if (m_count > storedObjs.Count)
                    {
                        GameObject o = Instantiate(shipPrefabs[(i += 1) % shipPrefabs.Length], new Vector3(UnityEngine.Random.Range(-1000, 1000), UnityEngine.Random.Range(-1000, 1000), UnityEngine.Random.Range(-1000, 1000)), Quaternion.identity);
                        storedObjs.Add(o);
                    }
                }

                storedObjs = storedObjs.Where(x => x != null).Distinct().ToList();
            }
            else
            {
                return;
            }
        }

        /// <summary>
        /// Remove object from list and scene
        /// </summary>
        /// <param name="shipObj"></param>
        /// <param name="deactivate"></param>
        public void UnSubscribe(SA_IShip shipObj, bool deactivate = false)
        {
            if (deactivate)
            {
                //TODO: Make ship gameObject active = false;
            }
            else
            {
                SharedTargets.Remove(shipObj);
                SharedTargets = SharedTargets.Where(x => x != null).Distinct().ToList();
                var ship = shipObj as Component;
                Destroy(ship.gameObject);
            }
        }

        /// <summary>
        /// Subscribe object
        /// </summary>
        /// <param name="shipObj"></param>
        public void Subscribe(SA_IShip shipObj)
        {
            if (!SharedTargets.Contains(shipObj))
            {
                SharedTargets.Add(shipObj);
            }
        }

        /// <summary>
        /// Basic target serch filter
        /// </summary>
        /// <param name="shipController"></param>
        /// <param name="scanRange"></param>
        /// <returns></returns>
        public bool GetTarget(SA_IShip shipController, float scanRange)
        {
            if (!shipController.CurrentEnemy && SharedTargets != null)
            {
                for (int i = 0; i < SharedTargets.Count; i++)
                {
                    for (int EnumMemberCount = 0; EnumMemberCount < shipController.ShipConfiguration.AIConfig.GroupTypesToAction.Length; EnumMemberCount++)
                    {
                        SA_IShip tar = SharedTargets[UnityEngine.Random.Range(0, SharedTargets.Count)];

                        if (tar.Ship() == shipController.ShipConfiguration.AIConfig.GroupTypesToAction[EnumMemberCount])
                        {
                            Component targetObject = tar as Component;

                            if (Vector3.Distance(shipController.CurrentShipTransform.position, targetObject.transform.position) < scanRange)
                            {
                                shipController.SetCurrentEnemy(targetObject.gameObject);
                                shipController.SetTarget(targetObject.gameObject);
                                shipController.CanFollowTarget(true);
                                return true;
                            }
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }

            return false;
        }

        public GameObject GetTarget(Transform ownerPos, GroupType[] groupTypes, float scanRange)
        {
            GameObject closestTarget = null;
            float shortestDistance = scanRange;

            foreach (var sharedTarget in SharedTargets)
            {
                foreach (var groupType in groupTypes)
                {
                    if (sharedTarget.Ship() == groupType)
                    {
                        Component targetObject = sharedTarget as Component;
                        float distance = Vector3.Distance(ownerPos.position, targetObject.transform.position);

                        if (distance < shortestDistance)
                        {
                            closestTarget = targetObject.gameObject;
                            shortestDistance = distance;
                        }
                    }
                }
            }

            return closestTarget;
        }
    }
}