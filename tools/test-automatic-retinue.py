"""Run production automatic-retinue scheduling methods against engine stubs."""
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
source = (root / 'BannerlordTwitch/BLTAdoptAHero/Behaviors/BLTSummonBehavior.cs').read_text(encoding='utf-8-sig')
def method(signature):
    start = source.index(signature)
    pos = source.index('{', start) + 1
    depth = 1
    end = pos
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end].replace('public override ', 'public ')

methods = '\n'.join(method(s) for s in [
    '        public HeroSummonState AddHeroSummonState(',
    '        public override void OnAgentBuild(',
    '        private void SpawnPendingAutomaticRetinues(',
    '        public override void OnMissionTick('])
out = Path(sys.argv[1]).resolve()
out.mkdir(parents=True, exist_ok=True)
fixture = (root / 'tools/automatic-retinue-fixture.cs.txt').read_text()
(out / 'Program.cs').write_text(fixture.replace('/* METHODS */', methods))
(out / 'Tests.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><NuGetAudit>false</NuGetAudit></PropertyGroup></Project>')
subprocess.run(['dotnet', 'run', '--project', str(out / 'Tests.csproj')], check=True)
