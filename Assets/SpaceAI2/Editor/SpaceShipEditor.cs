using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpaceAI2.Editor
{
    [CustomEditor(typeof(SpaceShip)), CanEditMultipleObjects]
    internal class SpaceShipEditor : UnityEditor.Editor
    {
        private static readonly string[] EventFields = { "onDestroyed", "onDamaged", "onTargetAcquired", "onMissileLaunched" };
        private static bool eventsOpen;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("faction"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("team"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);
            SerializedProperty profile = serializedObject.FindProperty("profile");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(profile);
            using (new EditorGUI.DisabledScope(profile.objectReferenceValue == null && !profile.hasMultipleDifferentValues))
            {
                SerializedProperty control = serializedObject.FindProperty("control");
                EditorGUILayout.PropertyField(control);
                if (control.enumValueIndex == (int)ShipControl.AI || control.hasMultipleDifferentValues)
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("pilot"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("weapon"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("missile"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("destructible"));
            }
            bool setupChanged = EditorGUI.EndChangeCheck();

            if (profile.objectReferenceValue == null && !profile.hasMultipleDifferentValues)
                EditorGUILayout.HelpBox("Assign a ship profile and the ship adds its motor, collider, health, gun, missiles and pilot itself. " +
                                        "Without one, set those components up by hand.", MessageType.Info);
            else if (GUILayout.Button("Apply setup")) setupChanged = true;

            EditorGUILayout.Space();
            eventsOpen = EditorGUILayout.Foldout(eventsOpen, "Events", true);
            if (eventsOpen)
                foreach (string field in EventFields)
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(field));

            serializedObject.ApplyModifiedProperties();
            if (setupChanged && !Application.isPlaying)
                foreach (Object target in targets) Apply((SpaceShip)target);
        }

        /// <summary>Runs the ship's setup with undo for the components it adds.</summary>
        public static void Apply(SpaceShip ship)
        {
            if (ship.Profile == null) return;
            GameObject go = ship.gameObject;
            Component[] existing = go.GetComponents<Component>();
            var before = new HashSet<Component>(existing);
            Undo.RecordObjects(existing, "Apply ship setup");
            ship.ApplySetup();
            foreach (Component component in go.GetComponents<Component>())
            {
                if (before.Contains(component)) EditorUtility.SetDirty(component);
                else Undo.RegisterCreatedObjectUndo(component, "Apply ship setup");
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(ship);
        }
    }
}
