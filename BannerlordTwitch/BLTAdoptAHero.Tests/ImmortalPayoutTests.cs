using BLTAdoptAHero.Util;
using Newtonsoft.Json;

internal static class ImmortalPayoutTests
{
    public static void Run()
    {
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); }
        foreach (bool deathFirst in new[] { true, false })
        {
            var state = new ImmortalEncounterState { Phase = RandomEventLifecycle.Active };
            var tracking = new ImmortalBattleTracking();
            var battle = new object();
            tracking.Bind(battle, 1);
            RandomEventPolicy.RecordParticipant(state, "viewer");
            // Hero death and party destruction can precede the payout callback in either order.
            Check(RandomEventPolicy.DeferImmortalCleanup(state), deathFirst ? "death retains participants" : "party destruction retains participants");
            Check(RandomEventPolicy.DeferImmortalCleanup(state), deathFirst ? "party destruction retains participants" : "death retains participants");
            Check(!tracking.Complete(state, new object(), 1, 100000), "unrelated battle cannot award");
            Check(tracking.Complete(state, battle, 1, 100000) && state.PlayerWon, "victory survives detached parties");
            Check(!tracking.Complete(state, battle, 0, 0) && state.RewardGold == 100000, "duplicate callback cannot rewrite reward/result");
            Check(RandomEventPolicy.DeferImmortalCleanup(state), "cleanup callbacks cannot erase queued rewards");
            // Saved between battle resolution and payment: still payable on the next campaign tick.
            state = JsonConvert.DeserializeObject<ImmortalEncounterState>(JsonConvert.SerializeObject(state));
            Check(state.CompletionPending && state.PlayerWon && state.ParticipantHeroIds.Contains("viewer"), "pending victory survives save/load");
            int balance = 0;
            Check(!RandomEventPolicy.TryGrantImmortalReward(state, "viewer", () => false) && state.RewardedHeroIds.Count == 0, "unavailable bank is not recorded paid");
            try { RandomEventPolicy.TryGrantImmortalReward(state, "viewer", () => throw new Exception("before credit")); } catch (Exception) { }
            Check(state.RewardedHeroIds.Count == 0, "failed credit remains unpaid");
            Check(RandomEventPolicy.TryGrantImmortalReward(state, "viewer", () => { balance += state.RewardGold; return true; }), "retry credits winner");
            Check(!RandomEventPolicy.TryGrantImmortalReward(state, "viewer", () => { balance += state.RewardGold; return true; }) && balance == 100000, "winner gets 100k exactly once");
            Check(!RandomEventPolicy.TryGrantImmortalReward(state, "spectator", () => { balance++; return true; }) && balance == 100000, "nonparticipant excluded");
            state = JsonConvert.DeserializeObject<ImmortalEncounterState>(JsonConvert.SerializeObject(state));
            Check(!RandomEventPolicy.TryGrantImmortalReward(state, "viewer", () => { balance++; return true; }), "paid ledger survives reload");
        }
        foreach (int winner in new[] { -1, 0 })
        {
            var state = new ImmortalEncounterState { Phase = RandomEventLifecycle.Active };
            var battle = new object(); var tracking = new ImmortalBattleTracking(); tracking.Bind(battle, 1);
            Check(tracking.Complete(state, battle, winner, 100000) && !state.PlayerWon, "loss/unresolved battle does not win");
            state.ParticipantHeroIds.Add("viewer");
            Check(!RandomEventPolicy.TryGrantImmortalReward(state, "viewer", () => throw new Exception("must not pay loss")), "loss/unresolved battle cannot pay");
        }
        Check(!RandomEventPolicy.DeferImmortalCleanup(new ImmortalEncounterState { Phase = RandomEventLifecycle.AwaitingResponse }), "prebattle cancellation still cleans up");
        Check(!RandomEventPolicy.DeferImmortalCleanup(null), "inactive event cleanup unaffected");
        var old = JsonConvert.DeserializeObject<ImmortalEncounterState>("{\"Phase\":4,\"ParticipantHeroIds\":[\"viewer\"]}");
        Check(!old.CompletionPending && old.ParticipantHeroIds.Contains("viewer"), "old save fields remain compatible");
        Console.WriteLine("Immortal payout lifecycle, 100k credit, retry, duplicate and save/load regression tests passed.");
    }
}
