# Classic / TOAM 5.5.21 parity inventory

All tracked files must match after UTF-8 BOM/line-ending normalization except the exact paths in `tools/parity-exceptions.json`. Each exception has a reason. The verifier rejects unlisted differences and checks common paid-equipment commands, command defaults and release versions. Stale exceptions are rejected.

Shared: installed-design forge/reforge and migration; unequip and safe profile insertion; classless equipment and no-change/no-charge handling; passive diagnostics/watchdog; decision queue and optional naval patches; retinue readiness; Twitch startup; Immortal payout lifecycle; heir, refund, policy and overlay behavior.

Variant-specific: race-aware equipment/starting tiers, TOAM resource preload mitigation, Harmony providers/load order/development launch arguments, related tests and historical TOAM documentation. See the machine-readable inventory for every permitted file.

Obsolete: Classic native random-part construction/registration is removed. Legacy forge settings and old saved custom-item metadata remain readable; no asset replacement, profile generation bump, or retroactive reward repair occurs.

Unrelated editor changes, local prestige wording, experimental death workaround and Twitch Extension development are excluded. Existing main/taom history is preserved through separate descendant integration branches. Draft packages identify exact source revisions. Live campaign validation is pending separately for each variant.
