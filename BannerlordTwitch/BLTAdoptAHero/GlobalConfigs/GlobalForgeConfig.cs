using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Util;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=}Weapon Forging")]
    public sealed class GlobalForgeConfig : ForgeSettings, IDocumentable
    {
        private const string Id = "Adopt A Hero - Forge Config";
        internal static void Register() => ActionManager.RegisterGlobalConfigType(Id, typeof(GlobalForgeConfig));
        internal static GlobalForgeConfig Get() => ActionManager.GetGlobalConfig<GlobalForgeConfig>(Id) ?? new GlobalForgeConfig();
        public void GenerateDocumentation(IDocumentationGenerator generator)
        {
            generator.P("!forge <type> [culture] [swift|balanced|heavy] [standard|fine|masterwork]; !forge random; !forge cultures; !forge types.");
            generator.P($"Standard {StandardCost:N0}, Fine {FineCost:N0}, Masterwork {MasterworkCost:N0} gold; explicit culture +{CultureSurcharge:N0}.");
            generator.P($"!reforge lists owned weapons; !reforge #N previews; !reforge #N upgrade costs {UpgradeCost:N0}; !reforge #N swift|balanced|heavy costs {StyleCost:N0}.");
            generator.P($"Swift: -{StylePercent}% damage/+{StylePercent}% speed; Heavy reverses this; Balanced has no style adjustment. Fine +{FineDamagePercent}% damage; Masterwork +{MasterworkDamagePercent}%. Rounded from base stats; enchantments are preserved. Reforging is guaranteed.");
        }
    }
}
