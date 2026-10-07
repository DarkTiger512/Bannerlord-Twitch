using System;
using System.Collections.Generic;

namespace BLTAdoptAHero.Util
{
    public enum RandomEventLifecycle
    {
        Inactive,
        Preparing,
        AwaitingResponse,
        BattlePending,
        Active,
        Resolved,
        Failed
    }

    public sealed class ImmortalEncounterState
    {
        public int Version { get; set; } = 1;
        public RandomEventLifecycle Phase { get; set; } = RandomEventLifecycle.Inactive;
        public string ClanId { get; set; }
        public string HeroId { get; set; }
        public string PartyId { get; set; }
        public string MapEventId { get; set; }
        public bool CompletionPending { get; set; }
        public bool PlayerWon { get; set; }
        public int RewardGold { get; set; }
        public double StartedDay { get; set; }
        public HashSet<string> ParticipantHeroIds { get; set; } = new(StringComparer.Ordinal);
        public HashSet<string> RewardedHeroIds { get; set; } = new(StringComparer.Ordinal);
    }

    public sealed class PriestCrusadeState
    {
        public int Version { get; set; } = 1;
        public RandomEventLifecycle Phase { get; set; } = RandomEventLifecycle.Inactive;
        public string ClanId { get; set; }
        public string LeaderId { get; set; }
        public string PartyId { get; set; }
        public string TargetKingdomId { get; set; }
        public double StartedDay { get; set; }
    }

    public sealed class ImmortalBattleTracking
    {
        private object battle;
        private int playerSide = -1;
        public void Bind(object value, int side)
        {
            if (value == null || side < 0 || (battle != null && !Matches(value))) return;
            battle = value;
            playerSide = side;
        }
        public bool Matches(object value) => battle != null && ReferenceEquals(battle, value);
        public bool PlayerWon(object value, int winnerSide) => Matches(value) && winnerSide >= 0 && winnerSide == playerSide;
        public bool Complete(ImmortalEncounterState state, object value, int winnerSide, int reward)
        {
            if (state == null || state.CompletionPending || !Matches(value)
                || state.Phase is not (RandomEventLifecycle.BattlePending or RandomEventLifecycle.Active)) return false;
            state.PlayerWon = PlayerWon(value, winnerSide);
            state.RewardGold = RandomEventPolicy.ClampReward(reward);
            state.CompletionPending = true;
            state.Phase = RandomEventLifecycle.Resolved;
            return true;
        }
        public void Clear() { battle = null; playerSide = -1; }
    }

    public static class RandomEventPolicy
    {
        public static bool CanStartImmortalBattle(float health, float maximumHealth)
            => maximumHealth > 0 && !float.IsInfinity(maximumHealth) && !float.IsInfinity(health)
                && health > maximumHealth * .2f;

        public static float ClampChance(float value) => Math.Max(0f, Math.Min(100f, value));
        public static int ClampCooldown(int value) => Math.Max(0, Math.Min(3650, value));
        public static int ClampPercent(int value) => Math.Max(1, Math.Min(500, value));
        public static int ClampReward(int value) => Math.Max(0, Math.Min(100000000, value));
        public static int CalculateArmySize(float baseStrength, int percent, int minimum, int maximum)
            => Math.Max(minimum, Math.Min(maximum, (int)Math.Ceiling(Math.Max(0f, baseStrength) * ClampPercent(percent) / 100f)));

        public static bool CanRoll(double currentDay, double lastSuccessfulTriggerDay, int cooldownDays, bool active)
            => !active && currentDay - lastSuccessfulTriggerDay >= ClampCooldown(cooldownDays);

        public static bool RollSucceeds(float rollPercent, float chancePercent)
            => rollPercent >= 0f && rollPercent < ClampChance(chancePercent);

        public static bool IsActive(RandomEventLifecycle phase)
            => phase is RandomEventLifecycle.Preparing or RandomEventLifecycle.AwaitingResponse
                or RandomEventLifecycle.BattlePending or RandomEventLifecycle.Active;

        public static bool TryTransition(RandomEventLifecycle current, RandomEventLifecycle next)
            => current switch
            {
                RandomEventLifecycle.Inactive => next == RandomEventLifecycle.Preparing,
                RandomEventLifecycle.Preparing => next is RandomEventLifecycle.AwaitingResponse or RandomEventLifecycle.Active or RandomEventLifecycle.Failed,
                RandomEventLifecycle.AwaitingResponse => next is RandomEventLifecycle.BattlePending or RandomEventLifecycle.Resolved or RandomEventLifecycle.Failed,
                RandomEventLifecycle.BattlePending => next is RandomEventLifecycle.Active or RandomEventLifecycle.Resolved or RandomEventLifecycle.Failed,
                RandomEventLifecycle.Active => next is RandomEventLifecycle.Resolved or RandomEventLifecycle.Failed,
                _ => false
            };

        public static bool RecordParticipant(ImmortalEncounterState state, string heroId)
        {
            if (state == null || !IsActive(state.Phase) || string.IsNullOrWhiteSpace(heroId)) return false;
            state.ParticipantHeroIds ??= new HashSet<string>(StringComparer.Ordinal);
            return state.ParticipantHeroIds.Add(heroId);
        }

        public static bool RecordReward(ImmortalEncounterState state, string heroId)
        {
            if (state == null || string.IsNullOrWhiteSpace(heroId)) return false;
            state.RewardedHeroIds ??= new HashSet<string>(StringComparer.Ordinal);
            return state.RewardedHeroIds.Add(heroId);
        }

        public static bool DeferImmortalCleanup(ImmortalEncounterState state)
            => state != null && (state.CompletionPending || state.Phase is RandomEventLifecycle.BattlePending or RandomEventLifecycle.Active);

        public static bool TryGrantImmortalReward(ImmortalEncounterState state, string heroId, Func<bool> grant)
        {
            if (state?.CompletionPending != true || !state.PlayerWon
                || state.ParticipantHeroIds?.Contains(heroId) != true || state.RewardedHeroIds?.Contains(heroId) == true) return false;
            if (!grant()) return false;
            return RecordReward(state, heroId);
        }

        public static bool CrusadeResolved(bool clanEliminated, bool atWar, bool hasViableForces)
            => clanEliminated || !atWar || !hasViableForces;
    }
}
