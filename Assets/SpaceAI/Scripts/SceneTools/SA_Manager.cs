using SpaceAI.Ship;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaceAI.ScaneTools
{
    public class SA_Manager : MonoBehaviour
    {
        public static SA_Manager instance;

        [Serializable]
        public class Pool
        {
            public int id;
            public List<GameObject> bullet = new List<GameObject>();
        }

        public GameObject[] shipPrefabs;
        private List<GameObject> storedObjs = new List<GameObject>();
        public int shipCount;
        public float initTime;
        private int m_count;
        private float t;
        private int i = 0;

        public List<GameObject> SharedTargets { get; set; } = new List<GameObject>();
        public List<Pool> BulletPool { get; set; } = new List<Pool>();

        private void Awake()
        {
            m_count = shipCount;
            instance = this;
        }

        private void Start()
        {
            t = Time.time;
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
                        GameObject o = Instantiate(shipPrefabs[(i += 1) % shipPrefabs.Length], new Vector3(UnityEngine.Random.Range(-100, 100), UnityEngine.Random.Range(-100, 100), UnityEngine.Random.Range(-100, 100)), Quaternion.identity);
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
        public void UnSubscribe(GameObject shipObj, bool deactivate = false)
        {
            if (deactivate)
            {
                //TODO: Make ship gameObject active = false;
            }
            else
            {
                SharedTargets.Remove(shipObj);
                SharedTargets = SharedTargets.Where(x => x != null).Distinct().ToList();
                Destroy(shipObj);
            }
        }

        /// <summary>
        /// Subscribe object
        /// </summary>
        /// <param name="shipObj"></param>
        public void Subscribe(GameObject shipObj)
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
        public bool GetTarget(SA_ShipController shipController, float scanRange)
        {
            if (!shipController.EnemyTarget && SharedTargets != null)
            {
                for (int i = 0; i < SharedTargets.Count; i++)
                {
                    for (int EnumMemberCount = 0; EnumMemberCount < shipController.Configuration.AIConfig.groupTypesToAction.Length; EnumMemberCount++)
                    {
                        if (SharedTargets[i] && SharedTargets[i].GetComponent(typeof(IShip)))
                        {
                            IShip tar = SharedTargets[UnityEngine.Random.Range(0, SharedTargets.Count)].GetComponent(typeof(IShip)) as IShip;

                            if (tar.Ship() == shipController.Configuration.AIConfig.groupTypesToAction[EnumMemberCount])
                            {
                                Component targetObject = tar as Component;

                                if (Vector3.Distance(shipController.transform.position, targetObject.transform.position) < scanRange)
                                {
                                    shipController.EnemyTarget = targetObject.gameObject;
                                    shipController.SetTarget(targetObject.gameObject);
                                    shipController.FollowTarget = true;
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
            }

            return false;
        }
    }
}