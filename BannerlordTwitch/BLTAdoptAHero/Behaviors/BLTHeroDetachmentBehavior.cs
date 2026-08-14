using System;
using System.Linq;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Util;

namespace BLTAdoptAHero
{
    internal class BLTHeroDetachmentBehavior : AutoMissionBehavior<BLTHeroDetachmentBehavior>
    {
        // ── Order enum ────────────────────────────────────────────────────────
        internal enum DetachmentOrder
        {
            None,     // Free-fighting — AI picks targets naturally
            Hold,     // Stand at a fixed position
            Follow,   // Shadow parent formation
            Navigate, // Generic one-shot navigation (used internally)
            Charge,   // Hunt and engage nearest enemy
            Flank,    // Navigate to the closest lateral flank of nearest enemy, then charge
            Gate,     // Gate-specific behaviour (attacker attacks / defender holds interior)
            Walls,    // Walls-specific behaviour (attacker climbs / defender covers breach)
        }

        // ── Wall navigation candidate ─────────────────────────────────────────
        internal readonly struct WallCandidate
        {
            public readonly WorldPosition Position;
            public readonly float SortKey; // original distance² for stable sort
            public WallCandidate(WorldPosition pos, float key) { Position = pos; SortKey = key; }
        }

        // ── Per-agent state ───────────────────────────────────────────────────
        internal class DetachmentState
        {
            public HeroDetachment Detachment;
            public DetachmentOrder Order = DetachmentOrder.None;

            // Navigation (shared across orders that move the agent somewhere)
            public WorldPosition NavigationTarget;
            public float LastNavigationReissueTime;
            public Vec2 LastCheckedPosition;
            public float LastPositionCheckTime;
            public int StuckCount;

            // Hold
            public WorldPosition HoldPosition;

            // Charge / Flank retarget
            public float LastRetargetTime;

            // Flank waypoint refresh
            public float LastFlankRefreshTime;

            // Gate
            public CastleGate GateTarget;
            public float LastGateCheckTime;

            // Walls — sorted list of standing-point / wall-position candidates; cycling index
            public List<WallCandidate> WallCandidates;
            public int WallCandidateIndex = -1;
            public bool WallObjectReached;

            // Melee-interrupt tracking (walls / gate orders)
            public float LastMeleeInterruptTime; // when the agent last stopped scripted movement due to melee
        }

        // ── Bookkeeping ───────────────────────────────────────────────────────
        private readonly Dictionary<Agent, DetachmentState> _detachments = new();
        private readonly List<KeyValuePair<Agent, DetachmentState>> _tickBuffer = new();

        // How long (seconds) after the last melee hit/attack before navigation resumes
        private const float MeleePauseWindow = 3f;

        // ── Public queries ────────────────────────────────────────────────────
        public bool IsDetached(Agent agent) => agent != null && _detachments.ContainsKey(agent);

        public bool TryGetDetachment(Agent agent, out HeroDetachment detachment)
        {
            if (agent != null && _detachments.TryGetValue(agent, out var s))
            { detachment = s.Detachment; return true; }
            detachment = null;
            return false;
        }

        public string GetStatus(Agent agent)
        {
            if (agent == null || !_detachments.TryGetValue(agent, out var s))
                return "Not detached";
            return s.Order switch
            {
                DetachmentOrder.None => "Detached — free-fighting",
                DetachmentOrder.Hold => "Holding position",
                DetachmentOrder.Follow => "Following formation",
                DetachmentOrder.Navigate => "Navigating",
                DetachmentOrder.Charge => "Charging nearest enemy",
                DetachmentOrder.Flank => "Flanking enemy formation",
                DetachmentOrder.Gate => $"Gate — {(s.GateTarget == null || s.GateTarget.IsGateOpen ? "holding chokepoint" : "targeting gate")}",
                DetachmentOrder.Walls => $"Walls — target {s.WallCandidateIndex + 1}/{s.WallCandidates?.Count ?? 0}",
                _ => "Unknown"
            };
        }

        // ── Commands ──────────────────────────────────────────────────────────

        public string Detach(Agent agent)
        {
            if (agent == null || !agent.IsActive()) return "Invalid agent";
            if (_detachments.ContainsKey(agent)) return "Already detached";

            var formation = agent.Formation;
            if (formation == null) return "Agent has no formation";

            try
            {
                if (agent.IsDetachedFromFormation)
                {
                    agent.TryAttachToFormation();
                    if (agent.IsDetachedFromFormation) return "Could not clear prior detachment";
                }

                var detachment = new HeroDetachment(formation);
                formation.JoinDetachment(detachment);
                detachment.AddAgentAtSlotIndex(agent, 0);

                _detachments[agent] = new DetachmentState
                {
                    Detachment = detachment,
                    Order = DetachmentOrder.None,
                    LastCheckedPosition = agent.Position.AsVec2,
                    LastPositionCheckTime = Mission.Current.CurrentTime,
                };
                return null;
            }
            catch (Exception e)
            {
                Log.Error($"Detach ({agent?.Name}): {e.Message}");
#if DEBUG
                Log.Trace(e.StackTrace);
#endif
                return "Detach failed (engine error)";
            }
        }

