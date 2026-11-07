namespace SpaceAI2
{
    /// <summary>Which part of a large ship fighters go for.</summary>
    public enum TargetPriority
    {
        /// <summary>Weighs everything: point defense and turrets firing at friends first, then engines.</summary>
        Utility,
        /// <summary>Point defense first, to open the way for torpedoes.</summary>
        PointDefense,
        /// <summary>Any turret, those firing at friends first.</summary>
        Turrets,
        /// <summary>Engines first, to leave the ship drifting.</summary>
        Engines,
        /// <summary>No subsystem: shoot the hull.</summary>
        Hull
    }
}
