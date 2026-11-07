using UnityEngine;

namespace SpaceAI2.Demo
{
    public class ChaseCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 4f, -16f);
        [SerializeField, Min(0.01f)] private float rotationSharpness = 6f;
        [SerializeField] private float lookAhead = 30f;

        private Quaternion smoothedRotation = Quaternion.identity;
        private Transform smoothedFor;

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

        private void LateUpdate()
        {
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
