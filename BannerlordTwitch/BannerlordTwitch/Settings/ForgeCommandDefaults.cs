using System;
using System.Collections.Generic;
using System.Linq;

namespace BannerlordTwitch
{
    internal static class ForgeCommandDefaults
    {
        internal static bool AddMissing(ICollection<Command> commands, IEnumerable<Command> defaults)
        {
            bool added = false;
            foreach (var command in defaults.Where(c => c.Handler == "ForgeWeapon" || c.Handler == "ReforgeWeapon"))
            {
                if (commands.Any(c => c.ID == command.ID || c.Handler == command.Handler
                    || string.Equals(c.Name.ToString(), command.Name.ToString(), StringComparison.OrdinalIgnoreCase))) continue;
                commands.Add(command);
                added = true;
            }
            return added;
        }
    }
}
