using UnityEngine;

namespace SpaceAI2.Demo
{
    public class ChaseCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 4f, -16f);
        [SerializeField, Min(0.01f)] private float rotationSharpness = 6f;
        [SerializeField] private float lookAhead = 30f;

        [Header("Cockpit")]
        [SerializeField] private Transform cockpit;
        [SerializeField] private bool inCockpit;
        [SerializeField] private KeyCode toggleView = KeyCode.C;
        [SerializeField, Min(0.01f)] private float cockpitNearClip = 0.05f;
        [SerializeField, Range(30f, 110f)] private float cockpitFieldOfView = 70f;

        private Quaternion smoothedRotation = Quaternion.identity;
        private Transform smoothedFor;
        private Camera view;
        private float chaseNearClip;
        private float chaseFieldOfView;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        public Vector3 Offset
        {
            get => offset;
            set => offset = value;
        }

        /// <summary>Eye point inside the target's cockpit; null disables the cockpit view.</summary>
        public Transform Cockpit
        {
            get => cockpit;
            set => cockpit = value;
        }

        public bool InCockpit => inCockpit && cockpit != null;

        private void Awake()
        {
            view = GetComponent<Camera>();
            if (view == null) return;
            chaseNearClip = view.nearClipPlane;
            chaseFieldOfView = view.fieldOfView;
        }

        private void LateUpdate()
        {
            if (UnityEngine.Input.GetKeyDown(toggleView)) inCockpit = !inCockpit;
            if (view != null)
            {
                view.nearClipPlane = InCockpit ? cockpitNearClip : chaseNearClip;
                view.fieldOfView = InCockpit ? cockpitFieldOfView : chaseFieldOfView;
            }
            if (InCockpit)
            {
                transform.SetPositionAndRotation(cockpit.position, cockpit.rotation);
                return;
            }
            if (target == null) return;
            if (smoothedFor != target)
            {
                smoothedFor = target;
                smoothedRotation = target.rotation;
            }

            // Position is derived from the target every frame; smoothing world position lags by v * dt and jitters with uneven frame times.
            smoothedRotation = Quaternion.Slerp(smoothedRotation, target.rotation, 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));
            transform.position = target.position + smoothedRotation * offset;

            Vector3 focus = target.position + target.forward * lookAhead;
            transform.rotation = Quaternion.LookRotation(focus - transform.position, smoothedRotation * Vector3.up);
        }
    }
}
