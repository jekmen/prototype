using UnityEngine;

namespace SpaceAI2
{
    public enum ProbeSide { Up, Down, Left, Right }

    public struct ProbeSetup
    {
        public Vector3 Origin;
        public Vector3 Forward;
        public Vector3 Up;
        public Vector3 Right;
        public float Radius;
        public float Range;

        public Vector3 SideDirection(ProbeSide side) => side switch
        {
            ProbeSide.Up => Up,
            ProbeSide.Down => -Up,
            ProbeSide.Left => -Right,
            _ => Right
        };
    }

    public struct ObstacleReading
    {
        public ProbeSetup Probe;
        public bool Blocked;
        public float Distance;
        public Vector3 Normal;
        public float FreeUp;
        public float FreeDown;
        public float FreeLeft;
        public float FreeRight;

        public float Free(ProbeSide side) => side switch
        {
            ProbeSide.Up => FreeUp,
            ProbeSide.Down => FreeDown,
            ProbeSide.Left => FreeLeft,
            _ => FreeRight
        };
    }
}
