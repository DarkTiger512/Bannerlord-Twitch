"""Exercise Shokuho party order dispatch and native army handoff."""
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
source = (root / "BannerlordTwitch/BLTAdoptAHero/Behaviors/PartyOrderBehavior.cs").read_text(encoding="utf-8-sig")
follower = source[source.index("        private static bool IsArmyFollower"):source.index("        private void OnArmyDispersed")]
dispatch = source[source.index("        public static void IssueOrder"):source.index("        /// <summary>\n        /// Dynamic defend/patrol")]
patrol = source[source.index("        private static void IssuePatrol"):source.index("        public static bool IsSettlementReachable")]
drift = source[source.index("                bool behaviorMatches"):source.index("                {\n                    order.ReissueAttempts = 0;\n                    if (order.Type", source.index("                bool behaviorMatches"))]
drift = drift.replace("if (behaviorMatches && targetMatches && !patrolHeld)", "return behaviorMatches && targetMatches && !patrolHeld;")
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
enum AiBehavior { None, PatrolAroundPoint }
enum MoveModeType { Hold, Point }
sealed class Order { public PartyOrderType Type = PartyOrderType.Patrol; }
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
    public Settlement TargetSettlement;
    public AiBehavior DefaultBehavior;
    public MoveModeType MoveMode;
    public List<string> Calls = new List<string>();
    public PartyAi Ai => new PartyAi(this);
}
sealed class PartyAi
{
    private readonly MobileParty party;
    public PartyAi(MobileParty party) => this.party = party;
    public bool DoNotMakeNewDecisions => party.DecisionsLocked;
    public MoveModeType PartyMoveMode => party.MoveMode;
    public void SetDoNotMakeNewDecisions(bool locked) => party.DecisionsLocked = locked;
    public void SetMovePatrolAroundSettlement(Settlement target)
    {
        if (DoNotMakeNewDecisions) throw new Exception("Previous order still locks patrol");
        party.Calls.Add("patrol");
    }
    public void RecalculateShortTermAi()
    {
        if (DoNotMakeNewDecisions) throw new Exception("AI locked during refresh");
        party.Calls.Add("refresh");
    }
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
    public static void GetActionForPatrollingAroundSettlement(MobileParty party, Settlement target)
        => party.Ai.SetMovePatrolAroundSettlement(target);
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
PATROL
    private static bool IsPatrolCurrent(MobileParty party, Settlement expectedTarget)
    {
        var order = new Order();
        var expectedBehavior = AiBehavior.PatrolAroundPoint;
DRIFT
    }
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
    }
    static void Main()
    {
        var home = new Settlement();
        var target = new Settlement();
        foreach (var order in new[] { (PartyOrderType.Patrol, "patrol,refresh"), (PartyOrderType.Garrison, "visit"),
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
        Check(string.Join(",", leader.Calls) == "patrol,refresh" && leader.DecisionsLocked,
            "Army leader can start a patrol and keep the active order lock");
        var stalled = new MobileParty { DefaultBehavior = AiBehavior.PatrolAroundPoint, TargetSettlement = target };
        Check(!IsPatrolCurrent(stalled, target), "A matching patrol order in Hold needs to be retried");
        stalled.MoveMode = MoveModeType.Point;
        Check(IsPatrolCurrent(stalled, target), "An active patrol needs no refresh");
        stalled.TargetSettlement = null;
        Check(!IsPatrolCurrent(stalled, target), "A missing patrol destination needs to be restored");
        Console.WriteLine("PASS party departures, settled garrison and native army order handoff");
    }
}
""".replace("FOLLOWER", follower).replace("DISPATCH", dispatch).replace("PATROL", patrol).replace("DRIFT", drift), encoding="utf-8")
subprocess.run(["dotnet", "run", "--project", str(out / "Test.csproj")], check=True)
