using System;
using System.Linq;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace BLTAdoptAHero.Util
{
    internal static class ForgeService
    {
        internal static (int damage, int speed) Contributions(ItemObject item, ForgeStyle style, ForgeQuality quality, ForgeSettings settings)
        {
            var weapon = item.PrimaryWeapon;
            int damage = Math.Max(weapon.SwingDamage, Math.Max(weapon.ThrustDamage, weapon.MissileDamage));
            int speed = Math.Max(weapon.SwingSpeed, weapon.ThrustSpeed);
            return ForgePolicy.Contributions(damage, speed, style, quality, settings);
        }

        internal static string Guard(Hero hero, int cost, EquipmentElement item = default) => ForgePolicy.PurchaseError(
            Mission.Current != null, hero.HeroState == Hero.CharacterStates.Prisoner,
            item.Item != null && BLTAdoptAHeroCampaignBehavior.Current.IsItemBeingAuctioned(item),
            BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero), cost);

        internal static string Forge(Hero hero, EquipmentType type, CultureObject culture, ForgeStyle style,
            ForgeQuality quality, ForgeSettings settings, bool explicitCulture, bool random, CraftingTemplate template)
        {
            var campaign = BLTAdoptAHeroCampaignBehavior.Current;
            var custom = BLTCustomItemsCampaignBehavior.Current;
            int cost = settings.Price(quality, explicitCulture);
            var error = Guard(hero, cost);
            if (error != null) throw new InvalidOperationException(error);
            var inventory = campaign.GetCustomItems(hero);
            if (inventory.Count >= BLTAdoptAHeroModule.CommonConfig.CustomItemLimit)
                throw new InvalidOperationException("Custom inventory is full. No gold charged.");
            ForgeResult result;
            if (random)
            {
                var generated = ForgeAssets.Current.GenerateRandom(hero, culture, style, settings);
                result = generated.result;
                type = generated.type;
            }
            else result = ForgeAssets.Current.Generate(hero, type, culture, style, settings, false, template);
            if (result.NativeCrafted) NativeForgeAdapter.PrepareIdentity(result.Item);
            var contributions = Contributions(result.Item, style, quality, settings);
            var oldItems = inventory.ToList();
            var oldBattle = hero.BattleEquipment.YieldEquipmentSlots().ToList();
            var oldCivilian = hero.CivilianEquipment.YieldEquipmentSlots().ToList();
            ItemModifier modifier = null;
            string assignment = "stored";
            campaign.CommitEnchantmentPurchase(hero, cost, () =>
            {
                if (inventory.Count >= BLTAdoptAHeroModule.CommonConfig.CustomItemLimit)
                    throw new InvalidOperationException("Custom inventory is full.");
                modifier = custom.CreateWeaponModifier("Forged {ITEMNAME}", contributions.damage, contributions.speed, 0, 0);
                custom.SetInitialForgeMetadata(modifier, new ForgeMetadata
                {
                    NativeCrafted = result.NativeCrafted, BaseItemId = result.Item.StringId,
                    CultureId = result.ActualCulture?.StringId, TemplateId = result.Item.WeaponDesign?.Template?.StringId,
                    Style = style, Quality = quality, AppliedDamage = contributions.damage, AppliedSpeed = contributions.speed
                });
                var element = new EquipmentElement(result.Item, modifier);
                campaign.AddCustomItem(hero, element);
                if (ForgeAssets.Allowed(hero, result.Item, true))
                {
                    var slots = hero.GetClass()?.IndexedWeapons.Where(s => s.type == type).Select(s => s.index).ToList()
                        ?? hero.BattleEquipment.YieldFilledWeaponSlots().Where(s => s.element.Item.IsEquipmentType(type)).Select(s => s.index).ToList();
                    foreach (var slot in slots)
                    {
                        var current = hero.BattleEquipment[slot];
                        if (!current.IsEmpty && custom.IsRegistered(current.ItemModifier)) continue;
                        if (!current.IsEmpty && current.Item.Tierf > result.Item.Tierf) continue;
                        hero.BattleEquipment[slot] = element;
                        assignment = "equipped";
                        break;
                    }
                }
                // Register native identity last, after all reversible inventory/equipment operations.
                if (result.NativeCrafted) NativeForgeAdapter.Register(result.Item);
            }, () =>
            {
                inventory.Clear();
                inventory.AddRange(oldItems);
                foreach (var slot in oldBattle) hero.BattleEquipment[slot.index] = slot.element;
                foreach (var slot in oldCivilian) hero.CivilianEquipment[slot.index] = slot.element;
                custom.RemoveNewForgeModifier(modifier);
                if (result.NativeCrafted && MBObjectManager.Instance.GetObject<ItemObject>(result.Item.StringId) == result.Item)
                    MBObjectManager.Instance.UnregisterObject(result.Item);
            });
            return $"{quality} {style} {result.Item.Name}: {assignment}; {result.Description}; damage {contributions.damage:+0;-0;0}, speed {contributions.speed:+0;-0;0}. Spent {cost:N0} gold; balance {campaign.GetHeroGold(hero):N0}.";
        }
    }
}
