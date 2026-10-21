# BLT TAOM 5.5.20 — Passive Diagnostics Update

Updated 21 October 2026. This replaces the previous 5.5.20 download; no additional release entry was created.

## Passive crash and freeze evidence

- Quiet diagnostics start automatically: no new popups, overlay messages or recovery attempts.
- An independent helper watches the game heartbeat and attempts two captures after a sustained stall, with at least 20 seconds between them. Full Windows process snapshots are preferred; a smaller stack dump is attempted if snapshots are unavailable.
- Reports identify active BLT command/queued operations, preload mesh/physics names, loading boundaries, shader/readiness sample ages, module hashes and matching binaries, exception chains and Harmony patch owners.
- Find incident files in `%LOCALAPPDATA%\BLTRefreshed\Diagnostics`. Send the affected session's `BLT-incident.zip` plus adjacent `capture*.dmp` files privately. No automatic upload occurs.
- Normal logs are bounded. Captures have disk headroom checks, a 32 GiB dump budget and a two-attempt limit per run. Capture overhead is possible; live campaign validation remains pending.
- Native crashes can end before a dump is taken; the helper still preserves available logs. These diagnostics improve evidence, not guarantee automatic culprit identification.
- Includes a BLT decision-initialization fix: war/expulsion constructors now finish normally; adopted-kingdom AI proposals are filtered when entering the queue. The previous constructor skip could leave incomplete ballots. This is not presented as a proven fix for the reported loading freeze or that specific vote crash.
- See `PASSIVE-DIAGNOSTICS.md` for capture details and privacy/storage information. Existing forging, unequip and siege preload changes remain included below.

## Siege loading guard restored

- Restores the TAOM 5.5.10 diagnostics build's guard against an unlimited mesh/physics preload wait during mission loading.
- A readiness wait that keeps reporting pending assets now times out after approximately 15 seconds and logs their names with `PRELOAD_TIMEOUT`. Ready assets log `PRELOAD_READY` and continue normally.
- Fixes optional Naval DLC patch initialization when its target method is unavailable, so that missing DLC does not interrupt initialization of the remaining patches.
- The guard bounds repeated polling; it cannot interrupt a native resource check that never returns. Continuing after timeout can leave missing visuals or physics. This mitigates the loading wait and does not repair missing assets.
- The reported attacking siege still needs an in-game replay to confirm the fix. The version remains **5.5.20**: download this refreshed ZIP even if you already installed 5.5.20.
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

Existing saves and forge metadata remain supported. Previously generated weapons are **not automatically replaced or repaired**. The preload guard is a mitigation for repeated resource-readiness waits; it is not a repair for missing TAOM assets or a confirmed fix for every siege freeze.

Requires Bannerlord 1.4.8 and your normal TAOM dependencies. Harmony and War Sails binaries are not bundled.

## Validation

Passive diagnostics passed 22 integration checks using production observer/helper code: unchanged exceptions with real Harmony, healthy/startup runs without captures, abrupt-exit evidence, capture/storage limits, two full process snapshots with memory/thread/module validation, capture spacing, target survival and ZIP integrity. The final callback update also passed 15 focused checks. Nine decision-queue regression checks pass. These use harmless synthetic processes and game-type stubs; live TAOM campaign validation remains pending.

Forge integration checks cover existing-design selection, individual stat modifiers, reforge, persistence, purchase rollback and absence of native weapon-generation events. These use engine stubs; an in-game TAOM campaign test remains pending.

Slot unequip validation covers each battle/civilian slot, rejected arguments, mission/prisoner guards, preserved class/tier/storage, and equip/reequip command dispatch after all slots are individually emptied. An additional 20 checks execute the current equipment generator and paid equip/reequip flow against a controlled engine fixture for classed and classless empty loadouts. Live campaign validation has not been performed.

Preload regression checks pass with Harmony 2.4.2: immediate/delayed readiness, the production 15-second deadline, unresolved asset names, calling-thread resource checks, preserved resource collections, original behavior outside missions, and no patch without TAOM. All four release modules compile against Bannerlord 1.4.8 reference assemblies. Live siege validation has not been performed.
