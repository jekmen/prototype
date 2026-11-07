namespace SpaceAI2
{
    /// <summary>Decision layer on the pilot's object. Called at the start of every pilot tick to issue commands.</summary>
    public interface IPilotBrain
    {
        void Think(AIPilot pilot, float dt);
    }
}
