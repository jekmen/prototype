using SpaceAI.Events;
using SpaceAI.Ship;
using SpaceAI.WeaponSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaceAI.ScaneTools
{
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

        public List<IShip> SharedTargets { get; set; } = new List<IShip>();
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
            EventsBus.AddEventListener<ShipRegistryEvent>(OnShipRegistryUpdate);
            EventsBus.AddEventListener<ShipUnRegisterEvent>(OnShipUnRegisterUpdate);
            EventsBus.AddEventListener<ShipTargetRequesEvent>(OnShipTargetUpdate);
        }

        private void OnDisable()
        {
            EventsBus.RemoveEventListener<ShipRegistryEvent>(OnShipRegistryUpdate);
            EventsBus.RemoveEventListener<ShipUnRegisterEvent>(OnShipUnRegisterUpdate);
            EventsBus.RemoveEventListener<ShipTargetRequesEvent>(OnShipTargetUpdate);
        }

        private void OnShipTargetUpdate(ShipTargetRequesEvent e)
        {
            GetTarget(e.Ship, e.ScanRange);
        }

        private void OnShipUnRegisterUpdate(ShipUnRegisterEvent e)
        {
            UnSubscribe(e.Ship);
        }

        private void OnShipRegistryUpdate(ShipRegistryEvent e)
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
        public void UnSubscribe(IShip shipObj, bool deactivate = false)
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
        public void Subscribe(IShip shipObj)
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
        public bool GetTarget(IShip shipController, float scanRange)
        {
            if (!shipController.CurrentEnemy && SharedTargets != null)
            {
                for (int i = 0; i < SharedTargets.Count; i++)
                {
                    for (int EnumMemberCount = 0; EnumMemberCount < shipController.ShipConfiguration.AIConfig.GroupTypesToAction.Length; EnumMemberCount++)
                    {
                        IShip tar = SharedTargets[UnityEngine.Random.Range(0, SharedTargets.Count)];

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
    }
}