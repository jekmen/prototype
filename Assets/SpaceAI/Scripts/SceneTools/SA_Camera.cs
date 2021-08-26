using UnityEngine;

namespace SpaceAI.ScaneTools
{
    /// <summary>
    /// This is just for preview you can remove this class
    /// </summary>
    public class SA_Camera : MonoBehaviour
    {
        public enum FollowMode { CHASE, SPECTATOR }
        public FollowMode followMode = FollowMode.SPECTATOR;
        public Transform target;
        public float distance = 60.0f;
        public float chaseHeight = 15.0f;
        public float followDamping = 0.3f;
        public float lookAtDamping = 4.0f;
        public KeyCode freezeKey = KeyCode.None;
        public bool swichTargets = false;

        private float t;
        private int index;
        private float camZoomSpeed = 15f;
        private Transform _cacheTransform;

        void Start()
        {
            t = Time.time;
            _cacheTransform = transform;
        }

        void FixedUpdate()
        {
            distance += Input.GetAxis("Mouse ScrollWheel") * camZoomSpeed;
            DoCamera();
        }

        void DoCamera()
        {
            if (Input.GetKeyDown(KeyCode.N))
            {
                index = (index + 1) % SA_Manager.instance.SharedTargets.Count;
                target = SA_Manager.instance.SharedTargets[index].transform;
            }

            if (swichTargets && Time.time > t + Random.Range(15, 30))
            {
                t = Time.time;

                if (SA_Manager.instance)
                {
                    Transform newTarget = SA_Manager.instance.SharedTargets[Random.Range(0, SA_Manager.instance.SharedTargets.Count)].transform;

                    if (newTarget && newTarget.gameObject.activeSelf)
                    {
                        target = newTarget;
                    }
                }
            }

            if (target == null) return;

            Quaternion _lookAt;


            switch (followMode)
            {
                case FollowMode.SPECTATOR:
                    _lookAt = Quaternion.LookRotation(target.position - _cacheTransform.position);
                    _cacheTransform.rotation = Quaternion.Lerp(_cacheTransform.rotation, _lookAt, Time.deltaTime * lookAtDamping);
                    if (!Input.GetKey(freezeKey))
                    {
                        if (Vector3.Distance(_cacheTransform.position, target.position) > distance)
                        {
                            _cacheTransform.position = Vector3.Lerp(_cacheTransform.position, target.position, Time.deltaTime * followDamping);
                        }
                    }
                    break;
                case FollowMode.CHASE:

                    if (!Input.GetKey(freezeKey))
                    {
                        _lookAt = target.rotation;
                        _cacheTransform.rotation = Quaternion.Lerp(_cacheTransform.rotation, _lookAt, Time.deltaTime * lookAtDamping);
                        _cacheTransform.position = Vector3.Lerp(_cacheTransform.position, target.position - target.forward * distance + target.up * chaseHeight, Time.deltaTime * followDamping * 10);
                    }
                    break;
            }
        }

        public void AutoSwich()
        {
            if (swichTargets)
            {
                swichTargets = false;
            }
            else
            {
                swichTargets = true;
            }
        }
    }
}
