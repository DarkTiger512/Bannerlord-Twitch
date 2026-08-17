using System;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.Util;
using JetBrains.Annotations;
using TaleWorlds.Core;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=0YSdK3zK}Name Item"),
     LocDescription("{=k7vnOEIN}Allow viewer to name custom items"),
     UsedImplicitly]
    public class NameItem : ICommandHandler
    {
        public Type HandlerConfigType => null;

        public void Execute(ReplyContext context, object config)
        {
            var adoptedHero = BLTAdoptAHeroCampaignBehavior.Current.GetAdoptedHero(context.UserName);
            if (adoptedHero == null)
            {
                ActionManager.SendReply(context, AdoptAHero.NoHeroMessage);
                return;
            }

            var nameableItems = BLTAdoptAHeroCampaignBehavior.Current.GetCustomItems(adoptedHero).ToList();

            if (!nameableItems.Any())
            {
                ActionManager.SendReply(context, "{=rXVOIMri}You have no nameable items".Translate());
                return;
            }

            if (string.IsNullOrWhiteSpace(context.Args))
            {
                ActionManager.SendReply(context, context.ArgsErrorMessage("{=}(custom item index) (new item name)".Translate()));
                return;
            }

            var argParts = context.Args.Trim().Split(' ').ToList();
            if (argParts.Count < 2)
            {
                ActionManager.SendReply(context,
                    context.ArgsErrorMessage("{=}(custom item index) (new item name)".Translate()));
                return;
            }

            (var element, string error) = BLTAdoptAHeroCampaignBehavior.Current.FindCustomItemByIndex(adoptedHero, argParts[0]);
            if (element.IsEqualTo(EquipmentElement.Invalid))
            {
                ActionManager.SendReply(context, error ?? "(unknown error)");
                return;
            }

            string previousName = element.GetModifiedItemName().ToString();

            // sanitize # out of the new name in case it breaks something 
            string itemNewName = string.Join(" ", argParts.Skip(1)).Replace("#", "");

            Log.Info($"NameItem requested by {context.UserName}: old='{previousName}', new='{itemNewName}', itemId={element.Item?.StringId}, itemType={element.Item?.ItemType}, modifierId={element.ItemModifier?.StringId}, canName={BLTCustomItemsCampaignBehavior.Current.ItemCanBeNamed(element.ItemModifier)}");

            if (!BLTCustomItemsCampaignBehavior.Current.NameItem(element.ItemModifier, itemNewName))
            {
                ActionManager.SendReply(context,
                    "{=rXVOIMri}Could not rename this item; custom item data was not found".Translate());
                return;
            }

            Log.Info($"NameItem completed for {context.UserName}: itemId={element.Item?.StringId}, modifierId={element.ItemModifier?.StringId}, result='{element.GetModifiedItemName()}'");
            ActionManager.SendReply(context,
                "{=iqNEr6Y7}{PreviousName} renamed to {NewName}"
                    .Translate(("PreviousName", previousName), ("NewName", element.GetModifiedItemName().ToString()))
                );
        }
    }
}
