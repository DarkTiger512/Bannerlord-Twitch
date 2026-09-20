# Weapon enchantment (Classic/main)

`!enchant` reforges an owned custom weapon using the viewer hero's gold. Choose damage, weapon speed, or projectile speed (bows, crossbows, and throwing weapons only). Ordinary weapons, ammunition, shields, armor, and mounts are excluded.

## Viewer commands

- `!enchant`: list eligible weapons with their existing custom inventory numbers and enchantment levels. Numbers can have gaps when other custom items occupy inventory positions.
- `!enchant 2` or `!enchant #2`: free preview of available gains, next price, and risks.
- `!enchant 2 damage`, `!enchant 2 speed`, or `!enchant 2 missilespeed`: purchase one attempt.

Each success adds one level, to a maximum of +5. Default gains are +5 damage, +2 weapon speed, or +5 projectile speed. These are modifier points, not percentages. Each failure removes the most recent successful enchantment, including its original stat and exact amount, and still costs gold. Original custom bonuses are protected. At +0 the next attempt is always guaranteed, including after a downgrade.

| Target level | Gold cost | Failure chance |
|---|---:|---:|
| +1 | 100,000 | 0% |
| +2 | 200,000 | 15% |
| +3 | 300,000 | 20% |
| +4 | 400,000 | 25% |
| +5 | 500,000 | 30% |

The price depends on the next target level, not lifetime attempts. Renaming does not reset enchantments; trading or inheriting transfers the history with the item. Equipping the same custom item in multiple slots shares the enchantment. The saved name is unchanged; the command shows the level separately.

Purchases are unavailable during missions, while imprisoned, and while the selected item is being auctioned. Invalid requests, insufficient funds, maximum level, and application errors do not charge gold. Previews and lists are free and available during missions.

## Streamer setup

New configurations include the enabled `enchant` command. For an existing installation, open BLT configuration, add a command named `enchant`, select the **Enchant Weapon** handler, enable it for viewers, and enable Twitch replies. Save the configuration. Do not replace your existing YAML or auth settings. Alternatively, copy only the `Handler: EnchantWeapon` command entry from the shipped `Bannerlord-Twitch-v4.yaml` into your existing `Commands` list, ensuring it is not already present.

The handler configuration exposes `DamageGain`, `SpeedGain`, `MissileSpeedGain`, `Level1GoldCost` through `Level5GoldCost`, and `Level2FailurePercent` through `Level5FailurePercent`. Gains must be positive integers, costs nonnegative integers, and failure percentages between 0 and 100. The five-level ceiling and guaranteed +1 cannot be changed. Generated command documentation and previews use the configured amounts. Changes affect future attempts; downgrades always reverse the actual saved gain.

Existing custom items without enchantment history start at +0 with their current bonuses intact. The existing custom modifier save mechanism stores the new ordered history. No save wipe is required.

## Validation

Automated policy coverage includes parsing, configuration validation, fixed gains, guaranteed +1, failure boundaries, recovery from +0, historical downgrade amounts after configuration changes, maximum level, funds/restrictions, JSON history round-tripping, and transaction rollback. Build against the 1.4.8 references. The local test runner can use the installed .NET 9 SDK via `/p:TargetFramework=net9.0`; this does not change the mod's .NET Framework 4.8 target.

In-game acceptance remains required in a disposable Bannerlord 1.4.8 campaign:

1. Load a pre-feature save and confirm existing custom bonuses and names are unchanged at +0. List weapons alongside unsupported items and verify inventory numbers match other commands.
2. Buy +1, inspect actual weapon stats and gold/spending totals, then save/reload. Test melee, bows, crossbows, and throwing weapons; reject projectile speed on melee and reject ammunition/shields/ordinary weapons.
3. Set +2 failure to 100%, choose a different stat, and confirm the exact previous upgrade is removed and gold is spent once. Return to +0 and verify guaranteed success. Change gains between attempts and verify historical reversal.
4. Set failure chances to 0%, reach +5 with mixed stats, and verify further attempts cost nothing. Test insufficient funds, mission, prisoner, auction, malformed arguments, and free previews.
5. Rename, equip in multiple matching slots, transfer, and inherit an enchanted item; verify the history and actual stats survive save/reload without duplication.

This feature is main-only. Integration command parity is expected to fail until a separately requested port; the parity gate remains strict. No game deployment, integration changes, or publication is included.
