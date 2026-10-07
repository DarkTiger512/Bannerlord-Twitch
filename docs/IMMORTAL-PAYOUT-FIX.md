# Immortal Encounter payout fix

The configured default is 100,000 gold per eligible player-side BLT participant. Both Classic main and TAOM had the same payout lifecycle.

The old hero-death and party-destruction handlers immediately called `Complete(false, ...)`, destroying the encounter state and participant list. If either callback arrived before `MapEventEnded`, the victory callback no longer had an active encounter to reward. The old callback also depended on the defeated party remaining in the map event. Participant registration ran only at agent creation, and the reward ledger was updated before resolving the hero/bank or crediting gold.

The fix retains battle state through death/destruction, binds the original map-event instance and player side before teardown, and queues its outcome for completion after leaving the battle/encounter UI. Agent build repeats participant registration idempotently. Winning participants receive the configured reward (default 100,000); losses, unresolved battles and nonparticipants receive no Immortal reward. Dead heroes remain excluded, matching the previous eligibility rule.

The victory result, configured amount, participants and successful-payment ledger persist in the existing JSON state. A temporarily unavailable hero/bank leaves payment pending for retry. The ledger is written after a successful credit. Repeated callbacks and campaign ticks cannot pay recorded winners again. Cleanup and the success announcement happen after rewards settle. This changes payout timing to the campaign tick after exiting the encounter.

Validation: shared policy regression suite covers callback-order cleanup deferral, exact battle matching, 100k once-only payment, failures before credit, retries, loss/unresolved outcomes, saved pending/paid state and old JSON compatibility. Both Classic and TAOM adoption modules compile against the locally installed game. These are code/policy/build checks, not a live streamer replay.

Only the Immortal payout path is fixed here; this does not establish that all other event rewards were broken. Historical payouts cannot be reconstructed from saves where the old implementation already discarded encounter state. No automatic retroactive credit is attempted. The default reward amount is unchanged.
