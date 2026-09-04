using BLTAdoptAHero.Util;
using BannerlordTwitch.Integration;

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

Assert(IntegrationCommandLine.TryParse("heal", out var bareCommand) && bareCommand.Name == "heal" && bareCommand.Args == string.Empty,
    "A command without ! must match chat parsing.");
Assert(IntegrationCommandLine.TryParse("  !nameitem   12 The Wolf's Oath  ", out var commandWithArgs) &&
       commandWithArgs.Name == "nameitem" && commandWithArgs.Args == "12 The Wolf's Oath",
    "Command parsing must preserve the complete multi-word argument substring after chat-compatible trimming.");
Assert(!IntegrationCommandLine.TryParse(" ! ", out _), "An empty command must be rejected.");
Assert(IntegrationCommandLine.TryParse("!!heal", out var repeatedPrefix) && repeatedPrefix.Name == "!heal",
    "Only one optional Extension prefix may be normalized.");
var requestLifecycle = new IntegrationRequestLifecycle();
var completedRequest = Guid.NewGuid();
Assert(requestLifecycle.TryAccept(completedRequest, out var completionTimeout) && !completionTimeout.IsCancellationRequested,
    "A new Extension request must enter the running state once.");
Assert(requestLifecycle.TryComplete(completedRequest) && completionTimeout.IsCancellationRequested &&
       !requestLifecycle.TryComplete(completedRequest) && !requestLifecycle.TryExpire(completedRequest),
    "Only the first terminal result may complete an Extension request.");
var expiredRequest = Guid.NewGuid();
Assert(requestLifecycle.TryAccept(expiredRequest, out _) && requestLifecycle.TryExpire(expiredRequest) &&
       !requestLifecycle.TryComplete(expiredRequest), "A timed-out request must reject late handler replies.");
requestLifecycle.Dispose();

var children = new Dictionary<string, string[]>
{
    ["root"] = new[] { "infantry", "ranged", "dead-end" },
    ["infantry"] = new[] { "legionary" },
    ["ranged"] = new[] { "sharpshooter" },
    ["dead-end"] = Array.Empty<string>(),
    ["legionary"] = Array.Empty<string>(),
    ["sharpshooter"] = Array.Empty<string>()
};
var terminals = CycleSafeGraph.FindTerminals("root", node => children[node]);
Assert(terminals.SequenceEqual(new[] { "legionary", "sharpshooter", "dead-end" }), "Branch/dead-end terminals failed.");

children["cycle-a"] = new[] { "cycle-b" };
children["cycle-b"] = new[] { "cycle-a" };
Assert(CycleSafeGraph.FindTerminals("cycle-a", node => children[node]).Count == 0,
    "A closed cycle must not masquerade as a terminal.");

var troops = new[]
{
    new Troop("same-incompatible", "A", false, 2),
    new Troop("other-compatible", "B", true, 6),
    new Troop("same-compatible-low", "A", true, 4),
    new Troop("same-compatible-high-z", "A", true, 6),
    new Troop("same-compatible-high-a", "A", true, 6)
};
var selected = SmartTroopPolicy.Select(troops, t => t.Culture == "A", t => t.Compatible,
    new[] { new Troop("fallback", "A", true, 1) }, t => t.Id, t => t.MaxTier);
Assert(selected.Value.Id == "same-compatible-high-a" && selected.FallbackTier == 1 && selected.Score == 6,
    "Tier 1 scoring/stable tie-break failed.");

selected = SmartTroopPolicy.Select(troops.Where(t => t.Culture != "A" || !t.Compatible),
    t => t.Culture == "A", t => t.Compatible, Array.Empty<Troop>(), t => t.Id, t => t.MaxTier);
Assert(selected.Value.Id == "other-compatible" && selected.FallbackTier == 2, "Tier 2 failed.");

selected = SmartTroopPolicy.Select(troops.Where(t => !t.Compatible), t => t.Culture == "A",
    t => t.Compatible, Array.Empty<Troop>(), t => t.Id, t => t.MaxTier);
Assert(selected.Value.Id == "same-incompatible" && selected.FallbackTier == 3, "Tier 3 failed.");

selected = SmartTroopPolicy.Select(Array.Empty<Troop>(), _ => false, _ => false,
    new[] { new Troop("fallback", "A", true, 1) }, t => t.Id, t => t.MaxTier);
Assert(selected.Value.Id == "fallback" && selected.FallbackTier == 4, "Tier 4 failed.");

var branch = SmartTroopPolicy.SelectCompatible(troops, t => t.Compatible, t => t.MaxTier, t => t.Id);
Assert(branch.Value.Id == "other-compatible", "Compatible branch score/tie-break failed.");
Assert(branch.Rejections.Any(r => r.Contains("same-incompatible")), "Rejected branches were not diagnosed.");

