using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI2
{
    public enum Stance
    {
        Hostile,
        Neutral,
        Friendly
    }

    /// <summary>What a faction does when a ship that is not its enemy damages one of its ships.</summary>
    public enum AttackResponse
    {
        /// <summary>Nothing changes.</summary>
        Ignore,
        /// <summary>The faction's ships treat that one ship as an enemy until it is destroyed or forgiven.</summary>
        AttackerShip,
        /// <summary>The faction turns hostile to the attacker's whole faction.</summary>
        AttackerFaction
    }

    [Serializable]
    public struct FactionRelation
    {
        public Faction Faction;
        public Stance Stance;
    }

    /// <summary>
    /// A side in the world. Ships of one faction never fight each other; how they treat other factions is set here
    /// as the starting stance and changed at runtime through <see cref="Factions"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "SpaceAI2/Faction", fileName = "Faction")]
    public class Faction : ScriptableObject
    {
        [Tooltip("Shield tint and debug colour for its ships.")]
        public Color Color = new Color(0.3f, 0.7f, 1f);
        [Tooltip("Stance toward factions not listed below.")]
        public Stance DefaultStance = Stance.Hostile;
        [Tooltip("Starting stance toward particular factions. Relations are mutual: when two factions list each other differently, the less friendly stance wins.")]
        public List<FactionRelation> Relations = new List<FactionRelation>();
        [Tooltip("What happens when a neutral ship damages one of ours.")]
        public AttackResponse WhenAttacked = AttackResponse.AttackerShip;
        [Tooltip("Apply the response to friendly ships too; off lets allies' stray shots pass.")]
        public bool RespondToFriendlyFire;

        [NonSerialized] internal int RuntimeIndex;
        [NonSerialized] internal int RuntimeGeneration;

        public Stance StartingStanceToward(Faction other)
        {
            foreach (FactionRelation relation in Relations)
            {
                if (relation.Faction == other) return relation.Stance;
            }
            return DefaultStance;
        }
    }
}
