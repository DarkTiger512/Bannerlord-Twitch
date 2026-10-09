# TOAM armour fallback regression checks

Run `./run.ps1` with the .NET 9 SDK. The runner extracts the current production equipment methods and compiles them with lightweight engine stubs; weapon physics are outside this fixture.

Covers every armour slot: empty race catalogue, forced culture, exact Tier 6 cross-race fallback, subsequent normal re-equip, distant tiers, old battle/civilian armour retention when assets disappear, configured restrictions, compatible-search precedence, starting tier caps and unchanged creature weapon restrictions.

This verifies policy and selection, not in-game mesh/skeleton rendering. Cross-race armour is intentionally permitted as the last resort. If neither allowed installed armour nor previous armour exists, no item can be supplied.

Saddle checks cover horse and camel families, cross-race/culture fallback, distant tiers, repeated re-equip, modifier preservation, restricted items and switching mount families. A saddle must match the selected mount's family; an incompatible old saddle is not retained.
