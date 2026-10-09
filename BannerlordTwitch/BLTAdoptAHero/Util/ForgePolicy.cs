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
        [Category("Prices"), DisplayName("Standard weapon cost") ]
        public int StandardCost { get; set; } = 500000;
        [Category("Prices"), DisplayName("Fine weapon cost") ]
        public int FineCost { get; set; } = 750000;
        [Category("Prices"), DisplayName("Masterwork weapon cost") ]
        public int MasterworkCost { get; set; } = 1000000;
        [Category("Prices"), DisplayName("Quality upgrade cost") ]
        public int UpgradeCost { get; set; } = 250000;
        [Category("Prices"), DisplayName("Style change cost") ]
        public int StyleCost { get; set; } = 100000;
        [Category("Prices"), DisplayName("Culture extra cost") ]
        public int CultureSurcharge { get; set; }
        [Category("Advanced"), DisplayName("Crafting attempts (1–128)") ]
        public int CandidateBudget { get; set; } = 64;
        [Category("Weapon balance"), DisplayName("Weapon target tier (0–6, internal numbering)") ]
        public int TargetTier { get; set; } = 5;
        [Category("Weapon balance"), DisplayName("Swift / Heavy trade-off (%)") ]
        public double StylePercent { get; set; } = 5;
        [Category("Weapon balance"), DisplayName("Fine extra damage (%)") ]
        public double FineDamagePercent { get; set; } = 5;
        [Category("Weapon balance"), DisplayName("Masterwork extra damage (%)") ]
        public double MasterworkDamagePercent { get; set; } = 10;
        [Category("Advanced"), DisplayName("Allow hidden crafting parts") ]
        public bool AllowHiddenParts { get; set; }
        [Category("Advanced"), DisplayName("Blocked crafting part IDs") ]
        public List<string> RestrictedPartIds { get; set; } = new();
        [Category("Advanced"), DisplayName("Culture aliases") ]
        public Dictionary<string, string> CultureAliases { get; set; } = new();
        [Category("Advanced"), DisplayName("Weapon type aliases") ]
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
            if (StandardCost < 0 || FineCost < StandardCost || MasterworkCost < FineCost || UpgradeCost < 0
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
            return (Round(damage * (qualityPercent + stylePercent) / 100), Round(speed * -stylePercent / 100));
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
