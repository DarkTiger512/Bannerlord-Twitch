# TAOM preload stall: dump findings and candidate 2

## Evidence

The supplied process-28440 captures came from 5.5.9-diagnostics.2. Both dumps completed successfully, approximately 20 seconds apart. Their native application thread is 46056. During the stall it accumulated approximately 0.85–0.92 seconds of CPU per second (one busy logical core); the heartbeat's last phase was `application.tick-returned`. This is not evidence of a synchronous Twitch network wait.

Using Microsoft's DbgEng, the matching CLR data-access DLL from Microsoft's symbol server, and the matching local TaleWorlds.Native.dll, both dumps resolve the managed path to:

```
PreloadHelper.WaitForMeshesToBeLoaded()
MissionViewsContainer.ForEach(...)
MissionScreen.IMissionSystemHandler.BeforeMissionTick(...)
MissionState.TickMission(...)
```

Both are inside native resource checking from that preloader. Native instruction RVAs differ (capture 1: 0x2121e1; capture 2: 0x20b87e), with shared callers at 0x7f3xx, 0x2bb179 and 0x4d1d0e. The matching native PE timestamp is 0x6a732505. Native export labels such as WotsMainNativeCoreCLR plus large offsets are **not actual resolved internal function names**. We did not rely on those misleading nearest-export labels to assign a cause.

Inspection of the installed game implementation shows `WaitForMeshesToBeLoaded` repeatedly checking all registered meshes and dynamic physics resources, sleeping 1 ms and repeating while any remain pending, with no deadline. Campaign `MissionPreloadView.OnSceneRenderingStarted` calls this helper after requesting equipment resources from the involved parties.

This establishes the stalled execution path and a mechanism capable of waiting indefinitely. It does not identify the unresolved asset, prove which mod authored it, or prove the game could never recover if left longer. The small dumps did not preserve enough helper heap state to reliably list its pending resources. The previous rgl ending at nordic_shortbow is a clue, not proof that this bow is faulty. BLT could have indirectly introduced an item into the roster; no BLT retinue loop appears on the resolved stack.

## Candidate 2 behavior

A Harmony prefix targets that exact preloader method only when TAOM is present and the expected method/private-field types match. Outside a mission the original runs. Within a TAOM mission, the patch performs the same mesh and physics readiness checks on the calling thread and returns normally when ready. It limits repeated polling to approximately 15 seconds, then returns to the caller and logs `PRELOAD_TIMEOUT` with the unresolved resource count and up to 64 mesh/physics names. It does not remove equipment, change retinues, alter race/tier behavior, cancel preload requests or modify the game's asset files.

This is a **bounded-wait mitigation**, not an asset repair. A resource still unavailable after timeout can cause missing visuals or physics. A single native check that never returns is not interrupted by this deadline; all checks stay on the game thread. The existing automatic dump capture remains enabled for such a stall. A complete resource sweep can extend past the nominal deadline before the timeout is checked.

The new log's `PRELOAD_READY` / `PRELOAD_TIMEOUT` messages distinguish success from a forced continuation. The phase field identifies the mesh/physics check currently executing. If incompatible engine internals are detected, the patch is skipped and this is logged; it does not guess field layouts.

## Verification and limits

All four modules compile against local Bannerlord 1.4.8. A .NET Framework fixture compiles the actual patch and policy with engine stubs and uses installed Harmony 2.4.2 to patch the stub preloader. It verifies ready resources, delayed readiness, the deadline, original behavior outside missions, names for permanently pending mesh and physics resources, and preservation of resource collections. A separate build without the TAOM stub verifies the original method stays untouched. The production 15-second deadline is exercised, not just a shortened test timeout.

Reproduce with `tools/preload-wait-probe.cs.txt`, compiling alongside `Util/ResourcePreloadWait.cs` and `Behaviors/TaomPreloadWaitPatch.cs` and referencing Harmony. Define `TAOM` for the patched test, omit it for the non-TAOM test. No game engine is launched by this fixture. In-game validation remains pending; the release is a prerelease candidate.

## Streamer next run

Install 5.5.10-candidate.2 with the game closed and replay the same battle. A problematic readiness wait should be capped at about 15 seconds. Send the run's `freeze-...log` whether it succeeds or fails: it now records unresolved asset names. If it still stalls, wait at least 70 seconds and send the automatically captured `.dmp` and `.status.txt` files privately as before. Do not post dumps publicly; they contain local process memory.
