"""Exercise real Shokuho orders with the engine's separate AI and navigation states."""
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
source = (root / "BannerlordTwitch/BLTAdoptAHero/Behaviors/PartyOrderBehavior.cs").read_text(encoding="utf-8-sig")
follower = source[source.index("        private static bool IsArmyFollower"):source.index("        private void OnArmyDispersed")]
dispatch = source[source.index("        public static void IssueOrder"):source.index("        public static bool IsSettlementReachable")]
monitor = source[source.index("                // ── SmartGuard: re-evaluate"):source.index("                // ── Garrison: satisfied")]
drift = source[source.index("                bool behaviorMatches"):source.index("                {\n                    order.ReissueAttempts = 0;\n                    if (order.Type", source.index("                bool behaviorMatches"))]
drift = drift.replace("if (behaviorMatches && targetMatches && !patrolHeld)", "return behaviorMatches && targetMatches && !patrolHeld;")
out = Path(sys.argv[1]).resolve()
out.mkdir(parents=True, exist_ok=True)
(out / "Test.csproj").write_text(
    '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
    '<TargetFramework>net9.0</TargetFramework><NuGetAudit>false</NuGetAudit>'
    '</PropertyGroup></Project>', encoding="utf-8")
fixture = (root / "tools/shokuho-party-orders-fixture.cs.txt").read_text(encoding="utf-8")
for marker, code in (("FOLLOWER", follower), ("DISPATCH", dispatch), ("MONITOR", monitor), ("DRIFT", drift)):
    fixture = fixture.replace(marker, code)
(out / "Program.cs").write_text(fixture, encoding="utf-8")
subprocess.run(["dotnet", "run", "--project", str(out / "Test.csproj")], check=True)
