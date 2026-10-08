using System;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Util;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=}Create Adult Heir"),
     LocDescription("{=}Create a child with your spouse, grow them up and reserve them as your future heir."), UsedImplicitly]
    public sealed class AdoptHeir : HeroCommandHandlerBase
    {
        public sealed class Settings : IDocumentable
        {
            [LocDisplayName("{=}Gold Cost"), LocDescription("{=}BLT gold charged after successfully reserving the adult child. Default: free.")]
            public int GoldCost { get; set; }

            public void GenerateDocumentation(IDocumentationGenerator generator) =>
                generator.P("!adoptheir creates a new adult child and reserves them as your future heir. Your current hero stays active. Uses the family baby limit; existing heirs are kept. Gold cost: " + GoldCost);
        }

        public override Type HandlerConfigType => typeof(Settings);

        protected override void ExecuteInternal(Hero hero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            string reply;
            try
            {
                if (!string.IsNullOrWhiteSpace(context.Args))
                    throw new InvalidOperationException("Use !adoptheir without arguments to create your future heir.");
                reply = AdoptHeirService.Execute(hero, (config as Settings)?.GoldCost ?? 0);
            }
            catch (InvalidOperationException ex) { onFailure(ex.Message); return; }
            catch (Exception ex)
            {
                Log.Error($"AdoptHeir: {ex}");
                onFailure("Heir creation was interrupted. No gold was charged. Any created child is retained; retry !adoptheir to check recovery.");
                return;
            }
            onSuccess(reply);
        }
    }
}