var replacement = SmartTroopPolicy.SelectClosestTier(troops, 5, t => t.Culture == "A", t => t.Compatible,
    t => t.MaxTier, t => t.MaxTier, t => t.Id);
Assert(replacement.Value.Id == "same-compatible-high-a", "Closest-tier culture/score preference failed.");
var missing = SmartTroopPolicy.SelectClosestTier(troops, 5, _ => true, _ => false,
    t => t.MaxTier, t => t.MaxTier, t => t.Id);
Assert(missing.Value == null && missing.Rejections.Count == 1, "Missing replacement failed.");

Assert(SmartTroopPolicy.InterpretRole("Horse Archer", true) == SmartTroopPolicy.SmartTroopRole.HorseArcher,
    "Explicit horse archer must override mounted cavalry.");
Assert(SmartTroopPolicy.InterpretRole("Ranged", true) == SmartTroopPolicy.SmartTroopRole.Cavalry,
    "Mounted must override a non-horse-archer formation.");
Assert(SmartTroopPolicy.InterpretRole("Ranged", false) == SmartTroopPolicy.SmartTroopRole.FootRanged,
    "Foot ranged role failed.");
Assert(SmartTroopPolicy.InterpretRole("Skirmisher", false) == SmartTroopPolicy.SmartTroopRole.InfantryFamily,
    "Skirmisher infantry-family role failed.");
Assert(SmartTroopPolicy.InterpretRole("CustomRole", false) == SmartTroopPolicy.SmartTroopRole.Unknown,
    "Unknown role fallback failed.");

Assert(!SmartTroopPolicy.CanAfford(100, 75, 50), "Insufficient gold should block mutation.");
Assert(SmartTroopPolicy.CanAfford(125, 75, 50), "Exact gold should be affordable.");

var ammo = AmmoReport.Create(new[]
{
    new AmmoStackSnapshot { Slot = 3, Name = "Javelins", Current = 0, Maximum = 5 },
    new AmmoStackSnapshot { Slot = 1, Name = "Arrows", Current = 12, Maximum = 24 },
    new AmmoStackSnapshot { Slot = 2, Name = "Bolts", Current = 7, Maximum = 10 }
}, true);
Assert(ammo.Kind == AmmoReportKind.Available && ammo.TotalCurrent == 19 && ammo.TotalMaximum == 39,
    "Mixed ammunition totals failed.");
Assert(ammo.Details == "Arrows: 12/24, Bolts: 7/10, Javelins: 0/5",
    "Ammunition output must follow stable equipment-slot order.");

ammo = AmmoReport.Create(new[]
{
    new AmmoStackSnapshot { Slot = 0, Name = "Throwing Axes", Current = 0, Maximum = 3 }
}, false);
Assert(ammo.Kind == AmmoReportKind.Depleted && ammo.TotalCurrent == 0 && ammo.TotalMaximum == 3,
    "Depleted thrown ammunition failed.");
Assert(AmmoReport.Create(Array.Empty<AmmoStackSnapshot>(), true).Kind == AmmoReportKind.MissingAmmo,
    "Ranged weapon without ammunition failed.");
Assert(AmmoReport.Create(Array.Empty<AmmoStackSnapshot>(), false).Kind == AmmoReportKind.NoRangedWeapon,
    "No ranged equipment failed.");

var projection = new MapProjection(0, 200, 0, 100);
Assert(Math.Abs(projection.DisplayWidth - 200) < .001f && Math.Abs(projection.DisplayHeight - 100) < .001f,
    "Projection must preserve the world aspect ratio.");
var projected = projection.Project(100, 25);
Assert(Math.Abs(projected.X - 100) < .001f && Math.Abs(projected.Y - 75) < .001f,
    "Projection coordinate conversion failed.");
var portraitProjection = new MapProjection(0, 50, 0, 100);
Assert(Math.Abs(portraitProjection.DisplayWidth - 50) < .001f,
    "Portrait projections must not be stretched to landscape.");

var land = new bool[2, 2];
land[0, 0] = true;
var contours = CampaignMapGeometry.TraceContours(land, projection, 0, 200, 0, 100);
Assert(contours.Count == 1, "Marching-squares single-corner contour failed.");
Assert(CampaignMapGeometry.TraceContours(new bool[1, 1], projection, 0, 1, 0, 1).Count == 0,
    "Undersized terrain grids must be safe.");

