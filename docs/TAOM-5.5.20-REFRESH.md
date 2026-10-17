# BLT TAOM 5.5.20 — Forging Compatibility Update

Updated 17 October 2026. This replaces the previous 5.5.20 download; no additional release entry was created.

## Forging that fits TAOM

Forging now selects an existing installed weapon design and gives your purchase its own BLT modifiers. It no longer assembles random crafting parts or creates new native weapon designs.

- **Balanced, Swift and Heavy stay available**, along with Standard, Fine and Masterwork quality.
- **Culture choices still work.** The forge prefers matching-culture weapons, then the closest configured weapon tier. Any culture fallback is reported.
- **Your weapon stays yours.** Naming, enchanting, equipping, trading and inheritance continue through the custom inventory.
- **Reforge keeps its current behavior:** change style, upgrade quality or refresh configured bonuses without stacking the previous forge contribution.
- Shared base weapon stats are never changed, so one viewer's upgrades do not affect another viewer's weapon.
- Obsolete crafting-part controls are hidden from the configuration editor. Existing prices, command names and enabled states are preserved.

## Also included

- Custom polearms check all usable weapon modes; two-handed-only pikes still require a matching class slot.
- Equip rejection messages show the weapon's actual modes.
- Long `!forge cultures` and `!forge types` lists appear in full in the overlay; short lists still reply in chat.
- The viewer command guide includes forge and reforge examples with configured prices and bonuses.

## Updating

Close Bannerlord and replace all four BLT module folders from the ZIP. Preserve your authentication and custom configuration files. The version remains **5.5.20**, so download the refreshed ZIP even if you already installed that version.

Existing saves and forge metadata remain supported. Previously generated weapons are **not automatically replaced or repaired**. This update removes native weapon generation from new purchases; it is not a confirmed fix for the earlier Minas Tirith freeze or missing TAOM assets.

Requires Bannerlord 1.4.8 and your normal TAOM dependencies. Harmony and War Sails binaries are not bundled.

## Validation

Forge integration checks cover existing-design selection, individual stat modifiers, reforge, persistence, purchase rollback and absence of native weapon-generation events. These use engine stubs; an in-game TAOM campaign test remains pending.
