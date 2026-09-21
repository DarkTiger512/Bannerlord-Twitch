# Classic 5.5.1 test checklist

This draft requires a clean configuration. Every older settings profile is replaced with generation 2 defaults when opened. Reapply desired customizations. Authentication and campaign saves are not reset. Nothing should be published until in-game testing is approved.

## Configuration and commands

- [ ] Launch with an old profile: old custom commands/settings disappear and the new defaults load without missing-handler errors.
- [ ] Edit one setting, save, restart twice: the edit remains. Open another old profile: that profile resets once as well.
- [ ] Global Configs has an Events tab with Stream Objectives, Random Events, Cursed Artifact, Immortal Encounter, and Priest's Crusade.
- [ ] Prestige appears under Chat Commands, with readable requirements/perks/reset fields. It no longer appears in Common Config. A Prestige alias shares the same settings.
- [ ] Neither commands nor rewards offer Respond in Extension. Twitch/overlay responses work through their selected destinations.
- [ ] Enchant, Balance, Ammo, Prestige and Predict are enabled in the default command list. Their help text and handlers work.
- [ ] New Command -> Predict creates a predict command using TournamentPrediction.
- [ ] Enable SimGold for testing: broadcaster/moderator `!simgold 100` adds 100 gold to their hero; `!simgold 100 viewer` targets that adopted hero.
- [ ] Ordinary viewers cannot grant gold, even if Moderator Only is unchecked. Zero, negative, malformed, overflowing amounts and nonexistent heroes change no balances. Disable SimGold again after testing.

## Cursed Artifact

Use a disposable campaign save. For faster triggering, temporarily set the Events daily curse chance to 100 and cooldown to 0. Keep Required Battle Wins at 5. Restore event defaults after testing.

- [ ] Identify the cursed hero from the overlay announcement. Summon them into five qualifying winning campaign battles. Confirm progress 1/5 through 5/5.
- [ ] Save/reload between victories: progress remains. Test normal party participation as well as summoned participation.
- [ ] Knockout on the winning side still counts. Losing, retreating, tournaments, training and auto-resolve do not count. An absent cursed hero gains no credit.
- [ ] At five wins, penalties stop and exactly one Cursed Legacy is delivered, with an overlay cleanse announcement. Having other custom weapons does not block it.
- [ ] Save/reload after completion: no duplicate reward or additional win. If delivery fails, a pending notification appears and a later campaign day/load retries it.

- [ ] Load a save containing the erroneous 5/5 lookup failure while the same cursed hero is alive. Confirm recovery and one weapon reward without replaying five battles; save/reload again and confirm no duplicate.
- [ ] Complete a new curse on a dynamically created adopted hero: the fifth win delivers the reward. A temporarily missing hero remains pending; a confirmed death reports the viewer and reason.

## Immortal and other events

- [ ] With daily chance 100, the Immortal does not trigger below or exactly at 20% player HP. The manual `blt.trigger_immortal_event` command also refuses.
- [ ] Above 20% HP and otherwise eligible, the encounter starts. Change configured chances/level requirements: none bypasses the health gate.
- [ ] If HP drops to 20% or below before acceptance, the encounter closes safely, the temporary enemy disappears, and no gold is awarded.
- [ ] Load a save with an outstanding encounter: existing safe-abort behavior leaves no stuck conversation.
- [ ] Run a Stream Objective and trigger a Priest's Crusade to verify both read the new Events settings.

## Retinue class regression

- [ ] With enough BLT gold: `!class archer`, `!retinue all`, `!retinue clear all`, `!class infantry`, `!retinue all`. The second retinue must finish as infantry, not archers.
- [ ] Switch class without clearing: existing class-guided retinue converts to a compatible troop of the closest available tier in the preferred culture.
- [ ] Repeat with `!eliteretinue` (secondary retinue).
- [ ] Test a class whose allowed paths do not exist in the viewer's culture: recruitment comes from another culture and every upgrade remains on a compatible path.
- [ ] With an overhaul enabled, start/load a campaign and repeat. Check the log for the loaded troop index and selected terminal paths. No vanilla troop IDs should be assumed.
- [ ] Save/reload and repeat the class switch: the index is rebuilt and the selected class remains current.

## Ordinary retinue recruitment boundary

- [ ] Ordinary retinue never recruits unassigned special trees, even when those trees share the hero culture or start at tier 4-5. Only enabled cultural recruitment roots and their upgrades are eligible.
- [ ] Disable a troop category and change class: conversion must not pick units from the excluded category. Repeat with class guidance disabled.
- [ ] Load a save containing special ordinary-retinue troops. On the next retinue command or class change, they become eligible troops without charging for the replacement or resetting paid upgrade counters. Save/reload and confirm the replacements persist.
- [ ] With no eligible replacement, the unit stays in its slot without upgrading; the reply explains why. Clearing the slot still works.
- [ ] Elite retinue retains its configured troop access and costs. Test an elite purchase without changing the ordinary retinue.
- [ ] Prayer/unlock mechanics attached to an otherwise normal cultural upgrade path are outside this restriction; confirm overhaul-specific behavior separately.

## Existing gameplay regression pass

- [ ] Retinue purchase and upgrades follow the selected class; changing class converts class-guided retinues correctly. Repeat for elite retinue.
- [ ] Enchant a compatible custom weapon through +5, including insufficient funds and failure outcomes; save/reload retains the result.
- [ ] Test summon/attack on land and, if available, a naval battle; confirm normal rewards and battle-balance reporting.
- [ ] Verify prestige status/preview/confirmation on a disposable hero and retained prestige after save/reload.
- [ ] Run a tournament prediction and resolve/refund the pool normally.
- [ ] Try markup-like item/clan names and inspect overlay rendering; no executable markup or broken UI.
- [ ] Play several battles, enter/leave settlements, and save/reload without crashes. Record failures with the command/event, expected result, actual result, and relevant log excerpt.
