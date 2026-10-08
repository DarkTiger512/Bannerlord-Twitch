import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { cleanName, parseCommands, profilePath } from './command-profile.mjs';
const [classic = 'main', taom = 'taom'] = process.argv.slice(2);
const read = (ref, file) => execFileSync('git', ['show', `${ref}:${file}`], { encoding: 'utf8' }).replace(/^\uFEFF/, '').replace(/\r\n/g, '\n');
const shared = [
  'BannerlordTwitch/BLTAdoptAHero/Actions/AdoptHeir.cs',
  'BannerlordTwitch/BLTAdoptAHero/Actions/FamilyManagement.cs',
  'BannerlordTwitch/BLTAdoptAHero/Util/AdoptHeirService.cs',
  'BannerlordTwitch/BLTAdoptAHero/Util/NativeOffspringAdapter.cs',
  'BannerlordTwitch/BLTAdoptAHero/Behaviors/BLTHeirBehavior.cs',
  'BannerlordTwitch/BannerlordTwitch/Settings/ForgeCommandDefaults.cs',
  'docs/ADOPT-HEIR.md',
  ...['AdoptHeirTests.csproj', 'Program.cs', 'Stubs.cs'].map(f => `tools/adopt-heir-tests/${f}`),
];
for (const file of shared) assert.equal(read(classic, file), read(taom, file), `Shared heir source divergence: ${file}`);
const heir = ref => read(ref, 'BannerlordTwitch/BLTAdoptAHero/Actions/HeirCommand.cs').replace(', enforceTierCap: true', '');
assert.equal(heir(classic), heir(taom), 'Heir behavior differs beyond intentional TOAM equipment wiring');
const commands = ref => parseCommands(read(ref, profilePath)).filter(c => c.Handler === 'AdoptHeir');
assert.deepEqual(commands(classic), commands(taom), 'AdoptHeir command configuration differs');
assert.equal(commands(classic).length, 1);
assert.equal(cleanName(commands(classic)[0].Name), 'adoptheir');
assert.equal(commands(classic)[0].HandlerConfig.GoldCost, 0);
for (const ref of [classic, taom]) {
  const project = read(ref, 'BannerlordTwitch/BLTAdoptAHero/BLTAdoptAHero.csproj');
  for (const file of ['Actions\\AdoptHeir.cs', 'Util\\AdoptHeirService.cs', 'Util\\NativeOffspringAdapter.cs']) assert(project.includes(file), `Missing compiled source: ${ref}/${file}`);
  const behavior = read(ref, 'BannerlordTwitch/BLTAdoptAHero/Behaviors/BLTHeirBehavior.cs');
  assert(behavior.includes('"HeirData"') && behavior.includes('"PendingOffspringV1"'), 'Save keys missing');
  assert(read(ref, 'BannerlordTwitch/BLTAdoptAHero/Behaviors/BLTAdoptAHeroCampaignBehavior.cs').includes('IsUnavailableForAdoption(h)'), 'Reserved heirs can be taken by adoption');
}
const properties = read(taom, 'BannerlordTwitch/BLTProperties.targets');
assert(properties.includes('TAOM_HARMONY_DLL') && properties.includes('TAOMHarmonyPath') && properties.includes('<Private>False</Private>'));
assert(read(taom, 'BannerlordTwitch/BLTAdoptAHero/Actions/HeirCommand.cs').includes('enforceTierCap: true'));
console.log(`PASS: shared heir source, metadata, tests and command match ${classic}/${taom}; TOAM Harmony/equipment wiring retained.`);
