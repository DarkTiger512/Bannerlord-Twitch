using System.Text.Json.Nodes;
using BLT.ExtensionService.Infrastructure;

namespace BLT.ExtensionService.Tests;

public sealed class PersonalBattleHudTests
{
    private const string Snapshot = """
        {"v":1,"kind":"state.snapshot","data":{"mission":{"revision":1,"combatants":[
          {"id":"hero-a","name":"Alice","hp":73,"ammoCurrent":14,"kills":3},
          {"id":"hero-b","name":"Bob","hp":42,"ammoCurrent":7,"kills":1},
          {"id":"hero-c","name":"Viewer","hp":99}
        ]}}}
        """;

    [Theory]
    [InlineData("hero-a", 73)]
    [InlineData("hero-b", 42)]
    public void SelectsOnlyAuthenticatedOwnersHeroAndPreservesStats(string heroId, int hp)
    {
        var combatants = JsonNode.Parse(PersonalBattleHud.Prepare(Snapshot, heroId))!["data"]!["mission"]!["combatants"]!.AsArray();
        var own = Assert.Single(combatants.Where(hero => hero!["name"]!.GetValue<string>() == "Viewer"));
        Assert.Equal("blt:personal-hud:" + heroId, own!["id"]!.GetValue<string>());
        Assert.Equal(hp, own["hp"]!.GetValue<int>());
        Assert.NotNull(own["ammoCurrent"]);
        Assert.NotNull(own["kills"]);
        Assert.Equal(4, combatants.Count);
        var roster = combatants.Where(row => row != own).ToArray();
        Assert.Equal(new[] { "hero-a", "hero-b", "hero-c" }, roster.Select(row => row!["id"]!.GetValue<string>()));
        Assert.Equal(new[] { "Alice", "Bob", "Viewer (roster)" }, roster.Select(row => row!["name"]!.GetValue<string>()));
        Assert.Equal(hp, roster.Single(row => row!["id"]!.GetValue<string>() == heroId)!["hp"]!.GetValue<int>());
        Assert.Equal("Alice", JsonNode.Parse(Snapshot)!["data"]!["mission"]!["combatants"]![0]!["name"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("missing")]
    public void UnlinkedOrAbsentViewerCannotClaimAnotherCombatant(string? heroId)
    {
        var combatants = JsonNode.Parse(PersonalBattleHud.Prepare(Snapshot, heroId))!["data"]!["mission"]!["combatants"]!.AsArray();
        Assert.DoesNotContain(combatants, hero => hero!["name"]!.GetValue<string>() == "Viewer");
    }

    [Fact]
    public void SoleParticipantRemainsInRosterAlongsidePersonalHud()
    {
        const string message = "{\"kind\":\"state.snapshot\",\"data\":{\"mission\":{\"combatants\":[{\"id\":\"solo\",\"name\":\"Alice\",\"hp\":73}]}}}";
        var rows = JsonNode.Parse(PersonalBattleHud.Prepare(message, "solo"))!["data"]!["mission"]!["combatants"]!.AsArray();
        Assert.Equal(2, rows.Count);
        Assert.Equal("Viewer", rows[0]!["name"]!.GetValue<string>());
        Assert.Equal("Alice", rows[1]!["name"]!.GetValue<string>());
        Assert.Equal("solo", rows[1]!["id"]!.GetValue<string>());
    }

    [Fact]
    public void DoesNotChangeCommandResponses()
    {
        const string message = "{\"kind\":\"action.result\",\"data\":{\"message\":\"Done\"}}";
        Assert.Equal(message, PersonalBattleHud.Prepare(message, "hero-a"));
    }
}
