# Bannerlord Twitch 5.5.21 for Shokuho

This prerelease targets Bannerlord **1.2.12.77991** and Shokuho's standalone **Bannerlord.Harmony 2.3.6.220**. The source starts at Classic 5.5.21 (`63143d6`). The comparison against TOAM 5.5.21 (`55c7b39`) found no additional shared fixes. The upstream `16e7d10` port was used to identify older engine APIs.

## Shared features

The build retains follow, guard, both retinue rosters, classed and classless equipment, slot unequip, party and family management, heir creation, prestige, enchantment, objectives, installed-design forge and reforge, training refunds, Immortal payouts, passive diagnostics and nonblocking Twitch startup. Forge and reforge are opt-in. Follow, guard and automatic retinues wait for a loaded battle outside deployment.

## Compatibility differences

- Shokuho's naval battle system requires its own integration. War Sails ships, ship purchasing, ship-aware party routing, naval viewer spawning and port-only upgrades depend on APIs absent from 1.2.12. Use the BLT battle commands in land encounters during testing.
- Native 1.4 alliance and trade-agreement APIs and event logs are unavailable. BLT's own alliance and tribute treaties remain available. Automatic peace proposals use zero native daily tribute because 1.2.12 lacks the quote API. Explicit BLT tribute agreements retain their configured terms.
- Native sling equipment, sling ammunition and the duel mark's native damage bonus are unavailable. Installed designs still follow the configured weapon types, cultures and tiers supported by the game. Shokuho's installed item catalogue and its smithing coverage need live validation.
- The newer siege retreat patch depends on `MapEvent.RetreatingSide`, which is absent in 1.2.12. Reinforcement cleanup uses the older siege-completed event. Reinforcement parties use the existing militia fallback.
- Custom viewer widgets remain available. The newer native agent name-marker constructor hook is unavailable.
- TOAM race rules, asset fallbacks, preload hooks, siege timeout mitigation and dependency ordering remain TOAM-specific. This port uses Classic equipment selection.

## Disposable campaign checklist

1. Start Shokuho by itself on 1.2.12.77991 with its documented dependencies. Back up your campaign and BLT profile. Replace the four BLT module directories from the ZIP and load them after Shokuho.
2. With the game closed, install the converted profile as `Bannerlord-Twitch-v4-p1.yaml` in the game's Configs directory and select profile 1. Keep Twitch authentication in its separate file. Start the game twice and verify classes, achievements, powers, settings and command availability after saving the profile.
3. Connect Twitch and test adoption, chat replies and rewards. Test `!follow` and `!guard` during deployment and then after battle starts. Test both ordinary and secondary retinues in a land battle.
4. Test battle and civilian `!unequip`, classless recovery, an unchanged equipment purchase, a training refund, and equipped custom items after save/reload.
5. Enable forge/reforge in the settings. Test an installed Shokuho weapon design, a failed purchase, reforge bonus replacement and saved item ownership. Test an heir and an Immortal Encounter through payout and a second reload.
6. Test a siege and verify reinforcements, retreat behavior and post-battle campaign recovery. Test a Shokuho naval battle separately for basic campaign stability before trying any BLT commands there.
7. Record exact game, Harmony and Shokuho versions, the command and its result, and any error. Share logs with authentication tokens removed. Live qualification is pending.
