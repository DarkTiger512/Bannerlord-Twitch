using System;
using System.Collections.Generic;
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
    [LocDisplayName("{=BLTAmmoName}Check Ammo"),
     LocDescription("{=BLTAmmoDescription}Shows your hero's remaining ammunition in the current mission"),
     UsedImplicitly]
    internal class CheckAmmo : HeroCommandHandlerBase
    {
        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (Mission.Current == null)
            {
                onFailure("{=BLTAmmoNoMission}No active mission - you must be in battle to check ammunition!".Translate());
                return;
            }

            var agent = adoptedHero.GetAgent();
            if (agent == null)
            {
                onFailure("{=BLTAmmoHeroAbsent}Your hero is not currently in this mission! Use !summon or !attack first.".Translate());
                return;
            }

            if (!agent.IsActive())
            {
                onFailure("{=BLTAmmoHeroInactive}Your hero is no longer active in this mission!".Translate());
                return;
            }

            var equipment = agent.Equipment;
            var stacks = new List<AmmoStackSnapshot>();
            bool hasRangedWeapon = false;
            for (EquipmentIndex slot = EquipmentIndex.WeaponItemBeginSlot;
                 slot < EquipmentIndex.NumAllWeaponSlots;
                 slot++)
            {
                var weapon = equipment[slot];
                if (weapon.IsEmpty) continue;

                if (weapon.IsAnyAmmo())
                {
                    stacks.Add(new AmmoStackSnapshot
                    {
                        Slot = (int)slot,
                        Name = weapon.Item?.Name?.ToString() ?? "Unknown ammunition",
                        Current = weapon.Ammo,
                        Maximum = weapon.MaxAmmo
                    });
                    Log.Trace($"Ammo slot {slot} for {adoptedHero.Name}: {weapon.Item?.StringId ?? "unknown"} {weapon.Ammo}/{weapon.MaxAmmo}");
                }
                else if (IsRangedWeapon(weapon.Item))
                {
                    hasRangedWeapon = true;
                }
            }

            var report = AmmoReport.Create(stacks, hasRangedWeapon);
            switch (report.Kind)
            {
                case AmmoReportKind.Available:
                    onSuccess("{=BLTAmmoAvailable}Ammunition: {DETAILS} | Total: {CURRENT}/{MAXIMUM}"
                        .Translate(("DETAILS", report.Details), ("CURRENT", report.TotalCurrent),
                            ("MAXIMUM", report.TotalMaximum)));
                    break;
                case AmmoReportKind.Depleted:
                    onFailure("{=BLTAmmoDepleted}Out of ammunition! {DETAILS} | Total: 0/{MAXIMUM}"
                        .Translate(("DETAILS", report.Details), ("MAXIMUM", report.TotalMaximum)));
                    break;
                case AmmoReportKind.MissingAmmo:
                    onFailure("{=BLTAmmoMissing}Your hero has a ranged weapon but no ammunition equipped.".Translate());
                    break;
                default:
                    onFailure("{=BLTAmmoNoRanged}Your hero has no ranged or throwing ammunition equipped.".Translate());
                    break;
            }
        }

        private static bool IsRangedWeapon(ItemObject item)
        {
            if (item == null) return false;
            switch (item.ItemType)
            {
                case ItemObject.ItemTypeEnum.Bow:
                case ItemObject.ItemTypeEnum.Crossbow:

                case ItemObject.ItemTypeEnum.Pistol:
                case ItemObject.ItemTypeEnum.Musket:
                    return true;
                default:
                    return false;
            }
        }
    }
}
