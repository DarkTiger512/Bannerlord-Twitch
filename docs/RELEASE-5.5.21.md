# BLT 5.5.21 — Classic and TOAM draft baseline

Both variants share installed-design forging, slot-specific unequip, classless equipment purchases, no-change/no-charge protection, passive diagnostics, safe kingdom decision filtering, deferred automatic retinues, optional naval integration, and reliable Immortal victory payouts.

Classic now selects installed weapon designs instead of generating native weapon designs. Individual modifiers preserve ownership and bonuses without altering shared base items. Existing forged items and metadata remain readable; old assets are not replaced or repaired. Forge and reforge stay disabled in new defaults until enabled by the streamer. Existing prices, aliases and enabled states remain authoritative.

Missing unequip commands are added on profile load without resetting configuration generation. Existing matching handlers, names or IDs prevent insertion; renamed and disabled commands are preserved. Use `!unequip [civilian] <slot>`; ordinary equipment is not stored/refunded, and custom items retain exact modifiers. Missions and imprisonment block unequip.

Classic requires standalone Bannerlord.Harmony. TOAM retains TAOM.Dependencies, custom-race equipment handling and the conditional siege preload guard. Install only one variant at a time. War Sails is optional. Neither package bundles Harmony or NavalDLC binaries. Cross-variant campaign compatibility is unverified.

Close Bannerlord, back up saves and configuration, and replace the four BLT module folders with one package. Preserve authentication and custom profiles. New commands and diagnostics do not require a profile reset. Keep the prior package and save backup for rollback; use an untouched save if reverting campaign changes.

Diagnostics write locally to `%LOCALAPPDATA%/BLTRefreshed/Diagnostics`; no automatic upload occurs. Full dumps can contain private process data. See PASSIVE-DIAGNOSTICS.md.

These are DRAFT packages. Automated checks do not establish campaign stability. Classic and TOAM live qualification are both PENDING; see CAMPAIGN-5.5.21-CHECKLIST.md.
