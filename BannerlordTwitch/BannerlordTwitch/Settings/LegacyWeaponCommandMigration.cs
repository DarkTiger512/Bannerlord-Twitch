using System;
using System.Collections.Generic;
using BannerlordTwitch.Util;

namespace BannerlordTwitch
{
    internal static class LegacyWeaponCommandMigration
    {
        private static long Nonnegative(object value) => long.TryParse(Convert.ToString(value), out long number)
            ? Math.Min(int.MaxValue, Math.Max(0, number)) : 0;

        internal static bool Migrate(IEnumerable<Command> commands)
        {
            bool changed = false;
            foreach (var command in commands)
            {
                if (command.Handler != "SmithItem") continue;
                var old = command.HandlerConfig == null ? new Dictionary<string, object>()
                    : YamlHelpers.ConvertObject<Dictionary<string, object>>(command.HandlerConfig);
                if (old.TryGetValue("Type", out var type)
                    && !string.Equals(Convert.ToString(type), "Weapon", StringComparison.OrdinalIgnoreCase)
                    && Convert.ToString(type) != "0") continue;
                var config = new Dictionary<string, object>
                {
                    ["UseGlobalSettings"] = false,
                    ["LegacySmithSettings"] = command.HandlerConfig
                };
                old.TryGetValue("GoldCost", out var price);
                long cost = Nonnegative(price);
                config["StandardCost"] = (int)cost;
                config["FineCost"] = (int)Math.Min(int.MaxValue, cost + 250000);
                config["MasterworkCost"] = (int)Math.Min(int.MaxValue, cost + 500000);
                if (old.TryGetValue("CultureGoldCost", out var surcharge))
                    config["CultureSurcharge"] = (int)Nonnegative(surcharge);
                command.Handler = "ForgeWeapon";
                command.HandlerConfig = config;
                command.Help = "Choose a weapon type, culture, style and quality; use this command without arguments for help.";
                command.Documentation = "Forge a custom weapon with automatic parts, style and quality.";
                changed = true;
            }
            return changed;
        }
    }
}
