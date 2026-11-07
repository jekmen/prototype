using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI2
{
    /// <summary>
    /// Runtime relations between factions. Each pair of factions has a reputation from -100 to 100 that sets its
    /// stance: hostile at <see cref="HostileBelow"/> or less, friendly at <see cref="FriendlyAbove"/> or more,
    /// neutral between. Starting values come from the <see cref="Faction"/> assets. Ships without a faction fall back
    /// to <see cref="SpaceShip.Team"/>: different teams fight.
    /// </summary>
    public static class Factions
    {
        public const float HostileBelow = -30f;
        public const float FriendlyAbove = 30f;

        /// <summary>Raised when the stance between two factions changes: the factions and the new stance.</summary>
        public static event Action<Faction, Faction, Stance> RelationChanged;
        /// <summary>Raised when a faction's ships start treating one ship as an enemy.</summary>
        public static event Action<SpaceShip, Faction> ShipMarkedHostile;

        private static readonly List<Faction> known = new List<Faction>();
        private static float[] reputation = Array.Empty<float>();
        private static readonly HashSet<(SpaceShip ship, Faction faction)> grudges = new HashSet<(SpaceShip, Faction)>();
        private static int generation = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            known.Clear();
            reputation = Array.Empty<float>();
            grudges.Clear();
            generation++;
            RelationChanged = null;
            ShipMarkedHostile = null;
        }

        /// <summary>True when the two ships fight: hostile factions, a grudge, or different teams without factions.</summary>
        public static bool AreHostile(SpaceShip a, SpaceShip b)
        {
            if (a == null || b == null || ReferenceEquals(a, b)) return false;
            Faction fa = a.Faction, fb = b.Faction;
            if (grudges.Count > 0 && (HasGrudge(a, fb) || HasGrudge(b, fa))) return true;
            if (fa is null || fb is null) return a.Team != b.Team;
            if (ReferenceEquals(fa, fb)) return false;
            return StanceOf(Reputation(fa, fb)) == Stance.Hostile;
        }

        /// <summary>True when the ships are on one side: the same faction, friendly factions, or the same team without factions.</summary>
        public static bool AreAllied(SpaceShip a, SpaceShip b)
        {
            if (a == null || b == null) return false;
            if (ReferenceEquals(a, b)) return true;
            if (AreHostile(a, b)) return false;
            Faction fa = a.Faction, fb = b.Faction;
            if (fa is null || fb is null) return a.Team == b.Team;
            return ReferenceEquals(fa, fb) || StanceOf(Reputation(fa, fb)) == Stance.Friendly;
        }

        public static Stance GetStance(Faction a, Faction b)
        {
            if (a == null || b == null) return Stance.Neutral;
            return ReferenceEquals(a, b) ? Stance.Friendly : StanceOf(Reputation(a, b));
        }

        public static void SetStance(Faction a, Faction b, Stance stance) =>
            SetReputation(a, b, stance == Stance.Hostile ? -60f : stance == Stance.Friendly ? 60f : 0f);

        public static float GetReputation(Faction a, Faction b) =>
            a == null || b == null || ReferenceEquals(a, b) ? 100f : Reputation(a, b);

        public static void AddReputation(Faction a, Faction b, float delta) => SetReputation(a, b, GetReputation(a, b) + delta);

        /// <summary>Sets the mutual reputation of two factions, clamped to -100..100.</summary>
        public static void SetReputation(Faction a, Faction b, float value)
        {
            if (a == null || b == null || ReferenceEquals(a, b)) return;
            int i = IndexOf(a), j = IndexOf(b), n = known.Count;
            Stance before = StanceOf(reputation[i * n + j]);
            value = Mathf.Clamp(value, -100f, 100f);
            reputation[i * n + j] = value;
            reputation[j * n + i] = value;
            Stance after = StanceOf(value);
            if (after != before) RelationChanged?.Invoke(a, b, after);
        }

        /// <summary>Makes the faction's ships treat this ship as an enemy until it is disabled or forgiven.</summary>
        public static void MarkHostile(SpaceShip ship, Faction faction)
        {
            if (ship == null || faction == null || ReferenceEquals(ship.Faction, faction)) return;
            if (grudges.Add((ship, faction))) ShipMarkedHostile?.Invoke(ship, faction);
        }

        /// <summary>Clears the ship's grudges with one faction, or with all of them when the faction is null.</summary>
        public static void Forgive(SpaceShip ship, Faction faction = null)
        {
            if (grudges.Count == 0) return;
            if (faction != null) grudges.Remove((ship, faction));
            else grudges.RemoveWhere(g => ReferenceEquals(g.ship, ship));
        }

        /// <summary>Applies the victim faction's <see cref="Faction.WhenAttacked"/> response; called for every damaging hit.</summary>
        public static void ReportAttack(SpaceShip victim, SpaceShip attacker)
        {
            if (victim == null || attacker == null || ReferenceEquals(victim, attacker)) return;
            Faction defender = victim.Faction;
            if (defender is null || AreHostile(victim, attacker)) return;
            Faction offender = attacker.Faction;
            if (ReferenceEquals(defender, offender)) return;
            if (offender is not null && GetStance(defender, offender) == Stance.Friendly && !defender.RespondToFriendlyFire) return;

            switch (defender.WhenAttacked)
            {
                case AttackResponse.AttackerShip:
                    MarkHostile(attacker, defender);
                    break;
                case AttackResponse.AttackerFaction:
                    if (offender is not null) SetStance(defender, offender, Stance.Hostile);
                    else MarkHostile(attacker, defender);
                    break;
            }
        }

        private static bool HasGrudge(SpaceShip ship, Faction faction) => faction is not null && grudges.Contains((ship, faction));

        private static Stance StanceOf(float value) =>
            value <= HostileBelow ? Stance.Hostile : value >= FriendlyAbove ? Stance.Friendly : Stance.Neutral;

        private static float Reputation(Faction a, Faction b)
        {
            int i = IndexOf(a), j = IndexOf(b);
            return reputation[i * known.Count + j];
        }

        // Factions get an index the first time they are seen; the matrix grows and new pairs start from the assets.
        private static int IndexOf(Faction faction)
        {
            if (faction.RuntimeGeneration == generation) return faction.RuntimeIndex;
            int old = known.Count, n = old + 1;
            var grown = new float[n * n];
            for (int i = 0; i < old; i++)
            for (int j = 0; j < old; j++)
                grown[i * n + j] = reputation[i * old + j];
            known.Add(faction);
            faction.RuntimeIndex = old;
            faction.RuntimeGeneration = generation;
            for (int k = 0; k < old; k++)
            {
                Faction other = known[k];
                var start = (Stance)Mathf.Min((int)faction.StartingStanceToward(other), (int)other.StartingStanceToward(faction));
                float value = start == Stance.Hostile ? -60f : start == Stance.Friendly ? 60f : 0f;
                grown[old * n + k] = value;
                grown[k * n + old] = value;
            }
            grown[old * n + old] = 100f;
            reputation = grown;
            return old;
        }
    }
}
