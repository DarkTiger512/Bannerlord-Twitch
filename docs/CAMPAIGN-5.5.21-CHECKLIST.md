# 5.5.21 disposable-campaign qualification

Record separate Classic and TOAM results with package SHA-256, source revision, game/mod versions, pass/fail, and relevant logs. Both variants are PENDING. Never overwrite the original campaign or profile backup.

- Load an existing current-generation profile: commands, costs, aliases, enabled states and authentication survive. Missing unequip appears once; renamed/disabled/colliding commands are retained. Restart twice.
- Load an existing save with older crafted weapons and custom modifiers. Inspect stats and ownership, equip, rename, enchant, trade and inherit representative items; save/reload without loss or duplication. No automatic asset repair is expected.
- Enable forge/reforge for testing. Forge installed melee and ranged designs by culture, style and quality. Reforge replaces bonuses rather than stacking; insufficient funds and invalid choices change nothing. Another hero using the same base weapon is unaffected.
- Unequip each battle and civilian slot independently, including mount/harness and custom items missing from storage. Confirm exact custom modifiers, unchanged class/tier, no generated replacements and no duplicate storage entries. Invalid arguments, mission and prisoner requests fail safely.
- Equip/reequip classed and classless heroes from empty loadouts. Confirm normal cost/progression once on success and no charge/tier advance when no loadout changes. Verify Classic culture/tier policy; verify TOAM race fit, mounts and sparse asset fallbacks separately.
- Create/adopt an adult heir; check parentage, reservations, inheritance and save/reload. Cancel full/partial training deposits; only unused spending is reversed.
- Enter land battles and sieges with automatic and voluntary retinues, both retinue types, formation/class changes and repeated summons. Check rewards and cleanup. TOAM: replay a representative siege-loading scenario and inspect preload logs.
- Resolve adopted-kingdom war/expulsion proposals without incomplete ballots. Win and lose Immortal encounters, including summoned/ordinary participants and knockout; verify eligible winners are paid exactly once after teardown. Save/reload pending rewards and verify no duplicate payment.
- Launch without War Sails; check patch initialization and Twitch startup. If available, also test naval summoning/reinforcement cleanup with the DLC enabled. Simulate authorization failure and confirm loading is not blocked by a modal dialog.
- Verify healthy play produces local diagnostics without popup/capture spam; inspect session files. Synthetic automated stalls validate dump capture separately; do not intentionally freeze a real campaign.
- Regression pass: prestige, balance, objectives, map overlay, ammo, enchantment, tournaments/predictions, curse rewards and settlement entry/exit; play several battles and save/reload.

Stable-release gate: all required scenarios pass on both variants; record unavailable optional-DLC scenarios separately. Investigate failures before marking either package stable. Cross-variant save switching is not certified by this checklist.
