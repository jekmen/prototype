using UnityEditor;

namespace SpaceAI2.Editor
{
    [CustomEditor(typeof(ShipProfile))]
    public class ShipProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var profile = (ShipProfile)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Derived", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                $"Turn radius: {profile.TurnRadius(profile.MinSpeed):0} m min, " +
                $"{profile.TurnRadius(profile.CruiseSpeed):0} m cruise, " +
                $"{profile.TurnRadius(profile.MaxSpeed):0} m max, " +
                $"{profile.TurnRadius(profile.BoostSpeed) / profile.BoostTurnFactor:0} m boost\n" +
                $"180° turn: {profile.HalfTurnTime:0.0} s, full roll: {360f / profile.RollRate:0.0} s\n" +
                $"Cruise to max: {(profile.MaxSpeed - profile.CruiseSpeed) / profile.Acceleration:0.0} s, " +
                $"max to min: {(profile.MaxSpeed - profile.MinSpeed) / profile.Deceleration:0.0} s",
                MessageType.None);
        }
    }
}
