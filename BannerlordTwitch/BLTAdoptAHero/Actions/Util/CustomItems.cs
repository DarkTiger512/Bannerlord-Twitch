using System;
using System.Collections.Generic;
using System.Linq;
using BannerlordTwitch.Annotations;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace BLTAdoptAHero.Actions.Util
{
    public static class CustomItems
    {
#if DEBUG
        [CommandLineFunctionality.CommandLineArgumentFunction("testcraft", "blt")]
        [UsedImplicitly]
        public static string TestCraft(List<string> strings)
        {
            try
            {
                var item = CreateCraftedWeapon(Hero.MainHero, (EquipmentType) Enum.Parse(typeof(EquipmentType), strings[0]), int.Parse(strings[1]));

                if (item != null)
                {
                    if (!Hero.MainHero.BattleEquipment[EquipmentIndex.Weapon0].IsEmpty)
                    {
                        Hero.MainHero.PartyBelongedTo.ItemRoster.AddToCounts(new EquipmentElement(Hero.MainHero.BattleEquipment[EquipmentIndex.Weapon0].Item), 1);
                    }
                    Hero.MainHero.BattleEquipment[EquipmentIndex.Weapon0] = new(item);
                    return $"crafted {item.Name}";
                }

                return $"Couldn't craft a matching item";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("testarmor", "blt")]
        [UsedImplicitly]
        public static string TestModifiedArmor(List<string> strings)
        {
            var item = CampaignHelpers.AllItems.Where(i => i.ItemType == ItemObject.ItemTypeEnum.BodyArmor)
                .Shuffle()
                .OrderByDescending(i => i.Tier)
                .FirstOrDefault();
            var modifier = BLTCustomItemsCampaignBehavior.Current.CreateArmorModifier("Test {ITEMNAME}", 100);
            var slotItem = new EquipmentElement(item, modifier);
            Hero.MainHero.BattleEquipment[EquipmentIndex.Body] = slotItem;
            return $"Assigned {slotItem.GetModifiedItemName()} to {Hero.MainHero.Name}";
        }
        
        [CommandLineFunctionality.CommandLineArgumentFunction("testweapon", "blt")]
        [UsedImplicitly]
        public static string TestModifiedWeapon(List<string> strings)
        {
            var item = CampaignHelpers.AllItems.Where(i => i.ItemType == ItemObject.ItemTypeEnum.TwoHandedWeapon)
                .Shuffle()
                .OrderByDescending(i => i.Tier)
                .FirstOrDefault();
            var modifier = BLTCustomItemsCampaignBehavior.Current.CreateWeaponModifier("Test {ITEMNAME}", 200, 200, 200, 200);
            var slotItem = new EquipmentElement(item, modifier);
            Hero.MainHero.BattleEquipment[EquipmentIndex.Weapon0] = slotItem;
            return $"Assigned {slotItem.GetModifiedItemName()} to {Hero.MainHero.Name}";
        }
        
        [CommandLineFunctionality.CommandLineArgumentFunction("testbow", "blt")]
        [UsedImplicitly]
        public static string TestModifiedBow(List<string> strings)
        {
            var item = CampaignHelpers.AllItems.Where(i => i.ItemType == ItemObject.ItemTypeEnum.Bow)
                .Shuffle()
                .OrderByDescending(i => i.Tier)
                .FirstOrDefault();
            var modifier = BLTCustomItemsCampaignBehavior.Current.CreateWeaponModifier("Test {ITEMNAME}", 200, 200, 200, 200);
            var slotItem = new EquipmentElement(item, modifier);
            Hero.MainHero.BattleEquipment[EquipmentIndex.Weapon1] = slotItem;
            return $"Assigned {slotItem.GetModifiedItemName()} to {Hero.MainHero.Name}";
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("testammo", "blt")]
        [UsedImplicitly]
        public static string TestModifiedAmmo(List<string> strings)
        {
            var item = CampaignHelpers.AllItems.Where(i => i.ItemType == ItemObject.ItemTypeEnum.Arrows)
                .Shuffle()
                .OrderByDescending(i => i.Tier)
                .FirstOrDefault();
            var modifier = BLTCustomItemsCampaignBehavior.Current.CreateAmmoModifier("Test {ITEMNAME}", 100, 100);
            var slotItem = new EquipmentElement(item, modifier);
            Hero.MainHero.BattleEquipment[EquipmentIndex.Weapon2] = slotItem;
            return $"Assigned {slotItem.GetModifiedItemName()} to {Hero.MainHero.Name}";
        }
        
        [CommandLineFunctionality.CommandLineArgumentFunction("testmount", "blt")]
        [UsedImplicitly]
        public static string TestModifiedMount(List<string> strings)
        {
            var item = CampaignHelpers.AllItems.Where(i => i.ItemType == ItemObject.ItemTypeEnum.Horse)
                .Shuffle()
                .OrderByDescending(i => i.Tier)
                .FirstOrDefault();
            var modifier = BLTCustomItemsCampaignBehavior.Current.CreateMountModifier("Test {ITEMNAME}", 2, 2, 2, 2);
            var slotItem = new EquipmentElement(item, modifier);
            Hero.MainHero.BattleEquipment[EquipmentIndex.Horse] = slotItem;
            return $"Assigned {slotItem.GetModifiedItemName()} to {Hero.MainHero.Name}";
        }
#endif

        public static EquipmentType[] CraftableEquipmentTypes { get; set; } = {
            EquipmentType.Dagger,
            EquipmentType.OneHandedSword,
            EquipmentType.TwoHandedSword,
            EquipmentType.OneHandedAxe,
            EquipmentType.TwoHandedAxe,
            EquipmentType.OneHandedMace,
            EquipmentType.TwoHandedMace,
            EquipmentType.OneHandedLance,
            EquipmentType.TwoHandedLance,
            EquipmentType.OneHandedGlaive,
            EquipmentType.TwoHandedGlaive,
            EquipmentType.ThrowingKnives,
            EquipmentType.ThrowingAxes,
            EquipmentType.ThrowingJavelins,
        };

        public static ItemObject CreateCraftedWeapon(Hero hero, EquipmentType weaponType, int desiredTier)
            => CreateCulturedCraftedWeapon(hero, weaponType, desiredTier, hero.Culture);

        public static ItemObject CreateCulturedCraftedWeapon(Hero hero, EquipmentType weaponType, int desiredTier, CultureObject culture)
        {
            try
            {
                var settings = GlobalForgeConfig.Get();
                settings.Validate();
                // Preserve legacy reward signatures, but honor the caller's requested tier.
                var legacySettings = new BLTAdoptAHero.Util.ForgeSettings
                {
                    CandidateBudget = settings.CandidateBudget,
                    TargetTier = Math.Max(0, Math.Min(6, desiredTier)),
                    AllowHiddenParts = settings.AllowHiddenParts,
                    RestrictedPartIds = settings.RestrictedPartIds
                };
                var assets = BLTAdoptAHero.Util.ForgeAssets.Current;
                if (!assets.ForType(weaponType).Any()) return null;
                var result = assets.Generate(hero, weaponType, culture ?? hero.Culture,
                    BLTAdoptAHero.Util.ForgeStyle.Balanced, legacySettings, false);
                BLTAdoptAHero.Util.NativeForgeAdapter.PrepareIdentity(result.Item);
                BLTAdoptAHero.Util.NativeForgeAdapter.Register(result.Item);
                return result.Item;
            }
            catch (Exception ex)
            {
                Log.Error($"Crafting {weaponType} for {hero.Name} failed: {ex}");
                return null;
            }
        }
    }
}