        public string Attach(Agent agent)
        {
            if (agent == null) return "Invalid agent";
            if (!_detachments.TryGetValue(agent, out var s)) return "Not detached";
            CleanupDetachment(agent, s);
            return null;
        }

        public string Charge(Agent agent)
        {
            if (agent == null || !agent.IsActive()) return "Invalid agent";
            if (!_detachments.TryGetValue(agent, out var state)) return "Not detached";

            agent.HumanAIComponent?.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.Charge);
            agent.SetAutomaticTargetSelection(true);
            agent.DisableScriptedCombatMovement();
            agent.SetScriptedCombatFlags(Agent.AISpecialCombatModeFlags.None);
            state.Order = DetachmentOrder.Charge;
            state.LastRetargetTime = -99f;
            return null;
        }

        public string Hold(Agent agent)
        {
            if (agent == null || !agent.IsActive()) return "Invalid agent";
            if (!_detachments.TryGetValue(agent, out var state)) return "Not detached";

            state.HoldPosition = agent.GetWorldPosition();
            state.Order = DetachmentOrder.Hold;
            ApplyHold(agent, state);
            return null;
        }

        public string Follow(Agent agent)
        {
            if (agent == null || !agent.IsActive()) return "Invalid agent";
            if (!_detachments.TryGetValue(agent, out var state)) return "Not detached";
            if (state.Detachment?.ParentFormation == null) return "No parent formation";

            agent.DisableScriptedCombatMovement();
            agent.SetScriptedFlags(Agent.AIScriptedFrameFlags.None);
            agent.HumanAIComponent?.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.DefaultDetached);
            state.Order = DetachmentOrder.Follow;
            return null;
        }

        /// <summary>
        /// Navigates to the closest lateral flank of the nearest enemy formation, then charges.
        /// </summary>
        public string Flank(Agent agent)
        {
            if (agent == null || !agent.IsActive()) return "Invalid agent";
            if (!_detachments.TryGetValue(agent, out var state)) return "Not detached";

            var enemy = agent.Formation?.CachedClosestEnemyFormation?.Formation;
            if (enemy == null || enemy.CountOfUnits == 0) return "No visible enemy formation";

            var pos = ComputeClosestFlankPosition(agent, enemy);
            if (!pos.IsValid) return "Could not compute flank position";

            SetNavigatingAggressively(agent);
            StartNavigation(state, agent, pos);
            state.Order = DetachmentOrder.Flank;
            state.LastFlankRefreshTime = Mission.Current.CurrentTime;
            return null;
        }

        /// <summary>
        /// Attacker: scripts the agent to attack the nearest closed gate, retargets automatically,
        /// and holds the chokepoint once all gates are open.
        /// Defender: navigates to a position ~3 m inside the gate and holds there.
        /// </summary>
        public string Gate(Agent agent)
        {
            if (!Mission.Current.IsSiegeBattle) return "Not a siege battle";
            if (agent == null || !agent.IsActive()) return "Invalid agent";
            if (!_detachments.TryGetValue(agent, out var state)) return "Not detached";

            var gate = FindNearestTargetableGate(agent);
            if (gate == null) return "No valid gate found";

            state.Order = DetachmentOrder.Gate;
            state.GateTarget = gate;
            state.HoldPosition = WorldPosition.Invalid;
            state.LastGateCheckTime = Mission.Current.CurrentTime;

            ApplyGateOrder(agent, state);
            return null;
        }

        /// <summary>
        /// Attacker: moves to nearest siege tower or ladder standing point and waits for the engine
        /// to trigger automatic climbing. Transitions to free-fight once inside the castle.
        /// Defender: moves to the defender side of the enemy's assault point.
        /// Re-issuing while already on Walls cycles to the next closest target.
        /// </summary>
        public string Walls(Agent agent)
        {
            if (!Mission.Current.IsSiegeBattle) return "Not a siege battle";
            if (agent == null || !agent.IsActive()) return "Invalid agent";
            if (!_detachments.TryGetValue(agent, out var state)) return "Not detached";

            bool cycling = state.Order == DetachmentOrder.Walls &&
                           state.WallCandidates != null &&
                           state.WallCandidates.Count > 1;

            if (!cycling)
            {
                var candidates = BuildWallCandidates(agent);
                if (candidates.Count == 0) return "No valid walls target found";
                state.WallCandidates = candidates;
                state.WallCandidateIndex = 0;
            }
            else
            {
                state.WallCandidateIndex = (state.WallCandidateIndex + 1) % state.WallCandidates.Count;
            }

            state.Order = DetachmentOrder.Walls;
            state.WallObjectReached = false;
            state.StuckCount = 0;

            ApplyWallsTarget(agent, state);
            return null;
        }

        // ── Mission callbacks ─────────────────────────────────────────────────

        public override void OnMissionTick(float dt)
        {
            _tickBuffer.Clear();
            _tickBuffer.AddRange(_detachments);

            foreach (var kvp in _tickBuffer)
            {
                var agent = kvp.Key;
                var state = kvp.Value;

                if (!_detachments.ContainsKey(agent)) continue;
                if (!agent.IsActive()) continue;

                // ── Prevent morale-based fleeing while detached ───────────────
                if (agent.IsRetreating())
                    agent.StopRetreatingMoraleComponent();

                // ── Order tick ────────────────────────────────────────────────
                switch (state.Order)
                {
                    case DetachmentOrder.Hold: ApplyHold(agent, state); break;
                    case DetachmentOrder.Follow: ApplyFollow(agent, state); break;
                    case DetachmentOrder.Navigate: TickNavigate(agent, state); break;
                    case DetachmentOrder.Charge: TickCharge(agent, state); break;
                    case DetachmentOrder.Flank: TickFlank(agent, state); break;
                    case DetachmentOrder.Gate: TickGate(agent, state); break;
                    case DetachmentOrder.Walls: TickWalls(agent, state); break;
                }
            }
        }

        public override void OnAgentRemoved(Agent killed, Agent killer,
            AgentState agentState, KillingBlow blow)
        {
            if (killed != null && _detachments.TryGetValue(killed, out var s))
                CleanupDetachmentOnDeath(killed, s);
        }

        public override void OnAgentDeleted(Agent agent)
        {
            if (agent != null) _detachments.Remove(agent);
        }

        protected override void OnEndMission()
        {
            _detachments.Clear();
            _tickBuffer.Clear();
        }

        // ── Order tick implementations ────────────────────────────────────────

        private static void ApplyHold(Agent agent, DetachmentState state)
        {
            if (!state.HoldPosition.IsValid) return;
            agent.SetAutomaticTargetSelection(true);
            agent.DisableScriptedCombatMovement();
            var pos = state.HoldPosition;
            agent.SetScriptedPosition(ref pos, false, Agent.AIScriptedFrameFlags.NeverSlowDown);
        }

        private static void ApplyFollow(Agent agent, DetachmentState state)
        {
            var parent = state.Detachment?.ParentFormation;
            if (parent == null || parent.CountOfUnits == 0) return;
            var median = parent.CachedMedianPosition;
            if (!median.IsValid) return;

            Vec2 behind = -parent.Direction * (parent.Depth * 0.5f + 4f);
            var targetPos = median;
            targetPos.SetVec2(median.AsVec2 + behind);
            if (!targetPos.IsValid) return;

            agent.SetAutomaticTargetSelection(true);
            agent.DisableScriptedCombatMovement();
            agent.SetScriptedPosition(ref targetPos, false, Agent.AIScriptedFrameFlags.None);
        }

        private static void TickNavigate(Agent agent, DetachmentState state)
        {
            RunNavigateTick(agent, state, onArrived: null, onStuck: null);
        }

        /// <summary>
        /// Core navigation tick with arrival detection, position-based stuck detection,
        /// and velocity-based stuck detection.
        /// Returns false when the agent has arrived or given up.
        /// </summary>
        private static bool RunNavigateTick(Agent agent, DetachmentState state,
            Action onArrived, Action onStuck,
            float arrivedDistSq = 16f)
        {
            const float ReissueInterval = 1.5f;
            const float StuckCheckPeriod = 3f;
            const float StuckPosDeltaSq = 4f;    // < 2 m moved since last check → stuck
            const float StuckVelThreshold = 0.05f; // almost no velocity → probably stuck

            if (!state.NavigationTarget.IsValid) return false;

            float now = Mission.Current.CurrentTime;
            float distSq = agent.Position.AsVec2.DistanceSquared(state.NavigationTarget.AsVec2);

            // ── Arrived ───────────────────────────────────────────────────────
            if (distSq < arrivedDistSq)
            {
                if (onArrived != null) { onArrived(); return false; }
                state.HoldPosition = agent.GetWorldPosition();
                state.Order = DetachmentOrder.Hold;
                ClearNavigatingAggressively(agent);
                ApplyHold(agent, state);
                return false;
            }

            // ── Stuck detection ───────────────────────────────────────────────
            if (now - state.LastPositionCheckTime > StuckCheckPeriod)
            {
                float movedSq = agent.Position.AsVec2.DistanceSquared(state.LastCheckedPosition);
                float velSq = agent.AverageVelocity.AsVec2.LengthSquared;
                bool stuck = movedSq < StuckPosDeltaSq && velSq < StuckVelThreshold;

                if (stuck)
                {
                    state.StuckCount++;
                    if (state.StuckCount >= 2)
                    {
                        if (onStuck != null) { onStuck(); return false; }
                        state.HoldPosition = agent.GetWorldPosition();
                        state.Order = DetachmentOrder.Hold;
                        ClearNavigatingAggressively(agent);
                        ApplyHold(agent, state);
                        return false;
                    }
                    // Force an immediate reissue with the same target (sometimes helps clear obstacles)
                    state.LastNavigationReissueTime = -99f;
                }
                else
                {
                    state.StuckCount = 0;
                }

                state.LastCheckedPosition = agent.Position.AsVec2;
                state.LastPositionCheckTime = now;
            }

            // ── Periodic reissue ──────────────────────────────────────────────
            if (now - state.LastNavigationReissueTime > ReissueInterval)
            {
                state.LastNavigationReissueTime = now;
                var pos = state.NavigationTarget;
                agent.SetScriptedPosition(ref pos, false, Agent.AIScriptedFrameFlags.NeverSlowDown);
            }

            return true;
        }

        private static void TickCharge(Agent agent, DetachmentState state)
        {
            const float RetargetInterval = 2.5f;
            float now = Mission.Current.CurrentTime;
            if (now - state.LastRetargetTime < RetargetInterval) return;
            state.LastRetargetTime = now;

            var nearest = FindNearestEnemy(agent);
            if (nearest == null)
            {
                state.HoldPosition = agent.GetWorldPosition();
                state.Order = DetachmentOrder.Hold;
                return;
            }
            var pos = nearest.GetWorldPosition();
            agent.SetScriptedPosition(ref pos, false, Agent.AIScriptedFrameFlags.NeverSlowDown);
        }

        private void TickFlank(Agent agent, DetachmentState state)
        {
            const float FlankRefresh = 4f;

            void OnArrived()
            {
                ClearNavigatingAggressively(agent);
                state.Order = DetachmentOrder.Charge;
                state.LastRetargetTime = -99f;
            }
            void OnStuck()
            {
                // Couldn't reach flank — just charge from current position
                ClearNavigatingAggressively(agent);
                state.Order = DetachmentOrder.Charge;
                state.LastRetargetTime = -99f;
            }

            if (!RunNavigateTick(agent, state, OnArrived, OnStuck, arrivedDistSq: 25f)) return;

            float now = Mission.Current.CurrentTime;
            if (now - state.LastFlankRefreshTime > FlankRefresh)
            {
                state.LastFlankRefreshTime = now;
                var enemy = agent.Formation?.CachedClosestEnemyFormation?.Formation;
                if (enemy != null && enemy.CountOfUnits > 0)
                {
                    var newTarget = ComputeClosestFlankPosition(agent, enemy);
                    if (newTarget.IsValid)
                    {
                        state.NavigationTarget = newTarget;
                        state.LastNavigationReissueTime = -99f;
                    }
                }
            }
        }

        // ── Gate tick ─────────────────────────────────────────────────────────

        private static void TickGate(Agent agent, DetachmentState state)
        {
            const float GateCheckInterval = 2f;

            // Melee interrupt — stop re-issuing movement commands while actively fighting
            if (IsInMeleeCombat(agent))
            {
                state.LastMeleeInterruptTime = Mission.Current.CurrentTime;
                agent.DisableScriptedMovement();
                agent.SetAutomaticTargetSelection(true);
                return;
            }

            // Brief grace period after melee ends so we don't immediately re-path
            if (Mission.Current.CurrentTime - state.LastMeleeInterruptTime < 1.5f) return;

            float now = Mission.Current.CurrentTime;
            if (now - state.LastGateCheckTime < GateCheckInterval) return;
            state.LastGateCheckTime = now;

            ApplyGateOrder(agent, state);
        }

        private static void ApplyGateOrder(Agent agent, DetachmentState state)
        {
            if (agent.Team.IsAttacker) ApplyGateAttacker(agent, state);
            else ApplyGateDefender(agent, state);
        }

        private static void ApplyGateAttacker(Agent agent, DetachmentState state)
        {
            var gate = state.GateTarget;

            // Gate gone or open — look for another closed one
            if (gate == null || gate.IsGateOpen)
            {
                var newGate = FindNearestTargetableGate(agent);
                if (newGate != null)
                {
                    state.GateTarget = newGate;
                    gate = newGate;
                }
                else
                {
                    // All gates open/destroyed — hold the chokepoint
                    if (!state.HoldPosition.IsValid)
                    {
                        Vec3 holdOrigin = gate != null
                            ? gate.GameEntity.GlobalPosition
                            : agent.Position;
                        state.HoldPosition = new WorldPosition(Mission.Current.Scene,
                            UIntPtr.Zero, holdOrigin, false);
                    }
                    state.Order = DetachmentOrder.Hold;
                    ClearNavigatingAggressively(agent);
                    ApplyHold(agent, state);
                    return;
                }
            }

            agent.DisableScriptedMovement();
            agent.SetScriptedTargetEntity(
                gate.GameEntity,
                Agent.AISpecialCombatModeFlags.AttackEntity,
                true);
        }

        private static void ApplyGateDefender(Agent agent, DetachmentState state)
        {
            var gate = state.GateTarget;
            if (gate == null)
            {
                gate = FindNearestTargetableGate(agent);
                if (gate == null) { state.Order = DetachmentOrder.None; return; }
                state.GateTarget = gate;
            }

            WorldPosition defendPos = ComputeGateDefenderPosition(gate);
            if (!defendPos.IsValid) return;

            float distSq = agent.Position.AsVec2.DistanceSquared(defendPos.AsVec2);
            if (distSq < 9f) // ~3 m — in position
            {
                if (!state.HoldPosition.IsValid)
                    state.HoldPosition = agent.GetWorldPosition();
                ApplyHold(agent, state);
                return;
            }

            agent.HumanAIComponent?.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.DefaultDetached);
            agent.SetAutomaticTargetSelection(true);
            agent.SetScriptedPosition(ref defendPos, false, Agent.AIScriptedFrameFlags.NeverSlowDown);
        }

        private static WorldPosition ComputeGateDefenderPosition(CastleGate gate)
        {
            // Stand a few metres inside the gate (castle-interior side) to block the passage
            MatrixFrame gf = gate.MiddleFrame.ToGroundMatrixFrame();
            Vec3 interior = gf.origin + gf.rotation.f * -3f;
            return new WorldPosition(Mission.Current.Scene, UIntPtr.Zero, interior, false);
        }

        // ── Walls tick ────────────────────────────────────────────────────────

        private static void TickWalls(Agent agent, DetachmentState state)
        {
            if (state.WallCandidates == null || state.WallCandidates.Count == 0)
            { state.Order = DetachmentOrder.None; return; }

            // Melee interrupt — stop pathing, let the agent fight
            if (IsInMeleeCombat(agent))
            {
                state.LastMeleeInterruptTime = Mission.Current.CurrentTime;
                agent.DisableScriptedMovement();
                agent.SetAutomaticTargetSelection(true);
                return;
            }

            // Brief grace period after melee ends
            if (Mission.Current.CurrentTime - state.LastMeleeInterruptTime < 1.5f) return;

            if (agent.Team.IsAttacker) TickWallsAttacker(agent, state);
            else TickWallsDefender(agent, state);
        }

        private static void TickWallsAttacker(Agent agent, DetachmentState state)
        {
            // Inside castle = climbed successfully → free-fight
            if (agent.GetCurrentNavigationFaceId() % 10 == 1)
            {
                ClearNavigatingAggressively(agent);
                agent.SetAutomaticTargetSelection(true);
                state.Order = DetachmentOrder.None;
                return;
            }

            var candidate = state.WallCandidates[state.WallCandidateIndex];
            float distSq = agent.Position.AsVec2.DistanceSquared(candidate.Position.AsVec2);

            // Close enough — stop scripting movement so the engine's game-object logic takes over
            if (distSq < 6.25f)
            {
                if (!state.WallObjectReached)
                {
                    state.WallObjectReached = true;
                    agent.DisableScriptedMovement();
                    agent.SetAutomaticTargetSelection(true);
                    agent.HumanAIComponent?.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.Default);
                }
                return;
            }

            state.WallObjectReached = false;

            void OnStuck()
            {
                if (state.WallCandidates.Count > 1)
                {
                    state.WallCandidateIndex = (state.WallCandidateIndex + 1) % state.WallCandidates.Count;
                    state.StuckCount = 0;
                    ApplyWallsTarget(agent, state);
                }
                else
                {
                    state.Order = DetachmentOrder.None;
                    ClearNavigatingAggressively(agent);
                }
            }

            RunNavigateTick(agent, state, onArrived: null, onStuck: OnStuck, arrivedDistSq: 6.25f);
        }

        private static void TickWallsDefender(Agent agent, DetachmentState state)
        {
            var candidate = state.WallCandidates[state.WallCandidateIndex];
            float distSq = agent.Position.AsVec2.DistanceSquared(candidate.Position.AsVec2);

            if (distSq < 16f) // ~4 m — in position
            {
                if (!state.WallObjectReached)
                {
                    state.WallObjectReached = true;
                    state.HoldPosition = agent.GetWorldPosition();
                    ClearNavigatingAggressively(agent);
                    ApplyHold(agent, state);
                }
                return;
            }

            state.WallObjectReached = false;

            void OnStuck()
            {
                if (state.WallCandidates.Count > 1)
                {
                    state.WallCandidateIndex = (state.WallCandidateIndex + 1) % state.WallCandidates.Count;
                    state.StuckCount = 0;
                    ApplyWallsTarget(agent, state);
                }
                else
                {
                    state.Order = DetachmentOrder.None;
                    ClearNavigatingAggressively(agent);
                }
            }

            RunNavigateTick(agent, state, onArrived: null, onStuck: OnStuck);
        }

        private static void ApplyWallsTarget(Agent agent, DetachmentState state)
        {
            if (state.WallCandidates == null || state.WallCandidateIndex < 0) return;

            var target = state.WallCandidates[state.WallCandidateIndex];
            SetNavigatingAggressively(agent);

            state.NavigationTarget = target.Position;
            state.LastNavigationReissueTime = Mission.Current.CurrentTime;
            state.LastPositionCheckTime = Mission.Current.CurrentTime;
            state.LastCheckedPosition = agent.Position.AsVec2;
            state.StuckCount = 0;

            var pos = target.Position;
            agent.SetScriptedPosition(ref pos, false, Agent.AIScriptedFrameFlags.NeverSlowDown);
        }

        // ── Wall candidate builders ───────────────────────────────────────────

        private static List<WallCandidate> BuildWallCandidates(Agent agent)
            => agent.Team.IsAttacker
               ? BuildAttackerWallCandidates(agent)
               : BuildDefenderWallCandidates(agent);

        private static List<WallCandidate> BuildAttackerWallCandidates(Agent agent)
        {
            var result = new List<WallCandidate>();
            var scene = Mission.Current.Scene;

            foreach (var obj in Mission.Current.ActiveMissionObjects)
            {
                if (obj is SiegeTower tower)
                {
                    if (tower.IsDestroyed || tower.IsDisabled) continue;

                    StandingPoint bestSP = null;
                    float bestDSq = float.MaxValue;

                    foreach (var sp in tower.StandingPoints)
                    {
                        if (sp == null || sp.IsDeactivated || !sp.GameEntity.HasTag("move")) continue;
                        float d = sp.GameEntity.GlobalPosition.DistanceSquared(agent.Position);
                        if (d < bestDSq) { bestDSq = d; bestSP = sp; }
                    }

                    Vec3 rawPos = bestSP != null ? bestSP.GameEntity.GlobalPosition
                                                  : tower.GameEntity.GlobalPosition;
                    float distSq = rawPos.DistanceSquared(agent.Position);
                    result.Add(new WallCandidate(new WorldPosition(scene, UIntPtr.Zero, rawPos, false), distSq));
                    continue;
                }

                if (obj is SiegeLadder ladder)
                {
                    if (ladder.IsDisabled || ladder.IsDeactivated) continue;

                    float bestDSq = float.MaxValue;
                    Vec3 bestPos = default;
                    bool found = false;

                    foreach (var sp in ladder.StandingPoints)
                    {
                        if (sp == null || sp.IsDeactivated) continue;
                        float d = sp.GameEntity.GlobalPosition.DistanceSquared(agent.Position);
                        if (d < bestDSq) { bestDSq = d; bestPos = sp.GameEntity.GlobalPosition; found = true; }
                    }

                    if (found)
                        result.Add(new WallCandidate(new WorldPosition(scene, UIntPtr.Zero, bestPos, false), bestDSq));
                }
            }

            result.Sort((a, b) => a.SortKey.CompareTo(b.SortKey));
            return result;
        }

        private static List<WallCandidate> BuildDefenderWallCandidates(Agent agent)
        {
            var result = new List<WallCandidate>();
            var scene = Mission.Current.Scene;

            foreach (var obj in Mission.Current.ActiveMissionObjects)
            {
                if (obj is SiegeTower tower)
                {
                    if (tower.IsDestroyed || tower.IsDisabled) continue;

                    var wallSeg = tower.TargetCastlePosition as WallSegment;
                    if (wallSeg == null) continue;

                    var defPos = wallSeg.MiddlePosition?.Position;
                    if (defPos == null || !defPos.Value.IsValid)
                        defPos = new WorldPosition(scene, UIntPtr.Zero, wallSeg.GameEntity.GlobalPosition, false);

                    float d = defPos.Value.GetNavMeshVec3().DistanceSquared(agent.Position);
                    result.Add(new WallCandidate(defPos.Value, d));
                    continue;
                }

                if (obj is SiegeLadder ladder)
                {
                    if (ladder.IsDisabled || ladder.IsDeactivated) continue;

                    var wallSeg = ladder.TargetCastlePosition as WallSegment;
                    if (wallSeg == null) continue;

                    var defPos = wallSeg.MiddlePosition?.Position;
                    if (defPos == null || !defPos.Value.IsValid)
                        defPos = new WorldPosition(scene, UIntPtr.Zero, wallSeg.GameEntity.GlobalPosition, false);

                    float d = defPos.Value.GetNavMeshVec3().DistanceSquared(agent.Position);
                    result.Add(new WallCandidate(defPos.Value, d));
                    continue;
                }

                if (obj is WallSegment seg && seg.IsBreachedWall)
                {
                    var pos = seg.MiddlePosition?.Position;
                    if (pos == null || !pos.Value.IsValid)
                        pos = new WorldPosition(scene, UIntPtr.Zero, seg.GameEntity.GlobalPosition, false);

                    float d = pos.Value.GetNavMeshVec3().DistanceSquared(agent.Position);
                    result.Add(new WallCandidate(pos.Value, d));
                }
            }

            result.Sort((a, b) => a.SortKey.CompareTo(b.SortKey));
            return result;
        }

        // ── Static helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if the agent was recently in melee (hit or attacked within MeleePauseWindow).
        /// Used to interrupt wall/gate pathing so the agent can fight naturally.
        /// </summary>
        private static bool IsInMeleeCombat(Agent agent)
        {
            float now = MBCommon.GetTotalMissionTime();

            // Being struck right now
            if (agent.IsInBeingStruckAction) return true;

            // Was hit recently
            if (agent.LastMeleeHitTime > 0f && now - agent.LastMeleeHitTime < MeleePauseWindow)
                return true;

            // Struck someone recently (we're the attacker)
            if (agent.LastMeleeAttackTime > 0f && now - agent.LastMeleeAttackTime < MeleePauseWindow)
                return true;

            return false;
        }

        private static void StartNavigation(DetachmentState state, Agent agent, WorldPosition target)
        {
            state.NavigationTarget = target;
            state.LastNavigationReissueTime = Mission.Current.CurrentTime;
            state.LastPositionCheckTime = Mission.Current.CurrentTime;
            state.LastCheckedPosition = agent.Position.AsVec2;
            state.StuckCount = 0;

            var pos = target;
            agent.SetScriptedPosition(ref pos, false, Agent.AIScriptedFrameFlags.NeverSlowDown);
        }

        private static WorldPosition ComputeClosestFlankPosition(Agent agent, Formation enemy)
        {
            Vec2 center = enemy.CachedAveragePosition;
            Vec2 facing = enemy.Direction.Normalized();
            Vec2 leftP = new Vec2(-facing.Y, facing.X);
            Vec2 rightP = new Vec2(facing.Y, -facing.X);

            float lateral = enemy.Width * 0.5f + 18f;
            Vec2 rearShift = -facing * 5f;

            Vec2 leftPos = center + leftP * lateral + rearShift;
            Vec2 rightPos = center + rightP * lateral + rearShift;

            Vec2 agentPos = agent.Position.AsVec2;
            Vec2 chosen = agentPos.DistanceSquared(leftPos) < agentPos.DistanceSquared(rightPos)
                            ? leftPos : rightPos;

            var scene = Mission.Current.Scene;
            float z = enemy.CachedMedianPosition.GetNavMeshZ();
            var wp = new WorldPosition(scene, UIntPtr.Zero, new Vec3(chosen.X, chosen.Y, z), false);

            if (wp.GetNavMeshMT() == UIntPtr.Zero)
            {
                Vec2 fb = chosen + (agentPos - chosen).Normalized() * 10f;
                wp = new WorldPosition(scene, UIntPtr.Zero, new Vec3(fb.X, fb.Y, agent.Position.Z), false);
            }

            return wp;
        }

        private static CastleGate FindNearestTargetableGate(Agent agent)
        {
            CastleGate nearest = null;
            float nearDist = float.MaxValue;

            foreach (var obj in Mission.Current.ActiveMissionObjects)
            {
                if (obj is not CastleGate gate) continue;
                if (agent.Team.IsAttacker && gate.IsGateOpen) continue; // attacker wants closed gates
                float d = gate.GameEntity.GlobalPosition.DistanceSquared(agent.Position);
                if (d < nearDist) { nearDist = d; nearest = gate; }
            }
            return nearest;
        }

        private static Agent FindNearestEnemy(Agent agent)
        {
            Agent nearest = null;
            float nearDist = float.MaxValue;
            foreach (var team in Mission.Current.Teams)
            {
                if (team == null || !team.IsEnemyOf(agent.Team)) continue;
                foreach (var enemy in team.ActiveAgents)
                {
                    if (enemy == null || !enemy.IsActive() || enemy.IsMount) continue;
                    float d = enemy.Position.DistanceSquared(agent.Position);
                    if (d < nearDist) { nearDist = d; nearest = enemy; }
                }
            }
            return nearest;
        }

        private static void SetNavigatingAggressively(Agent agent)
        {
            agent.SetAutomaticTargetSelection(false);
            agent.HumanAIComponent?.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.DefaultDetached);
            agent.SetScriptedFlags(agent.GetScriptedFlags() | Agent.AIScriptedFrameFlags.NeverSlowDown);
        }

        private static void ClearNavigatingAggressively(Agent agent)
        {
            agent.SetAutomaticTargetSelection(true);
            agent.HumanAIComponent?.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.Default);
            agent.SetScriptedFlags(agent.GetScriptedFlags() & ~Agent.AIScriptedFrameFlags.NeverSlowDown);
        }

        // ── Cleanup ───────────────────────────────────────────────────────────

        /// <summary>
        /// Safe death cleanup. Must NOT call formation.AttachUnit on a dying/dead agent.
        /// Remove the agent from our detachment internal list FIRST so that the subsequent
        /// LeaveDetachment → OnFormationLeave call finds an empty agent list and skips AttachUnit.
        /// </summary>
        private void CleanupDetachmentOnDeath(Agent agent, DetachmentState state)
        {
            // Remove from our dict immediately — prevents the tick loop from processing this agent again
            _detachments.Remove(agent);

            var detachment = state.Detachment;
            var formation = agent.Formation;

            // 1. Remove from detachment's internal list FIRST.
            //    This makes OnFormationLeave a no-op for this agent (it won't be found in _agents).
            try { detachment?.RemoveAgent(agent); } catch { }

            // 2. Leave detachment cleanly — OnFormationLeave will iterate _agents but won't
            //    find the dead agent (already removed above), so no AttachUnit is attempted.
            try { formation?.LeaveDetachment(detachment); } catch { }

            // 3. Clear engine flags — guard because agent may already be partially torn down
            try { agent.DisableScriptedMovement(); } catch { }
            try { agent.DisableScriptedCombatMovement(); } catch { }
        }

        private void CleanupDetachment(Agent agent, DetachmentState state)
        {
            ClearNavigatingAggressively(agent);

            var detachment = state.Detachment;
            var formation = agent.Formation;

            // Remove agent from detachment's list before LeaveDetachment to prevent
            // a redundant AttachUnit call inside OnFormationLeave
            detachment?.RemoveAgent(agent);

            if (formation != null)
            {
                formation.LeaveDetachment(detachment);
                agent.TryRemoveAllDetachmentScores();
                formation.AttachUnit(agent);
            }

            _detachments.Remove(agent);
        }
    }

    // ── HeroDetachment ────────────────────────────────────────────────────────

    internal class HeroDetachment : IDetachment
    {
        public Formation ParentFormation { get; }

        private readonly MBList<Formation> _userFormations = new();
        private readonly List<Agent> _agents = new();

        public MBReadOnlyList<Formation> UserFormations => _userFormations;
        public bool IsLoose => true;

        public HeroDetachment(Formation parent) { ParentFormation = parent; }

        public void AddAgent(Agent agent, int slotIndex = -1,
            Agent.AIScriptedFrameFlags customFlags = Agent.AIScriptedFrameFlags.None)
        {
            if (agent == null || _agents.Contains(agent)) return;
            _agents.Add(agent);
        }

        /// <summary>
        /// Removes the agent from its formation grid and places it in this detachment.
        /// Skips DetachUnit when the agent has no valid grid position (file index == -1),
        /// which would otherwise crash LineFormation.RemoveUnit.
        /// </summary>
        public void AddAgentAtSlotIndex(Agent agent, int slotIndex)
        {
            if (agent == null || _agents.Contains(agent)) return;
            _agents.Add(agent);

            var formation = agent.Formation;
            if (formation != null && !agent.IsDetachedFromFormation)
            {
                int fi = ((IFormationUnit)agent).FormationFileIndex;
                int ri = ((IFormationUnit)agent).FormationRankIndex;

                if (fi >= 0 && ri >= 0)
                {
                    try { formation.DetachUnit(agent, IsLoose); }
                    catch (Exception e)
                    {
                        Log.Error($"HeroDetachment.DetachUnit ({agent.Name}): {e.Message}");
#if DEBUG
                        Log.Trace(e.StackTrace);
#endif
                    }
                }
                // fi == -1 means unpositioned — skip DetachUnit to avoid grid array crash
            }

            agent.Detachment = this;
            agent.SetDetachmentWeight(1f);
        }

        public void RemoveAgent(Agent agent)
        {
            if (agent == null || !_agents.Contains(agent)) return;
            _agents.Remove(agent);
            try { agent.DisableScriptedMovement(); } catch { }
            try { agent.DisableScriptedCombatMovement(); } catch { }
        }

        public void FormationStartUsing(Formation formation)
        {
            if (formation != null && !_userFormations.Contains(formation))
                _userFormations.Add(formation);
        }

        public void FormationStopUsing(Formation formation)
        {
            if (formation != null) _userFormations.Remove(formation);
        }

        public bool IsUsedByFormation(Formation formation)
            => formation != null && _userFormations.Contains(formation);

        /// <summary>
        /// Called by Formation.LeaveDetachment. Because CleanupDetachmentOnDeath calls
        /// RemoveAgent before LeaveDetachment, dying agents will NOT be found in _agents here,
        /// preventing AttachUnit from being called on a dead/dying agent.
        /// </summary>
        public void OnFormationLeave(Formation formation)
        {
            if (formation == null) return;
            for (int i = _agents.Count - 1; i >= 0; i--)
            {
                var a = _agents[i];
                if (a == null || a.Formation != formation) continue;

                RemoveAgent(a);

                // Only reattach if genuinely alive — guards against edge cases where
                // this is called during agent removal before State has updated to Dead
                if (a.IsActive() && a.State == AgentState.Active)
                    try { formation.AttachUnit(a); } catch { }
            }
        }

        public WorldFrame? GetAgentFrame(Agent agent) => null;

        public bool IsAgentUsingOrInterested(Agent agent) => agent != null && _agents.Contains(agent);
        public bool IsAgentEligible(Agent agent) => agent != null && _agents.Contains(agent);
        public bool IsStandingPointAvailableForAgent(Agent agent) => false;
        public int GetNumberOfUsableSlots() => int.MaxValue;

        public Agent GetMovingAgentAtSlotIndex(int slotIndex)
            => slotIndex >= 0 && slotIndex < _agents.Count ? _agents[slotIndex] : null;

        // Return degenerate values so the engine's detachment manager never auto-pulls
        // foreign agents into this private detachment
        public float GetDetachmentWeight(BattleSideEnum s) => float.MinValue;
        public float ComputeAndCacheDetachmentWeight(BattleSideEnum s) => float.MinValue;
        public float GetDetachmentWeightFromCache() => float.MinValue;
        public float? GetWeightOfNextSlot(BattleSideEnum s) => null;
        public float GetWeightOfOccupiedSlot(Agent a) => float.MinValue;

        public float? GetWeightOfAgentAtOccupiedSlot(Agent d, List<Agent> c, out Agent match)
        { match = null; return float.MaxValue; }

        public float? GetWeightOfAgentAtNextSlot(List<Agent> c, out Agent match)
        { match = null; return null; }

        public float? GetWeightOfAgentAtNextSlot(List<ValueTuple<Agent, float>> s, out Agent match)
        { match = null; return null; }

        public float GetTemplateWeightOfAgent(Agent c) => float.MaxValue;
        public List<float> GetTemplateCostsOfAgent(Agent c, List<float> old) => old ?? new List<float>();
        public float GetExactCostOfAgentAtSlot(Agent c, int idx) => float.MaxValue;

        public void GetSlotIndexWeightTuples(List<ValueTuple<int, float>> t) { }
        public bool IsSlotAtIndexAvailableForAgent(int idx, Agent a) => false;
        public void MarkSlotAtIndex(int idx) { }
        public void UnmarkDetachment() { }
        public bool IsDetachmentRecentlyEvaluated() => true;
        public void ResetEvaluation() { }
        public bool IsEvaluated() => true;
        public void SetAsEvaluated() { }
    }
}