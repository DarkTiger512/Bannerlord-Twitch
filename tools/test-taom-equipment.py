"""Exercise actual TAOM equipment-selection/adoption code with small engine stubs.
Usage: python tools/test-taom-equipment.py OUTPUT_DIR
"""
from pathlib import Path
import subprocess, sys
root = Path(__file__).resolve().parents[1]
out = Path(sys.argv[1]).resolve()
out.mkdir(parents=True, exist_ok=True)
source = (root / 'BannerlordTwitch/BLTAdoptAHero/Actions/EquipHero.cs').read_text(encoding='utf-8-sig')
adopt = (root / 'BannerlordTwitch/BLTAdoptAHero/Actions/AdoptAHero.cs').read_text(encoding='utf-8-sig')
def block(text, start):
    brace = text.index('{', start)
    end = brace + 1
    depth = 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[start:end]
def method(signature):
    return block(source, source.index(signature))
methods = [method(s) for s in ['internal static void RemoveAllEquipment(', 'public static bool UpgradeEquipment(', 'public static bool HeroShouldUseHorse(', 'private static void UpgradeCivilian(', 'public enum MountFamilyType', 'public enum FindFlags', 'public static ItemObject FindRandomTieredEquipment(', 'public static ItemObject SelectRandomItemNearestTier(', 'public static bool CanUseItem(']]
# Preserve the real starting-tier branch and the paid command's mutation/cost path.
adoption = block(adopt, adopt.index('if (settings.StartingEquipmentTier.HasValue)', adopt.index('var inheritedItems =')))
paid = method('protected override void ExecuteInternal(')
paid = paid[paid.index('int targetTier'):paid.rfind('}')]
fixture = (root / 'tools/taom-equipment-fixture.cs.txt').read_text()
(out / 'Program.cs').write_text(fixture.replace('/* EQUIPMENT METHODS */', '\n'.join(methods)).replace('/* ADOPTION */', adoption).replace('/* PAID COMMAND */', paid))
helper = root / 'BannerlordTwitch/BLTAdoptAHero/Util/TaomEquipmentCompatibility.cs'
(out / 'EquipmentTests.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup><Compile Include="{helper}" Link="TaomEquipmentCompatibility.cs" /></ItemGroup></Project>')
sys.exit(subprocess.call(['dotnet', 'run', '--project', str(out / 'EquipmentTests.csproj')]))
