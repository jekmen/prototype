using UnityEngine;

namespace SpaceAI2
{
    internal static class EffectsMeshes
    {
        public static Mesh Sphere(int segments, int rings)
        {
            var vertices = new Vector3[(segments + 1) * (rings + 1)];
            var normals = new Vector3[vertices.Length];
            var indices = new int[segments * rings * 6];
            int at = 0;
            for (int y = 0; y <= rings; y++)
            {
                float latitude = Mathf.PI * y / rings;
                for (int x = 0; x <= segments; x++)
                {
                    float longitude = 2 * Mathf.PI * x / segments;
                    int i = y * (segments + 1) + x;
                    normals[i] = new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude));
                    vertices[i] = normals[i] * 0.5f;
                    if (y == rings || x == segments) continue;
                    int below = i + segments + 1;
                    indices[at++] = i; indices[at++] = i + 1; indices[at++] = below;
                    indices[at++] = i + 1; indices[at++] = below + 1; indices[at++] = below;
                }
            }
            var mesh = new Mesh { name = "Effect sphere", vertices = vertices, normals = normals, triangles = indices };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
