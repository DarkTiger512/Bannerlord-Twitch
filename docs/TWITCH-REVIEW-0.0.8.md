# Twitch Extension 0.0.8

Twitch confirmed **In Review** on September 19, 2026. Version 0.0.8 is active as Overlay 1 on FNC_Chair. The submission requests contact to schedule a live Bannerlord review session. This is not an approved or publicly released version.

## Exact submitted source

- Branch: `BLT/twitch-integration`
- Source commit: `c5c29481d1bb523ff6205474b0fb6315f24e2570`
- Review baseline: `a44ffb3171207649e7ce716041573584ac9a38da`
- Module version: 5.4.0; Bannerlord version: 1.4.8; Twitch protocol: 1.

The submitted commit is preserved in the history of `BLT/twitch-integration`. Later integration commits combine the review fixes with newer development; check out the exact commit above to reproduce the submission. Neither current branch tip is an exact substitute for that snapshot. Prestige, battle-balance development and unfinished enchantment work were not folded into the review build.

## Included corrections

- Synchronous Twitch CDN Helper first in every packaged HTML entry.
- Public tournament command `!predict`, corrected wording and saved-setting migration.
- Authenticated hero-ID matching for the personal HUD, real names and complete roster; backward-compatible presentation for older viewers.
- Visible shell before campaign detection and campaign reconnect handling.
- Null stream-objective save fix, guarded connector shutdown and bounded request handling.
- Land-battle summon/attack dispatch with War Sails enabled. This shared correction is also present on Classic main.

## Artifact checksums (SHA-256)

| Artifact | SHA-256 |
| --- | --- |
| BLT-Twitch-0.0.8.zip | `8d78bec0e4722187c1e650329c32f47563ddb1427b5f21e564d82abd29d71fce` |
| BLT-Modules-for-Twitch-0.0.8.zip | `621a98690fbd7c59fecc522ea6a2155dd15f9cfe91b3ef443a01af0d56bacc3b` |
| BLT-Source-0.0.8.zip | `f255f502652d3eca097fb6272415ebcceecb1efe8daa7c361933a8fd53fd73a3` |

The frontend upload MD5 shown by Twitch was `40d6c891ea7a04679883812cdbe227aa`, matching the local ZIP. Archives and private backups remain in the ignored local `releases/resubmission` folder; they are not committed to Git.

## Verification and remaining checks

Final ZIP validation passed for four HTML entries and eight files, including Helper ordering, asset references, terminology and endpoint checks. Twitch-hosted configuration loaded the authenticated channel settings and corrected Predict metadata. Saving was reported working. The summon routing regression failed on the old code and passed on the correction for both sides and DLC states.

Full live gameplay rehearsal remains unconfirmed: actual summon/attack deployment, health/ammo, ownership and roster together, multiple viewers, death, reconnect/save-reload, and tournament rewards/refunds. Hosted configuration loading does not establish these gameplay results. Detailed validation and the submission record are retained under `docs/twitch-integration` on the review branch.

Do not replace or withdraw the submitted assets while review is underway. Keep the exact source available for the scheduled review session.
