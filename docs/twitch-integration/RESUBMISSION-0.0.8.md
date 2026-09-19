# Validation — Twitch 0.0.8 candidate

Status: implemented and installed; **not ready for resubmission until rehearsal and Twitch Hosted Test pass**. The user deferred the real Bannerlord rehearsal. No Twitch upload, version creation, resubmission, or reviewer correspondence was performed.

## Build and baseline

Based on reviewed commit `a44ffb3171207649e7ce716041573584ac9a38da`, plus the deployed campaign/reconnect and personal-HUD/roster fixes. Source is preserved on `codex/twitch-resubmission-008` in the `source` worktree. Unrelated main/enchantment changes were excluded.

The Twitch console showed 0.0.7 in Pending Action and no 0.0.8. The ZIP is named 0.0.8. The matching modules retain module version 5.4.0 for Bannerlord 1.4.8; `review-metadata.json` identifies this candidate precisely.

## Completed checks

- Frontend TypeScript/Vite production build passed. 18 frontend tests passed.
- Backend: 20 tests passed, covering legacy HUD projection, native pass-through, monotonic replay revisions, identifier migration, and preference preservation.
- Module Release build passed for BannerlordTwitch, BLTAdoptAHero, BLTBuffet, and BLTConfigure. Existing engine-independent module policy tests passed. The build retains existing missing-reference warnings for two optional TaleWorlds assemblies.
- Packaging checks: four HTML entries, Helper first and synchronous; HTML-referenced assets exist; no old tournament terminology or development endpoints in upload assets. A separate five-test packaging regression suite covers misordered/missing/asynchronous Helpers, forgotten HTML entries, missing assets, old wording, and development endpoints.
- The actual frontend ZIP was extracted and checked. Its extracted production assets were rendered at a simulated HTTPS hosted origin in Chromium. Real Twitch authorization and a real game were replaced with controlled fixtures for this browser test.
- Browser assertions: visible shell before campaign, no initial demo campaign/hero, hero-ID ownership, real hero name, missing ownership does not guess, solo/full roster counts, health/ammo updates, death/removal, and no browser errors. Screenshot: `packaged-native-hud.png` in the Codex visualization folder.
- Live VPS test: authenticated old and new viewer WebSockets simultaneously on an isolated synthetic channel; legacy synthetic HUD only for old connections; native real combatants only for `hud=hero-id-v1`; solo and multiple participants; missing ownership; canonical runtime command/configuration metadata; saved disabled state and custom amount preserved.
- Live Predict test: discovery, disabled legacy/canonical command rejection, authenticated command routing and private result delivery. The game-side result was simulated; this is not a claim that a real tournament reward/refund was executed.
- Live configuration persistence: initial save, subsequent save, and rejection of stale revisions. Corrected an existing SQL condition that blocked all nonzero-revision updates and prevented migration of existing profiles.
- Reconnect: source revision reset accepted with monotonically increasing viewer revision; authenticated subscriptions replayed to a restarted game. New frontend clears old ownership on disconnect.
- Installed modules were hash-verified. The saved profile changed only Name, Handler, Help, and Documentation (plus UTF-8 BOM removal); enabled flags, custom values, and all other settings remained intact. Previous modules and full configuration are backed up locally.

## Terminology and compatibility

The public manifest is generated from corrected source and uses `command.predict` / `TournamentPrediction`. Runtime metadata and persisted command preferences are normalized, including stale saved Name/Help/Documentation settings. Internal legacy aliases and internal game-gold field names remain for compatibility; they are not advertised in new upload assets.

Updated game localization entries explicitly use corrected English fallback text across existing locales. They do not claim newly reviewed native-language translations. Gold mechanics were retained: commitments are deducted, the shared pool is distributed to winners, and single-team predictions are refunded. Real-game reward/refund execution remains part of the rehearsal gate.

Protocol remains version 1 and authentication remains Twitch JWT based. The frontend uses Twitch Helper's linked viewer ID; HUD ownership uses the private game `viewer.state.heroId`, not display names.

## Required remaining gates

1. Real Bannerlord rehearsal: startup, load campaign, summon/attack, own hero plus multiple viewers, HUD and roster names/counts together, health/ammo/stats, death, leaving battle, reconnect, and campaign save/reload. Exercise Predict with valid/invalid inputs, insufficient gold, winning/losing outcomes, single-team refund, and tournament cancellation/refund. Watch for startup errors, including the previously observed optional naval patch issue; this package does not claim to fix that unrelated patch.
2. Explicit authorization to upload 0.0.8, then Twitch Hosted Test of viewer, configuration, and live-configuration entries with actual Helper authorization/CSP. Inspect network/console and test both a linked viewer and broadcaster.
3. Only after those checks, explicitly authorize resubmission and any reviewer correspondence.

