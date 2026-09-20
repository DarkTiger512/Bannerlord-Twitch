# Classic and Twitch Extension draft releases

Both variants support campaign prestige, permanent perks, and voluntary smaller-side joining bonuses. The prior paired baseline has 63 commands, including `!prestige`, `!balance` and `!battle`. Main now adds `!enchant` as command 64; this main-only feature has not been ported to integration. Shared release parity remains blocked until that port is explicitly requested and verified. See [Enchantment](ENCHANTMENT.md).

| Download variant | Source branch | Streamer setup | Viewer interface |
|---|---|---|---|
| **Classic** | `main` | Existing BLT chat/overlay configuration | Chat commands and existing overlays; no extension pairing required |
| **Twitch Extension** | `BLT/twitch-integration` | BLT configuration paired with the managed service, plus the Twitch extension | Chat commands plus extension panels, prestige choices and balance offers |

Each mod archive contains `BannerlordTwitch`, `BLTAdoptAHero`, `BLTBuffet`, and `BLTConfigure` at its root. Extract those four folders into Bannerlord's `Modules` directory. Keep the existing Harmony dependency and load order. Read the included metadata for the supported Bannerlord version and source commit.

Install **one variant at a time**: the module IDs deliberately remain unchanged, so they cannot be installed side by side. Back up saves and local configuration before upgrading or switching. Cross-variant save compatibility has not been verified. Keep each variant's configuration rather than copying the other variant's default YAML over it. Existing local auth/settings are not included in these draft downloads; Classic uses the normal BLT configuration/auth flow, and Extension uses pairing.

The Twitch frontend ZIP is a separate artifact for a future Twitch asset upload. It is not a Bannerlord module and is not deployed to the backend VPS. No upload or publication is part of draft preparation.

## Gameplay defaults and notes

Prestige and battle balance ship enabled on both variants. The default smaller-side bonus is capped at 20%, with a denominator floor of four viewers. Only the first successful voluntary join earns a locked bonus. Automatic participants count but earn no joining bonus. Both branches retain disabled troop-strength difficulty scaling and otherwise keep their own configuration values.

See [Prestige](PRESTIGE.md) and [Battle balance](BATTLE-BALANCE.md) for requirements, resets and reward exclusions. Draft packages include their exact source commit, unchanged module version, game version, variant and manual-test status in `release-metadata.json`. A filename is not a new published version: these are unpromoted drafts.

## Repeatable local preparation

Run from a Windows PowerShell 7 terminal with Bannerlord 1.4.8, Visual Studio MSBuild/.NET Framework 4.8 tools, .NET SDK 9, Node/npm, Chrome, and official 7-Zip installed. Restore the legacy solution's NuGet packages and run `npm ci` in the integration frontend before packaging. The script reuses these dependency caches; it verifies the frontend lockfile matches the selected committed ref.

```powershell
pwsh -File tools/package-draft-releases.ps1 `
  -ClassicRef main -IntegrationRef BLT/twitch-integration `
  -SevenZipPath 'C:\Program Files\7-Zip\7z.exe' `
  -ManagedServiceUrl 'https://bltrefreshed.evepirate.nl'
```

Use `-MSBuildPath`, `-GameDirectory`, `-NuGetPackagesPath` and `-NodeModulesPath` for alternate installations. `-PolicyTestFramework net9.0` builds the engine-independent tests using the available SDK without editing their committed target; use `net8.0` when its targeting pack is installed. `-BrowserPort` selects an unused local test port. The default output is a fresh ignored `releases/draft-<timestamp>` directory; `-OutputDirectory` can select another new directory.

The script resolves refs once, exports committed snapshots with `git archive`, and builds into separate folders. It never runs Clean/Rebuild targets, never writes into the installed game, and sets `DeployToGame=false` and `CreatePackage=false`. It excludes auth YAML, credential files, local environment files and debug symbols. The temporary uneven preview and other uncommitted edits are not exported. Existing output directories are rejected rather than overwritten.

Outputs: two variant-named `.7z` archives, one production frontend ZIP, `SHA256SUMS.txt`, `release-manifest.json`, `RELEASE-NOTES.md`, `VALIDATION.md`, and per-step logs. The short temporary workspace recorded in `build-workspace.txt` holds isolated source/staging/extraction directories for investigation and avoids legacy Windows path limits; do not distribute it. A failed run writes an incomplete report; do not distribute its artifacts.

## Paired maintenance and release gate

1. Implement shared gameplay changes on main, then port the relevant commits to integration. Keep interface/service adapters separate. Preserve branch-specific configuration values.
2. Run `node tools/verify-release-parity.mjs --classic-ref main --integration-ref BLT/twitch-integration`. It compares reset/policy/ledger code, rewards, campaign persistence and commands, while allowing reply routing, diagnostics, presentation and configuration differences. Update the explicit shared-file list when extracting new shared logic; do not broadly whitelist drift.
3. Build and test both complete mod variants before a shared gameplay release. Run integration backend/frontend tests and prestige/balance browser scenarios. Interface-only fixes may release independently.
4. Inspect extracted archives, module IDs/versions, production URLs, configuration defaults and credential exclusions. Check metadata and SHA-256 hashes.
5. Before publication, run disposable-campaign smoke tests on **both**: prestige preview/confirm/reset, save/reload, death and replacement, automatic entrants, failed/duplicate joins, resummons, positive loss payouts and personal/retinue reward separation on land and sea. Complete main's flow using chat alone and integration's flow using the extension. Verify actual gold/XP matches replies and bonuses apply once.
6. Record results in the validation report. Keep drafts unreleased while campaign checks are pending. Publish paired notes identifying tested game versions and known limits only in a separate authorized release task.

No branch pushes, GitHub publication, Twitch uploads, VPS updates or game deployment occur during this preparation workflow.
