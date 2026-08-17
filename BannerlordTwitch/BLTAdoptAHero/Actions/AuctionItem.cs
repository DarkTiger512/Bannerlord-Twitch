using System;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.Util;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=Q1QZbwR3}Auction Item"),
     LocDescription("{=024hOo3G}Allows viewers to auction custom items, for other viewers to bid on (make sure to add a bid command also)"),
     UsedImplicitly]
    public class AuctionItem : HeroCommandHandlerBase
    {
        private class Settings
        {
            [LocDisplayName("{=34GjlaWu}Auction Duration In Seconds"),
             LocDescription("{=zsvhQABf}How long the auction should last before the highest bidder wins"),
             PropertyOrder(1), UsedImplicitly]
            public int AuctionDurationInSeconds { get; set; } = 60;

            [LocDisplayName("{=ssmJ9c5L}Auction Reminder Interval In Seconds"),
             LocDescription("{=ijkjWj5q}Interval at which to output a reminder of the auction"),
             PropertyOrder(2), UsedImplicitly]
            public int AuctionReminderIntervalInSeconds { get; set; } = 15;

            [LocDisplayName("{=BLT_AuctionExtendOnBid_Name}Extend Auction On Bid"),
             LocDescription("{=BLT_AuctionExtendOnBid_Desc}Add time to the auction whenever a valid bid is placed"),
             PropertyOrder(3), UsedImplicitly]
            public bool ExtendAuctionOnBid { get; set; } = true;

            [LocDisplayName("{=BLT_AuctionBidExtensionSeconds_Name}Bid Extension In Seconds"),
             LocDescription("{=BLT_AuctionBidExtensionSeconds_Desc}How many seconds to add to the auction after each valid bid"),
             PropertyOrder(4), UsedImplicitly]
            public int BidExtensionInSeconds { get; set; } = 15;

            [LocDisplayName("{=BLT_AuctionMinimumBidIncrement_Name}Minimum Bid Increment"),
             LocDescription("{=BLT_AuctionMinimumBidIncrement_Desc}Minimum amount a new bid must exceed the current highest bid by"),
             PropertyOrder(5), UsedImplicitly]
            public int MinimumBidIncrement { get; set; } = 1;
        }

        public override Type HandlerConfigType => typeof(Settings);

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            var settings = (Settings)config;

            if (BLTAdoptAHeroCampaignBehavior.Current.AuctionInProgress)
            {
                ActionManager.SendReply(context,
                    "{=T2R35HHV}Another auction is already in progress".Translate());
                return;
            }

            if (string.IsNullOrWhiteSpace(context.Args))
            {
                ActionManager.SendReply(context,
                    context.ArgsErrorMessage("{=}(custom item index) (reserve price)".Translate()));
                return;
            }

            var argParts = context.Args.Trim().Split(' ').ToList();
            if (argParts.Count != 2)
            {
                ActionManager.SendReply(context, "{=}(custom item index) (reserve price)".Translate());
                return;
            }

            (var element, string error) = BLTAdoptAHeroCampaignBehavior.Current.FindCustomItemByIndex(adoptedHero, argParts[0]);
            if (element.IsEqualTo(EquipmentElement.Invalid))
            {
                ActionManager.SendReply(context, error ?? "(unknown error)");
                return;
            }

            if (!int.TryParse(argParts[1], out int reservePrice) || reservePrice < 0)
            {
                ActionManager.SendReply(context, "{=mm1ay4I7}Invalid reserve price '{Arg}'".Translate(("Arg", argParts[1])));
                return;
            }

            BLTAdoptAHeroCampaignBehavior.Current.StartItemAuction(element, adoptedHero, reservePrice,
                settings.AuctionDurationInSeconds, settings.AuctionReminderIntervalInSeconds,
                settings.MinimumBidIncrement,
                settings.ExtendAuctionOnBid ? settings.BidExtensionInSeconds : 0,
                s => ActionManager.SendNonReply(context, s));

            ActionManager.SendNonReply(context,
                "{=BH5rnHNq}Auction of '{ItemName}' is OPEN! Reserve price is {ReservePrice}{GoldIcon}, bidding closes in {AuctionDurationInSeconds} seconds."
                    .Translate(
                        ("ItemName", RewardHelpers.GetItemNameAndModifiers(element)),
                        ("ReservePrice", reservePrice),
                        ("GoldIcon", Naming.Gold),
                        ("AuctionDurationInSeconds", settings.AuctionDurationInSeconds)
                    ));
        }
    }

    [LocDisplayName("{=rBAvqAh7}Bid On Item"),
     LocDescription("{=XuvGyCwD}Allows viewers bid on an active custom item auction (make sure to add an auction command also)"),
     UsedImplicitly]
    public class BidOnItem : HeroCommandHandlerBase
    {
        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (string.IsNullOrWhiteSpace(context.Args))
            {
                ActionManager.SendReply(context,
                    context.ArgsErrorMessage("{=ewjqhPqj}(bid amount)".Translate()));
                return;
            }

            if (!int.TryParse(context.Args, out int bid) || bid < 0)
            {
                ActionManager.SendReply(context, "{=dgG5WPrC}Invalid bid amount".Translate());
                return;
            }

            (bool _, string description) = BLTAdoptAHeroCampaignBehavior.Current.AuctionBid(adoptedHero, bid);

            ActionManager.SendReply(context, description);
        }
    }
}
