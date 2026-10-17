# Universal weapon forging

TAOM forging selects existing installed weapon designs and gives each purchase its own custom modifier. It never generates or registers a new weapon design or dispatches a native crafting event. Shared base items are never edited. Previously generated weapons and their forge metadata remain readable; this change does not replace or repair their assets. Classic is unchanged by this TAOM compatibility change.

## Enable and configure

`forge` and `reforge` are installed **disabled by default**, pending real-game verification. Enable them in BLT Configure's Commands list. Existing profiles receive only missing feature commands; renamed/disabled handlers, name/ID collisions, costs, and other settings are preserved. Configuration generation stays unchanged.

Each command exposes its own prices and balance settings in Handler Config. TargetTier chooses the nearest installed tier after culture and compatibility filtering. Obsolete native-generation settings are retained for old configuration files but hidden from the editor. Item restrictions remain in Common Config. Defaults:

| Choice | Gold | Effect |
| --- | ---: | --- |
| Standard | 500,000 | +10 damage / +5 speed points |
| Fine | 750,000 | +5% base damage, +20 damage / +10 speed points |
| Masterwork | 1,000,000 | +10% base damage, +30 damage / +15 speed points |
| Quality upgrade | 250,000 | Advance one step, guaranteed |
| Style change | 100,000 | Replace the previous style contribution |
| Explicit culture surcharge | 0 | Added once per forge |

Swift uses -5% damage/+5% weapon speed; Heavy reverses this; Balanced contributes neither. Styles change modifier statistics, not the weapon geometry. Bonuses become rounded additive native modifiers derived from the strongest base damage and speed of the primary weapon. Swing/thrust modes share that modifier, so their individual percentage changes can differ. Projectile speed and ammunition counts are unchanged. Ranged speed uses the native weapon speed modifier, subject to the engine's firing/reload behavior.

## Chat examples

- `!forge longsword elven swift fine`
- `!forge bow "Elven Kingdom" heavy masterwork`
- `!forge random`
- `!forge cultures` / `!forge types`
- `!reforge` / `!reforge #2`
- `!reforge #2 balanced` / `!reforge #2 upgrade`

`longsword` maps to TwoHandedSword. Culture resolution uses registered IDs, exact display names, configured aliases, then unique display-name prefixes. `elven` resolves only when an installed culture or configured alias identifies it unambiguously. CultureAliases values are culture IDs; TypeAliases values are equipment types or crafting template IDs. Unknown and ambiguous choices are free errors.

Culture discovery reuses `CampaignHelpers.MainCultures` (as adoption and campaign info do), supplemented by registered non-main cultures with installed weapons. No kingdom is required. Selection prefers exact culture, then the nearest configured tier; ties are random. Culture fallback is always disclosed. Template IDs remain aliases for weapon families that have installed candidates. Player-crafted items are excluded from the base catalogue.

Random forging prefers configured class types, falling back to current weapon types if the class has no available weapon type. It tries other eligible types when a type is unusable. No crafting pieces or native generation attempts are used. Explicit forging may store a weapon beyond current skill requirements, but race/gender compatibility still applies; auto-equip requires usability and a matching slot, preserves custom weapons, and does not replace higher-tier equipment.

## Ownership, reforging and legacy saves

Forged weapons enter the existing custom inventory and use existing naming, equipping, auctions/trading and inheritance. Inventory numbers are the existing custom inventory indices. Forge metadata is optional JSON on the modifier; old save keys and object/modifier IDs are unchanged. Existing base item identities are retained; only custom modifiers are newly registered.

Reforging keeps the original design and item identity for both generation methods, updates only forge stat contributions, and preserves names and enchantment histories. This avoids replacing equipped/stored references during style changes. Style changes never regenerate geometry. Quality is capped at Masterwork. Same-style requests, capped upgrades and changes that round to identical stats are free errors.

Legacy bonuses are snapshotted as a protected baseline at the first reforge; legacy weapons start at forge quality Standard and style Balanced. Future changes subtract only the previous forge contribution. `!enchant` keeps its existing independent +5 progression and risks. Gold and modifier/inventory/equipment state roll back on purchase failures. Missions, prisoners, auctioned weapons, insufficient gold and full inventories block the relevant purchases. Missing mod assets continue through the existing invalid-item handling/compensation; the metadata does not recreate removed assets. Persistence is within a campaign, not between unrelated campaigns.

## Verification and release gate

Run from the repository root:

```powershell
dotnet run --project tools/forge-tests/ForgeTests.csproj
dotnet run --project BannerlordTwitch/BLTAdoptAHero.Tests/BLTAdoptAHero.Tests.csproj -p:NuGetAudit=false
node tools/verify-enchantment-profile.mjs
node tools/verify-forge-parity.mjs main taom
```

The integration fixture links production handlers, asset generation, purchase service, modifier behavior, enchantment policy, profile merge policy, and the existing JSON save serializer against engine stubs. It exercises culture discovery and ambiguity, existing-design selection, restrictions, absence of native generation/events, pricing, style reversal, enchantment preservation, old-save compatibility, rollback, profile preservation and campaign isolation. Engine stubs cannot verify native visuals, combat speed, actual game serialization, or third-party event behavior.

Before enabling commands in release defaults, test a campaign with vanilla Classic, Classic content/crafting mods, and actual TOAM: forge melee/throwing/ranged weapons, confirm parts and culture fallbacks, test mounted/race restrictions, rename/equip/trade/inherit/reforge, save/reload, and test absent crafting assets. Confirm Harmony loading under each TOAM-supported dependency path. No real-game verification is claimed by the automated fixture.

The TAOM existing-design compatibility change intentionally diverges from Classic in forge selection, purchase registration, editor text and integration fixtures. The historical main/taom parity script will report those differences until the change is ported to Classic or its expected differences are updated. TOAM Harmony resolution, disabled copying, manifests, optional naval dependencies and race compatibility remain branch-owned.

## Updating older profiles

Weapon commands using the old SmithItem handler (including renamed smithweapon commands) now migrate to ForgeWeapon. Their names, IDs and enabled states stay unchanged. The old gold price becomes Standard; Fine and Masterwork cost 250,000 and 500,000 more. Culture surcharges carry over. Old random ItemPower/name settings are retained in LegacySmithSettings for reference; new purchases use forging quality/style balance instead. Armour, shields and mount purchases remain on SmithItem.

In BLT Configure, expand Handler Config on forge or reforge to edit its prices, weapon tier, style trade-off and quality bonuses directly. There is no shared/global forging switch. Previously shared forging values are copied into the commands that used them, while existing command-specific values and migrated weapon purchase prices are preserved. The obsolete Weapon Forging global entry is removed. TargetTier uses internal numbering (5 means displayed Tier 6). Existing command enabled states remain authoritative.

## Quality point bonuses

Each command has configurable extra damage and speed points for each quality. Defaults are Standard +10 damage/+5 speed, Fine +20/+10, and Masterwork +30/+15. These are total quality point bonuses, not cumulative steps. Existing damage percentages and Swift/Heavy trade-offs apply separately, using the original weapon stats. Projectile speed is unchanged.

For an already-forged weapon, check `!reforge #N` for its current style, then request that same style (for example, `!reforge #6 balanced`). If the configured bonuses have changed, BLT refreshes its forge bonuses for free, preserving its name, ownership and enchantments. Repeating this cannot stack bonuses. Legacy weapons without forge metadata keep their existing bonuses until a normal reforge.

When updating from the stronger defaults, existing saved command settings remain unchanged. Set the six quality point bonuses to the values above in both forge and reforge Handler Config to use the reduced balance, then use the same-style refresh for previously forged weapons.
