# TAOM adoption and equipment

New `!adopt` heroes and heirs with a configured starting equipment tier now clear their template loadout and use that progression tier for both battle and civilian equipment selection. Tier 1 means internal item tier 0. A configured tier of 0 removes equipment; an unset tier retains the existing configuration behavior.

The old custom-race exception preserved elite template equipment and derived the viewer tier from it. The old re-equip block avoided incompatible skeletons and mounts but prevented all equipment progression for these heroes. Both exceptions are removed.

With TAOM loaded, fitted armour and mounts must appear in an authored troop or hero-template loadout for the hero's race. Ordinary weapons, shields and ammunition do not require troop-template evidence; missing evidence is not an incompatibility rule. Live hero inventories cannot expand this allowlist. Selection first prefers the requested culture (or the hero's culture) at the target tier, then within one tier above or below. Equally close tiers prefer the lower tier. If that culture has no candidate within one tier, selection tries other cultures within the same one-tier limit. All candidates must still pass race compatibility and the existing usability/restriction checks. Explicit exact-tier requests remain exact. Adoption still excludes above-target inherited/custom items from the retained pool; newly selected fallback items may be one tier above. The viewer's recorded progression tier stays at the configured value. Normal upgrades can preserve compatible higher-tier equipment already earned by the viewer.

Missing compatible equipment within one tier leaves a slot empty instead of granting elite or another race's gear. Re-equip/upgrade attempts that change neither loadout fail without spending gold or advancing the recorded tier. Existing saves are not automatically downgraded: this does not distinguish previously gifted gear from legitimately earned gear.

## Verification

From the repository root, run:

```powershell
python tools/test-taom-equipment.py <temporary-output-directory>
```

Requires Python and .NET 9. The runner extracts the production selection, adoption and equip-action code and links the production compatibility catalogue against engine stubs. The 66 checks cover starting tiers, missing gear, custom races, culture fallback, retained items, no-charge failures, re-equip, mounts, restrictions and campaign isolation. These are simulated checks, not actual game verification.

The BLTAdoptAHero project was also successfully built against the locally available Bannerlord assemblies. TAOM was not installed locally. Before shipping, test with the target TAOM version: adopt Easterling and Noldor heroes at Tier 1; check battle/civilian gear, race visuals and mounts; upgrade and re-equip; save/reload and repeat. Authored loadouts are compatibility evidence, not an engine-level guarantee that every item combination fits. Review missing low-tier coverage in TAOM data rather than weakening the tier or race checks.

## Last-resort class weapons

After the requested weapon type exhausts culture/nearby-tier fallbacks, an empty melee class slot can receive any compatible melee weapon at the exact progression tier. An empty ranged class slot can receive a bow, crossbow, sling or throwing weapon at that exact tier. Launchers require compatible same-tier ammunition and a free or unused-ammunition slot; existing weapons and ammunition needed by other launchers are preserved. Shields, ammunition requests and explicitly empty slots do not become weapons. Race, skill, mounted-use and restricted-item checks remain active. This does not guarantee a filled slot if no usable candidate exists.

## Armour fallback

Battle and civilian head, body, leg, hand and cape slots use the same culture/one-tier fallback sequence. Any compatible armour for that slot is already eligible across cultures, including exact-tier alternatives; no extra subtype relaxation is needed as for swords versus maces. A helmet cannot replace boots. Race, item restrictions and civilian suitability remain enforced. If no compatible candidate is available within the tier limit, the slot remains empty. Regression checks cover every armour slot in both loadouts.

## Preview 3 weapon compatibility correction

Ordinary handheld equipment now bypasses the troop-template race allowlist on all selection and retention paths. Skill, gender, mounted usability, tier and restricted-item checks still apply to item selection. Known creature markers in item IDs (troll, olog, giant, ent/ents and balrog) retain same-race template requirements. This marker guard is a heuristic, not exhaustive knowledge of every custom weapon; use RestrictedItemIds for additional exceptions. Fitted armour and mounts keep the existing conservative race rules. The Gondor/Dwarf Warrior regression supplies a mace, shield and throwing axes without any troop-template donors and verifies four filled weapon slots at recorded Tier 1. Actual TAOM testing remains pending.
