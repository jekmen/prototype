using UnityEngine;

namespace SpaceAI2
{
    /// <summary>
    /// Keeps this object (the player ship or the camera rig) near the world origin: once it is farther than
    /// <see cref="threshold"/>, the whole world shifts back under it. Floats stay precise however far the fight goes.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class FloatingOrigin : MonoBehaviour
    {
        [Tooltip("Distance from the origin in metres that triggers a shift.")]
        [SerializeField, Min(100f)] private float threshold = 5000f;

        public float Threshold
        {
            get => threshold;
            set => threshold = Mathf.Max(100f, value);
        }

        public int Shifts { get; private set; }

        // Before the physics step and before any system reads positions this step.
        private void FixedUpdate()
        {
            Vector3 position = transform.position;
            if (position.sqrMagnitude < threshold * threshold) return;
            SpaceAI.ShiftOrigin(position);
            Shifts++;
        }
    }
}
