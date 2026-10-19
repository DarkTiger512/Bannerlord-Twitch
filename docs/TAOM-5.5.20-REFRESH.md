# BLT TAOM 5.5.20 — Slot Unequip Update



Updated 19 October 2026. This replaces the previous 5.5.20 download; no additional release entry was created.



## Slot-specific unequip

- Use `!unequip mount` to remove only your battle mount, or `!unequip harness` for its harness.
- Other supported slots: `weapon1`–`weapon4`, `head`, `body`, `legs`, `hands`, and `cape`.
- Use `!unequip civilian <slot>` to select civilian equipment instead. Other slots remain unchanged.
- A slot is required. Bare `!unequip` and `!unequip all` do not remove anything.
- Equipped custom items are explicitly returned to custom inventory before their slot is cleared, including legacy items missing from storage. Their exact modifiers are retained and existing inventory entries are not duplicated.
- Unequipping does not refresh equipment or generate a replacement horse. Class, purchased equipment tier, and custom-item storage are preserved. Ordinary equipment is not stored or refunded.
- `!equip` and `!reequip` support empty loadouts and classless heroes; their normal tier progression and gold costs still apply.
- Unequip is unavailable during missions or captivity.
- Existing saved profiles: add an enabled command named `unequip` with handler `UnequipHero` in BLT Configure. New/default profiles already include it. No profile reset is required.

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


Slot unequip validation covers each battle/civilian slot, rejected arguments, mission/prisoner guards, preserved class/tier/storage, and equip/reequip command dispatch after all slots are individually emptied. An additional 20 checks execute the current equipment generator and paid equip/reequip flow against a controlled engine fixture for classed and classless empty loadouts. Live campaign validation has not been performed.
