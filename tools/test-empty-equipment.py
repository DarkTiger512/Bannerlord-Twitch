"""Exercise current production TAOM generation and paid commands from empty loadouts.
Usage: python tools/test-empty-equipment.py OUTPUT_DIR
Uses the shared small engine fixture; weapon engine semantics are stubbed.
"""
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
out = Path(sys.argv[1]).resolve()
out.mkdir(parents=True, exist_ok=True)
source = (root / 'BannerlordTwitch/BLTAdoptAHero/Actions/EquipHero.cs').read_text(encoding='utf-8-sig')

def block(text, start):
    brace = text.index('{', start)
    end, depth = brace + 1, 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[start:end]

def method(signature):
    start = source.index(signature)
    brace = source.index('{', start)
    arrow = source.find('=>', start, brace)
    if arrow >= 0:
        return source[start:source.index(';', arrow) + 1]
    return block(source, start)

signatures = ['internal static void RemoveAllEquipment(', 'public static bool UpgradeEquipment(',
              'public static bool HeroShouldUseHorse(', 'private static void UpgradeCivilian(',
              'public enum MountFamilyType', 'public enum FindFlags',
              'public static ItemObject FindRandomTieredEquipment(', 'public static ItemObject SelectRandomItemNearestTier(',
              'public static bool CanUseItem(', 'private static ItemObject FindLastResortEquipment(',
              'private static bool CanUseItemIgnoringRace(']
fixture = (root / 'tools/taom-equipment-fixture.cs.txt').read_text()
fixture = fixture.replace('(EquipmentIndex, ItemObject.ItemTypeEnum)[] ArmorIndexType',
                          '(EquipmentIndex index, ItemObject.ItemTypeEnum itemType)[] ArmorIndexType')
fixture = fixture.replace('/* EQUIPMENT METHODS */', '\n'.join(method(s) for s in signatures))
fixture = fixture.replace('/* ADOPTION */', '')
paid = method('protected override void ExecuteInternal(')
fixture = fixture.replace('/* PAID COMMAND */', paid[paid.index('int targetTier'):paid.rfind('}')])
main = block(fixture, fixture.index('    public static void Main()'))
fixture = fixture.replace(main, '''public static void Main()
    {
        foreach (bool classless in new[] { false, true })
        foreach (bool reequip in new[] { true, false })
        {
            Reset();
            var hero = Hero(1); hero.Tier = 0;
            if (classless) hero.Class = null;
            var sword = Item("ordinary-sword", reequip ? 0 : 1, ItemObject.ItemTypeEnum.OneHandedWeapon);
            sword.RelevantSkill = SkillGroup.SkillItemPairs[0].skill;
            var armour = Item("ordinary-armour", reequip ? 0 : 1);
            Donor(1, sword, armour);
            var custom = Item("stored-custom", 0, ItemObject.ItemTypeEnum.Cape);
            Donor(1, custom); hero.CustomItems.Add(new(custom));
            EquipHero.RemoveAllEquipment(hero);
            Check(!hero.BattleEquipment.YieldFilledEquipmentSlots().Any()
                && !hero.CivilianEquipment.YieldFilledEquipmentSlots().Any(), "Start from genuinely empty loadouts");
            string failure = null, success = null;
            EquipHero.Paid(hero, new Settings { ReequipInsteadOfUpgrade = reequip }, s => success = s, s => failure = s);
            Check(failure == null && success != null, "Empty loadout generation succeeds for classless=" + classless);
            Check(hero.BattleEquipment[EquipmentIndex.Weapon0].Item == sword
                && hero.BattleEquipment[EquipmentIndex.Body].Item == armour
                && hero.CivilianEquipment[EquipmentIndex.Body].Item == armour, "Rebuild actual weapons and armour");
            Check(hero.Tier == (reequip ? 0 : 1) && hero.Gold == 900, "Keep normal tier and cost rules");
            Check(hero.CustomItems.Count == 1, "Stored custom items survive generation");
        }
        Console.WriteLine($"{checks} empty-equipment recovery checks passed.");
    }''')
(out / 'Program.cs').write_text(fixture, encoding='utf-8')
helper = root / 'BannerlordTwitch/BLTAdoptAHero/Util/TaomEquipmentCompatibility.cs'
(out / 'Test.csproj').write_text(f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
<OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings>
<NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup><Compile Include="{helper}" /></ItemGroup></Project>''', encoding='utf-8')
sys.exit(subprocess.call(['dotnet', 'run', '--project', str(out / 'Test.csproj')]))
