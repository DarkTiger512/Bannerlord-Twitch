using System;
using System.Collections.Generic;
using System.Linq;

namespace BLTAdoptAHero.Util
{
    /// <summary>Engine-independent ordering shared by hiring, upgrading and class conversion.</summary>
    public static class SmartTroopPolicy
    {
        public enum SmartTroopRole { Unknown, InfantryFamily, FootRanged, Cavalry, HorseArcher }

        public static SmartTroopRole InterpretRole(string formation, bool mounted)
        {
            string normalized = formation?.Replace(" ", string.Empty).ToLowerInvariant() ?? string.Empty;
            if (normalized == "horsearcher") return SmartTroopRole.HorseArcher;
            if (mounted) return SmartTroopRole.Cavalry;
            return normalized switch
            {
                "ranged" or "archer" or "crossbow" => SmartTroopRole.FootRanged,
                "infantry" or "heavyinfantry" or "skirmisher" => SmartTroopRole.InfantryFamily,
                "cavalry" or "lightcavalry" or "heavycavalry" => SmartTroopRole.Cavalry,
                _ => SmartTroopRole.Unknown
            };
        }

        public static bool CanAfford(int availableGold, int committedCost, int nextCost) =>
            nextCost >= 0 && committedCost >= 0 && committedCost <= availableGold - nextCost;

        public readonly struct Selection<T>
        {
            public Selection(T value, int fallbackTier, int score, IReadOnlyList<string> rejections)
            {
                Value = value;
                FallbackTier = fallbackTier;
                Score = score;
                Rejections = rejections ?? Array.Empty<string>();
            }

            public T Value { get; }
            public int FallbackTier { get; }
            public int Score { get; }
            public IReadOnlyList<string> Rejections { get; }
        }

        public static Selection<T> Select<T>(IEnumerable<T> candidates, Func<T, bool> sameCulture,
            Func<T, bool> classCompatible, Func<T, string> stableKey,
            Func<T, int> compatibilityScore = null)
        {
            compatibilityScore ??= _ => 0;
            var available = Clean(candidates).ToList();
            var rejections = new List<string>();

            var selected = Best(available.Where(v => sameCulture(v) && classCompatible(v)), compatibilityScore, stableKey);
            if (selected != null) return Result(selected, 1, compatibilityScore, rejections);
            rejections.Add("tier 1 rejected: no same-culture class-compatible path");

            selected = Best(available.Where(classCompatible), compatibilityScore, stableKey);
            if (selected != null) return Result(selected, 2, compatibilityScore, rejections);
            rejections.Add("tier 2 rejected: no cross-culture class-compatible path");

            // A different troop role is never a successful class-guided hire.
            return Result(default(T), 0, compatibilityScore, rejections);
        }

        public static Selection<T> SelectCompatible<T>(IEnumerable<T> candidates,
            Func<T, bool> classCompatible, Func<T, int> compatibilityScore, Func<T, string> stableKey)
        {
            var available = Clean(candidates).ToList();
            var compatible = available.Where(classCompatible).ToList();
            var selected = Best(compatible, compatibilityScore, stableKey);
            var rejected = available.Except(compatible)
                .Select(v => $"{stableKey(v)} rejected: incompatible destination").ToList();
            return Result(selected, 0, compatibilityScore, rejected);
        }

        public static Selection<T> SelectClosestTier<T>(IEnumerable<T> candidates, int targetTier,
            Func<T, bool> sameCulture, Func<T, bool> classCompatible, Func<T, int> tier,
            Func<T, int> compatibilityScore, Func<T, string> stableKey)
        {
            var selected = Clean(candidates).Where(classCompatible)
                .OrderByDescending(sameCulture)
                .ThenBy(v => Math.Abs(tier(v) - targetTier))
                .ThenByDescending(compatibilityScore)
                .ThenBy(stableKey, StringComparer.Ordinal)
                .FirstOrDefault();
            var rejected = selected == null ? new[] { "no compatible class-change replacement" } : Array.Empty<string>();
            return Result(selected, 0, compatibilityScore, rejected);
        }

        private static IEnumerable<T> Clean<T>(IEnumerable<T> values) =>
            (values ?? Array.Empty<T>()).Where(v => v != null).Distinct();

        private static T Best<T>(IEnumerable<T> values, Func<T, int> score, Func<T, string> key) =>
            values.OrderByDescending(score).ThenBy(key, StringComparer.Ordinal).FirstOrDefault();

        private static Selection<T> Result<T>(T value, int fallbackTier, Func<T, int> score,
            IReadOnlyList<string> rejections) => new(value, fallbackTier, value == null ? 0 : score(value), rejections);
    }
}
