"""Exercise the real Shokuho battle-readiness predicate used by follow and guard."""
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
source = root / "BannerlordTwitch"
helper = (source / "BannerlordTwitch/Helpers/MissionHelpers.cs").read_text(encoding="utf-8-sig")
start = helper.index("        public static bool IsBattleReady")
predicate = helper[start:helper.index(";", start) + 1]
for action in ("FollowCommand", "GuardCommand"):
    text = (source / f"BLTAdoptAHero/Actions/{action}.cs").read_text(encoding="utf-8-sig")
    assert "MissionHelpers.IsBattleReady" in text, f"{action} bypasses the readiness gate"

out = Path(sys.argv[1]).resolve()
out.mkdir(parents=True, exist_ok=True)
(out / "Readiness.csproj").write_text(
    '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
    '<TargetFramework>net9.0</TargetFramework><NuGetAudit>false</NuGetAudit>'
    '</PropertyGroup></Project>', encoding="utf-8")
(out / "Program.cs").write_text("""
using System;
class Mission
{
    public enum State { Loading, Continuing }
    public State CurrentState = State.Continuing;
    public bool IsLoadingFinished = true, IsMissionEnding;
    public MissionMode Mode = MissionMode.Battle;
    public Result MissionResult;
}
enum MissionMode { Battle, Deployment }
class Result { public bool BattleResolved; }
class Test
{
PREDICATE
    static void Check(Mission mission, bool expected)
    {
        if (IsBattleReady(mission) != expected) throw new Exception("Readiness mismatch");
    }
    static void Main()
    {
        Check(null, false);
        Check(new Mission(), true);
        Check(new Mission { IsLoadingFinished = false }, false);
        Check(new Mission { CurrentState = Mission.State.Loading }, false);
        Check(new Mission { Mode = MissionMode.Deployment }, false);
        Check(new Mission { IsMissionEnding = true }, false);
        Check(new Mission { MissionResult = new Result { BattleResolved = true } }, false);
        Check(new Mission { MissionResult = new Result() }, true);
        Console.WriteLine("PASS follow/guard battle-readiness states");
    }
}
""".replace("PREDICATE", predicate), encoding="utf-8")
subprocess.run(["dotnet", "run", "--project", str(out / "Readiness.csproj")], check=True)
