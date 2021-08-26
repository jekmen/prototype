namespace SpaceAI.Ship
{
    /// <summary>
    /// Target identifier
    /// </summary>
    public enum GroupType
    {
        Enemy,
        Player
        // Add more here
    }

    public interface IShip
    {
        /// <summary>
        /// returns target identifier
        /// </summary>
        /// <returns GroupType value></returns>
        GroupType Ship();
    }
}