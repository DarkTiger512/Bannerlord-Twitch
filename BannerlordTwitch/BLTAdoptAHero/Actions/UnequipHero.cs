using System;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.Util;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=}Unequip Hero"),
     LocDescription("{=}Unequip one selected slot without changing class, equipment tier, or custom-item storage."),
     UsedImplicitly]
    public class UnequipHero : HeroCommandHandlerBase
    {
        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            var args = (context.Args ?? "").Trim().ToLowerInvariant()
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            bool civilian = args.Length == 2 && args[0] == "civilian";
            string selection = args.Length == 1 ? args[0] : civilian ? args[1] : "";
            EquipmentIndex? slot = selection switch
            {
                "weapon1" => EquipmentIndex.Weapon0,
                "weapon2" => EquipmentIndex.Weapon1,
                "weapon3" => EquipmentIndex.Weapon2,
                "weapon4" => EquipmentIndex.Weapon3,
                "head" or "helmet" => EquipmentIndex.Head,
                "body" => EquipmentIndex.Body,
                "legs" or "boots" => EquipmentIndex.Leg,
                "hands" or "gloves" => EquipmentIndex.Gloves,
                "cape" => EquipmentIndex.Cape,
                "mount" or "horse" => EquipmentIndex.Horse,
                "harness" => EquipmentIndex.HorseHarness,
                _ => null
            };
            if (!slot.HasValue)
            {
                onFailure("{=}Usage: !unequip [civilian] <weapon1|weapon2|weapon3|weapon4|head|body|legs|hands|cape|mount|harness>".Translate());
                return;
            }
            if (Mission.Current != null)
            {
                onFailure("{=}You cannot unequip during a mission!".Translate());
                return;
            }
            if (adoptedHero.HeroState == Hero.CharacterStates.Prisoner)
            {
                onFailure("{=}You cannot unequip while imprisoned!".Translate());
                return;
            }

            // Do not refresh equipment here: that would immediately generate replacements.
            var equipment = civilian ? adoptedHero.CivilianEquipment : adoptedHero.BattleEquipment;
            equipment[slot.Value] = EquipmentElement.Invalid;

            onSuccess("{=}Selected slot unequipped.".Translate()
                + " " + "{=}Custom items remain in storage; ordinary items are not stored. Your equipment tier is unchanged.".Translate());
        }
    }
}
