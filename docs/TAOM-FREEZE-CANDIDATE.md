# TAOM 5.5.8 candidate 1

Corrects two concrete defects found while investigating the reproduced battle-entry stall. This is a fix candidate, not a confirmed resolution of that stall.

- Optional naval patrol patch now has HarmonyPrepare gating: an absent NavalDLC target skips the patch class rather than aborting the remaining PatchAll processing.
- MainThreadSync takes its owner thread from the actual application-tick queue drain. Module initialization and battle callbacks used different thread IDs in the submitted diagnostics.
- Async queued work uses TaskCompletionSource rather than a blocking ThreadPool wait. Exceptions fault the returned task; legacy queued actions always signal their completion handle. Failed queued actions are logged without abandoning subsequent queued work.
- Diagnostics remain enabled; queued main-thread actions now receive entry/exit markers too.

All four modules built. Five .NET Framework queue checks passed using the production MainThreadSync source. Installed Harmony 2.4.2 reproduces an empty-target failure and safely skips a representative guarded patch. The previously downloaded TAOM folder is no longer available at its supplied path, so this last test uses installed Bannerlord.Harmony, not the TAOM fork. All 66 equipment checks passed. Actual battle replay is not available locally.

Install the four module folders preserving local configuration/credentials/saves. Replay the same pre-battle save. If it still freezes, wait 20 seconds and send the run's logs from %LOCALAPPDATA%\BLTRefreshed\Diagnostics. No manual memory dump is required to try this candidate. See TAOM-FREEZE-DIAGNOSTICS.md for telemetry limitations. Classic remains Latest.
