using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BLTAdoptAHero.Util
{
    public enum ForgeStyle { Swift, Balanced, Heavy }
    public enum ForgeQuality { Standard, Fine, Masterwork }

    // Optional JSON data on the existing modifier; old saves require no migration.
    public sealed class ForgeMetadata
    {
        public int Version { get; set; } = 1;
        public bool NativeCrafted { get; set; }
        public string CultureId { get; set; }
        public string TemplateId { get; set; }
        public string BaseItemId { get; set; }
        public ForgeStyle Style { get; set; } = ForgeStyle.Balanced;
        public ForgeQuality Quality { get; set; }
        public int BaselineDamage { get; set; }
        public int BaselineSpeed { get; set; }
        public int AppliedDamage { get; set; }
        public int AppliedSpeed { get; set; }
        public ForgeMetadata Copy() => (ForgeMetadata)MemberwiseClone();
    }

    public class ForgeSettings
    {
        [Category("Prices"), DisplayName("Standard weapon cost"), Description("BLT gold charged for a new Standard weapon. Standard includes its configured damage and speed point bonuses. Set to 0 to make it free. Used by forge.")]
        public int StandardCost { get; set; } = 500000;
        [Category("Prices"), DisplayName("Fine weapon cost"), Description("BLT gold charged for a new Fine weapon, including its quality bonus. Must be at least the Standard price. Used by forge.")]
        public int FineCost { get; set; } = 750000;
        [Category("Prices"), DisplayName("Masterwork weapon cost"), Description("BLT gold charged for a new Masterwork weapon, including its quality bonus. Must be at least the Fine price. Used by forge.")]
        public int MasterworkCost { get; set; } = 1000000;
        [Category("Prices"), DisplayName("Quality upgrade cost"), Description("BLT gold charged for each reforge quality step: Standard to Fine, or Fine to Masterwork. Masterwork cannot be upgraded further. Set to 0 for free upgrades. Used by reforge.")]
        public int UpgradeCost { get; set; } = 250000;
        [Category("Prices"), DisplayName("Style change cost"), Description("BLT gold charged to change an owned weapon to Swift, Balanced or Heavy. Choosing its current style costs nothing. Set to 0 for free changes. Used by reforge.")]
        public int StyleCost { get; set; } = 100000;
        [Category("Prices"), DisplayName("Culture extra cost"), Description("Extra BLT gold added when a viewer names a culture in a forge request. No extra charge when they leave culture out. Set to 0 for no surcharge. Used by forge.")]
        public int CultureSurcharge { get; set; }
        [Category("Advanced"), DisplayName("Crafting attempts (1–128)"), Description("How many possible weapon designs BLT tries before choosing the best match. Higher values may find a closer match but take more work. Range: 1 to 128; recommended: 64. This is not a success chance.")]
        public int CandidateBudget { get; set; } = 64;
        [Category("Weapon balance"), DisplayName("Weapon target tier (0–6, internal numbering)"), Description("Preferred strength tier when BLT chooses a newly crafted design. The game counts from 0: enter 5 for displayed Tier 6. Range: 0 to 6. This is a preference, not a guaranteed tier, and does not control the quality bonus.")]
        public int TargetTier { get; set; } = 5;
        [Category("Weapon balance"), DisplayName("Swift / Heavy trade-off (%)"), Description("The damage/speed trade-off for Swift and Heavy. At 5, Swift loses 5% damage and gains 5% weapon speed; Heavy gains 5% damage and loses 5% speed. Balanced has no trade-off. Range: 0 to 25. Projectile speed is unchanged.")]
        public double StylePercent { get; set; } = 5;
        [Category("Weapon balance"), DisplayName("Fine extra damage (%)"), Description("Extra damage for Fine quality, measured from the original weapon damage. Enter 5 for +5%. This replaces the Standard quality bonus; repeated reforges do not stack it. Must be between 0 and the Masterwork bonus.")]
        public double FineDamagePercent { get; set; } = 5;
        [Category("Weapon balance"), DisplayName("Masterwork extra damage (%)"), Description("Total extra damage for Masterwork quality, measured from the original weapon damage. Enter 10 for +10%, not an additional 10% on top of Fine. Must be at least the Fine bonus and no more than 25.")]
        public double MasterworkDamagePercent { get; set; } = 10;
        [Category("Weapon balance"), DisplayName("Standard extra damage (points)"), Description("Flat damage points added for Standard quality, before style effects. This is the total quality bonus, not added on top of lower qualities. Damage percentage bonuses apply separately. Reforging replaces the previous forge bonus; it does not stack.")]
        public int StandardDamageBonus { get; set; } = 10;
        [Category("Weapon balance"), DisplayName("Standard extra speed (points)"), Description("Flat speed points added for Standard quality, before style effects. This is the total quality bonus, not added on top of lower qualities. Damage percentage bonuses apply separately. Reforging replaces the previous forge bonus; it does not stack.")]
        public int StandardSpeedBonus { get; set; } = 5;
        [Category("Weapon balance"), DisplayName("Fine extra damage (points)"), Description("Flat damage points added for Fine quality, before style effects. This is the total quality bonus, not added on top of lower qualities. Damage percentage bonuses apply separately. Reforging replaces the previous forge bonus; it does not stack.")]
        public int FineDamageBonus { get; set; } = 20;
        [Category("Weapon balance"), DisplayName("Fine extra speed (points)"), Description("Flat speed points added for Fine quality, before style effects. This is the total quality bonus, not added on top of lower qualities. Damage percentage bonuses apply separately. Reforging replaces the previous forge bonus; it does not stack.")]
        public int FineSpeedBonus { get; set; } = 10;
        [Category("Weapon balance"), DisplayName("Masterwork extra damage (points)"), Description("Flat damage points added for Masterwork quality, before style effects. This is the total quality bonus, not added on top of lower qualities. Damage percentage bonuses apply separately. Reforging replaces the previous forge bonus; it does not stack.")]
        public int MasterworkDamageBonus { get; set; } = 30;
        [Category("Weapon balance"), DisplayName("Masterwork extra speed (points)"), Description("Flat speed points added for Masterwork quality, before style effects. This is the total quality bonus, not added on top of lower qualities. Damage percentage bonuses apply separately. Reforging replaces the previous forge bonus; it does not stack.")]
        public int MasterworkSpeedBonus { get; set; } = 15;
        [Category("Advanced"), DisplayName("Allow hidden crafting parts"), Description("Allow BLT to use weapon pieces that the game or a mod hides from the normal smithing screen. Hidden means unavailable in that screen, not invisible on the weapon. Leave off unless you want these extra parts; some mods hide parts they do not intend players to use.")]
        public bool AllowHiddenParts { get; set; }
        [Category("Advanced"), DisplayName("Blocked crafting part IDs"), Description("Weapon pieces BLT must never use when crafting. Add their exact internal part IDs, not their display names. Leave empty unless you need to exclude a specific unwanted or broken mod part. This does not block whole weapons.")]
        public List<string> RestrictedPartIds { get; set; } = new();
        [Category("Advanced"), DisplayName("Culture aliases"), Description("Chat shortcuts for cultures. Each entry maps a shortcut (key) to an exact culture ID or name (value). For example, a short nickname can point to a long modded culture name. Leave empty to use the normal culture names.")]
        public Dictionary<string, string> CultureAliases { get; set; } = new();
        [Category("Advanced"), DisplayName("Weapon type aliases"), Description("Chat shortcuts for weapon types. Each entry maps what viewers type (key) to a weapon type (value). For example, longsword maps to TwoHandedSword. Keep the defaults unless you want different shortcuts.")]
        public Dictionary<string, string> TypeAliases { get; set; } = new()
        {
            ["longsword"] = "TwoHandedSword", ["sword"] = "OneHandedSword",
            ["axe"] = "OneHandedAxe", ["mace"] = "OneHandedMace",
            ["javelin"] = "ThrowingJavelins"
        };

        public int Price(ForgeQuality quality, bool explicitCulture) => checked(
            (quality == ForgeQuality.Standard ? StandardCost : quality == ForgeQuality.Fine ? FineCost : MasterworkCost)
            + (explicitCulture ? CultureSurcharge : 0));

        public ForgeSettings Copy() => (ForgeSettings)MemberwiseClone();

        public void Validate()
        {
            if (StandardDamageBonus < 0 || FineDamageBonus < StandardDamageBonus || MasterworkDamageBonus < FineDamageBonus
                || MasterworkDamageBonus > 100 || StandardSpeedBonus < 0 || FineSpeedBonus < StandardSpeedBonus
                || MasterworkSpeedBonus < FineSpeedBonus || MasterworkSpeedBonus > 50
                || StandardCost < 0 || FineCost < StandardCost || MasterworkCost < FineCost || UpgradeCost < 0
                || StyleCost < 0 || CultureSurcharge < 0 || CandidateBudget < 1 || CandidateBudget > 128
                || TargetTier < 0 || TargetTier > 6 || !Finite(StylePercent) || StylePercent < 0 || StylePercent > 25
                || !Finite(FineDamagePercent) || FineDamagePercent < 0 || !Finite(MasterworkDamagePercent)
                || MasterworkDamagePercent < FineDamagePercent || MasterworkDamagePercent > 25)
                throw new ArgumentException("Invalid forge settings: check nonnegative costs, tier 0–6, budget 1–128 and bonuses 0–25%.");
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public static class ForgePolicy
    {
        public static string Normalize(string value) => new string((value ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

        public static List<string> Tokens(string args)
        {
            var result = new List<string>();
            var token = new StringBuilder();
            bool quoted = false;
            foreach (char c in args ?? "")
            {
                if (c == '"') quoted = !quoted;
                else if (char.IsWhiteSpace(c) && !quoted)
                {
                    if (token.Length > 0) { result.Add(token.ToString()); token.Clear(); }
                }
                else token.Append(c);
            }
            if (quoted) throw new ArgumentException("Close the quoted culture name.");
            if (token.Length > 0) result.Add(token.ToString());
            return result;
        }

        public static bool TryEnum<T>(string value, out T parsed) where T : struct
        {
            foreach (T candidate in Enum.GetValues(typeof(T)))
                if (Normalize(candidate.ToString()) == Normalize(value)) { parsed = candidate; return true; }
            parsed = default;
            return false;
        }

        public static (int damage, int speed) Contributions(int damage, int speed, ForgeStyle style, ForgeQuality quality, ForgeSettings settings)
        {
            if (!Enum.IsDefined(typeof(ForgeStyle), style) || !Enum.IsDefined(typeof(ForgeQuality), quality))
                throw new ArgumentException("Unknown forge style or quality.");
            double qualityPercent = quality == ForgeQuality.Fine ? settings.FineDamagePercent
                : quality == ForgeQuality.Masterwork ? settings.MasterworkDamagePercent : 0;
            double stylePercent = style == ForgeStyle.Swift ? -settings.StylePercent : style == ForgeStyle.Heavy ? settings.StylePercent : 0;
            int Round(double value) => checked((int)Math.Round(value, MidpointRounding.AwayFromZero));
            int qualityDamage = quality == ForgeQuality.Masterwork ? settings.MasterworkDamageBonus
                : quality == ForgeQuality.Fine ? settings.FineDamageBonus : settings.StandardDamageBonus;
            int qualitySpeed = quality == ForgeQuality.Masterwork ? settings.MasterworkSpeedBonus
                : quality == ForgeQuality.Fine ? settings.FineSpeedBonus : settings.StandardSpeedBonus;
            return (checked(qualityDamage + Round(damage * (qualityPercent + stylePercent) / 100)),
                checked(qualitySpeed + Round(speed * -stylePercent / 100)));
        }

        public static string PurchaseError(bool mission, bool prisoner, bool auction, int gold, int cost)
        {
            if (mission) return "You cannot forge or reforge during a mission.";
            if (prisoner) return "You cannot forge or reforge while imprisoned.";
            if (auction) return "You cannot reforge an auctioned weapon.";
            if (cost < 0 || gold < cost) return $"Not enough gold: costs {cost:N0}; balance {gold:N0}.";
            return null;
        }
    }
}
