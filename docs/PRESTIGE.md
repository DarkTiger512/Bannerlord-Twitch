# Campaign prestige

Use `!prestige` to view progress and `!prestige perks` for the five permanent choices.
Use `!prestige choose might` (or resilience, vitality, fortune, insight), read the reset
preview, then `!prestige confirm might` within 60 seconds. Confirmation is destructive
and only works outside missions/encounters, with no tournament queue entry or auction.

The first prestige requires 500 new personal human battle defeats and 5,000,000 held
BLT gold. Each prestige adds 250 kills and 2,500,000 gold to the next requirements.
Knockouts count; mounts, friendly troops, practice, tournaments and retinue kills do
not. Installation does not credit historical kills. Death/replacement starts a new run.

Each prestige grants one chosen rank: +2% damage, 2% damage reduction, +5 maximum
battle HP, +2% battle gold, or +2% BLT XP. Each choice caps at 10 ranks; prestige caps
at 50. Perks and the `[P#]` name marker survive replacement heroes in the campaign,
but do not transfer into new campaigns. Perks apply to the hero, not retinues.

A reset removes progression, equipment, custom inventory, both retinues and achievement
unlocks/powers. It keeps appearance, age, class, personality, family, clan, titles,
property, relationships and lifetime stats. Defaults restart at level 1, attributes 2,
zero focus/unspent points, melee/ranged 50, riding/athletics 25, other skills 0,
tier-1 equipment and 50,000 BLT gold. The entire prior gold balance is replaced;
the eligibility cost is not subtracted in addition. Ordinary inheritance cannot restore
the cleared record. Existing campaign holdings continue producing their normal income.

Smaller-side voluntary joins can earn a locked [battle balance bonus](BATTLE-BALANCE.md). Both sides use identical rules; there is no permanent attacker advantage.

Configure **Chat Commands → prestige → Handler Config**. Aliases share the canonical
Prestige command settings, including campaign-wide perk calculations. Invalid settings block resets
and disable perk application. Disabling prestige retains saved progression for later
re-enablement. Battle balance is configured separately.

## Release validation

Run the BLTAdoptAHero console test project and build against the installed supported
Bannerlord assemblies. Before releasing, use a disposable campaign save to verify:

- Install on an old save: zero qualifying kills; save/reload preserves new progress.
- Land and naval battle personal kills/knockouts count once; excluded kills do not.
- Confirm a reset, inspect all skill XP/focus/perks, equipment and both retinues, then reload.
- Compare friendly/enemy result and kill payouts, including positive loss rewards/refunds.
- Verify all five perks, repeated spawn callbacks, death/re-adoption, name lookup and inheritance.
- Force a reset failure and verify exact engine/BLT state rollback, without awarding a rank.
- Verify existing clan/family/property/traits and lifetime statistics remain intact.
- On Twitch, verify choose/confirm, stale or missing snapshots, offline state and capped perks.

No live campaign smoke test is implied by automated build/test success. Do not deploy
until these campaign checks have been completed. Both branches retain their own config baseline.

Battle participation incentives now use the symmetric [battle balance joining bonus](BATTLE-BALANCE.md), replacing the permanent attacker multiplier.
