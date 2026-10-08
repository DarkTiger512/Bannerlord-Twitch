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
    [LocDisplayName("{=}Reforge Weapon"), LocDescription("{=}Guaranteed custom weapon quality upgrades and style changes."), UsedImplicitly]
    public sealed class ReforgeWeapon : HeroCommandHandlerBase
    {
        private const string Usage = "!reforge #N previews; !reforge #N upgrade or swift|balanced|heavy buys a guaranteed change.";
        private static bool Eligible(EquipmentElement item) => ForgeAssets.IsWeapon(item.Item)
            && BLTCustomItemsCampaignBehavior.Current.IsRegistered(item.ItemModifier);

        protected override void ExecuteInternal(Hero hero, ReplyContext context, object config, Action<string> onSuccess, Action<string> onFailure)
        {
            string purchaseReply;
            try
            {
                var settings = GlobalForgeConfig.Get();
                settings.Validate();
                var args = ForgePolicy.Tokens(context.Args);
                var campaign = BLTAdoptAHeroCampaignBehavior.Current;
                var custom = BLTCustomItemsCampaignBehavior.Current;
                var items = campaign.GetCustomItems(hero);
                if (args.Count == 0)
                {
                    var list = items.Select((item, index) => (item, index)).Where(x => Eligible(x.item)).Select(x =>
                    {
                        var metadata = custom.GetForgeMetadata(x.item.ItemModifier);
                        return $"#{x.index + 1} {x.item.GetModifiedItemName()} ({metadata?.Style ?? ForgeStyle.Balanced}, {metadata?.Quality ?? ForgeQuality.Standard})";
                    }).ToList();
                    onSuccess((list.Count == 0 ? "You have no eligible custom weapons." : string.Join(" | ", list)) + " " + Usage);
                    return;
                }
                if (args.Count > 2 || args[0].StartsWith("##", StringComparison.Ordinal)
                    || !int.TryParse(args[0].TrimStart('#'), out int index) || index < 1 || index > items.Count || !Eligible(items[index - 1]))
                    throw new ArgumentException("Select an owned custom weapon from !reforge. " + Usage);
                var item = items[index - 1];
                var metadata = custom.GetForgeMetadata(item.ItemModifier);
                var style = metadata?.Style ?? ForgeStyle.Balanced;
                var quality = metadata?.Quality ?? ForgeQuality.Standard;
                if (metadata != null && metadata.Version != 1) throw new InvalidOperationException("This weapon requires a newer forge version.");
                if (args.Count == 1)
                {
                    var options = Enum.GetValues(typeof(ForgeStyle)).Cast<ForgeStyle>().Where(s => s != style).Select(s =>
                    {
                        var delta = ForgeService.Contributions(item.Item, s, quality, settings);
                        return $"{s}: damage {delta.damage - (metadata?.AppliedDamage ?? 0):+0;-0;0}, speed {delta.speed - (metadata?.AppliedSpeed ?? 0):+0;-0;0}";
                    });
                    string upgrade = "Masterwork: maximum quality.";
                    if (quality < ForgeQuality.Masterwork)
                    {
                        var delta = ForgeService.Contributions(item.Item, style, quality + 1, settings);
                        upgrade = $"Upgrade to {quality + 1}: {settings.UpgradeCost:N0} gold, damage {delta.damage - (metadata?.AppliedDamage ?? 0):+0;-0;0}.";
                    }
                    onSuccess($"{item.GetModifiedItemName()}: {style}, {quality}. {upgrade} Style {settings.StyleCost:N0} gold: {string.Join("; ", options)}. Enchantments preserved. " + Usage);
                    return;
                }
                int cost;
                if (args[1].Equals("upgrade", StringComparison.OrdinalIgnoreCase))
                {
                    if (quality >= ForgeQuality.Masterwork) throw new ArgumentException("Already Masterwork. No gold charged.");
                    quality++;
                    cost = settings.UpgradeCost;
                }
                else
                {
                    if (!ForgePolicy.TryEnum(args[1], out ForgeStyle next)) throw new ArgumentException(Usage);
                    if (style == next) throw new ArgumentException("Already that style. No gold charged.");
                    style = next;
                    cost = settings.StyleCost;
                }
                var error = ForgeService.Guard(hero, cost, item);
                if (error != null) throw new InvalidOperationException(error);
                int damage = item.ItemModifier.Damage, speed = item.ItemModifier.Speed;
                custom.Reforge(hero, item, style, quality, settings, cost);
                purchaseReply = $"{item.GetModifiedItemName()}: {style}, {quality}; damage {item.ItemModifier.Damage - damage:+0;-0;0}, speed {item.ItemModifier.Speed - speed:+0;-0;0}. Spent {cost:N0} gold; balance {campaign.GetHeroGold(hero):N0}.";
            }
            catch (ArgumentException ex) { onFailure(ex.Message); return; }
            catch (InvalidOperationException ex) { onFailure(ex.Message); return; }
            catch (Exception ex)
            {
                Log.Error($"ReforgeWeapon: {ex}");
                onFailure("Reforging could not be completed. No gold was charged.");
                return;
            }
            // Reply transport failures must not turn a committed purchase into a reported rollback.
            onSuccess(purchaseReply);
        }
    }
}
