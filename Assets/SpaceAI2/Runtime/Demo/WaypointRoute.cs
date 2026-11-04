using UnityEngine;

namespace SpaceAI2.Demo
{
    [RequireComponent(typeof(AIPilot))]
    public class WaypointRoute : MonoBehaviour
    {
        [SerializeField] private Transform[] points;
        [SerializeField] private bool randomOrder = true;

        private AIPilot pilot;
        private int index = -1;

        public Transform[] Points
        {
            get => points;
            set => points = value;
        }

        private void Awake() => pilot = GetComponent<AIPilot>();

        private void OnEnable() => pilot.Arrived += OnArrived;

        private void OnDisable() => pilot.Arrived -= OnArrived;

        private void Start() => Next();

        private void OnArrived(AIPilot _) => Next();

        private void Next()
        {
            if (points == null || points.Length == 0) return;
            if (randomOrder && points.Length > 1)
            {
                int next = Random.Range(0, points.Length - 1);
                index = next >= index ? next + 1 : next;
            }
            else
            {
                index = (index + 1) % points.Length;
            }
            pilot.FlyTo(points[index]);
        }
    }
}
