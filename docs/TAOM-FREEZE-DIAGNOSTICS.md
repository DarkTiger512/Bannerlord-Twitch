# TAOM 5.5.9 — extensive freeze diagnostics 2

Diagnostics were introduced in 5.5.9-diagnostics.2 and are retained in 5.5.10-candidate.2. See `TAOM-PRELOAD-STALL.md` for the targeted preload-wait mitigation and its limits. This is not a confirmed in-game freeze fix. It retains the optional naval-patch guard and main-thread queue fixes from 5.5.8. In the latest streamer log all recorded BLT scopes returned before application ticks stopped; that evidence does not identify the blocking code.

## Streamer: install, reproduce, send files

1. Close the game, back up saves/settings, and extract the four module folders over the existing BLT installation. Preserve configuration/authentication files and normal TAOM dependencies/load order.
2. Reproduce the battle stall. Leave the game running for at least **70 seconds** after it appears stuck. Capture can briefly pause execution; do not interpret that pause as a new bug.
3. Open `%LOCALAPPDATA%\BLTRefreshed\Diagnostics` (paste into Win+R). Send the files from that run: `freeze-...log`, both `.dmp` files and `.dmp.status.txt` files, plus the game's rgl/watchdog logs if available. No Task Manager dump steps are needed.
4. Share dumps privately: even these smaller dumps contain stack memory and may include private values. Nothing is uploaded automatically.

## Added evidence

- Automatic out-of-process Windows minidumps at approximately 20 and 40 seconds without an application heartbeat **while the last recorded context is a mission**. Maximum two attempts per process run; restarting the game resets the limit. Startup/menu stalls do not consume captures.
- Native OS thread ID of the application-tick thread, per-thread accumulated CPU, CPU delta in milliseconds between stall samples, state and wait reason. The first delta is -1 (no baseline). CPU movement is evidence of activity, not proof of useful progress.
- Application phase: base callback, queued actions or callback returned. This can distinguish a queue stall from a stall elsewhere in the engine after BLT's callback returned.
- Loaded managed assembly identities/paths at capture time, process handle/thread counts, GC collection counters; existing heartbeat, active scopes, process CPU, memory and Windows commit statistics remain.
- Existing BLT scope traces cover troop spawning, retinues, agent callbacks, rewards, achievements, initialization and queued actions. EXIT means return/unwind, not necessarily success.

## Capture implementation and limits

`tools/diagnostic-dump-helper.cs` builds as the x64 .NET Framework console helper `BLT.Diagnostics.exe` beside BannerlordTwitch.dll through the main project's MSBuild target. The game launches it hidden with PID and output path. It uses Windows DbgHelp MiniDumpWriteDump with stack memory, thread data, unloaded modules and memory-region metadata (0x1920), **not full process memory**. This should enable native stack inspection; complete managed heap/stack reconstruction is not guaranteed. Matching game/mod symbols may still be required. Inspect the application thread and hottest threads in both captures; compare instruction/stack positions before assigning a cause.

A per-target mutex prevents overlapping captures; helper exits after 60 seconds if capture hangs. It never explicitly kills or suspends the game. At least 1 GiB of free disk space is required before capture; size depends on process threads/modules. Successful dumps are renamed from `.partial` only after writing completes. `.status.txt` records START/SUCCESS/FAILED; START plus `.partial` without SUCCESS may indicate timeout or interrupted capture. Old sessions are retained. Remove them manually after investigation if disk space matters.

Sampling uses a timer thread without reading engine objects. Engine context is copied by the application thread. A runtime-wide deadlock, blocked ThreadPool/disk, process suspension, security software blocking the helper, or system-wide lockup can prevent capture; errors are contained so diagnostics do not throw into gameplay. A missing dump is not evidence that no stall occurred. Long loading, OS scheduling and debugger pauses can also trigger `stalled=True`; that flag alone is not proof of a permanent freeze.

Logs flush each second, cap the event queue at 8,192 with dropped counters, and rotate three 16 MiB segments per run. Per-thread records are emitted during stalls only. Dumps are local diagnostic evidence, not a promise that the next run will establish a root cause.

## Verification

All four modules compile against locally installed Bannerlord 1.4.8 assemblies. The actual diagnostic source is exercised with `tools/freeze-diagnostic-probe.cs.txt`: a deliberate 65-second heartbeat interruption produces exactly two minidumps with valid MDMP headers, thread/active-scope records and subsequent recovery. That also checks no third capture is created at 60 seconds. The equipment regression fixture has 66 checks. This is controlled-process verification, **not an in-game TAOM reproduction**.

To run the diagnostic probe, compile the helper and probe as x64 with Visual Studio Roslyn csc into one temporary directory, linking `BannerlordTwitch/BannerlordTwitch/Util/FreezeDiagnostics.cs` into the probe. Run the probe with a fresh output directory as its argument. Build modules with the existing `OutputBuildRoot` override, `DeployToGame=false` and `CreatePackage=false`; the helper is built automatically with BannerlordTwitch. Keep matching module PDBs from the build for investigation.
