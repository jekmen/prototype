using UnityEngine;

namespace SpaceAI2.Demo
{
    public class ChaseCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 4f, -16f);
        [SerializeField, Min(0.01f)] private float positionSharpness = 12f;
        [SerializeField, Min(0.01f)] private float rotationSharpness = 8f;
        [SerializeField] private float lookAhead = 30f;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;

            Vector3 desired = target.TransformPoint(offset);
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-positionSharpness * dt));

            Vector3 focus = target.position + target.forward * lookAhead;
            Quaternion look = Quaternion.LookRotation(focus - transform.position, target.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-rotationSharpness * dt));
        }
    }
}
