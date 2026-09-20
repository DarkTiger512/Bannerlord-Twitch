using System;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Util;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=}Enchant Weapon"), LocDescription("{=}Reforge a custom weapon for gold, up to +5."), UsedImplicitly]
    public class EnchantWeapon : HeroCommandHandlerBase
    {
        public sealed class Settings : IDocumentable
        {
            [LocDisplayName("{=}Damage Gain"), UsedImplicitly]
            public int DamageGain { get; set; } = 5;
            [LocDisplayName("{=}Weapon Speed Gain"), UsedImplicitly]
            public int SpeedGain { get; set; } = 2;
            [LocDisplayName("{=}Projectile Speed Gain"), UsedImplicitly]
            public int MissileSpeedGain { get; set; } = 5;
            [LocDisplayName("{=}Level 1 Gold Cost"), UsedImplicitly]
            public int Level1GoldCost { get; set; } = 100000;
            [LocDisplayName("{=}Level 2 Gold Cost"), UsedImplicitly]
            public int Level2GoldCost { get; set; } = 200000;
            [LocDisplayName("{=}Level 3 Gold Cost"), UsedImplicitly]
            public int Level3GoldCost { get; set; } = 300000;
            [LocDisplayName("{=}Level 4 Gold Cost"), UsedImplicitly]
            public int Level4GoldCost { get; set; } = 400000;
            [LocDisplayName("{=}Level 5 Gold Cost"), UsedImplicitly]
            public int Level5GoldCost { get; set; } = 500000;
            [LocDisplayName("{=}Level 2 Failure Percent"), UsedImplicitly]
            public int Level2FailurePercent { get; set; } = 15;
            [LocDisplayName("{=}Level 3 Failure Percent"), UsedImplicitly]
            public int Level3FailurePercent { get; set; } = 20;
            [LocDisplayName("{=}Level 4 Failure Percent"), UsedImplicitly]
            public int Level4FailurePercent { get; set; } = 25;
            [LocDisplayName("{=}Level 5 Failure Percent"), UsedImplicitly]
            public int Level5FailurePercent { get; set; } = 30;

            public int[] Costs() => new[] { Level1GoldCost, Level2GoldCost, Level3GoldCost, Level4GoldCost, Level5GoldCost };
            public int[] Failures() => new[] { 0, Level2FailurePercent, Level3FailurePercent, Level4FailurePercent, Level5FailurePercent };
            public int[] Gains() => new[] { DamageGain, SpeedGain, MissileSpeedGain };

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.P(Usage.Replace("<", "&lt;").Replace(">", "&gt;"));
                generator.P($"Choose +{DamageGain} damage, +{SpeedGain} weapon speed, or +{MissileSpeedGain} projectile speed (ranged weapons only). Maximum +5.");
                var costs = Costs();
                var failures = Failures();
                for (int i = 0; i < 5; i++)
                    generator.P($"Attempt +{i + 1}: {costs[i]:N0} gold, {failures[i]}% failure chance.");
                generator.P("Every attempt costs gold. Failure removes the latest enchantment. Original bonuses are protected; +1 is always guaranteed. Listing and previews are free.");
            }
        }

        private const string Usage = "!enchant lists weapons; !enchant <number> previews; !enchant <number> damage|speed|missilespeed buys one attempt.";
        public override Type HandlerConfigType => typeof(Settings);

        private static bool Eligible(EquipmentElement item) => item.Item != null &&
            BLTCustomItemsCampaignBehavior.Current.IsRegistered(item.ItemModifier) &&
            (item.Item.ItemType == ItemObject.ItemTypeEnum.OneHandedWeapon
             || item.Item.ItemType == ItemObject.ItemTypeEnum.TwoHandedWeapon
             || item.Item.ItemType == ItemObject.ItemTypeEnum.Polearm
             || item.Item.ItemType == ItemObject.ItemTypeEnum.Bow
             || item.Item.ItemType == ItemObject.ItemTypeEnum.Crossbow
             || item.Item.ItemType == ItemObject.ItemTypeEnum.Thrown);

        private static bool Ranged(EquipmentElement item) =>
            item.Item.ItemType == ItemObject.ItemTypeEnum.Bow || item.Item.ItemType == ItemObject.ItemTypeEnum.Crossbow
            || item.Item.ItemType == ItemObject.ItemTypeEnum.Thrown;

        protected override void ExecuteInternal(Hero hero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (config is not Settings settings)
            {
                onFailure("Enchantment configuration is missing.");
                return;
            }
            if (!EnchantmentPolicy.TryParse(context.Args, out var index, out var stat))
            {
                onFailure(Usage);
                return;
            }
            var campaign = BLTAdoptAHeroCampaignBehavior.Current;
            var custom = BLTCustomItemsCampaignBehavior.Current;
            var items = campaign.GetCustomItems(hero);
            if (index == 0)
            {
                var list = items.Select((item, i) => (item, i)).Where(x => Eligible(x.item))
                    .Select(x => $"#{x.i + 1} {x.item.GetModifiedItemName()} [+{custom.GetEnchantmentLevel(x.item.ItemModifier)}]").ToList();
                onSuccess((list.Count == 0 ? "You have no eligible custom weapons." : string.Join(" | ", list)) + " " + Usage);
                return;
            }
            if (index > items.Count || !Eligible(items[index - 1]))
            {
                onFailure("Select an owned custom weapon from !enchant. Ammunition, shields, armor, and mounts cannot be enchanted.");
                return;
            }
            var item = items[index - 1];
            var costs = settings.Costs();
            var failures = settings.Failures();
            var gains = settings.Gains();
            try { EnchantmentPolicy.Validate(costs, failures, gains); }
            catch (ArgumentException ex) { onFailure(ex.Message); return; }
            int level = custom.GetEnchantmentLevel(item.ItemModifier);
            if (level >= EnchantmentPolicy.MaxLevel)
            {
                onFailure($"{item.GetModifiedItemName()} is already enchanted to +5.");
                return;
            }
            if (!stat.HasValue)
            {
                onSuccess($"{item.GetModifiedItemName()} [+{level}]: damage +{gains[0]}, speed +{gains[1]}"
                    + (Ranged(item) ? $", missilespeed +{gains[2]}" : "")
                    + $". Attempt +{level + 1}: {costs[level]:N0} gold; {100 - failures[level]}% success / {failures[level]}% failure."
                    + " Failure removes the latest enchantment and still costs gold. " + Usage);
                return;
            }
            if (stat == EnchantmentStat.MissileSpeed && !Ranged(item))
            {
                onFailure("Projectile speed is only available for bows, crossbows, and throwing weapons.");
                return;
            }
            var error = EnchantmentPolicy.AttemptError(level, campaign.GetHeroGold(hero), costs[level],
                Mission.Current != null, hero.HeroState == Hero.CharacterStates.Prisoner, campaign.IsItemBeingAuctioned(item));
            if (error != null) { onFailure(error); return; }
            string result;
            try
            {
                var outcome = custom.Enchant(item.ItemModifier, stat.Value, gains[(int)stat.Value], failures[level],
                    MBRandom.RandomInt(100), hero, costs[level]);
                string statName = outcome.change.Stat == EnchantmentStat.MissileSpeed ? "projectile speed"
                    : outcome.change.Stat == EnchantmentStat.Speed ? "weapon speed" : "damage";
                result = $"{item.GetModifiedItemName()}: reforging {(outcome.success ? "succeeded" : "failed")}, "
                    + $"{(outcome.success ? "+" : "-")}{outcome.change.Gain} {statName}; now +{outcome.level}. "
                    + $"Spent {costs[level]:N0} gold; balance {campaign.GetHeroGold(hero):N0}.";
            }
            catch (Exception ex)
            {
                Log.Error($"EnchantWeapon: {ex}");
                onFailure("Enchantment could not be applied. No gold was charged.");
                return;
            }
            onSuccess(result);
        }
    }
}
