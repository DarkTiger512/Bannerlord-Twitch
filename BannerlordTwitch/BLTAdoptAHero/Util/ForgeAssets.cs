using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

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
            var partCultures = Templates.SelectMany(t => t.Pieces).Where(p => p.IsValid).Select(p => p.Culture).ToList();
            Cultures = CampaignHelpers.MainCultures.Concat(CampaignHelpers.AllCultures.Where(c =>
                partCultures.Contains(c) || Items.Any(i => i.Culture == c))).Distinct().OrderBy(c => c.Name.ToString()).ToList();
        }

        internal static bool IsWeapon(ItemObject item) => item?.PrimaryWeapon != null &&
            (item.PrimaryWeapon.IsMeleeWeapon || item.PrimaryWeapon.IsRangedWeapon) && !item.PrimaryWeapon.IsAmmo && !item.PrimaryWeapon.IsShield;

        internal List<CraftingTemplate> ForType(EquipmentType type) => Templates.Where(t =>
            t.WeaponDescriptions?.Any(w => w.WeaponClass == EquipmentTypeHelpers.GetWeaponClass(type)) == true).ToList();

        internal bool Available(EquipmentType type) => ForType(type).Count > 0 || Items.Any(i => i.IsEquipmentType(type));

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
                var types = WeaponTypes.Where(t => ForType(t).Contains(selectedTemplate)).ToList();
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
            var candidates = settings.Copy();
            // Divide the native budget across types, so random retries remain bounded.
            int nativeTypes = types.Count(t => ForType(t).Count > 0);
            candidates.CandidateBudget = Math.Max(1, settings.CandidateBudget / Math.Max(1, nativeTypes));
            int remaining = settings.CandidateBudget;
            while (types.Count > 0)
            {
                int index = MBRandom.RandomInt(types.Count);
                var type = types[index];
                types.RemoveAt(index);
                if (ForType(type).Count > 0)
                {
                    if (remaining == 0) continue;
                    candidates.CandidateBudget = Math.Min(candidates.CandidateBudget, remaining);
                    remaining -= candidates.CandidateBudget;
                }
                try { return (Generate(hero, type, culture, style, candidates, true), type); }
                catch (InvalidOperationException) { /* Try another class/loadout type before charging. */ }
            }
            throw new InvalidOperationException("No usable weapon could be forged for your class or loadout. No gold charged.");
        }

        internal ForgeResult Generate(Hero hero, EquipmentType type, CultureObject culture, ForgeStyle style,
            ForgeSettings settings, bool requireUsable, CraftingTemplate template = null)
        {
            var templates = template == null ? ForType(type) : new List<CraftingTemplate> { template };
            if (templates.Count > 0) return NativeForgeAdapter.Generate(hero, type, culture, style, settings, requireUsable, templates);
            var eligible = Items.Where(i => i.IsEquipmentType(type) && Allowed(hero, i, requireUsable)).ToList();
            var cultured = eligible.Where(i => i.Culture == culture).ToList();
            var pool = cultured.Count > 0 ? cultured : eligible;
            if (pool.Count == 0) throw new InvalidOperationException("No eligible installed weapon of that type. No gold charged.");
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

    // All native construction and registration lives here; candidate items are never registered.
    internal static class NativeForgeAdapter
    {
        internal static ForgeResult Generate(Hero hero, EquipmentType type, CultureObject culture, ForgeStyle style,
            ForgeSettings settings, bool requireUsable, List<CraftingTemplate> templates)
        {
            ForgeResult best = null;
            double bestScore = double.MaxValue;
            Exception lastError = null;
            for (int attempt = 0; attempt < settings.CandidateBudget; attempt++)
            {
                var template = templates[MBRandom.RandomInt(templates.Count)];
                try
                {
                    var pieces = Enumerable.Range(0, 4).Select(i => WeaponDesignElement.GetInvalidPieceForType((CraftingPiece.PieceTypes)i)).ToArray();
                    bool fallback = false, matched = false;
                    foreach (var slot in template.BuildOrders)
                    {
                        var pool = template.Pieces.Where(p => p.PieceType == slot.PieceType && p.IsValid
                            && (settings.AllowHiddenParts || !p.IsHiddenOnDesigner)
                            && !(settings.RestrictedPartIds?.Contains(p.StringId) ?? false)).ToList();
                        var same = pool.Where(p => p.Culture == culture && culture != null).ToList();
                        var neutral = pool.Where(p => p.Culture == null).ToList();
                        if (same.Count > 0) { pool = same; matched = true; }
                        else if (neutral.Count > 0) pool = neutral;
                        else if (pool.Count > 0) fallback = true;
                        if (pool.Count == 0)
                        {
                            if (slot.Order >= 0) throw new InvalidOperationException("Missing required crafting pieces.");
                            continue;
                        }
                        var part = pool[MBRandom.RandomInt(pool.Count)];
                        pieces[(int)slot.PieceType] = WeaponDesignElement.CreateUsablePiece(part, 100);
                    }
                    fallback |= culture != null && !matched;
                    var actual = matched ? culture : pieces.Where(p => p.IsValid && p.CraftingPiece.Culture != null)
                        .GroupBy(p => p.CraftingPiece.Culture).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key as CultureObject;
                    var design = new WeaponDesign(template, template.TemplateName, pieces, null);
                    ItemObject item = null;
                    Crafting.GenerateItem(design, template.TemplateName, actual, template.ItemModifierGroup, ref item, null);
                    if (item == null || !item.IsEquipmentType(type) || !ForgeAssets.Allowed(hero, item, requireUsable)) continue;
                    // Culture fidelity wins first, then target tier, then native style statistics.
                    var weapon = item.PrimaryWeapon;
                    double styleScore = style == ForgeStyle.Swift ? item.Weight * 2 - Math.Max(weapon.SwingSpeed, weapon.ThrustSpeed) * .02
                        : style == ForgeStyle.Heavy ? -item.Weight * 2 - Math.Max(weapon.SwingDamage, weapon.ThrustDamage) * .02 : 0;
                    double score = (fallback ? 10000 : 0) + Math.Abs(item.Tierf - settings.TargetTier) * 100 + styleScore;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = new ForgeResult { Item = item, NativeCrafted = true, Fallback = fallback, ActualCulture = actual };
                    }
                }
                catch (Exception ex) { lastError = ex; }
            }
            if (best != null) return best;
            if (lastError != null) Log.Error($"Native forge failed: {lastError}");
            throw new InvalidOperationException("No compatible native design could be made with the installed crafting assets. No gold charged.");
        }

        internal static void PrepareIdentity(ItemObject item)
        {
            string id = Guid.NewGuid().ToString();
            // Regenerate with a unique native design identity, not merely a unique object ID.
            var source = item;
            Crafting.GenerateItem(source.WeaponDesign, source.Name, source.Culture,
                source.WeaponDesign.Template.ItemModifierGroup, ref item, id);
            if (!ReferenceEquals(source, item)) throw new InvalidOperationException("Native crafting unexpectedly replaced the selected item.");
        }

        internal static void Register(ItemObject item)
        {
            MBObjectManager.Instance.RegisterObject(item);
            CampaignEventDispatcher.Instance.OnNewItemCrafted(item, null, false);
        }
    }
}
