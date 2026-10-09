using System;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Util;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=}Forge Weapon"), LocDescription("{=}Forge a cultured custom weapon with automatic parts, style and quality."), UsedImplicitly]
    public sealed class ForgeWeapon : HeroCommandHandlerBase
    {
        public override Type HandlerConfigType => typeof(ForgeCommandSettings);

        protected override void ExecuteInternal(Hero hero, ReplyContext context, object config, Action<string> onSuccess, Action<string> onFailure)
        {
            string purchaseReply;
            try
            {
                var settings = (config as ForgeCommandSettings)?.Resolve() ?? GlobalForgeConfig.Get();
                settings.Validate();
                var args = ForgePolicy.Tokens(context.Args);
                if (args.Count == 0)
                {
                    onSuccess($"!forge <type> [culture] [swift|balanced|heavy] [standard|fine|masterwork]; !forge random; !forge types; !forge cultures. Gold: {settings.StandardCost:N0}/{settings.FineCost:N0}/{settings.MasterworkCost:N0}; culture +{settings.CultureSurcharge:N0}.");
                    return;
                }
                var assets = ForgeAssets.Current;
                if (args.Count == 1 && args[0].Equals("cultures", StringComparison.OrdinalIgnoreCase))
                {
                    onSuccess(string.Join(", ", assets.Cultures.Select(c => $"{c.Name} ({c.StringId})")));
                    return;
                }
                if (args.Count == 1 && args[0].Equals("types", StringComparison.OrdinalIgnoreCase))
                {
                    onSuccess(string.Join(", ", ForgeAssets.WeaponTypes.Where(assets.Available))
                        + ". Template IDs: " + string.Join(", ", assets.Templates.Select(t => t.StringId)));
                    return;
                }
                bool random = args[0].Equals("random", StringComparison.OrdinalIgnoreCase);
                CraftingTemplate template = null;
                EquipmentType type = random ? EquipmentType.None : assets.ResolveType(args[0], settings, out template);
                args.RemoveAt(0);
                var style = ForgeStyle.Balanced;
                var quality = ForgeQuality.Standard;
                bool hasStyle = false, hasQuality = false;
                while (args.Count > 0)
                {
                    string last = args[args.Count - 1];
                    if (!hasQuality && ForgePolicy.TryEnum(last, out ForgeQuality parsedQuality)) { quality = parsedQuality; hasQuality = true; }
                    else if (!hasStyle && ForgePolicy.TryEnum(last, out ForgeStyle parsedStyle)) { style = parsedStyle; hasStyle = true; }
                    else break;
                    args.RemoveAt(args.Count - 1);
                }
                bool explicitCulture = args.Count > 0;
                var culture = explicitCulture ? assets.ResolveCulture(string.Join(" ", args), settings) : hero.Culture;
                purchaseReply = ForgeService.Forge(hero, type, culture, style, quality, settings, explicitCulture, random, template);
            }
            catch (ArgumentException ex) { onFailure(ex.Message); return; }
            catch (InvalidOperationException ex) { onFailure(ex.Message); return; }
            catch (Exception ex)
            {
                Log.Error($"ForgeWeapon: {ex}");
                onFailure("Forging could not be completed. No gold was charged.");
                return;
            }
            // Reply transport failures must not turn a committed purchase into a reported rollback.
            onSuccess(purchaseReply);
        }
    }
}
