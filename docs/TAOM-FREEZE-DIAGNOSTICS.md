# TAOM 5.5.7 diagnostic build 1

This build adds diagnostics to Preview 3 without changing gameplay or attempting speculative freeze fixes. It does not fix the known optional naval-patch startup error. It records actual loaded TAOM/BLT/Harmony assembly identities at startup rather than assuming the streamer uses an older version.

## Streamer instructions

1. Close the game. Back up saves and BLT settings. Install the four module folders from the archive over the existing BLT installation, preserving local configuration/authentication files. Keep the normal TAOM dependencies and load order.
2. Launch and replay the battle that froze, preferably from the save immediately before it.
3. If it freezes, leave it running for at least 20 seconds before closing it, so the separate diagnostic worker can record the stall.
4. Press Win+R and open `%LOCALAPPDATA%\BLTRefreshed\Diagnostics`. Send every `freeze-...log` file for that run, plus the corresponding rgl/watchdog logs. No automatic uploads occur.
5. If possible, use Task Manager > Details > the game process > Create memory dump file while frozen. Dumps can contain private data, so share privately with a trusted developer rather than posting publicly. The diagnostic logs alone may not identify a native engine deadlock.

## Recorded evidence

UTC timestamps and process ID; application-tick heartbeat age; last mission mode/state/loading flag captured on the main thread; process working set/private bytes/managed heap; process CPU normalized across logical processors; system physical RAM load and available RAM; Windows commit limit and availability. Low free RAM alone does not establish causation. Thread entry/exit IDs cover BLT agent creation/build callbacks, explicit retinue SpawnTroop calls, retinue spawn groups, achievement application, reward assignment and mission behavior initialization.

The writer runs on a timer thread and never reads game/mission objects. Missing application ticks for 10 seconds produces `stalled=True`, with currently active instrumented operations. Loading, pauses, debugging and OS scheduling can also delay ticks: the label is a diagnostic signal, not proof of deadlock. Completed EXIT means the scope returned or unwound, not necessarily success. A stall outside instrumented BLT code requires a process dump for its stack.

Logs flush once per second. Queue is capped at 8,192 entries with a dropped-event counter. Each run rotates three 16 MiB log segments (approximately 48 MiB total, with possible one-batch overshoot); prior runs are retained. Logs include local assembly paths and troop IDs, but do not intentionally record chat, OAuth credentials or hero/viewer names. Disk failures disable current writes without throwing into gameplay. A system-wide freeze, blocked disk, suspended process or runtime-wide stall can also stop this worker.

## Verification

All four modules built against locally installed Bannerlord 1.4.8 assemblies. An isolated .NET Framework probe compiled the actual diagnostic helper and deliberately stopped its heartbeat for 12.5 seconds: the worker recorded a stall, the active callback, memory values and subsequent recovery. Probe source is `tools/freeze-diagnostic-probe.cs.txt`; compile alongside `BannerlordTwitch/BannerlordTwitch/Util/FreezeDiagnostics.cs` using Visual Studio Roslyn csc, then run with a fresh output directory argument. This verifies telemetry, not the reported in-game freeze. Actual TAOM runtime validation remains pending.