var clusters = CampaignMapGeometry.ClusterMarkers(new[]
{
    new MapMarkerInput { Id = "b", X = 2, Y = 2 },
    new MapMarkerInput { Id = "a", X = 1, Y = 1 },
    new MapMarkerInput { Id = "c", X = 50, Y = 50 }
}, 3);
Assert(clusters["a"] == "a+b" && clusters["b"] == "a+b" && clusters["c"] == "c",
    "Stable marker clustering failed.");

var labels = CampaignMapGeometry.PrioritizeLabels(new[]
{
    new Label("castle", false, false), new Label("town", false, true), new Label("hero", true, false)
}, value => value.Hero, value => value.Town, value => value.Id);
Assert(labels.SequenceEqual(new[] { "hero", "town", "castle" }), "Smart label priority failed.");

var centerView = CampaignMapGeometry.FocusView(150, 100, 75, 50, 2.5f);
Assert(Math.Abs(centerView.Width - 60) < .001f && Math.Abs(centerView.Height - 40) < .001f &&
       Math.Abs(centerView.X - 45) < .001f && Math.Abs(centerView.Y - 30) < .001f,
    "Spectator camera zoom/centering failed.");
var edgeView = CampaignMapGeometry.FocusView(150, 100, 0, 100, 2.5f);
Assert(Math.Abs(edgeView.X) < .001f && Math.Abs(edgeView.Y - 60) < .001f,
    "Spectator camera must clamp to map borders.");

Assert(StreamObjectivePolicy.TryParseStart("start kills 10 gold=500 xp=250", _ => true, out var objectiveStart, out _)
       && objectiveStart.Kind == StreamObjectiveKind.Kills && objectiveStart.Target == 10 && objectiveStart.Gold == 500 && objectiveStart.XP == 250,
    "Objective start parsing failed.");
Assert(StreamObjectivePolicy.TryParseStart("start captures 3 culture=empire gold=1000 xp=50", id => id == "empire", out objectiveStart, out _)
       && objectiveStart.CultureId == "empire", "Culture-filtered capture parsing failed.");
Assert(!StreamObjectivePolicy.TryParseStart("start kills 0 gold=1 xp=1", _ => true, out _, out _),
    "Zero objective targets must be rejected.");
Assert(!StreamObjectivePolicy.TryParseStart("start kills 2 gold=-1 xp=1", _ => true, out _, out _),
    "Negative rewards must be rejected.");
Assert(!StreamObjectivePolicy.TryParseStart("start captures 2 culture=missing gold=1 xp=1", _ => false, out _, out _),
    "Unknown capture cultures must be rejected.");

StreamObjectiveState noObjective = null;
StreamObjectivePolicy.RestoreCollections(noObjective);
Assert(noObjective == null, "Fresh-campaign save must preserve the absence of an objective.");
var legacyObjective = new StreamObjectiveState { Contributors = null, ProcessedEvents = null };
StreamObjectivePolicy.RestoreCollections(legacyObjective);
Assert(legacyObjective.Contributors != null && legacyObjective.ProcessedEvents != null,
    "Legacy objective collections must be restored.");
legacyObjective.Contributors["Viewer"] = new StreamObjectiveContribution { Amount = 3 };
legacyObjective.ProcessedEvents.Add("event-1");
StreamObjectivePolicy.RestoreCollections(legacyObjective);
Assert(legacyObjective.Contributors["viewer"].Amount == 3 && legacyObjective.ProcessedEvents.Contains("event-1"),
    "Restoring objective collections must preserve progress and comparer behavior.");

var objective = new StreamObjectiveState { Kind = StreamObjectiveKind.Kills, Target = 2 };
Assert(StreamObjectivePolicy.AddProgress(objective, "kill-1", "Viewer", "hero-1", "Viewer Hero"),
    "First objective contribution failed.");
Assert(!StreamObjectivePolicy.AddProgress(objective, "kill-1", "Viewer", "hero-1", "Viewer Hero") && objective.Progress == 1,
    "Duplicate objective events must not count twice.");
Assert(StreamObjectivePolicy.AddProgress(objective, "kill-2", "Other", "hero-2", "Other Hero") &&
       StreamObjectivePolicy.IsComplete(objective) && StreamObjectivePolicy.Milestone(objective) == 100,
    "Shared objective completion failed.");

var survive = new StreamObjectiveState { Kind = StreamObjectiveKind.Survive, Target = 2, RequiredHeroes = 2, RequiredBattles = 2 };
Assert(StreamObjectivePolicy.RecordSurvival(survive, "battle-1", new[]
{
    ("A", "a", "Hero A", false), ("B", "b", "Hero B", false)
}), "Initial survival progress failed.");
StreamObjectivePolicy.RecordSurvival(survive, "battle-2", new[]
{
    ("A", "a", "Hero A", false), ("B", "b", "Hero B", true)
});
Assert(survive.Progress == 1 && survive.Contributors["A"].SurvivalStreak == 2 && survive.Contributors["B"].SurvivalStreak == 0,
    "Individual survival/death reset failed.");
