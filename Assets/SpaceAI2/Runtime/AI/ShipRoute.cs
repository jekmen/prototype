using UnityEngine;

namespace SpaceAI2
{
    /// <summary>
    /// A path for AI ships: the child transforms in order are its waypoints. One route can be shared by any number
    /// of ships; give it to a <see cref="CombatBrain"/> in the inspector or through <see cref="CombatBrain.FollowRoute(ShipRoute)"/>.
    /// </summary>
    [AddComponentMenu("SpaceAI2/Ship Route")]
    public class ShipRoute : MonoBehaviour
    {
        [Tooltip("Fly back to the first waypoint after the last one; otherwise the order completes at the last one.")]
        public bool Loop = true;
        [Tooltip("A waypoint counts as reached inside this distance.")]
        [Min(10f)] public float ArriveRadius = 200f;

        public int Count => transform.childCount;

        public Vector3 GetPoint(int index) => transform.GetChild(index).position;

        public int Nearest(Vector3 position)
        {
            int best = 0;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < Count; i++)
            {
                float distance = (GetPoint(i) - position).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = i;
            }
            return best;
        }

        private void OnDrawGizmos()
        {
            int count = Count;
            if (count == 0) return;
            Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.8f);
            for (int i = 0; i < count; i++)
            {
                Vector3 point = GetPoint(i);
                Gizmos.DrawWireSphere(point, ArriveRadius * 0.25f);
                if (i + 1 < count) Gizmos.DrawLine(point, GetPoint(i + 1));
                else if (Loop && count > 2) Gizmos.DrawLine(point, GetPoint(0));
            }
        }
    }
}
