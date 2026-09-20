using System.Text.Json;
using BLT.ExtensionService.Infrastructure;
using BLT.ExtensionService.Models;

namespace BLT.ExtensionService.Tests;

public sealed class PredictionCompatibilityTests
{
    [Fact]
    public void MigratesDisabledCommandAndCustomAmountWithoutChangingInput()
    {
        var settings = new Dictionary<string, JsonElement> { ["Amount"] = JsonSerializer.SerializeToElement(1234) };
        var original = new CommandPreference("command.bltbet", false, settings);
        var migrated = Assert.Single(PredictionCompatibility.Commands([original]));
        Assert.Equal("command.predict", migrated.ActionId);
        Assert.False(migrated.Enabled);
        Assert.Equal(1234, migrated.Settings!["Amount"].GetInt32());
        Assert.Equal("command.bltbet", original.ActionId);
        Assert.Equal(migrated, Assert.Single(PredictionCompatibility.Commands([migrated])) with { Settings = migrated.Settings });
    }

    [Fact]
    public void ExistingCanonicalPreferenceWinsCollisionAndRetainsAdditionalSettings()
    {
        var migrated = Assert.Single(PredictionCompatibility.Commands([
            new("command.bltbet", true, new() { ["Amount"] = JsonSerializer.SerializeToElement(50) }),
            new("command.predict", false, new() { ["Other"] = JsonSerializer.SerializeToElement(7) })]));
        Assert.False(migrated.Enabled);
        Assert.Equal(50, migrated.Settings!["Amount"].GetInt32());
        Assert.Equal(7, migrated.Settings["Other"].GetInt32());
    }

    [Fact]
    public void RuntimeMetadataIsCanonicalWithoutRewritingCombatantNames()
    {
        const string input = "{\"kind\":\"state.snapshot\",\"data\":{\"commands\":[{\"name\":\"bltbet\",\"handler\":\"TournamentBet\",\"help\":\"bet on tournament\"}],\"mission\":{\"combatants\":[{\"name\":\"Bet\"}]}}}";
        using var doc = JsonDocument.Parse(PredictionCompatibility.PublicMessage(input));
        Assert.Equal("predict", doc.RootElement.GetProperty("data").GetProperty("commands")[0].GetProperty("name").GetString());
        Assert.Equal("Bet", doc.RootElement.GetProperty("data").GetProperty("mission").GetProperty("combatants")[0].GetProperty("name").GetString());
    }

    [Fact]
    public void NewAndLegacyViewerCanReceiveTheSameAuthoritativeState()
    {
        const string input = "{\"kind\":\"state.snapshot\",\"data\":{\"mission\":{\"combatants\":[{\"id\":\"a\",\"name\":\"Alice\"}]}}}";
        Assert.Equal(input, PersonalBattleHud.Prepare(input, "a", true));
        using var doc = JsonDocument.Parse(PersonalBattleHud.Prepare(input, "a"));
        Assert.Equal(2, doc.RootElement.GetProperty("data").GetProperty("mission").GetProperty("combatants").GetArrayLength());
    }
}
