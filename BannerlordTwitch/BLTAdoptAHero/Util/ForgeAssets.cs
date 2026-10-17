using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace BLTAdoptAHero.Util
{
    internal sealed class ForgeAssets
    {
        private static ConditionalWeakTable<Campaign, ForgeAssets> catalogues = new();
        internal static void Reset() => catalogues = new ConditionalWeakTable<Campaign, ForgeAssets>();
        internal static ForgeAssets Current => Campaign.Current == null ? throw new InvalidOperationException("A campaign must be loaded.")
            : catalogues.GetValue(Campaign.Current, _ => new ForgeAssets());

        internal readonly List<CraftingTemplate> Templates = CraftingTemplate.All.ToList();
        internal readonly List<ItemObject> Items = CampaignHelpers.AllItems.Where(i => i != null && !i.NotMerchandise && !i.IsCraftedByPlayer && IsWeapon(i)).ToList();
        internal readonly List<CultureObject> Cultures;
        internal static readonly EquipmentType[] WeaponTypes = Enum.GetValues(typeof(EquipmentType)).Cast<EquipmentType>()
            .Where(t => t != EquipmentType.None && t != EquipmentType.Num && t != EquipmentType.Shield
                && t != EquipmentType.Arrows && t != EquipmentType.Bolts && t != EquipmentType.SlingStones && t != EquipmentType.Stone).ToArray();

        private ForgeAssets()
        {
            Cultures = CampaignHelpers.MainCultures.Concat(CampaignHelpers.AllCultures.Where(c =>
                Items.Any(i => i.Culture == c))).Distinct().OrderBy(c => c.Name.ToString()).ToList();
        }

        internal static bool IsWeapon(ItemObject item) => item?.PrimaryWeapon != null &&
            (item.PrimaryWeapon.IsMeleeWeapon || item.PrimaryWeapon.IsRangedWeapon) && !item.PrimaryWeapon.IsAmmo && !item.PrimaryWeapon.IsShield;

        internal List<CraftingTemplate> ForType(EquipmentType type) => Templates.Where(t =>
            t.WeaponDescriptions?.Any(w => w.WeaponClass == EquipmentTypeHelpers.GetWeaponClass(type)) == true).ToList();

        internal bool Available(EquipmentType type) => Items.Any(i => i.IsEquipmentType(type));

        internal CultureObject ResolveCulture(string text, ForgeSettings settings)
        {
            var exact = Cultures.Where(c => c.StringId.Equals(text, StringComparison.OrdinalIgnoreCase)).ToList();
            if (exact.Count == 0) exact = Cultures.Where(c => c.Name.ToString().Equals(text, StringComparison.OrdinalIgnoreCase)).ToList();
            if (exact.Count == 0 && settings.CultureAliases != null)
            {
                var alias = settings.CultureAliases.FirstOrDefault(p => p.Key.Equals(text, StringComparison.OrdinalIgnoreCase));
                if (alias.Value != null) exact = Cultures.Where(c => c.StringId.Equals(alias.Value, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            if (exact.Count == 0) exact = Cultures.Where(c => c.Name.ToString().StartsWith(text, StringComparison.OrdinalIgnoreCase)).ToList();
            if (exact.Count == 1) return exact[0];
            throw new ArgumentException(exact.Count == 0 ? $"Unknown culture '{text}'. Use !forge cultures."
                : "Ambiguous culture: " + string.Join(", ", exact.Select(c => $"{c.Name} ({c.StringId})")));
        }

        internal EquipmentType ResolveType(string text, ForgeSettings settings, out CraftingTemplate template)
        {
            string target = settings.TypeAliases?.FirstOrDefault(p => ForgePolicy.Normalize(p.Key) == ForgePolicy.Normalize(text)).Value ?? text;
            template = Templates.FirstOrDefault(t => t.StringId.Equals(target, StringComparison.OrdinalIgnoreCase));
            if (ForgePolicy.TryEnum(target, out EquipmentType type) && WeaponTypes.Contains(type) && Available(type))
            {
                template = null; // A weapon family must include mod templates as well as vanilla.
                return type;
            }
            if (template != null)
            {
                var selectedTemplate = template;
                var types = WeaponTypes.Where(t => Available(t) && ForType(t).Contains(selectedTemplate)).ToList();
                if (types.Count > 0) return types[0];
            }
            throw new ArgumentException($"Unavailable weapon type '{text}'. Use !forge types.");
        }

        internal static bool Allowed(Hero hero, ItemObject item, bool requireUsable)
        {
            if (!IsWeapon(item) || BLTAdoptAHeroModule.CommonConfig.RestrictedItemIds.Contains(item.StringId ?? "")) return false;
            // CanUseItem is the existing branch-local boundary, including TOAM race compatibility.
            return EquipHero.CanUseItem(hero, item, !requireUsable,
                requireUsable && (hero.GetClass()?.Mounted == true || !hero.BattleEquipment.Horse.IsEmpty));
        }

        internal (ForgeResult result, EquipmentType type) GenerateRandom(Hero hero, CultureObject culture, ForgeStyle style, ForgeSettings settings)
        {
            var types = hero.GetClass()?.Weapons.Where(t => WeaponTypes.Contains(t) && Available(t)).Distinct().ToList();
            if (types == null || types.Count == 0)
                types = hero.BattleEquipment.YieldFilledWeaponSlots().Select(s => s.element.Item.GetEquipmentType())
                    .Where(t => WeaponTypes.Contains(t) && Available(t)).Distinct().ToList();
            if (types.Count == 0) throw new InvalidOperationException("No available weapon type fits your class or loadout. Choose from !forge types.");
            while (types.Count > 0)
            {
                int index = MBRandom.RandomInt(types.Count);
                var type = types[index];
                types.RemoveAt(index);
                try { return (Generate(hero, type, culture, style, settings, true), type); }
                catch (InvalidOperationException) { /* Try another class/loadout type before charging. */ }
            }
            throw new InvalidOperationException("No usable weapon could be forged for your class or loadout. No gold charged.");
        }

        internal ForgeResult Generate(Hero hero, EquipmentType type, CultureObject culture, ForgeStyle style,
            ForgeSettings settings, bool requireUsable, CraftingTemplate template = null)
        {
            // Reuse installed item identities and assets. Style/quality belong to the owned modifier.
            // Legacy template arguments remain aliases for the corresponding weapon family.
            var eligible = Items.Where(i => i.IsEquipmentType(type) && Allowed(hero, i, requireUsable)).ToList();
            var cultured = eligible.Where(i => i.Culture == culture).ToList();
            var pool = cultured.Count > 0 ? cultured : eligible;
            if (pool.Count == 0) throw new InvalidOperationException("No eligible installed weapon of that type. No gold charged.");
            float nearestTier = pool.Min(i => Math.Abs(i.Tierf - settings.TargetTier));
            pool = pool.Where(i => Math.Abs(i.Tierf - settings.TargetTier) == nearestTier).ToList();
            var item = pool[MBRandom.RandomInt(pool.Count)];
            return new ForgeResult { Item = item, Fallback = item.Culture != culture, NativeCrafted = false,
                ActualCulture = item.Culture as CultureObject };
        }
    }

    internal sealed class ForgeResult
    {
        internal ItemObject Item;
        internal bool NativeCrafted;
        internal bool Fallback;
        internal CultureObject ActualCulture;
        internal string Description => (NativeCrafted ? "native-crafted design" : "existing weapon design")
            + (Fallback ? $"; culture fallback (neutral or mixed assets; profile {ActualCulture?.Name.ToString() ?? "neutral"})" : "");
    }

}