The reviewer note is a draft. Approval is not guaranteed by the terminology change.
## Save-crash regression corrected — 2026-09-19

The real rehearsal failed on save. Inspection found that the reviewed baseline retained the known null stream-objective bug: SyncData dereferenced `active` when there was no active objective. The previously committed fix `03dba015b8e0625b661c8d0d5c4450d905295af2` was backported onto the candidate branch (`de1790c`). The game watchdog recorded a crash, but the crash dump/report was cancelled, so this diagnosis is based on the confirmed known defect in the installed binary's matching source, not a new stack trace.

The corrected module built successfully. The policy suite, including null-objective and legacy-collection preservation regression assertions, passed using the installed .NET 9 SDK for the isolated test harness. The rebuilt BLTAdoptAHero DLL was installed and hash-verified:
`1f9d6c9d8465f821c0e67ee402ca5c3e6342bca0af4053e2735d374cad8285f5`.

The replaced DLL is in `save-fix-20260919/before-BLTAdoptAHero.dll`; it contains the save bug and is retained only for diagnosis. Saved campaigns and configuration were not changed by this correction. The module and source ZIPs/checksums were refreshed; the frontend ZIP and backend were not changed. Real save/reload retesting is still required before declaring the rehearsal passed.

## Request/shutdown investigation — 2026-09-19

Saving was confirmed working by the user. The next rehearsal found summon/attack requests stuck at “Waiting for Bannerlord…” with no hero deployment, and a managed exception on exit. The backend accepted the requests. The game crash report was cancelled, leaving no new managed stack trace.

The connector now starts the request deadline before name lookup or main-thread queuing, bounds Twitch name lookup to five seconds, explicitly terminates stale requests, and prevents execution after a request expires. Request receipt, execution and completion are traced without credentials. Queued integration callbacks check connector lifetime before accessing the game. Cancellation tokens are captured before disposal, and request completion/expiry/disposal are synchronized.

The new BLTIntegration.Tests harness compiles the real connector/request-lifecycle source with game-service stubs. It passed wire-format action/command dispatch, early request tracking, stale request termination, post-disposal queued callback suppression, late replies, and 100 concurrent completion/expiry/disposal races. Release module build passed. The installed BannerlordTwitch.dll hash is `2d8f8b4f4e1fbc14dc0fc27881e20cc0774694f9dceaa6b2145956ebe0b55414`; the previous DLL is backed up in `integration-fix-20260919`.

These checks do not establish the original cause of stalled in-game execution or prove the exit crash eliminated. A traced real-game retest is in progress. No frontend or backend deployment was required for these changes, and the save fix remains installed.

## War Sails land-battle summon correction — 2026-09-19

The traced retest reached SummonHero after successful adoption but never completed its callback. War Sails was enabled. The handler selected its naval branch based on DLC presence, then silently did nothing when the mission was a land battle. Both Summon and Attack use this handler. The condition now requires both the DLC and an actual naval battle, allowing land battles and locations to reach their existing spawn paths. Missing mission/location contexts are also null-guarded.

The regression harness compiles the actual dispatch method with engine stubs. The previous source failed for DLC enabled + land battle; the corrected source passed DLC on/off, both player/enemy sides, land/location/naval routing, mission-tick deferral, exactly one completion callback, no-mission rejection, and missing campaign location. This validates routing, not engine spawning. The existing module policy suite and Release build also passed. Logs are in `summon-fix-20260919`.

The installed and packaged BLTAdoptAHero.dll SHA-256 is `f71296d476831f4f6fbad47b1d8a58184ddcdb7f683c29d445855e95f0d771db`. The previous DLL and archives are backed up in `summon-fix-20260919`. The save and connector fixes remain included. No Twitch frontend upload or backend deployment was needed.

Saving was reported working. The latest game log shows successful return to the menu, campaign reload, and complete managed cleanup on exit, without the earlier managed exit exception. This is evidence from one retest, not a guarantee against every exit crash. Real summon/attack deployment and HUD/roster verification remain pending with the corrected module; investigate response display further if it still fails to complete.

## Submission status

Version 0.0.8 was submitted on September 19, 2026. Twitch confirmed In Review. See [submission record](SUBMISSION-20260919.md). The submitted source is c5c29481d1bb523ff6205474b0fb6315f24e2570; subsequent documentation commits do not change the uploaded ZIP. Full gameplay checks are not claimed complete.
