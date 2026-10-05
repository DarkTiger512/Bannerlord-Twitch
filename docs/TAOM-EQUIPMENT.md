# TAOM adoption and equipment

New `!adopt` heroes and heirs with a configured starting equipment tier now clear their template loadout and obey that tier cap in both battle and civilian equipment. Tier 1 means internal item tier 0. A configured tier of 0 removes equipment; an unset tier retains the existing configuration behavior.

The old custom-race exception preserved elite template equipment and derived the viewer tier from it. The old re-equip block avoided incompatible skeletons and mounts but prevented all equipment progression for these heroes. Both exceptions are removed.

With TAOM loaded, equipment candidates must appear in an authored troop or hero-template loadout for the hero's race. Live hero inventories cannot expand this allowlist. Culture selection still narrows the pool, but no longer selects the culture's highest-tier gear. Selection falls back only to the requested tier or below in TAOM; adoption also explicitly caps retained/custom items. Normal upgrades can preserve compatible higher-tier equipment already earned by the viewer.

Missing compatible low-tier equipment leaves a slot empty instead of granting elite or another race's gear. Re-equip/upgrade attempts that change neither loadout fail without spending gold or advancing the recorded tier. Existing saves are not automatically downgraded: this does not distinguish previously gifted gear from legitimately earned gear.

## Verification

From the repository root, run:

```powershell
python tools/test-taom-equipment.py <temporary-output-directory>
```

Requires Python and .NET 9. The runner extracts the production selection, adoption and equip-action code and links the production compatibility catalogue against engine stubs. The 24 checks cover starting tiers, missing gear, custom races, culture fallback, retained items, no-charge failures, re-equip, mounts, restrictions and campaign isolation. These are simulated checks, not actual game verification.

The BLTAdoptAHero project was also successfully built against the locally available Bannerlord assemblies. TAOM was not installed locally. Before shipping, test with the target TAOM version: adopt Easterling and Noldor heroes at Tier 1; check battle/civilian gear, race visuals and mounts; upgrade and re-equip; save/reload and repeat. Authored loadouts are compatibility evidence, not an engine-level guarantee that every item combination fits. Review missing low-tier coverage in TAOM data rather than weakening the tier or race checks.
