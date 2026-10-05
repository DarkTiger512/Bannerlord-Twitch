using System;
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
    /// Ordinary handheld equipment is shared across races. Authored same-race loadouts
    /// constrain fitted armour, mounts and explicitly creature-marked handheld items.
    /// A class name or missing troop usage is not evidence that a weapon is unusable.
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

        private static bool IsSharedHandheld(ItemObject item) => item.ItemType is
            ItemObject.ItemTypeEnum.OneHandedWeapon or ItemObject.ItemTypeEnum.TwoHandedWeapon
            or ItemObject.ItemTypeEnum.Polearm or ItemObject.ItemTypeEnum.Bow
            or ItemObject.ItemTypeEnum.Crossbow or ItemObject.ItemTypeEnum.Sling
            or ItemObject.ItemTypeEnum.Thrown or ItemObject.ItemTypeEnum.Arrows
            or ItemObject.ItemTypeEnum.Bolts or ItemObject.ItemTypeEnum.SlingStones
            or ItemObject.ItemTypeEnum.Shield;

        // TAOM has no universal weapon race-compatibility flag. Known creature ID markers
        // remain same-race-only; RestrictedItemIds can cover additional custom items.
        // This is a naming guard, not a complete size/skeleton compatibility guarantee.
        private static bool IsCreatureMarked(ItemObject item)
        {
            var id = (item.StringId ?? "").ToLowerInvariant();
            return id.Contains("troll") || id.Contains("olog") || id.Contains("giant")
                || id.Split(new[] { '_', '-', '.' }, StringSplitOptions.RemoveEmptyEntries)
                    .Any(token => token == "ent" || token == "ents" || token == "balrog");
        }

        internal static bool CanUse(Hero hero, ItemObject item)
        {
            if (hero?.CharacterObject == null || item == null) return false;
            var catalogue = Current;
            if (catalogue?.Enabled != true) return true;
            if (IsSharedHandheld(item) && !IsCreatureMarked(item)) return true;
            return catalogue.Allows(hero.CharacterObject.Race, item);
        }
    }
}
