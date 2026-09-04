using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BLTAdoptAHero.Util
{
    public enum StreamObjectiveKind { Kills, Cavalry, Battles, Tournaments, Captures, Survive }
    public enum StreamObjectiveStatus { Active, Completed, Cancelled }

    public sealed class StreamObjectiveState
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public StreamObjectiveKind Kind { get; set; }
        public StreamObjectiveStatus Status { get; set; } = StreamObjectiveStatus.Active;
        public int Target { get; set; }
        public int RequiredHeroes { get; set; }
        public int RequiredBattles { get; set; }
        public int Progress { get; set; }
        public int GoldReward { get; set; }
        public int XPReward { get; set; }
        public string CultureId { get; set; }
        public string StartedBy { get; set; }
        public string StartedAt { get; set; }
        public string FinishedAt { get; set; }
        public int LastMilestone { get; set; }
        public bool RewardsGranted { get; set; }
        public Dictionary<string, StreamObjectiveContribution> Contributors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ProcessedEvents { get; set; } = new(StringComparer.Ordinal);
    }

    public sealed class StreamObjectiveContribution
    {
        public string Owner { get; set; }
        public string HeroId { get; set; }
        public string HeroName { get; set; }
        public int Amount { get; set; }
        public int SurvivalStreak { get; set; }
    }

    public sealed class StreamObjectiveStart
    {
        public StreamObjectiveKind Kind { get; set; }
        public int Target { get; set; }
        public int RequiredHeroes { get; set; }
        public int RequiredBattles { get; set; }
        public int Gold { get; set; }
        public int XP { get; set; }
        public string CultureId { get; set; }
    }

    public static class StreamObjectivePolicy
    {
        // No active objective is normal on fresh campaigns and after clearing an objective.
        public static void RestoreCollections(StreamObjectiveState state)
        {
            if (state == null) return;
            state.Contributors ??= new Dictionary<string, StreamObjectiveContribution>(StringComparer.OrdinalIgnoreCase);
            state.ProcessedEvents ??= new HashSet<string>(StringComparer.Ordinal);
        }

        public static readonly string[] KindNames = { "kills", "cavalry", "battles", "tournaments", "captures", "survive" };

        public static bool TryParseStart(string args, Func<string, bool> cultureExists,
            out StreamObjectiveStart start, out string error)
        {
            start = null;
            error = null;
            var tokens = (args ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 5 || !tokens[0].Equals("start", StringComparison.OrdinalIgnoreCase))
            { error = "Usage: start <type> <target> [options] gold=<amount> xp=<amount>"; return false; }
            if (!Enum.TryParse(tokens[1], true, out StreamObjectiveKind kind))
            { error = $"Unknown objective '{tokens[1]}'. Available: {string.Join(", ", KindNames)}"; return false; }

            int cursor = 2;
            var parsed = new StreamObjectiveStart { Kind = kind };
            if (kind == StreamObjectiveKind.Survive)
            {
                if (tokens.Length <= cursor + 1 || !Positive(tokens[cursor++], out int heroes) || !Positive(tokens[cursor++], out int battles))
                { error = "Usage: start survive <hero-count> <battle-count> gold=<amount> xp=<amount>"; return false; }
                parsed.RequiredHeroes = heroes; parsed.RequiredBattles = battles; parsed.Target = heroes;
            }
            else if (!Positive(tokens[cursor++], out int target))
            { error = "The target must be a positive whole number."; return false; }
            else parsed.Target = target;

            bool hasGold = false, hasXP = false;
            for (; cursor < tokens.Length; cursor++)
            {
                var pair = tokens[cursor].Split(new[] { '=' }, 2);
                if (pair.Length != 2) { error = $"Invalid option '{tokens[cursor]}'."; return false; }
                if (pair[0].Equals("gold", StringComparison.OrdinalIgnoreCase))
                { if (!NonNegative(pair[1], out int value)) { error = "Gold must be a non-negative whole number."; return false; } parsed.Gold = value; hasGold = true; }
                else if (pair[0].Equals("xp", StringComparison.OrdinalIgnoreCase))
                { if (!NonNegative(pair[1], out int value)) { error = "XP must be a non-negative whole number."; return false; } parsed.XP = value; hasXP = true; }
                else if (pair[0].Equals("culture", StringComparison.OrdinalIgnoreCase) && kind == StreamObjectiveKind.Captures)
                { parsed.CultureId = pair[1]; }
                else { error = $"Unknown option '{pair[0]}'."; return false; }
            }
            if (!hasGold || !hasXP) { error = "Both gold=<amount> and xp=<amount> are required."; return false; }
            if (!string.IsNullOrWhiteSpace(parsed.CultureId) && cultureExists != null && !cultureExists(parsed.CultureId))
            { error = $"Unknown culture '{parsed.CultureId}'."; return false; }
            start = parsed;
            return true;
        }

        public static bool AddProgress(StreamObjectiveState state, string eventId, string owner, string heroId,
            string heroName, int amount = 1)
        {
            if (state?.Status != StreamObjectiveStatus.Active || amount <= 0 || string.IsNullOrWhiteSpace(owner)) return false;
            if (!string.IsNullOrEmpty(eventId) && !state.ProcessedEvents.Add(eventId)) return false;
            if (!state.Contributors.TryGetValue(owner, out var contribution))
                state.Contributors[owner] = contribution = new StreamObjectiveContribution { Owner = owner, HeroId = heroId, HeroName = heroName };
            contribution.HeroId = heroId ?? contribution.HeroId;
            contribution.HeroName = heroName ?? contribution.HeroName;
            contribution.Amount += amount;
            state.Progress = Math.Min(state.Target, state.Progress + amount);
            return true;
        }

        public static bool AddContributor(StreamObjectiveState state, string owner, string heroId, string heroName, int amount = 1)
        {
            if (state?.Status != StreamObjectiveStatus.Active || amount <= 0 || string.IsNullOrWhiteSpace(owner)) return false;
            if (!state.Contributors.TryGetValue(owner, out var contribution))
                state.Contributors[owner] = contribution = new StreamObjectiveContribution { Owner = owner };
            contribution.HeroId = heroId ?? contribution.HeroId;
            contribution.HeroName = heroName ?? contribution.HeroName;
            contribution.Amount += amount;
            return true;
        }

        public static bool RecordSurvival(StreamObjectiveState state, string eventId,
            IEnumerable<(string Owner, string HeroId, string HeroName, bool Died)> participants)
        {
            if (state?.Kind != StreamObjectiveKind.Survive || state.Status != StreamObjectiveStatus.Active) return false;
            if (!string.IsNullOrEmpty(eventId) && !state.ProcessedEvents.Add(eventId)) return false;
            bool changed = false;
            foreach (var p in participants ?? Enumerable.Empty<(string, string, string, bool)>())
            {
                if (string.IsNullOrWhiteSpace(p.Owner)) continue;
                if (!state.Contributors.TryGetValue(p.Owner, out var c))
                    state.Contributors[p.Owner] = c = new StreamObjectiveContribution { Owner = p.Owner, HeroId = p.HeroId, HeroName = p.HeroName };
                c.HeroId = p.HeroId; c.HeroName = p.HeroName;
                c.SurvivalStreak = p.Died ? 0 : Math.Min(state.RequiredBattles, c.SurvivalStreak + 1);
                if (!p.Died) c.Amount++;
                changed = true;
            }
            state.Progress = state.Contributors.Values.Count(c => c.SurvivalStreak >= state.RequiredBattles);
            return changed;
        }

        public static bool IsComplete(StreamObjectiveState state) => state != null &&
            (state.Kind == StreamObjectiveKind.Survive ? state.Progress >= state.RequiredHeroes : state.Progress >= state.Target);

        public static int Milestone(StreamObjectiveState state)
        {
            int target = state?.Kind == StreamObjectiveKind.Survive ? state.RequiredHeroes : state?.Target ?? 0;
            if (target <= 0) return 0;
            int percent = Math.Min(100, state.Progress * 100 / target);
            return percent >= 100 ? 100 : percent >= 75 ? 75 : percent >= 50 ? 50 : percent >= 25 ? 25 : 0;
        }

        public static string Describe(StreamObjectiveState state)
        {
            if (state == null) return "No stream objective is active.";
            return state.Kind switch
            {
                StreamObjectiveKind.Kills => $"Defeat {state.Target} enemies",
                StreamObjectiveKind.Cavalry => $"Defeat {state.Target} mounted enemies",
                StreamObjectiveKind.Battles => $"Win {state.Target} battles",
                StreamObjectiveKind.Tournaments => $"Win {state.Target} tournaments",
                StreamObjectiveKind.Captures => $"Capture {state.Target} towns/castles" + (string.IsNullOrWhiteSpace(state.CultureId) ? "" : $" of culture {state.CultureId}"),
                StreamObjectiveKind.Survive => $"Get {state.RequiredHeroes} heroes through {state.RequiredBattles} battles each",
                _ => state.Kind.ToString()
            };
        }

        private static bool Positive(string value, out int result) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result) && result > 0;
        private static bool NonNegative(string value, out int result) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result) && result >= 0;
    }
}
