using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using BannerlordTwitch.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace BLTAdoptAHero.Util
{
    /// <summary>
    /// Conservative compatibility evidence from loaded, authored loadouts. Culture is not race:
    /// human Easterlings and elves must not be classified by Race > 0 or by culture names.
    /// No guessing from item names and no fallback to another race's armour/mounts.
    /// </summary>
    internal static class TaomEquipmentCompatibility
    {
        private sealed class Catalogue
        {
            internal readonly bool Enabled = AccessTools.TypeByName("TAOM.SubModule") != null;
            private readonly Dictionary<int, HashSet<ItemObject>> items = new();

            internal Catalogue()
            {
                if (!Enabled) return;
                foreach (var character in CharacterObject.All.Where(c => c != null && (!c.IsHero || c.IsTemplate)))
                {
                    // Do not learn compatibility from mutable live-hero inventories.
                    if (!items.TryGetValue(character.Race, out var allowed))
                        items[character.Race] = allowed = new HashSet<ItemObject>();
                    foreach (var loadout in character.BattleEquipments.Concat(character.CivilianEquipments))
                    {
                        foreach (var (element, _) in loadout.YieldFilledEquipmentSlots())
                            if (element.Item != null) allowed.Add(element.Item);
                    }
                }
            }

            internal bool Allows(int race, ItemObject item)
                => items.TryGetValue(race, out var allowed) && allowed.Contains(item);
        }

        // New/load campaigns get a fresh catalogue without retaining old game objects.
        private static ConditionalWeakTable<Campaign, Catalogue> catalogues = new();
        internal static void Reset() => catalogues = new ConditionalWeakTable<Campaign, Catalogue>();
        private static Catalogue Current => Campaign.Current == null ? null
            : catalogues.GetValue(Campaign.Current, _ => new Catalogue());
        internal static bool Enabled => Current?.Enabled == true;

        internal static bool CanUse(Hero hero, ItemObject item)
        {
            if (hero?.CharacterObject == null || item == null) return false;
            var catalogue = Current;
            return catalogue?.Enabled != true || catalogue.Allows(hero.CharacterObject.Race, item);
        }
    }
}
