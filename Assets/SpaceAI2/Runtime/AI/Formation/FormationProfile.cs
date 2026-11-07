using UnityEngine;

namespace SpaceAI2
{
    public enum FormationShape { Wedge, Echelon, LineAbreast, Diamond, Column, Custom }

    [CreateAssetMenu(menuName = "SpaceAI2/Formation Profile", fileName = "FormationProfile")]
    public class FormationProfile : ScriptableObject
    {
        public FormationShape Shape = FormationShape.Wedge;
        [Min(5f)] public float Spacing = 40f;
        [Tooltip("Slot offsets in the leader's space, meters, for the Custom shape. Wingmen past the list fall back to a wedge.")]
        public Vector3[] CustomSlots = new Vector3[0];

        [Header("Keeping station")]
        [Tooltip("A wingman is back in formation within this distance of its slot.")]
        [Min(1f)] public float JoinDistance = 25f;
        [Tooltip("A wingman has broken off beyond this distance from its slot.")]
        [Min(1f)] public float BreakDistance = 80f;
        [Tooltip("Expected seconds from breaking off to rejoining. Demo stats compare against it.")]
        [Min(0.1f)] public float RejoinTime = 10f;
        [Tooltip("Leader throttle while the formation holds, so wingmen keep speed in reserve.")]
        [Range(-1f, 1f)] public float LeaderThrottle = 0f;
        [Tooltip("Leader throttle while a wingman is out of formation.")]
        [Range(-1f, 1f)] public float WaitThrottle = -0.4f;

        /// <summary>Offset of a wingman's slot in the leader's space. Index 0 is the first wingman.</summary>
        public Vector3 Slot(int index)
        {
            if (Shape == FormationShape.Custom && index < CustomSlots.Length) return CustomSlots[index];
            float s = Spacing;
            switch (Shape)
            {
                case FormationShape.Echelon:
                    return new Vector3(s, 0f, -s) * (index + 1);
                case FormationShape.LineAbreast:
                    return new Vector3(Side(index) * (index / 2 + 1) * s, 0f, 0f);
                case FormationShape.Column:
                    return new Vector3(0f, 0f, -s * (index + 1));
                case FormationShape.Diamond:
                    if (index < 2) return new Vector3(Side(index) * s, 0f, -s);
                    if (index == 2) return new Vector3(0f, 0f, -2f * s);
                    return Wedge(index + 1, s);
                default:
                    return Wedge(index, s);
            }
        }

        private static float Side(int index) => index % 2 == 0 ? -1f : 1f;

        private static Vector3 Wedge(int index, float spacing)
        {
            int row = index / 2 + 1;
            return new Vector3(Side(index) * row * spacing, 0f, -row * spacing);
        }
    }
}
