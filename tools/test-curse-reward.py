"""Exercise production curse reward methods with campaign/engine stubs."""
from pathlib import Path
import subprocess, sys
root=Path(__file__).resolve().parents[1]
out=Path(sys.argv[1]).resolve(); out.mkdir(parents=True, exist_ok=True)
s=(root/'BannerlordTwitch/BLTAdoptAHero/Behaviors/CursedArtifactBehavior.cs').read_text(encoding='utf-8-sig')
def method(signature):
 start=s.index(signature); pos=s.index('{',start); depth=1; end=pos+1
 while depth:
  depth+=(s[end]=='{')-(s[end]=='}'); end+=1
 return s[start:end]
methods='\n'.join(method(x) for x in ['        private void TryGrantPendingReward()', '        private void NotifyPendingReward()', '        private Hero ResolveHero()', '        private static Hero ResolveCampaignHero(', '        private void RestoreLookupFailure()', '        private void Fail('])
fixture=(root/'tools/curse-reward-fixture.cs.txt').read_text(encoding='utf-8')
(out/'Program.cs').write_text(fixture.replace('/* METHODS */',methods),encoding='utf-8')
source=root/'BannerlordTwitch/BLTAdoptAHero/Util/CursedArtifactPolicy.cs'
(out/'Test.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup><Compile Include="{source}" /></ItemGroup></Project>',encoding='utf-8')
sys.exit(subprocess.call(['dotnet','run','--project',str(out/'Test.csproj')]))
