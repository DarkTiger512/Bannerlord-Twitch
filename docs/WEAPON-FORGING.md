# Universal weapon forging

Classic and TOAM use the same forge implementation. Native-craftable weapons are assembled from loaded crafting templates and compatible pieces. Types without a native template use a random eligible installed weapon from the requested culture, with a unique custom modifier. Shared base items are never edited.

## Enable and configure

`forge` and `reforge` are installed **disabled by default**, pending real-game verification. Enable them in BLT Configure's Commands list. Existing profiles receive only missing feature commands; renamed/disabled handlers, name/ID collisions, costs, and other settings are preserved. Configuration generation stays unchanged.

The global **Weapon Forging** configuration controls prices, quality/style percentages, target native tier, candidate budget, culture/type aliases, hidden-part access, and restricted part IDs. Item restrictions remain in Common Config. Both commands use the same global configuration. Defaults:

| Choice | Gold | Effect |
| --- | ---: | --- |
| Standard | 500,000 | No quality bonus |
| Fine | 750,000 | +5% base damage |
| Masterwork | 1,000,000 | +10% base damage |
| Quality upgrade | 250,000 | Advance one step, guaranteed |
| Style change | 100,000 | Replace the previous style contribution |
| Explicit culture surcharge | 0 | Added once per forge |

Swift uses -5% damage/+5% weapon speed; Heavy reverses this; Balanced contributes neither. Native part selection also favors lighter/faster or heavier/harder-hitting designs. Bonuses become rounded additive native modifiers derived from the strongest base damage and speed of the primary weapon. Swing/thrust modes share that modifier, so their individual percentage changes can differ. Projectile speed and ammunition counts are unchanged. Ranged speed uses the native weapon speed modifier, subject to the engine's firing/reload behavior.

## Chat examples

- `!forge longsword elven swift fine`
- `!forge bow "Elven Kingdom" heavy masterwork`
- `!forge random`
- `!forge cultures` / `!forge types`
- `!reforge` / `!reforge #2`
- `!reforge #2 balanced` / `!reforge #2 upgrade`

`longsword` maps to TwoHandedSword. Culture resolution uses registered IDs, exact display names, configured aliases, then unique display-name prefixes. `elven` resolves only when an installed culture or configured alias identifies it unambiguously. CultureAliases values are culture IDs; TypeAliases values are equipment types or crafting template IDs. Unknown and ambiguous choices are free errors.

Culture discovery reuses `CampaignHelpers.MainCultures` (as adoption and campaign info do), supplemented by registered non-main cultures with weapon/crafting assets. No kingdom is required. Native generation prefers matching pieces and neutral pieces; existing-weapon selection prefers exact culture. Culture fallback is always disclosed. Unsupported/broken native templates fail without silently turning into existing-item forging.

Random forging prefers configured class types, falling back to current weapon types if the class has no available weapon type. It tries other eligible types when a type is unusable. Native attempts share the configured budget (64 by default, maximum 128). Explicit forging may store a weapon beyond current skill requirements, but race/gender compatibility still applies; auto-equip requires usability and a matching slot, preserves custom weapons, and does not replace higher-tier equipment.

## Ownership, reforging and legacy saves

Forged weapons enter the existing custom inventory and use existing naming, equipping, auctions/trading and inheritance. Inventory numbers are the existing custom inventory indices. Forge metadata is optional JSON on the modifier; old save keys and object/modifier IDs are unchanged. Real native crafted-item registration is retained.

Reforging keeps the original design and item identity for both generation methods, updates only forge stat contributions, and preserves names and enchantment histories. This avoids replacing equipped/stored references during style changes. Only initial native forging selects style-specific geometry. Quality is capped at Masterwork. Same-style requests, capped upgrades and changes that round to identical stats are free errors.

Legacy bonuses are snapshotted as a protected baseline at the first reforge; legacy weapons start at forge quality Standard and style Balanced. Future changes subtract only the previous forge contribution. `!enchant` keeps its existing independent +5 progression and risks. Gold and modifier/inventory/equipment state roll back on purchase failures. Missions, prisoners, auctioned weapons, insufficient gold and full inventories block the relevant purchases. Missing mod assets continue through the existing invalid-item handling/compensation; the metadata does not recreate removed assets. Persistence is within a campaign, not between unrelated campaigns.

## Verification and release gate

Run from the repository root:

```powershell
dotnet run --project tools/forge-tests/ForgeTests.csproj
dotnet run --project BannerlordTwitch/BLTAdoptAHero.Tests/BLTAdoptAHero.Tests.csproj -p:NuGetAudit=false
node tools/verify-enchantment-profile.mjs
node tools/verify-forge-parity.mjs DT/shared-forging taom
```

The integration fixture links production handlers, asset generation, purchase service, modifier behavior, enchantment policy, profile merge policy, and the existing JSON save serializer against engine stubs. It exercises culture discovery and ambiguity, generation methods, restrictions, candidate limits, unique identity, pricing, style reversal, enchantment preservation, old-save compatibility, rollback, profile preservation and campaign isolation. Engine stubs cannot verify native visuals, combat speed, actual game serialization, or third-party event behavior.

Before enabling commands in release defaults, test a campaign with vanilla Classic, Classic content/crafting mods, and actual TOAM: forge melee/throwing/ranged weapons, confirm parts and culture fallbacks, test mounted/race restrictions, rename/equip/trade/inherit/reforge, save/reload, and test absent crafting assets. Confirm Harmony loading under each TOAM-supported dependency path. No real-game verification is claimed by the automated fixture.

Shared feature commits are based on `main` and cherry-picked to `taom`. The parity script compares shared production sources, test fixtures and feature commands while allowing existing project/dependency and equipment-policy differences. TOAM Harmony resolution, disabled copying, manifests, optional naval dependencies and race compatibility remain branch-owned.
