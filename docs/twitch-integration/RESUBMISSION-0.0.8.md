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
