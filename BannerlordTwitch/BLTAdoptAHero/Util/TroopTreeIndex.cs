using System;
using System.Collections.Generic;
using System.Linq;
using BannerlordTwitch.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace BLTAdoptAHero.Util
{
    /// <summary>Cycle-safe index and the single source of truth for smart-retinue selection.</summary>
    public static class TroopTreeIndex
    {
        public sealed class TroopInfo
        {
            public CharacterObject Troop { get; internal set; }
            public IReadOnlyList<CharacterObject> UpgradeTargets { get; internal set; } = Array.Empty<CharacterObject>();
            internal Dictionary<SmartTroopPolicy.SmartTroopRole, int> Distances { get; } = new();
            public IReadOnlyList<CharacterObject> TerminalDestinations { get; internal set; } = Array.Empty<CharacterObject>();
            public IReadOnlyCollection<FormationClass> ReachableFormations { get; internal set; } = Array.Empty<FormationClass>();
            public int MaximumReachableTier { get; internal set; }
            public bool CanReach(HeroClassDef heroClass) => CompatibleTerminals(heroClass).Any();
            public IEnumerable<CharacterObject> CompatibleTerminals(HeroClassDef heroClass) =>
                TerminalDestinations.Where(t => IsCompatible(t, InterpretRole(heroClass)));
        }

        public sealed class SelectionResult
        {
            public SmartTroopPolicy.SmartTroopRole InterpretedRole { get; internal set; }
            public CharacterObject SelectedTroop { get; internal set; }
            public int FallbackTier { get; internal set; }
            public int Score { get; internal set; }
            public IReadOnlyList<string> RejectionReasons { get; internal set; } = Array.Empty<string>();
        }

        private static readonly Dictionary<CharacterObject, TroopInfo> Index = new();
        private static bool isBuilt;

        public static void Reset() { Index.Clear(); isBuilt = false; }

        public static void BuildIndex()
        {
            Index.Clear();
            foreach (var troop in CharacterObject.All.Where(t => t != null && !t.IsHero))
            {
                var terminals = CycleSafeGraph.FindTerminals(troop,
                    t => (t.UpgradeTargets ?? Array.Empty<CharacterObject>()).Where(c => c != null && !c.IsHero));
                Index[troop] = new TroopInfo
                {
                    Troop = troop,
                    UpgradeTargets = (troop.UpgradeTargets ?? Array.Empty<CharacterObject>()).Where(t => t != null && !t.IsHero).Distinct().ToList(),
                    TerminalDestinations = terminals,
                    ReachableFormations = terminals.Select(t => t.DefaultFormationClass).Distinct().ToList(),
                    MaximumReachableTier = terminals.Any() ? terminals.Max(t => t.Tier) : troop.Tier
                };
            }
            isBuilt = true;
            Log.Info($"[TroopTreeIndex] Indexed {Index.Count} loaded troops");
        }

        public static TroopInfo GetTroopInfo(CharacterObject troop)
        {
            EnsureBuilt();
            return troop != null && Index.TryGetValue(troop, out var info) ? info : null;
        }

        public static bool CanReachHeroClass(CharacterObject troop, HeroClassDef heroClass) =>
            GetTroopInfo(troop)?.CanReach(heroClass) == true;

        public static SelectionResult SelectHire(Hero hero, HeroClassDef heroClass,
            IEnumerable<CharacterObject> candidates)
        {
            var role = InterpretRole(heroClass);
            WarnUnknown(heroClass, role);
            var selection = SmartTroopPolicy.Select(candidates,
                t => t.Culture == hero?.Culture,
                t => IsCompatiblePath(t, role),
                t => t.StringId,
                t => CompatibleScore(t, role));
            return ToResult(role, selection);
        }

        public static SelectionResult SelectCompatibleUpgrade(CharacterObject troop, HeroClassDef heroClass)
        {
            var role = InterpretRole(heroClass);
            WarnUnknown(heroClass, role);
            int distance = CompatibleDistance(troop, role);
            int score = CompatibleScore(troop, role);
            var selection = SmartTroopPolicy.SelectCompatible(GetTroopInfo(troop)?.UpgradeTargets,
                t => IsCompatiblePath(t, role) && CompatibleScore(t, role) == score && CompatibleDistance(t, role) < distance,
                t => CompatibleScore(t, role),
                t => t.StringId);
            return ToResult(role, selection);
        }

        public static SelectionResult SelectClosestReplacement(CharacterObject troop, HeroClassDef heroClass,
            CultureObject preferredCulture)
        {
            EnsureBuilt();
            var role = InterpretRole(heroClass);
            WarnUnknown(heroClass, role);
            var selection = SmartTroopPolicy.SelectClosestTier(Index.Keys.Where(IsCombatTroop), troop?.Tier ?? 0,
                t => t.Culture == preferredCulture,
                t => IsCompatiblePath(t, role),
                t => t.Tier,
                t => CompatibleScore(t, role),
                t => t.StringId);
            return ToResult(role, selection);
        }

        private static bool IsCombatTroop(CharacterObject troop) => troop != null && !troop.IsHero
            && troop.Occupation is Occupation.Soldier or Occupation.Mercenary or Occupation.Bandit;

        public static IReadOnlyList<CharacterObject> RecruitmentRoots(IEnumerable<CultureObject> cultures,
            bool basic, bool elite, bool militia, bool eliteMilitia, bool bandits)
        {
            EnsureBuilt();
            var roots = new HashSet<CharacterObject>();
            var anchored = new HashSet<CharacterObject>();
            foreach (var culture in cultures.Where(c => c != null))
            {
                var anchors = new[] { culture.BasicTroop, culture.EliteBasicTroop, culture.MeleeMilitiaTroop,
                    culture.RangedMilitiaTroop, culture.MeleeEliteMilitiaTroop, culture.RangedEliteMilitiaTroop };
                foreach (var troop in anchors.Where(t => t != null)) anchored.Add(troop);
                if (!bandits && culture.IsBandit) continue;
                if (basic) roots.Add(culture.BasicTroop);
                if (elite) roots.Add(culture.EliteBasicTroop);
                if (militia) { roots.Add(culture.MeleeMilitiaTroop); roots.Add(culture.RangedMilitiaTroop); }
                if (eliteMilitia) { roots.Add(culture.MeleeEliteMilitiaTroop); roots.Add(culture.RangedEliteMilitiaTroop); }
            }
            // Overhauls can add military trees without assigning a culture's BasicTroop slot.
            // Treat these otherwise unclassified recruitment roots as basic troops.
            if (basic)
            {
                var children = new HashSet<CharacterObject>(Index.Values.SelectMany(i => i.UpgradeTargets));
                foreach (var troop in Index.Keys.Where(IsCombatTroop))
                    if (!children.Contains(troop) && !anchored.Contains(troop)
                        && (bandits || troop.Culture?.IsBandit != true && troop.Occupation != Occupation.Bandit)) roots.Add(troop);
            }
            return roots.Where(t => t != null && !t.IsHero && GetTroopInfo(t)?.TerminalDestinations.Count > 0)
                .OrderBy(t => t.StringId, StringComparer.Ordinal).ToList();
        }

        private static int CompatibleDistance(CharacterObject troop, SmartTroopPolicy.SmartTroopRole role)
        {
            var info = GetTroopInfo(troop);
            if (info == null) return int.MaxValue;
            if (info.Distances.TryGetValue(role, out int cached)) return cached;
            int tier = CompatibleScore(troop, role);
            var visited = new HashSet<CharacterObject>();
            var pending = new Queue<(CharacterObject troop, int distance)>();
            pending.Enqueue((troop, 0));
            while (pending.Count > 0)
            {
                var next = pending.Dequeue();
                if (!visited.Add(next.troop)) continue;
                var node = GetTroopInfo(next.troop);
                if (node == null) continue;
                if (node.UpgradeTargets.Count == 0 && next.troop.Tier == tier && IsCompatible(next.troop, role))
                    return info.Distances[role] = next.distance;
                foreach (var child in node.UpgradeTargets) pending.Enqueue((child, next.distance + 1));
            }
            return info.Distances[role] = int.MaxValue;
        }

        public static string Describe(CharacterObject troop, HeroClassDef heroClass)
        {
            var info = GetTroopInfo(troop);
            if (info == null) return $"{troop?.StringId ?? "<null>"}: not indexed";
            var role = InterpretRole(heroClass);
            string destinations = string.Join(", ", info.TerminalDestinations.Select(t =>
                $"{t.StringId}[T{t.Tier},{t.DefaultFormationClass}{(t.IsMounted ? ",mounted" : "")}]"));
            return $"{troop.StringId} -> {destinations}; class={heroClass?.Formation ?? "none"}; " +
                   $"role={role}; maxTier={info.MaximumReachableTier}; compatible={IsCompatiblePath(troop, role)}";
        }

        public static SmartTroopPolicy.SmartTroopRole InterpretRole(HeroClassDef heroClass) =>
            SmartTroopPolicy.InterpretRole(heroClass?.Formation, heroClass?.Mounted == true);

        private static bool IsCompatiblePath(CharacterObject troop, SmartTroopPolicy.SmartTroopRole role) =>
            GetTroopInfo(troop)?.TerminalDestinations.Any(t => IsCompatible(t, role)) == true;

        private static int CompatibleScore(CharacterObject troop, SmartTroopPolicy.SmartTroopRole role)
        {
            var info = GetTroopInfo(troop);
            var compatible = info?.TerminalDestinations.Where(t => IsCompatible(t, role)).ToList();
            return compatible?.Any() == true ? compatible.Max(t => t.Tier) : -1;
        }

        private static bool IsCompatible(CharacterObject troop, SmartTroopPolicy.SmartTroopRole role)
        {
            if (troop == null) return false;
            var actual = troop.DefaultFormationClass;
            return role switch
            {
                SmartTroopPolicy.SmartTroopRole.HorseArcher => troop.IsMounted && actual == FormationClass.HorseArcher,
                SmartTroopPolicy.SmartTroopRole.Cavalry => troop.IsMounted &&
                    actual is FormationClass.Cavalry or FormationClass.LightCavalry or FormationClass.HeavyCavalry,
                SmartTroopPolicy.SmartTroopRole.FootRanged => !troop.IsMounted && actual == FormationClass.Ranged,
                SmartTroopPolicy.SmartTroopRole.InfantryFamily => !troop.IsMounted &&
                    actual is FormationClass.Infantry or FormationClass.HeavyInfantry or FormationClass.Skirmisher,
                _ => false
            };
        }

        private static SelectionResult ToResult(SmartTroopPolicy.SmartTroopRole role,
            SmartTroopPolicy.Selection<CharacterObject> selection) => new()
        {
            InterpretedRole = role,
            SelectedTroop = selection.Value,
            FallbackTier = selection.FallbackTier,
            Score = selection.Score,
            RejectionReasons = selection.Rejections
        };

        private static void WarnUnknown(HeroClassDef heroClass, SmartTroopPolicy.SmartTroopRole role)
        {
            if (role == SmartTroopPolicy.SmartTroopRole.Unknown)
                Log.Info($"[TroopTreeIndex] WARNING: unknown hero formation '{heroClass?.Formation ?? "<none>"}'; choose a supported class before hiring retinue");
        }

        private static void EnsureBuilt()
        {
            if (!isBuilt) BuildIndex();
        }
    }
}
