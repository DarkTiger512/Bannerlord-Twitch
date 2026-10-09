using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("{=EquipCustom}Equip Custom Item"),
     LocDescription("{=EquipCustomDesc}Equip a custom item from your inventory to all matching slots"),
     UsedImplicitly]
    public class EquipCustomItemAction : HeroCommandHandlerBase
    {
        [CategoryOrder("General", 0)]
        private class Settings : IDocumentable
        {
            [LocDisplayName("{=EquipCustomEnabled}Enabled"),
             LocCategory("General", "{=EquipCustomGeneral}General"),
             LocDescription("{=EquipCustomEnabledDesc}Enable this action"),
             PropertyOrder(1), UsedImplicitly]
            public bool Enabled { get; set; } = true;

            [LocDisplayName("{=EquipCustomGoldCost}Gold Cost"),
             LocCategory("General", "{=EquipCustomGeneral}General"),
             LocDescription("{=EquipCustomGoldCostDesc}Cost in gold to equip a custom item"),
             PropertyOrder(2), UsedImplicitly]
            public int GoldCost { get; set; } = 0;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                if (Enabled)
                {
                    generator.Value("<strong>Enabled:</strong> Yes");
                    if (GoldCost > 0)
                    {
                        generator.Value("<strong>Gold Cost:</strong> {cost}{icon}"
                            .Translate(("cost", GoldCost.ToString()), ("icon", Naming.Gold)));
                    }
                    generator.Value("<strong>Usage:</strong> !equipcustom [item name or number]");
                    generator.Value("Use without arguments to list your custom items");
                }
                else
                {
                    generator.Value("<strong>Enabled:</strong> No");
                }
            }
        }

        public override Type HandlerConfigType => typeof(Settings);

        protected override void ExecuteInternal(
            Hero adoptedHero,
            ReplyContext context,
            object config,
            Action<string> onSuccess,
            Action<string> onFailure)
        {
            if (config is not Settings settings) return;

            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }

            if (!settings.Enabled)
            {
                onFailure("{=EquipCustomDisabled}This action is disabled".Translate());
                return;
            }

            if (Mission.Current != null)
            {
                onFailure("{=EquipCustomInMission}You cannot use this action during a mission!".Translate());
                return;
            }

            if (adoptedHero.HeroState == Hero.CharacterStates.Prisoner)
            {
                onFailure("{=EquipCustomPrisoner}You cannot use this action while imprisoned!".Translate());
                return;
            }

            // Check gold cost
            if (settings.GoldCost > 0 &&
                BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.GoldCost)
            {
                onFailure(Naming.NotEnoughGold(settings.GoldCost,
                    BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }

            // Get hero's custom items
            var customItems = BLTAdoptAHeroCampaignBehavior.Current.GetCustomItems(adoptedHero);

            if (customItems == null || !customItems.Any())
            {
                onFailure("{=EquipCustomNone}You don't have any custom items!".Translate());
                return;
            }

            // If no arguments, list custom items
            if (string.IsNullOrWhiteSpace(context.Args))
            {
                ListCustomItems(adoptedHero, customItems, onSuccess);
                return;
            }

            // Try to find the item by name or index
            EquipmentElement? itemToEquip = FindCustomItem(customItems, context.Args.Trim());

            if (!itemToEquip.HasValue || itemToEquip.Value.IsEmpty)
            {
                onFailure("{=EquipCustomNotFound}Custom item '{itemName}' not found! Use !equipcustom to see your items."
                    .Translate(("itemName", context.Args.Trim())));
                return;
            }

            try
            {
                var campaign = BLTAdoptAHeroCampaignBehavior.Current;
                var previous = adoptedHero.BattleEquipment.YieldEquipmentSlots().ToList();
                int slotsEquipped = 0;
                campaign.CommitEnchantmentPurchase(adoptedHero, settings.GoldCost, () =>
                {
                    slotsEquipped = EquipCustomItemToAllSlots(adoptedHero, itemToEquip.Value);
                    if (slotsEquipped == 0)
                        {
                        var allowed = adoptedHero.GetClass()?.IndexedSlots.Select(s => s.type)
                            ?? adoptedHero.BattleEquipment.YieldFilledWeaponSlots().Select(s => s.element.Item.GetEquipmentType());
                        var modes = itemToEquip.Value.Item.Weapons?.Select(w => w.WeaponClass).Distinct()
                            ?? Enumerable.Empty<WeaponClass>();
                        throw new InvalidOperationException($"Cannot equip {itemToEquip.Value.Item.GetEquipmentType()} (native modes: {string.Join(", ", modes)}). Class/loadout slots: {string.Join(", ", allowed.Where(t => t != EquipmentType.None).Distinct())}. No gold charged.");
                    }
                }, () =>
                {
                    foreach (var slot in previous) adoptedHero.BattleEquipment[slot.index] = slot.element;
                });

                if (slotsEquipped > 0)
                {
                    string itemName = RewardHelpers.GetItemNameAndModifiers(itemToEquip.Value);
                    string message = slotsEquipped == 1
                        ? "{=EquipCustomSuccess}Equipped {itemName}!"
                            .Translate(("itemName", itemName))
                        : "{=EquipCustomSuccessMulti}Equipped {itemName} to {count} slots!"
                            .Translate(("itemName", itemName), ("count", slotsEquipped.ToString()));

                    onSuccess(message);

                    Log.ShowInformation(
                        "{=EquipCustomLog}{heroName} equipped {itemName}!"
                            .Translate(("heroName", adoptedHero.Name.ToString()), ("itemName", itemName)),
                        adoptedHero.CharacterObject);
                }
                else
                {
                    onFailure("{=EquipCustomNoSlots}Could not find any suitable equipment slots for this item!".Translate());
                }
            }
            catch (InvalidOperationException ex)
            {
                onFailure(ex.Message);
            }
            catch (Exception ex)
            {
                onFailure($"Failed to equip item: {ex.Message}");
                Log.Error($"EquipCustomItem error: {ex}");
            }
        }

        private void ListCustomItems(Hero hero, List<EquipmentElement> customItems, Action<string> onSuccess)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{=EquipCustomList}Your custom items:".Translate());

            for (int i = 0; i < customItems.Count; i++)
            {
                var item = customItems[i];
                string itemName = RewardHelpers.GetItemNameAndModifiers(item);
                sb.AppendLine($"{i + 1}. {itemName} ({item.Item.ItemType})");
            }

            sb.AppendLine();
            sb.AppendLine("{=EquipCustomListHelp}Use: !equipcustom [item name or number]".Translate());

            onSuccess(sb.ToString().TrimEnd());
        }

        private EquipmentElement? FindCustomItem(List<EquipmentElement> customItems, string searchTerm)
        {
            // Try to parse as index (1-based)
            if (int.TryParse(searchTerm, out int index))
            {
                if (index >= 1 && index <= customItems.Count)
                {
                    return customItems[index - 1];
                }
            }

            // Search by name (case-insensitive, partial match)
            searchTerm = searchTerm.ToLower();
            foreach (var item in customItems)
                if (!item.IsEmpty && item.GetModifiedItemName()?.ToString().ToLower().Contains(searchTerm) == true)
                    return item;
            return null;
        }

        private int EquipCustomItemToAllSlots(Hero hero, EquipmentElement customItem)
        {
            int slotsEquipped = 0;
            var equipment = hero.BattleEquipment;
            var heroClass = hero.GetClass();

            // Get indexed slots from hero's class (tuple of index and type)
            if (customItem.IsEmpty || equipment == null) return 0;
            var indexedSlots = heroClass?.IndexedSlots
                ?? equipment.YieldFilledWeaponSlots().Select(s => (index: s.index, type: s.element.Item.GetEquipmentType()));

            foreach (var (slotIndex, slotType) in indexedSlots)
            {
                // Check if this slot type can hold this item
                if (!IsItemCompatibleWithSlot(customItem.Item, slotType))
                    continue;

                // Class compatibility determines the destination, regardless of what currently occupies it.
                equipment[slotIndex] = customItem;
                slotsEquipped++;
            }

            return slotsEquipped;
        }

        private bool IsItemCompatibleWithSlot(ItemObject item, EquipmentType slotType)
        {
            if (item == null || slotType == EquipmentType.None || slotType == EquipmentType.Num) return false;
            // Manual equip historically allowed thrusting and swinging polearms in the same handed slot.
            // Keep that behavior; the stricter generator matcher distinguishes lances from glaives.
            // Template/display names and the primary mode do not describe every usable mode.
            if (slotType is EquipmentType.OneHandedLance or EquipmentType.OneHandedGlaive)
                return item.AnyWeaponMatches(w => w.WeaponClass == WeaponClass.OneHandedPolearm);
            if (slotType is EquipmentType.TwoHandedLance or EquipmentType.TwoHandedGlaive)
                return item.AnyWeaponMatches(w => w.WeaponClass is WeaponClass.TwoHandedPolearm or WeaponClass.LowGripPolearm);
            return item.IsEquipmentType(slotType);
        }
    }
}
