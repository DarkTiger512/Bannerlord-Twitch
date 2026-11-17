"""Exercise Shokuho party order dispatch and native army handoff."""
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
source = (root / "BannerlordTwitch/BLTAdoptAHero/Behaviors/PartyOrderBehavior.cs").read_text(encoding="utf-8-sig")
follower = source[source.index("        private static bool IsArmyFollower"):source.index("        private void OnArmyDispersed")]
dispatch = source[source.index("        public static void IssueOrder"):source.index("        /// <summary>\n        /// Dynamic defend/patrol")]
out = Path(sys.argv[1]).resolve()
out.mkdir(parents=True, exist_ok=True)
(out / "Test.csproj").write_text(
    '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
    '<TargetFramework>net9.0</TargetFramework><NuGetAudit>false</NuGetAudit>'
    '</PropertyGroup></Project>', encoding="utf-8")
(out / "Program.cs").write_text("""
using System;
using System.Collections.Generic;

enum PartyOrderType { Siege, Defend, Patrol, Garrison, Raid, SmartGuard }
sealed class Settlement
{
    public bool IsUnderSiege;
    public bool IsFortification = true;
}
sealed class Army { public MobileParty LeaderParty; }
sealed class MobileParty
{
    public Army Army;
    public Settlement CurrentSettlement;
    public object MapFaction, MapEvent;
    public string StringId = "party";
    public bool IsActive = true, IsMainParty;
    public bool DecisionsLocked, HasOrder;
    public List<string> Calls = new List<string>();
}
static class LeaveSettlementAction
{
    public static void ApplyForParty(MobileParty party)
    {
        party.Calls.Add("leave");
        party.CurrentSettlement = null;
    }
}
static class SetPartyAiAction
{
    public static void GetActionForPatrollingAroundSettlement(MobileParty party, Settlement target) => party.Calls.Add("patrol");
    public static void GetActionForVisitingSettlement(MobileParty party, Settlement target) => party.Calls.Add("visit");
    public static void GetActionForBesiegingSettlement(MobileParty party, Settlement target) => party.Calls.Add("siege");
    public static void GetActionForRaidingSettlement(MobileParty party, Settlement target) => party.Calls.Add("raid");
}
sealed class PartyManagement
{
    public Settlement FindBestSettlementToDefend(MobileParty party, object faction) => null;
}

sealed class Test
{
    private MobileParty party;
    private void CancelOrdersForParty(string id, string reason, bool notify)
    {
        Check(id == party.StringId, "Correct party identity");
        party.HasOrder = false;
        party.DecisionsLocked = false;
    }
    private static void IssueForceDefendOrder(MobileParty party, Settlement target) => party.Calls.Add("defend");
    private static void IssueSmartGuardOrder(MobileParty party, Settlement target) => party.Calls.Add("guard");
FOLLOWER
DISPATCH
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
    }
    static void Main()
    {
        var home = new Settlement();
        var target = new Settlement();
        foreach (var order in new[] { (PartyOrderType.Patrol, "patrol"), (PartyOrderType.Garrison, "visit"),
            (PartyOrderType.Raid, "raid"), (PartyOrderType.Siege, "siege"),
            (PartyOrderType.Defend, "defend"), (PartyOrderType.SmartGuard, "guard") })
        {
            var outbound = new MobileParty { CurrentSettlement = home };
            IssueOrder(outbound, order.Item1, target);
            Check(string.Join(",", outbound.Calls) == "leave," + order.Item2, "Departure before " + order.Item1);
        }

        var garrison = new MobileParty { CurrentSettlement = home };
        IssueOrder(garrison, PartyOrderType.Garrison, home);
        Check(garrison.CurrentSettlement == home && garrison.Calls.Count == 0, "Garrison stays home");
        IssueOrder(garrison, PartyOrderType.Patrol, null);
        Check(garrison.Calls.Count == 0, "Missing patrol target does not trigger departure");
        garrison.MapEvent = new object();
        IssueOrder(garrison, PartyOrderType.Patrol, target);
        Check(garrison.Calls.Count == 0, "Combat blocks departure");
        garrison.MapEvent = null;
        home.IsUnderSiege = true;
        IssueOrder(garrison, PartyOrderType.Patrol, target);
        Check(garrison.Calls.Count == 0, "Siege blocks departure");
        home.IsUnderSiege = false;

        var leader = new MobileParty { HasOrder = true, DecisionsLocked = true };
        var army = new Army { LeaderParty = leader };
        leader.Army = army;
        var follower = new MobileParty { Army = army, HasOrder = true, DecisionsLocked = true };
        var behavior = new Test { party = follower };
        behavior.OnPartyJoinedArmy(follower);
        Check(!follower.HasOrder && !follower.DecisionsLocked, "Native army call releases BLT order");
        IssueOrder(follower, PartyOrderType.Patrol, target);
        Check(follower.Calls.Count == 0, "Follower does not run BLT movement");
        behavior.party = leader;
        behavior.OnPartyJoinedArmy(leader);
        Check(leader.HasOrder && leader.DecisionsLocked, "Army leader keeps its order");
        IssueOrder(leader, PartyOrderType.Patrol, target);
        Check(string.Join(",", leader.Calls) == "patrol", "Army leader can patrol");
        Console.WriteLine("PASS party departures, settled garrison and native army order handoff");
    }
}
""".replace("FOLLOWER", follower).replace("DISPATCH", dispatch), encoding="utf-8")
subprocess.run(["dotnet", "run", "--project", str(out / "Test.csproj")], check=True)
