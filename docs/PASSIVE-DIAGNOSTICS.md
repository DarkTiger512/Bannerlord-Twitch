# Passive TAOM diagnostics (5.5.20 refresh, 21 October 2026)

Diagnostics start automatically with BLT. No popup, overlay notification, debugger attachment, exception suppression, resource-wait bypass, or attempt to recover/terminate Bannerlord is added by this feature. Existing preload-guard behavior from the previous release remains separate and unchanged.

## When something goes wrong

Open `%LOCALAPPDATA%\BLTRefreshed\Diagnostics` (paste this into Windows Explorer). Each run has its own `session-...` folder. Send the affected run's **BLT-incident.zip and adjacent capture1.dmp/capture2.dmp files** privately. Nothing is uploaded automatically. Full memory can contain authentication tokens and other private process data; text-log redaction is best effort.

- Ordinary play writes a ring of three logs, approximately 4 MiB each, with a one-second heartbeat file and a ten-second process/operation summary. No command arguments, viewer names, authentication configuration or game saves are deliberately collected by BLT's own instrumentation. Copied game logs may include chat.
- A helper process runs independently of the game and CLR writer. After **180 seconds without an application heartbeat**, it attempts two memory captures, with at least 20 seconds between completion of the first and starting the second. It also covers startup without a first heartbeat after a ten-minute grace period. Two capture attempts maximum per run prevent repeated disk traffic.
- Windows process snapshots are preferred: full dump writing reads a cloned address space while the game remains free to run. Snapshot creation still has a cost; captures and disk I/O cannot be guaranteed invisible. The helper runs below normal CPU priority.
- If a full snapshot is unavailable, it attempts a smaller stack/thread dump, explicitly labeled `STACK_ONLY_DUMP_SUCCESS`. With insufficient disk space, it preserves text evidence and explains why capture was skipped. Dumps are capped at a 32 GiB aggregate budget across diagnostic session folders, with additional free-space headroom checks. Old dumps are never silently deleted; move/delete old incident folders if the budget is reached.
- Managed exceptions recorded by BLT or its passive boundary hooks include the complete inner-exception chain and Harmony patch owners for those frames. Exceptions are left unchanged. Up to 32 exception reports per run bound repeated failures. A terminating managed exception makes a best-effort capture request, allowing up to two seconds for acknowledgment during termination only.
- A native crash may end the process before a snapshot can be taken. The watcher then preserves available logs and module identification; native crash dumps are not guaranteed without attaching a debugger or changing Windows crash handling. Neither is done here.

## What the evidence tells us

The heartbeat records the last BLT application stage, context, current BLT command/reward or queued operation, last shader count and its sampling timestamp, and the most recent scene-view readiness result/timestamp. Scene-view readiness can reflect any observed scene view; it is not assumed to identify a specific mission. Engine values are sampled only on game callbacks, never from a diagnostic worker.

The watcher records heartbeat/sample ages separately. A stale shader count is never treated as proof that compilation stopped. Snapshots include full process memory where possible, thread contexts/stacks and module metadata. Incident files preserve matching BLT/TAOM/key engine DLLs and available BLT PDBs for stack resolution, module hashes, and recent RGL/TAOM log tails. The small ZIP excludes large dumps and incomplete files and is refreshed after captures and process exit.

This improves evidence collection; it does not automatically prove which mod caused every failure. Exact ownership may still require reproduction or native engine symbols. No live TAOM campaign test was performed for this release.

## Development validation

`tools/test-passive-diagnostics.py OUTPUT_DIR HARMONY_DLL` compiles the production observer/helper and tests healthy/startup sessions, unchanged exceptions under real Harmony, and an external watcher capturing two full snapshots from a harmless simulated stalled process. The test checks dump headers/streams, an in-memory sentinel, capture spacing, target survival, and ZIP contents. `tools/test-kingdom-decision-queue.py` checks the decision-constructor correction independently.

Snapshot implementation follows Microsoft's process-snapshot/MiniDumpWriteDump contract: https://learn.microsoft.com/en-us/previous-versions/windows/desktop/proc_snap/export-a-process-snapshot-to-a-file