StreamObjectivePolicy.RecordSurvival(survive, "battle-3", new[] { ("B", "b", "Hero B", false) });
StreamObjectivePolicy.RecordSurvival(survive, "battle-4", new[] { ("B", "b", "Hero B", false) });
Assert(StreamObjectivePolicy.IsComplete(survive), "Individual survivor target failed.");
Assert(!StreamObjectivePolicy.RecordSurvival(survive, "battle-4", new[] { ("B", "b", "Hero B", false) }),
    "Duplicate survival battles must not count twice.");

var curse = new CurseRecord { HeroId = "hero-1", Owner = "Viewer" };
Assert(CursedArtifactPolicy.RecordVictory(curse, "battle-1", 2) && curse.QualifyingWins == 1 && curse.Status == CurseLifecycle.Active,
    "First cursed victory failed.");
Assert(!CursedArtifactPolicy.RecordVictory(curse, "battle-1", 2) && curse.QualifyingWins == 1,
    "Duplicate cursed battle callbacks must not advance progress.");
Assert(CursedArtifactPolicy.RecordVictory(curse, "battle-2", 2) && curse.Status == CurseLifecycle.CompletedPendingReward,
    "Curse must enter pending-reward state at the configured target.");
Assert(Math.Abs(CursedArtifactPolicy.OutgoingMultiplier(20) - .8f) < .001f &&
       Math.Abs(CursedArtifactPolicy.IncomingMultiplier(25) - 1.25f) < .001f,
    "Curse combat multipliers failed.");
Assert(Math.Abs(CursedArtifactPolicy.OutgoingMultiplier(500) - .05f) < .001f &&
       Math.Abs(CursedArtifactPolicy.IncomingMultiplier(500) - 5f) < .001f &&
       CursedArtifactPolicy.ClampRequiredWins(0) == 1,
    "Unsafe curse settings must be clamped.");

Assert(RandomEventPolicy.ClampChance(-1) == 0 && RandomEventPolicy.ClampChance(101) == 100 &&
       RandomEventPolicy.ClampCooldown(-3) == 0 && RandomEventPolicy.ClampCooldown(5000) == 3650,
    "Random-event chance/cooldown clamps failed.");
Assert(!RandomEventPolicy.CanRoll(99, 10, 90, false) && RandomEventPolicy.CanRoll(100, 10, 90, false) &&
       !RandomEventPolicy.CanRoll(1000, 0, 0, true), "Random-event cooldown/active gating failed.");
Assert(RandomEventPolicy.RollSucceeds(.19f, .2f) && !RandomEventPolicy.RollSucceeds(.2f, .2f),
    "Random-event chance boundary failed.");
Assert(RandomEventPolicy.TryTransition(RandomEventLifecycle.Inactive, RandomEventLifecycle.Preparing) &&
       RandomEventPolicy.TryTransition(RandomEventLifecycle.Preparing, RandomEventLifecycle.AwaitingResponse) &&
       !RandomEventPolicy.TryTransition(RandomEventLifecycle.Resolved, RandomEventLifecycle.Active),
    "Random-event lifecycle transitions failed.");
var immortal = new ImmortalEncounterState { Phase = RandomEventLifecycle.Active };
Assert(RandomEventPolicy.RecordParticipant(immortal, "hero") && !RandomEventPolicy.RecordParticipant(immortal, "hero") &&
       RandomEventPolicy.RecordReward(immortal, "hero") && !RandomEventPolicy.RecordReward(immortal, "hero"),
    "Immortal participant/reward idempotence failed.");
Assert(RandomEventPolicy.CalculateArmySize(500, 80, 100, 1000) == 400 &&
       RandomEventPolicy.CalculateArmySize(10, 80, 100, 1000) == 100 &&
       RandomEventPolicy.CalculateArmySize(5000, 80, 100, 1000) == 1000,
    "Random-event army sizing failed.");
Assert(RandomEventPolicy.CrusadeResolved(true, true, true) && RandomEventPolicy.CrusadeResolved(false, false, true) &&
       RandomEventPolicy.CrusadeResolved(false, true, false) && !RandomEventPolicy.CrusadeResolved(false, true, true),
    "Crusade resolution policy failed.");

Console.WriteLine("Smart troop, ammunition, campaign map, stream objective, cursed artifact, and random-event policy tests passed.");

internal sealed record Troop(string Id, string Culture, bool Compatible, int MaxTier);
internal sealed record Label(string Id, bool Hero, bool Town);
