# BLT Classic 5.5.1 - configuration and campaign event fixes

Draft for Bannerlord 1.4.8. Awaiting in-game testing and owner approval; do not publish yet.

## Mandatory clean configuration

This release replaces older command/configuration YAML with a new clean default configuration. Existing settings profiles reset once when opened. This retinue test uses configuration generation 2 so the previous draft cannot retain its incorrect Infantry definition. Reapply your custom command names, costs, event settings, and other preferences after upgrading. Settings edited after the reset persist normally. Authentication credentials and campaign saves are not reset.

Install the new `Bannerlord-Twitch-v4.yaml` along with all four modules. Do not restore an older YAML over it.

## Changes

- Dedicated Events global-config tab for Stream Objectives, Cursed Artifact, Immortal Encounter, Priest's Crusade, and the random-events master switch.
- Prestige requirements, perks, and reset settings now belong to the Prestige chat command, with readable fields and shared settings for aliases.
- Removed the obsolete Respond in Extension option and routing.
- Added an admin-only SimGold handler (`!simgold <positive amount> [viewer]`), disabled by default, with authorization and overflow checks.
- Clean defaults include Enchant, Balance, Ammo, Prestige, and Predict. Predict now uses the correctly registered TournamentPrediction handler in both chat and New Command.
- Curse wins track actual mission participation and fighting side, including summoned heroes. Duplicate callbacks, losses and unresolved retreats do not count. Rewards support retry without duplication and report pending delivery.
- Immortal encounters require strictly more than 20% player health, regardless of configuration, and recheck before battle acceptance.

## Retinue class correction

- Corrected the shipped Infantry formation from Ranged to Infantry and enabled Hire By Hero Class for both retinue commands.
- Rebuild the loaded troop-tree index for new campaigns and loaded saves. Military trees added by overhauls are discovered without hardcoded troop IDs or a main-culture restriction; ordinary retinue uses explicit cultural recruitment roots, while secondary retinue retains its existing discovery rules.
- Prefer compatible paths in the adopted hero's culture, then compatible paths in other cultures, within the enabled troop categories. Never substitute an incompatible troop when no valid path exists.
- Keep every upgrade progressing toward a compatible terminal, including cyclic trees with an exit. Closed cycles are not valid destinations.
- Regression coverage exercises the actual command and campaign methods for archer → hire → clear → infantry → hire, both retinue systems, class conversion, culture fallback, changed rosters, and cycles.

## Test gate

See [the manual checklist](CLASSIC-5.5.1-TEST-CHECKLIST.md). Automated and compiled-configuration checks do not replace in-game testing. Use a disposable campaign save to test prestige resets and event rewards. Publication requires explicit approval after testing.

## Ordinary retinue recruitment boundary

Ordinary retinue now uses only explicitly configured cultural recruitment trees and their upgrades. Unassigned special trees are excluded from hiring, upgrading, and class conversion, including with class guidance disabled. Previously acquired ineligible units are replaced on the next retinue command or class change without a replacement charge or resetting paid upgrade counters. If no eligible replacement exists, they remain without upgrades and can still be cleared. Elite retinue access and pricing are unchanged. Configuration generation remains 2; this correction does not require another profile reset.

## Curse reward lookup correction

Completed curses resolve heroes through the campaign roster before the object registry. A missing hero lookup now leaves the earned reward pending instead of discarding five completed wins. The latest legacy completion that failed specifically because the hero could not be resolved is recovered on load or daily tick when that same hero is alive; genuine deaths are not recovered. Failure announcements now include the viewer and reason.
