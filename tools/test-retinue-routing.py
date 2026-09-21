"""Compile the real retinue command, campaign and troop-index code against small engine stubs.
Usage: python tools/test-retinue-routing.py OUTPUT_DIR
"""
from pathlib import Path
import subprocess, sys
root = Path(__file__).resolve().parents[1]
out = Path(sys.argv[1]).resolve()
out.mkdir(parents=True, exist_ok=True)
source = (root / "BannerlordTwitch/BLTAdoptAHero/Behaviors/BLTAdoptAHeroCampaignBehavior.cs").read_text(encoding="utf-8-sig")
def method(text, signature):
    start = text.index(signature)
    brace = text.index("{", start)
    depth = 1
    pos = brace + 1
    while depth:
        depth += (text[pos] == "{") - (text[pos] == "}")
        pos += 1
    return text[start:pos]
methods = [method(source, s) for s in [
    "        public void SetClass(", "        private CharacterObject SelectClassGuidedTroop(",
    "        private void ConvertClassGuidedRetinues(",
    "        private static IReadOnlyList<CharacterObject> OrdinaryRetinueRoots(",
    "        private (bool changed, List<string> messages) ReconcileOrdinaryRetinue(", "        private static void ConvertIncompatible<T>(",
    "        public (bool success, string status) UpgradeRetinue(",
    "        public (bool success, string status) UpgradeRetinue2(",
    "        public void KillRetinueAtIndex(", "        public void KillRetinue2AtIndex("
]]
commands=[]
for name in ["Retinue", "Retinue2"]:
    text = (root / f"BannerlordTwitch/BLTAdoptAHero/Actions/{name}.cs").read_text(encoding="utf-8-sig")
    m = method(text, "        protected override void ExecuteInternal(")
    commands.append(m.replace("protected override void ExecuteInternal", f"public void Execute{name}").replace("(Settings)config", "(CommandSettings)config"))
fixture = (root / "tools/retinue-routing-fixture.cs.txt").read_text(encoding="utf-8")
(out / "Program.cs").write_text(fixture.replace("/* CAMPAIGN METHODS */", "\n".join(methods)).replace("/* COMMAND METHODS */", "\n".join(commands)), encoding="utf-8")
links = "".join(f'<Compile Include="{root / "BannerlordTwitch/BLTAdoptAHero/Util" / name}" Link="{name}" />' for name in ["SmartTroopPolicy.cs", "TroopTreeIndex.cs", "CycleSafeGraph.cs"])
(out / "RetinueRouting.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup>'+links+'</ItemGroup></Project>', encoding="utf-8")
sys.exit(subprocess.call(["dotnet", "run", "--project", str(out / "RetinueRouting.csproj")]))
