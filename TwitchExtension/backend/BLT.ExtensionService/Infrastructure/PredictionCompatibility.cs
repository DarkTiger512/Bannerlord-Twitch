using System.Text.Json;
using System.Text.Json.Nodes;
using BLT.ExtensionService.Models;

namespace BLT.ExtensionService.Infrastructure;

public static class PredictionCompatibility
{
    public static string PublicMessage(string message)
    {
        var root = JsonNode.Parse(message)!.AsObject();
        if (root["data"]?["commands"] is not JsonArray commands) return message;
        foreach (var command in commands.OfType<JsonObject>())
        {
            if (command["handler"]?.GetValue<string>() is not ("TournamentBet" or "TournamentPrediction") && command["name"]?.GetValue<string>() is not ("bltbet" or "predict")) continue;
            command["name"] = "predict";
            command["handler"] = "TournamentPrediction";
            command["help"] = "Predict the winning team";
            command["helpKey"] = "command.tournamentprediction.help";
        }
        return root.ToJsonString();
    }
    public static string ActionId(string id) => string.Equals(id, "command.bltbet", StringComparison.OrdinalIgnoreCase) ? "command.predict" : id;

    public static IReadOnlyList<CommandPreference> Commands(IReadOnlyList<CommandPreference> commands) => commands
        .GroupBy(command => ActionId(command.ActionId), StringComparer.Ordinal)
        .Select(group => {
            var canonical = group.FirstOrDefault(command => command.ActionId == group.Key) ?? group.First();
            var settings = new Dictionary<string, JsonElement>();
            foreach (var command in group.Where(command => command != canonical).Append(canonical))
                if (command.Settings is not null) foreach (var pair in command.Settings) settings[pair.Key] = pair.Value;
            if (group.Key == "command.predict")
                foreach (var key in settings.Keys.ToArray())
                    if (key.Equals("Name", StringComparison.OrdinalIgnoreCase)) settings[key] = JsonSerializer.SerializeToElement("predict");
                    else if (key.Equals("Help", StringComparison.OrdinalIgnoreCase)) settings[key] = JsonSerializer.SerializeToElement("Predict the winning team");
                    else if (key.Equals("Documentation", StringComparison.OrdinalIgnoreCase)) settings[key] = JsonSerializer.SerializeToElement("Predict the winning team using in-game hero gold. Gold committed may be lost; rewards are shared from the prediction pool.");
                    else if (key.Equals("Handler", StringComparison.OrdinalIgnoreCase)) settings[key] = JsonSerializer.SerializeToElement("TournamentPrediction");
            return canonical with { ActionId = group.Key, Settings = settings.Count == 0 ? canonical.Settings : settings };
        }).ToArray();
}
