using System.Text.Json.Nodes;

namespace BLT.ExtensionService.Infrastructure;

// Compatibility with the reviewed frontend, which identifies its own combatant
// by the helper's missing displayName fallback, "Viewer". Never alter shared state.
public static class PersonalBattleHud
{
    public static string Prepare(string message, string? ownedHeroId, bool nativeHeroHud = false)
    {
        if (nativeHeroHud) return message;
        var envelope = JsonNode.Parse(message)!.AsObject();
        if (envelope["kind"]?.GetValue<string>() is not ("state.snapshot" or "state.patch")) return message;
        if (envelope["data"]?["mission"]?["combatants"] is not JsonArray combatants) return message;
        JsonObject? personalHud = null;
        foreach (var combatant in combatants.OfType<JsonObject>())
        {
            var isOwner = !string.IsNullOrEmpty(ownedHeroId) && combatant["id"]?.GetValue<string>() == ownedHeroId;
            if (isOwner) personalHud = (JsonObject)combatant.DeepClone();
            if (string.Equals(combatant["name"]?.GetValue<string>(), "Viewer", StringComparison.OrdinalIgnoreCase))
                combatant["name"] = "Viewer (roster)";
        }
        if (personalHud is not null)
        {
            // The reviewed UI excludes its personal-HUD row from the roster.
            // Supply a separate presentation-only row and preserve every real row.
            var hudId = "blt:personal-hud:" + ownedHeroId;
            while (combatants.OfType<JsonObject>().Any(row => row["id"]?.GetValue<string>() == hudId)) hudId += ":hud";
            personalHud["id"] = hudId;
            personalHud["name"] = "Viewer";
            combatants.Insert(0, personalHud);
        }
        return envelope.ToJsonString();
    }
}
