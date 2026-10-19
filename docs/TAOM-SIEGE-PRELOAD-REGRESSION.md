# TAOM siege preload regression

The reported attacking siege loads with TAOM 5.5.10 and stalls with 5.5.20 using the same save. The supplied `rgl_log_34400.txt` confirms 5.5.20, starts `SiegeMissionWithDeployment`, creates both teams, and ends during resource loading without a siege exception.

Commit `ca173f6` on `DT/taom-freeze-diagnostics` identifies itself as 5.5.10 and adds a bounded preload wait. Its dump investigation located an earlier stall in `PreloadHelper.WaitForMeshesToBeLoaded`. Neither that guard nor its resource-wait policy is present in `v5.5.20-taom.1`. This missing mitigation is a strong explanation for the version comparison; the supplied log alone does not establish the current process's blocked stack, or the exact contents of the streamer's working 5.5.10 package.

This change restores the 15-second polling deadline with regular BLT logging, without the diagnostic build's automatic dump capture. It retains preload requests and checks resources on the calling thread. Timeout logs identify up to 64 pending meshes or physics resources. It also skips the optional Naval patrol patch when its target is absent: the supplied log shows that missing target aborting patch initialization, and the diagnostics branch already contained a guard for it.

The deadline bounds repeated polling, not a single native call that never returns. A full resource sweep can exceed the nominal deadline. Continuing after timeout can leave missing visuals or physics; this mitigates the wait rather than repairing assets.

## Verification

Run with .NET 9 from the repository root:

```powershell
dotnet run --project tools/preload-wait-tests/PreloadWaitTests.csproj -p:WithTaom=true
dotnet run --project tools/preload-wait-tests/PreloadWaitTests.csproj -p:WithTaom=false
```

The fixture compiles the production guard and polling policy against engine stubs and uses Harmony 2.4.2. Checks cover immediate readiness, delayed readiness, a persistent wait, readiness at the deadline, the real 15-second deadline, thread ownership, pending resource names, resource collection preservation, original behavior outside missions, and no patch when TAOM is absent. Both configurations pass. A full game build and replay of the reported siege remain pending.

On the next in-game run, look for `PRELOAD_GUARD`, then `PRELOAD_READY` or `PRELOAD_TIMEOUT` in the log. A timeout followed by successful siege entry would support the missing-guard explanation and identify resources for further investigation.
