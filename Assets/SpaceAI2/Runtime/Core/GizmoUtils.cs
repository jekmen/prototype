using UnityEngine;

namespace SpaceAI2
{
    internal static class GizmoUtils
    {
        /// <summary>Circle around <paramref name="normal"/>, starting from the direction of <paramref name="from"/>.</summary>
        public static void DrawCircle(Vector3 center, Vector3 normal, Vector3 from, float radius, int segments = 48)
        {
            Vector3 axis = Vector3.ProjectOnPlane(from, normal).normalized * radius;
            DrawArc(center, normal, axis, 360f, segments);
        }

        /// <summary>Arc of <paramref name="angle"/> degrees from <paramref name="start"/> (a radius vector) around <paramref name="normal"/>.</summary>
        public static void DrawArc(Vector3 center, Vector3 normal, Vector3 start, float angle, int segments = 32)
        {
            Vector3 previous = center + start;
            for (int i = 1; i <= segments; i++)
            {
                Vector3 next = center + Quaternion.AngleAxis(angle * i / segments, normal) * start;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
