"""Validate TAOM manifests, staging, no-DLC type loading and real optional DLC bindings.

Usage: python tools/test-optional-dependencies.py STAGED_RELEASE_DIR OUTPUT_DIR [GAME_DIR]
Does not install modules or open a campaign. Requires an installed DLC for the present test.
"""
from pathlib import Path
import os
import subprocess
import sys
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
staged, output = map(lambda p: Path(p).resolve(), sys.argv[1:3])
game = Path(sys.argv[3]) if len(sys.argv) > 3 else Path(r'C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord')
modules = ('BannerlordTwitch', 'BLTAdoptAHero', 'BLTBuffet', 'BLTConfigure')
for module in modules:
    manifest = ET.parse(staged / module / 'SubModule.xml')
    required = {e.get('Id') for e in manifest.findall('./DependedModules/DependedModule')}
    assert 'TAOM.Dependencies' in required, (module, required)
    assert not required.intersection(('NavalDLC', 'Bannerlord.Harmony')), (module, required)
    metadata = {e.get('id'): e for e in manifest.findall('./DependedModuleMetadatas/DependedModuleMetadata')}
    assert 'Bannerlord.Harmony' not in metadata, module
    assert metadata['TAOM.Dependencies'].get('order') == 'LoadBeforeThis', module
    assert metadata['NavalDLC'].get('optional') == 'true', module
    for path in (staged / module).rglob('*.dll'):
        assert not path.name.lower().startswith('navaldlc') and path.name.lower() != '0harmony.dll', path
print('PASS: all four launcher manifests and packaged DLLs', flush=True)

harmony = next(game / f'Modules/{m}/bin/Win64_Shipping_Client/0Harmony.dll'
               for m in ('TAOM.Dependencies', 'TAOM', 'Bannerlord.Harmony')
               if (game / f'Modules/{m}/bin/Win64_Shipping_Client/0Harmony.dll').exists())
output.mkdir(parents=True, exist_ok=True)
exe = output / 'OptionalDependenciesTests.exe'
compiler = Path(os.environ['WINDIR']) / 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
subprocess.run([str(compiler), '/nologo', f'/out:{exe}', str(root / 'tools/optional-dependencies-fixture.cs')], check=True)
subprocess.run([str(exe), str(staged), str(game), str(harmony), 'absent'], check=True)
subprocess.run([str(exe), str(staged), str(game), str(harmony), 'present'], check=True)
