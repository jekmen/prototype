namespace SpaceAI.WeaponSystem
{
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(SA_Turret))]
    [CanEditMultipleObjects]
    public class SA_TurretEditor : Editor
    {
        private const float ArcSize = 10.0f;

        public override void OnInspectorGUI()
        {
            SA_Turret turret = (SA_Turret)target;

            DrawDefaultInspector();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(new GUIContent("Clear Transforms", "Sets the \"Turret Base\" and \"Turret Barrels\" references to None.")))
            {
                turret.ClearTransforms();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void OnSceneGUI()
        {
            SA_Turret turret = (SA_Turret)target;
            Transform transform = turret.transform;

            // Don't show turret arcs when playing, because they won't be correct.
            if (turret.showArcs && !Application.isPlaying)
            {
                if (turret.turretBarrels != null)
                {
                    // Traverse
                    Handles.color = new Color(1.0f, 0.5f, 0.5f, 0.1f);

                    foreach (var turrBarrel in turret.turretBarrels)
                    {
                        if (turret.limitTraverse)
                        {
                            Handles.DrawSolidArc(turrBarrel.position, turrBarrel.up, turrBarrel.forward, turret.rightTraverse, ArcSize);
                            Handles.DrawSolidArc(turrBarrel.position, turrBarrel.up, turrBarrel.forward, -turret.leftTraverse, ArcSize);
                        }
                        else
                        {
                            Handles.DrawSolidArc(turrBarrel.position, turrBarrel.up, turrBarrel.forward, 360.0f, ArcSize);
                        }

                        // Elevation
                        Handles.color = new Color(0.5f, 1.0f, 0.5f, 0.1f);
                        Handles.DrawSolidArc(turrBarrel.position, turrBarrel.right, turrBarrel.forward, -turret.elevation, ArcSize);

                        // Depression
                        Handles.color = new Color(0.5f, 0.5f, 1.0f, 0.1f);
                        Handles.DrawSolidArc(turrBarrel.position, turrBarrel.right, turrBarrel.forward, turret.depression, ArcSize);
                    }

                }
                else
                {
                    Handles.color = new Color(1.0f, 0.5f, 0.5f, 0.1f);
                    Handles.DrawSolidArc(transform.position, transform.up, transform.forward, turret.leftTraverse, ArcSize);
                    Handles.DrawSolidArc(transform.position, transform.up, transform.forward, -turret.leftTraverse, ArcSize);

                    Handles.color = new Color(0.5f, 1.0f, 0.5f, 0.1f);
                    Handles.DrawSolidArc(transform.position, transform.right, transform.forward, -turret.elevation, ArcSize);

                    Handles.color = new Color(0.5f, 0.5f, 1.0f, 0.1f);
                    Handles.DrawSolidArc(transform.position, transform.right, transform.forward, turret.depression, ArcSize);
                }
            }
        }
    }
}
