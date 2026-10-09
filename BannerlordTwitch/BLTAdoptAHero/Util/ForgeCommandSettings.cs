using System.ComponentModel;
using BannerlordTwitch;
using BannerlordTwitch.Util;

namespace BLTAdoptAHero.Util
{
    public class ForgeCommandSettings : ForgeSettings, IDocumentable
    {
        // Retain the old configuration for reference; its old random power system is retired.
        [Browsable(false)]
        public object LegacySmithSettings { get; set; }

        public virtual void GenerateDocumentation(IDocumentationGenerator generator)
        {
            generator.Value("<strong>Forge a custom weapon</strong><br>Choose a weapon type, culture, fighting style and quality. BLT chooses the parts for you. Types without smithing parts use an existing weapon design.");
            generator.Value("<strong>Command:</strong> <code>!forge &lt;type&gt; [culture] [style] [quality]</code><br>Replace each placeholder with your choice; do not type the brackets. Type is required. Everything in square brackets is optional. Defaults: your hero's culture, Balanced style, Standard quality.");
            generator.Value("<strong>Find your choices:</strong><br><code>!forge types</code> lists available weapon types.<br><code>!forge cultures</code> lists culture names and IDs. Use a culture from that list, or put a name containing spaces in double quotes. Long lists appear in the stream overlay instead of chat.");
            generator.Value("<strong>Weapon types:</strong> <code>sword</code> means one-handed sword; <code>longsword</code> means two-handed sword. Other examples: <code>axe</code>, <code>mace</code>, <code>javelin</code>, <code>Bow</code>, <code>Crossbow</code>, <code>OneHandedLance</code>, <code>TwoHandedLance</code>, <code>OneHandedGlaive</code>, <code>TwoHandedGlaive</code>. Lances thrust; glaives swing. A two-handed-only pike needs a suitable class slot. Armour, shields, mounts and ammunition are not forged by this command. Custom shortcuts depend on the streamer's settings.");
            generator.Value($"<strong>Styles:</strong> <code>balanced</code> has no trade-off. <code>swift</code> trades {StylePercent:0.##}% damage for weapon speed; <code>heavy</code> trades {StylePercent:0.##}% speed for damage. Quality bonuses apply separately. Projectile travel speed is unchanged.");
            generator.Value($"<strong>Quality and prices:</strong><br><code>standard</code>: {StandardCost:N0} BLT gold; +{StandardDamageBonus} damage points, +{StandardSpeedBonus} speed points.<br><code>fine</code>: {FineCost:N0} BLT gold; +{FineDamageBonus} damage points, +{FineSpeedBonus} speed points, plus {FineDamagePercent:0.##}% base damage.<br><code>masterwork</code>: {MasterworkCost:N0} BLT gold; +{MasterworkDamageBonus} damage points, +{MasterworkSpeedBonus} speed points, plus {MasterworkDamagePercent:0.##}% base damage.<br>Each quality shows its total bonus, not bonuses stacked from previous qualities. Naming a culture adds {CultureSurcharge:N0} BLT gold. These values follow this command's configured settings.");
            generator.Value("<strong>Copyable examples:</strong><br><code>!forge sword</code> - Standard Balanced sword from your hero's culture.<br><code>!forge sword balanced masterwork</code> - Masterwork sword from your hero's culture.<br><code>!forge random</code> - Standard weapon chosen to fit your class or current loadout.<br><code>!forge random swift fine</code> - Class/loadout choice with Swift style and Fine quality.<br>Culture example, if Gondor appears in <code>!forge cultures</code>: <code>!forge OneHandedLance gondor balanced masterwork</code>. Replace <code>gondor</code> with a culture listed in your campaign.");
            generator.Value("<strong>After forging:</strong> The weapon belongs to you and appears in <code>!customs</code>. BLT may equip it if a suitable class/loadout slot is available; otherwise it stays in custom storage. Use <code>!equipcustom 6</code> to equip item #6, replacing 6 with your actual item number. Do not add a slot argument. Your class must support the weapon's usable modes.");
            generator.Value("<strong>Before you start:</strong> Finish your mission first. Prisoners cannot forge. Keep enough gold and room in your custom inventory. If requested-culture parts are unavailable, the reply explains any culture fallback. Failed forging does not charge gold. Use <code>!reforge</code> for guaranteed quality/style changes to an owned weapon; <code>!enchant</code> is separate.");
        }
    }

    public sealed class ReforgeCommandSettings : ForgeCommandSettings
    {
        public override void GenerateDocumentation(IDocumentationGenerator generator)
        {
            generator.Value("<strong>Reforge an owned weapon</strong><br><code>!reforge</code> lists your weapons. <code>!reforge #6</code> previews changes for item #6. Replace 6 with your item number.");
            generator.Value($"<code>!reforge #6 upgrade</code> advances Standard to Fine, or Fine to Masterwork, for {UpgradeCost:N0} BLT gold per step.<br><code>!reforge #6 swift</code>, <code>!reforge #6 balanced</code> or <code>!reforge #6 heavy</code> changes style for {StyleCost:N0} BLT gold. Check the preview for actual damage and speed changes.");
            generator.Value("Masterwork is the highest quality. If balance settings changed, the preview offers a free same-style refresh for an already-forged weapon. Names, ownership and enchantments are preserved; repeated reforges do not stack forge bonuses. Finish your mission first. Prisoners and auctioned weapons cannot reforge. Failed or unchanged purchases do not charge gold. See forge for crafting examples; enchanting has separate costs and risks.");
        }
    }
}
