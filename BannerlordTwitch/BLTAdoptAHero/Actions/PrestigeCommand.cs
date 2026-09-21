using System;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Util;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=BLTPrestigeCommand}Prestige"), LocDescription("{=BLTPrestigeCommandDesc}Reset progression to choose a permanent perk."), UsedImplicitly]
    internal class PrestigeCommand : HeroCommandHandlerBase
    {
        internal static readonly Guid DefaultCommandId = new("5d4dcbde-937b-4ed5-88a5-b909d31bdb21");
        public override Type HandlerConfigType => typeof(Settings);
        public sealed class Settings : PrestigeSettings, ILoaded
        {
            public void OnLoaded(BannerlordTwitch.Settings settings)
            {
                var commands = settings.Commands.Where(c => c.Handler == nameof(PrestigeCommand))
                    .OrderByDescending(c => c.ID == DefaultCommandId).ThenBy(c => c.ID).ToList();
                var canonical = commands.FirstOrDefault()?.HandlerConfig as Settings;
                if (canonical == null) return;
                foreach (var command in commands) command.HandlerConfig = canonical;
            }
        }

        internal static PrestigeSettings CurrentSettings =>
            BannerlordTwitch.Rewards.ActionManager.GetCommandConfig(nameof(PrestigeCommand), DefaultCommandId) as PrestigeSettings
            ?? new PrestigeSettings { Enabled = false };

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config, Action<string> onSuccess, Action<string> onFailure)
        {
            var behavior = BLTAdoptAHeroCampaignBehavior.Current;
            var args = (context.Args ?? "").Trim().ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (args.Length == 0 || args.Length == 1 && args[0] == "status") { onSuccess(behavior.PrestigeStatus(adoptedHero)); return; }
            if (args.Length == 1 && args[0] == "perks")
            {
                var c = BLTAdoptAHeroCampaignBehavior.PrestigeConfig;
                onSuccess(string.Join("; ", PrestigePolicy.Perks.Select((p, i) => $"{i + 1}. {p} {behavior.GetPrestige(adoptedHero).Rank(p)}/{c.RankCap}"))
                    + " " + "{=BLTPrestigePerkHelp}Per rank: might +{M}% damage; resilience -{R}% damage taken; vitality +{V} HP; fortune +{F}% battle gold; insight +{I}% BLT XP. Use !prestige choose <perk>."
                        .Translate(("M", c.MightPerRank * 100), ("R", c.ResiliencePerRank * 100), ("V", c.VitalityPerRank), ("F", c.FortunePerRank * 100), ("I", c.InsightPerRank * 100)));
                return;
            }
            if (args.Length == 2 && args[0] == "choose")
            {
                string blocked = behavior.PrestigeBlock(adoptedHero);
                if (blocked != null) onFailure(blocked);
                else if (!PrestigePolicy.CanChoose(BLTAdoptAHeroCampaignBehavior.PrestigeConfig, behavior.GetPrestige(adoptedHero), args[1]))
                    onFailure("{=BLTPrestigePerkUnavailable}Unknown or fully ranked perk. Use !prestige perks.".Translate());
                else onSuccess(behavior.PreviewPrestige(adoptedHero, args[1]));
                return;
            }
            if (args.Length == 2 && args[0] == "confirm")
            {
                if (behavior.ConfirmPrestige(adoptedHero, args[1], out string reply)) onSuccess(reply); else onFailure(reply);
                return;
            }
            onFailure("{=BLTPrestigeUsage}Use !prestige, !prestige perks, !prestige choose <perk>, or !prestige confirm <perk>.".Translate());
        }
    }
}
