using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI2
{
    public enum SquadOrder { HoldFormation, Cover, Attack }

    /// <summary>
    /// A leader and wingmen in slots. Wingmen keep station through their combat brain's Formation behavior, break off
    /// for obstacles, threats or orders and rejoin. Slots close up when wingmen are lost; a wingman takes over a lost leader.
    /// </summary>
    public class Formation : MonoBehaviour
    {
        [SerializeField] private FormationProfile profile;
        [SerializeField] private SpaceShip leader;
        [SerializeField] private List<CombatBrain> wingmen = new List<CombatBrain>();
        [Tooltip("Cover: engage enemies within this distance of the leader or attacking the squad.")]
        [SerializeField, Min(0f)] private float coverRadius = 1200f;

        private readonly List<CombatBrain> members = new List<CombatBrain>();
        private readonly Dictionary<CombatBrain, float> brokenSince = new Dictionary<CombatBrain, float>();
        private readonly List<CombatBrain> scratch = new List<CombatBrain>();
        private readonly List<int> freeSlots = new List<int>();
        private AIPilot leaderPilot;

        public event Action<Formation, CombatBrain> MemberBroke;
        /// <summary>A wingman is back in its slot; the float is seconds since it broke off.</summary>
        public event Action<Formation, CombatBrain, float> MemberRejoined;
        public event Action<Formation, SpaceShip> LeaderChanged;

        public FormationProfile Profile
        {
            get => profile;
            set => profile = value;
        }

        public SpaceShip Leader => leader;
        /// <summary>Wingmen alive and assigned to slots.</summary>
        public IReadOnlyList<CombatBrain> Members => members;
        public SquadOrder Order { get; private set; }
        public SpaceShip OrderTarget { get; private set; }
        public Vector3 LeaderVelocity => leader != null ? leader.Velocity : Vector3.zero;
        public int BrokenCount => brokenSince.Count;

        public void Setup(FormationProfile shape, SpaceShip lead, IEnumerable<CombatBrain> wings)
        {
            profile = shape;
            leader = lead;
            wingmen.Clear();
            wingmen.AddRange(wings);
        }

        public void HoldFormation() => SetOrder(SquadOrder.HoldFormation, null);

        /// <summary>Same as holding formation: wingmen fly back to their slots and only return fire.</summary>
        public void Return() => HoldFormation();

        public void Cover() => SetOrder(SquadOrder.Cover, null);

        public void Attack(SpaceShip target)
        {
            if (target == null) HoldFormation();
            else SetOrder(SquadOrder.Attack, target);
        }

        public Vector3 SlotPosition(int slot) => leader.Position + leader.transform.rotation * profile.Slot(slot);

        public bool IsBroken(CombatBrain member) => brokenSince.ContainsKey(member);

        /// <summary>
        /// Whether the current order lets wingmen pick this enemy as a target. Holding formation still returns fire
        /// at enemies attacking the squad; cover also takes enemies close to the leader; attack takes anyone.
        /// </summary>
        public bool MayEngage(SpaceShip enemy)
        {
            switch (Order)
            {
                case SquadOrder.HoldFormation: return Attacks(enemy);
                case SquadOrder.Cover: return Attacks(enemy) || leader != null && (enemy.Position - leader.Position).sqrMagnitude < coverRadius * coverRadius;
                default: return true;
            }
        }

        /// <summary>Whether this enemy is targeting the leader or a wingman.</summary>
        public bool Attacks(SpaceShip enemy)
        {
            if (!enemy.TryGetComponent(out CombatBrain brain) || brain.Target == null) return false;
            if (brain.Target == leader) return true;
            foreach (CombatBrain member in members)
                if (brain.Target == member.Ship) return true;
            return false;
        }

        private void SetOrder(SquadOrder order, SpaceShip target)
        {
            Order = order;
            OrderTarget = target;
            brokenSince.Clear();
        }

        private void OnDisable()
        {
            foreach (CombatBrain wingman in wingmen)
                if (wingman != null && wingman.Formation == this) wingman.Formation = null;
            members.Clear();
            brokenSince.Clear();
            if (leaderPilot != null) leaderPilot.MaxThrottle = 1f;
        }

        private void Update()
        {
            if (profile == null) return;
            if (!IsAlive(leader)) PromoteLeader();
            if (leader == null) return;
            if (leaderPilot == null || leaderPilot.Ship != leader) leaderPilot = leader.GetComponent<AIPilot>();
            if (Order == SquadOrder.Attack && !IsAlive(OrderTarget)) HoldFormation();

            RefreshMembers();
            if (Order == SquadOrder.HoldFormation) UpdateBreaks();
            if (leaderPilot != null)
                leaderPilot.MaxThrottle = Order != SquadOrder.HoldFormation ? 1f : brokenSince.Count > 0 ? profile.WaitThrottle : profile.LeaderThrottle;
        }

        private void PromoteLeader()
        {
            if (leaderPilot != null) leaderPilot.MaxThrottle = 1f;
            leaderPilot = null;
            leader = null;
            CombatBrain heir = null;
            foreach (CombatBrain wingman in wingmen)
                if (IsAlive(wingman) && (heir == null || wingman.Slot < heir.Slot)) heir = wingman;
            if (heir == null) return;

            wingmen.Remove(heir);
            members.Remove(heir);
            brokenSince.Remove(heir);
            heir.Formation = null;
            leader = heir.Ship;
            LeaderChanged?.Invoke(this, leader);
        }

        /// <summary>Keeps slots stable; wingmen past the end move into the nearest free slots left by losses.</summary>
        private void RefreshMembers()
        {
            members.Clear();
            foreach (CombatBrain wingman in wingmen)
            {
                if (!IsAlive(wingman)) continue;
                if (wingman.Formation != this)
                {
                    wingman.Formation = this;
                    wingman.Slot = int.MaxValue;
                }
                members.Add(wingman);
            }

            freeSlots.Clear();
            for (int slot = 0; slot < members.Count; slot++)
            {
                bool taken = false;
                foreach (CombatBrain member in members)
                    if (member.Slot == slot) taken = true;
                if (!taken) freeSlots.Add(slot);
            }
            foreach (int slot in freeSlots)
            {
                CombatBrain nearest = null;
                float nearestDistance = float.MaxValue;
                Vector3 position = SlotPosition(slot);
                foreach (CombatBrain member in members)
                {
                    if (member.Slot < members.Count) continue;
                    float distance = (member.Ship.Position - position).sqrMagnitude;
                    if (distance < nearestDistance)
                    {
                        nearest = member;
                        nearestDistance = distance;
                    }
                }
                if (nearest != null) nearest.Slot = slot;
            }

            scratch.Clear();
            foreach (CombatBrain broken in brokenSince.Keys)
                if (!members.Contains(broken)) scratch.Add(broken);
            foreach (CombatBrain gone in scratch) brokenSince.Remove(gone);
        }

        private void UpdateBreaks()
        {
            foreach (CombatBrain member in members)
            {
                float error = Vector3.Distance(SlotPosition(member.Slot), member.Ship.Position);
                if (brokenSince.TryGetValue(member, out float since))
                {
                    if (error >= profile.JoinDistance) continue;
                    brokenSince.Remove(member);
                    MemberRejoined?.Invoke(this, member, Time.time - since);
                }
                else if (error > profile.BreakDistance)
                {
                    brokenSince[member] = Time.time;
                    MemberBroke?.Invoke(this, member);
                }
            }
        }

        private static bool IsAlive(Component component) => component != null && component.gameObject.activeInHierarchy;
    }
}
