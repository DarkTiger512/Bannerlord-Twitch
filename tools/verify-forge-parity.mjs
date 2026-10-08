import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { cleanName, parseCommands, profilePath } from './command-profile.mjs';

const [classic = 'main', taom = 'taom'] = process.argv.slice(2);
const read = (ref, path) => execFileSync('git', ['show', `${ref}:${path}`], { encoding: 'utf8' }).replace(/^\uFEFF/, '').replace(/\r\n/g, '\n');
const shared = [
  'BLTAdoptAHero/Actions/ForgeWeapon.cs', 'BLTAdoptAHero/Actions/ReforgeWeapon.cs',
  'BLTAdoptAHero/Actions/Util/CustomItems.cs', 'BLTAdoptAHero/GlobalConfigs/GlobalForgeConfig.cs',
  'BLTAdoptAHero/Util/ForgePolicy.cs', 'BLTAdoptAHero/Util/ForgeAssets.cs', 'BLTAdoptAHero/Util/ForgeService.cs',
  'BLTAdoptAHero/Behaviors/BLTCustomItemsCampaignBehavior.cs',
  'BannerlordTwitch/Settings/Settings.cs', 'BannerlordTwitch/Settings/ForgeCommandDefaults.cs',
];
for (const file of shared) assert.equal(read(classic, `BannerlordTwitch/${file}`), read(taom, `BannerlordTwitch/${file}`), `Shared source diverged: ${file}`);
for (const file of ['ForgeTests.csproj', 'Program.cs', 'Stubs.cs'])
  assert.equal(read(classic, `tools/forge-tests/${file}`), read(taom, `tools/forge-tests/${file}`), `Test divergence: ${file}`);
const featureCommands = ref => parseCommands(read(ref, profilePath)).filter(c => ['ForgeWeapon', 'ReforgeWeapon'].includes(c.Handler));
assert.deepEqual(featureCommands(classic), featureCommands(taom), 'Forge command configuration divergence');
assert.deepEqual(featureCommands(classic).map(c => cleanName(c.Name)), ['forge', 'reforge']);
assert(featureCommands(classic).every(c => c.Enabled === false), 'Real-game verification gate: commands should remain opt-in');
for (const ref of [classic, taom]) {
  assert(read(ref, 'BannerlordTwitch/BLTAdoptAHero/Behaviors/BLTAdoptAHeroCampaignBehavior.cs').includes('ForgeAssets.Reset()'));
  const project = read(ref, 'BannerlordTwitch/BLTAdoptAHero/BLTAdoptAHero.csproj');
  for (const file of shared.filter(f => f.startsWith('BLTAdoptAHero/'))) assert(project.includes(file.replace('BLTAdoptAHero/', '').replaceAll('/', '\\')));
}
// Intentional branch differences are in existing equipment policy and build/dependency files.
assert(read(taom, 'BannerlordTwitch/BLTAdoptAHero/Actions/EquipHero.cs').includes('TaomEquipmentCompatibility.CanUse(hero, item)'));
const properties = read(taom, 'BannerlordTwitch/BLTProperties.targets');
assert(properties.includes('TAOM_HARMONY_DLL') && properties.includes('TAOMHarmonyPath') && properties.includes('<Private>False</Private>'));
console.log(`PASS: shared forging source, fixtures and commands match ${classic} / ${taom}; TOAM compatibility and Harmony wiring retained.`);
