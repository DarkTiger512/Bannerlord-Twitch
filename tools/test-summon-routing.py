"""Compile the real SummonHero dispatch method against engine stubs; no game spawning.

Usage: python tools/test-summon-routing.py OUTPUT_DIR [SOURCE_FILE]
The optional source file allows proving the regression against a previous revision.
"""
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
source = Path(sys.argv[2]) if len(sys.argv) > 2 else root / "BannerlordTwitch/BLTAdoptAHero/Actions/SummonHero.cs"
text = source.read_text(encoding="utf-8-sig")
start = text.index("        protected override void ExecuteInternal(Hero adoptedHero")
end = text.index("        public class SimpleAgentOrigin", start)
method = text[start:end].replace("protected override void ExecuteInternal", "public void ExecuteInternal", 1)
output = Path(sys.argv[1]).resolve()
output.mkdir(parents=True, exist_ok=True)
template = (root / "tools/summon-routing-fixture.cs.txt").read_text(encoding="utf-8")
(output / "Program.cs").write_text(template.replace("/* ACTUAL_DISPATCH_METHOD */", method), encoding="utf-8")
(output / "SummonRouting.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><NuGetAudit>false</NuGetAudit></PropertyGroup></Project>', encoding="utf-8")
sys.exit(subprocess.call(["dotnet", "run", "--project", str(output / "SummonRouting.csproj")]))
