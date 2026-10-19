"""Run production unequip/equip command bodies against an isolated campaign fixture.

The item generator is a spy: these tests cover clearing, guards, stored progression,
and dispatch into generation, not Bannerlord's runtime item catalogue.
Usage: python tools/test-unequip.py OUTPUT_DIR
"""
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
out = Path(sys.argv[1]).resolve()
out.mkdir(parents=True, exist_ok=True)

def method(source, signature):
    start = source.index(signature)
    pos = source.index('{', start)
    end, depth = pos + 1, 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]

actions = root / 'BannerlordTwitch/BLTAdoptAHero/Actions'
equip = (actions / 'EquipHero.cs').read_text(encoding='utf-8-sig')
unequip = (actions / 'UnequipHero.cs').read_text(encoding='utf-8-sig')
fixture = (root / 'tools/unequip-fixture.cs.txt').read_text(encoding='utf-8')
fixture = fixture.replace('/* UNEQUIP */', method(unequip, '        protected override void ExecuteInternal('))
fixture = fixture.replace('/* EQUIP */', method(equip, '        protected override void ExecuteInternal('))
fixture = fixture.replace('/* CLEAR */', method(equip, '        internal static void RemoveAllEquipment('))
(out / 'Program.cs').write_text(fixture, encoding='utf-8')
(out / 'Test.csproj').write_text('''<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework>
<ImplicitUsings>enable</ImplicitUsings><NuGetAudit>false</NuGetAudit></PropertyGroup>
</Project>''', encoding='utf-8')
sys.exit(subprocess.call(['dotnet', 'run', '--project', str(out / 'Test.csproj')]))
