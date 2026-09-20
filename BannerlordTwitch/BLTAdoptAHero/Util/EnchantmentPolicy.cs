using System;
using System.Collections.Generic;
using System.Linq;

namespace BLTAdoptAHero.Util
{
    public enum EnchantmentStat { Damage, Speed, MissileSpeed }

    public sealed class EnchantmentEntry
    {
        public EnchantmentStat Stat { get; set; }
        public int Gain { get; set; }
    }

    public static class EnchantmentPolicy
    {
        public const int MaxLevel = 5;

        public static void Validate(int[] costs, int[] failures, int[] gains)
        {
            if (costs == null || costs.Length != MaxLevel || costs.Any(x => x < 0)
                || failures == null || failures.Length != MaxLevel || failures[0] != 0
                || failures.Any(x => x < 0 || x > 100)
                || gains == null || gains.Length != 3 || gains.Any(x => x <= 0))
                throw new ArgumentException("Invalid enchantment configuration: costs must be nonnegative, gains positive, and failure chances 0–100% (+1 must be guaranteed).");
        }

        public static bool TryParse(string args, out int index, out EnchantmentStat? stat)
        {
            index = 0;
            stat = null;
            var parts = (args ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return true;
            if (parts.Length > 2 || !int.TryParse(parts[0].TrimStart('#'), out index) || index < 1
                || parts[0].StartsWith("##", StringComparison.Ordinal)) return false;
            if (parts.Length == 1) return true;
            switch (parts[1].ToLowerInvariant())
            {
                case "damage": stat = EnchantmentStat.Damage; return true;
                case "speed": stat = EnchantmentStat.Speed; return true;
                case "missilespeed": stat = EnchantmentStat.MissileSpeed; return true;
                default: return false;
            }
        }

        public static string AttemptError(int level, int gold, int cost, bool mission, bool prisoner, bool auction)
        {
            if (mission) return "You cannot enchant during a mission.";
            if (prisoner) return "You cannot enchant while imprisoned.";
            if (auction) return "You cannot enchant an item being auctioned.";
            if (level >= MaxLevel) return "This weapon is already enchanted to +5.";
            if (gold < cost) return $"Not enough gold: this attempt costs {cost:N0}; you have {gold:N0}.";
            return null;
        }

        public static List<EnchantmentEntry> Roll(IReadOnlyList<EnchantmentEntry> history,
            EnchantmentStat stat, int gain, int failurePercent, int roll, out EnchantmentEntry change, out bool success)
        {
            var next = (history ?? Array.Empty<EnchantmentEntry>()).Select(x =>
                new EnchantmentEntry { Stat = x.Stat, Gain = x.Gain }).ToList();
            if (next.Count >= MaxLevel || next.Any(x => x.Gain <= 0 || !Enum.IsDefined(typeof(EnchantmentStat), x.Stat)))
                throw new InvalidOperationException("Invalid or maximum enchantment level.");
            if (gain <= 0 || !Enum.IsDefined(typeof(EnchantmentStat), stat)
                || failurePercent < 0 || failurePercent > 100 || roll < 0 || roll >= 100)
                throw new ArgumentOutOfRangeException(nameof(gain));
            success = next.Count == 0 || roll >= failurePercent;
            if (success)
            {
                change = new EnchantmentEntry { Stat = stat, Gain = gain };
                next.Add(change);
            }
            else
            {
                change = next[next.Count - 1];
                next.RemoveAt(next.Count - 1);
            }
            return next;
        }

        // Keep replies outside the transaction: a transport failure must not undo a completed purchase.
        public static void Commit(Action apply, Action charge, Action restoreItem, Action restoreGold)
        {
            try { apply(); charge(); }
            catch
            {
                try { restoreItem(); }
                finally { restoreGold(); }
                throw;
            }
        }
    }
}
