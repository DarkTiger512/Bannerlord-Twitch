using System;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using BLTAdoptAHero.Util;
using JetBrains.Annotations;

namespace BLTAdoptAHero
{
    [UsedImplicitly, LocDisplayName("SimGold"), LocDescription("Grant test gold. Broadcaster/moderator only.")]
    public sealed class SimGold : ICommandHandler
    {
        public Type HandlerConfigType => null;

        public void Execute(ReplyContext context, object config)
        {
            if (!SimGoldPolicy.TryParse(context.IsBroadcaster, context.IsModerator, context.UserName, context.Args,
                out int amount, out string viewer, out string error))
            {
                ActionManager.SendReply(context, error);
                return;
            }
            var behavior = BLTAdoptAHeroCampaignBehavior.Current;
            var hero = behavior?.GetAdoptedHero(viewer);
            if (hero == null)
            {
                ActionManager.SendReply(context, "That viewer has no adopted hero.");
                return;
            }
            if (!SimGoldPolicy.CanGrant(behavior.GetHeroGold(hero), amount))
            {
                ActionManager.SendReply(context, "This grant would exceed the maximum hero gold balance.");
                return;
            }
            int balance = behavior.ChangeHeroGold(hero, amount);
            ActionManager.SendReply(context, $"Granted {amount} test gold to @{viewer}. Balance: {balance}.");
        }
    }
}
