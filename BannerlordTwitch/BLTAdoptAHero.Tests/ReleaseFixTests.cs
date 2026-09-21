using BannerlordTwitch;
using BLTAdoptAHero.Util;
using Newtonsoft.Json;

internal static class ReleaseFixTests
{
    public static void Run()
    {
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        const string defaults = "ConfigurationGeneration: 1\nCommands: []\nGlobalConfigs: []\n";
        int reads = 0;
        string ReadDefaults() { reads++; return defaults; }
        foreach (string old in new[] { null, "", "Commands: [!RemovedHandler {Name: obsolete}]\n", "ConfigurationGeneration: 0\nCommands: []\n" })
        {
            Check(ConfigurationVersioning.SelectYaml(old, ReadDefaults, out bool reset) == defaults && reset,
                "Missing/old profiles must reset before resolving obsolete tagged handlers.");
        }
        string edited = "ConfigurationGeneration: 1\nCommands: [{Name: mycustomcommand, Enabled: false}]\n";
        int before = reads;
        for (int i = 0; i < 2; i++)
            Check(ConfigurationVersioning.SelectYaml(edited, ReadDefaults, out bool reset) == edited && !reset,
                "Current profile edits must survive repeated loads.");
        Check(reads == before, "Current profiles must not read or merge defaults.");
        bool rejected = false;
        try { ConfigurationVersioning.SelectYaml(null, () => "Commands: []", out _); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Outdated installed defaults must not cause a perpetual reset.");
        rejected = false;
        try { ConfigurationVersioning.SelectYaml("ConfigurationGeneration: 2", ReadDefaults, out _); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Do not downgrade future-generation profiles.");

        Check(!SimGoldPolicy.TryParse(false, false, "caller", "500", out _, out _, out _), "Viewer must not grant gold.");
        Check(SimGoldPolicy.TryParse(true, false, "caller", "500", out int gold, out string viewer, out _) && gold == 500 && viewer == "caller", "Broadcaster self grant.");
        Check(SimGoldPolicy.TryParse(false, true, "caller", "250 @target", out gold, out viewer, out _) && gold == 250 && viewer == "target", "Moderator targeted grant.");
        foreach (string invalid in new[] { "", "0", "-1", "1.5", "NaN", "2147483648", "100 user extra", "100 @" })
            Check(!SimGoldPolicy.TryParse(true, true, "caller", invalid, out _, out _, out _), "Reject malformed grant: " + invalid);
        Check(SimGoldPolicy.CanGrant(int.MaxValue - 1, 1) && !SimGoldPolicy.CanGrant(int.MaxValue, 1), "Gold overflow boundary.");

        Check(!RandomEventPolicy.CanStartImmortalBattle(19, 100) && !RandomEventPolicy.CanStartImmortalBattle(20, 100)
            && RandomEventPolicy.CanStartImmortalBattle(21, 100), "Immortal requires strictly more than 20% health.");
        Check(!RandomEventPolicy.CanStartImmortalBattle(40, 200) && RandomEventPolicy.CanStartImmortalBattle(41, 200), "Health must use max HP, not a fixed hit-point count.");
        Check(!RandomEventPolicy.CanStartImmortalBattle(10, 0) && !RandomEventPolicy.CanStartImmortalBattle(float.NaN, 100)
            && !RandomEventPolicy.CanStartImmortalBattle(float.PositiveInfinity, 100), "Invalid HP must not bypass the gate.");

        var record = new CurseRecord { HeroId = "cursed" };
        var participation = new CursedBattleParticipation();
        for (int i = 0; i < 5; i++)
        {
            var battle = new object();
            participation.Mark(battle, i % 2); // Both attacker and defender, independent of campaign party.
            participation.Mark(battle, i % 2); // Repeated spawn/knockout callbacks must not add a win.
            Check(!participation.Complete(record, new object(), i % 2, true, 5), "Unrelated AI battle must not consume participation.");
            Check(participation.Complete(record, battle, i % 2, true, 5), "Actual winning mission participant must progress.");
            Check(!participation.Complete(record, battle, i % 2, true, 5), "Duplicate completion must not count.");
            Check(record.QualifyingWins == i + 1, "Exactly one increment per victory.");
            record = JsonConvert.DeserializeObject<CurseRecord>(JsonConvert.SerializeObject(record));
        }
        Check(record.Status == CurseLifecycle.CompletedPendingReward && record.QualifyingWins == 5, "Fifth win must enter pending reward state across saves.");
        var other = new CurseRecord();
        var loss = new object();
        participation.Mark(loss, 0);
        Check(!participation.Complete(other, loss, 1, true, 5), "Loss must not count.");
        var retreat = new object();
        participation.Mark(retreat, 0);
        Check(!participation.Complete(other, retreat, 0, false, 5), "Unresolved retreat must not count.");
        Check(!participation.Complete(other, retreat, 0, true, 5), "Later auto-resolve must not reuse retreat participation.");
        Check(!participation.Complete(other, new object(), 0, true, 5) && other.QualifyingWins == 0, "No participation means no victory credit.");
        Console.WriteLine("Clean configuration, SimGold authorization/input, Immortal HP, and cursed participation regression tests passed.");
    }
}
