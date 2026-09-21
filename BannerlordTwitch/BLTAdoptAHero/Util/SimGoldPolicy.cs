using System;
using System.Globalization;

namespace BLTAdoptAHero.Util
{
    public static class SimGoldPolicy
    {
        public static bool TryParse(bool isBroadcaster, bool isModerator, string caller, string args,
            out int amount, out string viewer, out string error)
        {
            amount = 0;
            viewer = null;
            error = "Only the broadcaster or a moderator can grant test gold.";
            if (!isBroadcaster && !isModerator) return false;
            var parts = (args ?? "").Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            error = "Use !simgold <positive amount> [viewer].";
            if (parts.Length < 1 || parts.Length > 2 || !int.TryParse(parts[0], NumberStyles.None,
                CultureInfo.InvariantCulture, out amount) || amount <= 0) return false;
            viewer = (parts.Length == 2 ? parts[1] : caller)?.TrimStart('@');
            if (string.IsNullOrWhiteSpace(viewer)) return false;
            error = null;
            return true;
        }

        public static bool CanGrant(int balance, int amount) => amount > 0 && balance >= 0
            && (long)balance + amount <= int.MaxValue;
    }
}
