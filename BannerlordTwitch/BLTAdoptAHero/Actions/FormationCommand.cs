using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using BLTAdoptAHero;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

// ============================================================
//  FormationCommand
// ============================================================

namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("{=TESTING}FormationCommand"),
     LocDescription("{=TESTING}Show and change hero formation"),
     UsedImplicitly]
    public class FormationCommand : HeroCommandHandlerBase
    {
        public class Settings : IDocumentable
        {
            [LocDisplayName("{=TESTING}Respect class"),
             LocCategory("General", "{=TESTING}General"),
             LocDescription("{=TESTING}Turn off to allow any formation otherwise infantry can only change to other infantry formations"),
             PropertyOrder(1), UsedImplicitly]
            public bool Filter { get; set; } = true;

            [LocDisplayName("{=TESTING}Detachments"),
             LocCategory("General", "{=TESTING}General"),
             LocDescription("{=TESTING}Enable detach commands"),
             PropertyOrder(2), UsedImplicitly]
            public bool Detach { get; set; } = true;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.Value("<strong>Usage:</strong> [number | keyword]");
                generator.Value("No arg: show current formation info");
                generator.Value("number: switch to formation N");
                generator.Value("front / back: move to front or back rank");
                generator.Value("detach / attach: detach from or reattach to formation");
                generator.Value("(while detached): charge / hold / follow / gate / walls");
            }
        }

        public override Type HandlerConfigType => typeof(Settings);

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (config is not Settings settings) return;

            if (adoptedHero == null) { onFailure(AdoptAHero.NoHeroMessage); return; }
            if (Mission.Current == null) { onFailure("No mission"); return; }
            if (Mission.Current.IsNavalBattle) { onFailure("Cannot change formation in naval battle"); return; }
            if (MissionHelpers.InTournament()) { onFailure("Cannot change formation in tournament"); return; }

            var agent = adoptedHero.GetAgent();
            if (agent == null) { onFailure("Hero not in mission"); return; }

            var formation = agent.Formation;
            if (formation == null) { onFailure("Hero has no formation"); return; }

            var args = context.Args?.Trim() ?? "";
            var parts = args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var cmd = parts.Length > 0 ? parts[0].ToLowerInvariant() : "";

            var behavior = BLTHeroDetachmentBehavior.Current;

            // ---- Detachment commands ----
            var detachKeywords = new[] { "detach", "attach", "charge", "hold", "follow", "gate", "walls" };
            if (detachKeywords.Contains(cmd))
            {
                if (!settings.Detach) { onFailure("Detach commands are disabled"); return; }
                if (behavior == null) { onFailure("Detachment system not active"); return; }
                if (!Mission.Current.IsDeploymentFinished) { onFailure("Cannot detach while deploying"); return; }

                string error = cmd switch
                {
                    "detach" => behavior.Detach(agent),
                    "attach" => behavior.Attach(agent),
                    "charge" => behavior.Charge(agent),
                    "hold" => behavior.Hold(agent),
                    "follow" => behavior.Follow(agent),
                    "gate" => behavior.TargetDoor(agent),
                    "walls" => behavior.Walls(agent),
                    _ => "Unknown detach command"
                };

                if (error != null) onFailure(error);
                else onSuccess($"{cmd} OK");
                return;
            }

            // ---- Position adjustment commands ----
            if (cmd == "front" || cmd == "back")
            {
                if (agent.IsDetachedFromFormation) { onFailure("Reattach first (use: attach)"); return; }
                SetHeroFormationPosition(agent, cmd, onSuccess, onFailure);
                return;
            }

            // ---- Formation list / switch ----
            var detachedMarker = (behavior?.IsDetached(agent) ?? false) ? " [DETACHED]" : "";

            if (settings.Filter)
            {
                // Only formations matching hero's class
                var formType = GetFormationClass(formation);

                var matchingFormations = agent.Team.FormationsIncludingSpecialAndEmpty
                    .Where(f => f.CountOfUnits > 0 && f.PhysicalClass == formType)
                    .OrderBy(f => f.Index)
                    .ToList();

                int total = matchingFormations.Count;
                int position = matchingFormations.IndexOf(formation) + 1;

                // No argument → show info
                if (string.IsNullOrEmpty(cmd))
                {
                    var sb = BuildFormationList(matchingFormations, formation);
                    onSuccess($"{formType} {position}/{total}{detachedMarker} | {GetOrderSummary(formation)} | {sb}");
                    return;
                }

                if (!int.TryParse(cmd, out int target)) { onFailure($"Unknown command: {cmd}"); return; }
                if (agent.IsDetachedFromFormation) { onFailure("Reattach first (use: attach)"); return; }
                if (target < 1 || target > total) { onFailure($"Invalid number. Range: 1-{total}"); return; }

                var dest = matchingFormations[target - 1];
                if (dest == formation) { onSuccess($"Already in formation {target}"); return; }

                TransferHeroToFormation(agent, dest);
                onSuccess($"Moved to {formType} formation {target} ({dest.CountOfUnits} troops)");
            }
            else
            {
                // All non-empty formations
                var allFormations = agent.Team.FormationsIncludingSpecialAndEmpty
                    .Where(f => f.CountOfUnits > 0)
                    .OrderBy(f => f.Index)
                    .ToList();

                int total = allFormations.Count;
                int position = allFormations.IndexOf(formation) + 1;

                if (string.IsNullOrEmpty(cmd))
                {
                    var sb = BuildFormationListFull(allFormations, formation);
                    onSuccess($"Fmn {position}/{total}{detachedMarker} | {GetOrderSummary(formation)} | {sb}");
                    return;
                }

                if (!int.TryParse(cmd, out int target)) { onFailure($"Unknown command: {cmd}"); return; }
                if (agent.IsDetachedFromFormation) { onFailure("Reattach first (use: attach)"); return; }
                if (target < 1 || target > total) { onFailure($"Invalid number. Range: 1-{total}"); return; }

                var dest = allFormations[target - 1];
                if (dest == formation) { onSuccess($"Already in formation {target}"); return; }

                TransferHeroToFormation(agent, dest);
                onSuccess($"Moved to formation {target} ({GetFormationClass(dest)} {dest.CountOfUnits} troops)");
            }
        }

        // ---- Helpers ----

        private static FormationClass GetFormationClass(Formation f)
        {
            var q = f.QuerySystem;
            return q switch
            {
                _ when q.IsInfantryFormationReadOnly => FormationClass.Infantry,
                _ when q.IsRangedFormationReadOnly => FormationClass.Ranged,
                _ when q.IsCavalryFormationReadOnly => FormationClass.Cavalry,
                _ when q.IsRangedCavalryFormationReadOnly => FormationClass.HorseArcher,
                _ => FormationClass.Infantry
            };
        }

        private static string FormationClassName(Formation f) => GetFormationClass(f) switch
        {
            FormationClass.Infantry => "Inf",
            FormationClass.Ranged => "Rng",
            FormationClass.Cavalry => "Cav",
            FormationClass.HorseArcher => "HA",
            _ => "?"
        };

        private static string GetOrderSummary(Formation f)
        {
            var m = f.GetReadonlyMovementOrderReference().OrderEnum;
            var a = f.ArrangementOrder.OrderEnum;
            return $"{MovementAbbrev(m)}-{ArrangementAbbrev(a)}";
        }

        private static string BuildFormationList(List<Formation> formations, Formation current)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < formations.Count; i++)
            {
                var f = formations[i];
                var tag = f == current ? "*" : "";
                sb.Append($"{tag}{i + 1}:{f.CountOfUnits}[{GetOrderSummary(f)}] ");
            }
            return sb.ToString().TrimEnd();
        }

        private static string BuildFormationListFull(List<Formation> formations, Formation current)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < formations.Count; i++)
            {
                var f = formations[i];
                var tag = f == current ? "*" : "";
                sb.Append($"{tag}{i + 1}:{FormationClassName(f)}({f.CountOfUnits})[{GetOrderSummary(f)}] ");
            }
            return sb.ToString().TrimEnd();
        }

        private static void TransferHeroToFormation(Agent heroAgent, Formation target)
        {
            if (heroAgent == null || target == null) return;
            var oldFormation = heroAgent.Formation;
            heroAgent.Formation = target;
            oldFormation?.Team.TriggerOnFormationsChanged(oldFormation);
            target.Team.TriggerOnFormationsChanged(target);
        }

        private static string MovementAbbrev(MovementOrder.MovementOrderEnum o) => o switch
        {
            MovementOrder.MovementOrderEnum.Charge => "Chrg",
            MovementOrder.MovementOrderEnum.ChargeToTarget => "Chrg",
            MovementOrder.MovementOrderEnum.Advance => "Adv",
            MovementOrder.MovementOrderEnum.FallBack => "Fall",
            MovementOrder.MovementOrderEnum.Retreat => "Ret",
            MovementOrder.MovementOrderEnum.Stop => "Hold",
            MovementOrder.MovementOrderEnum.Invalid => "Hold",
            MovementOrder.MovementOrderEnum.Follow => "Fol",
            MovementOrder.MovementOrderEnum.FollowEntity => "FolE",
            MovementOrder.MovementOrderEnum.Move => "Move",
            _ => "?"
        };

        private static string ArrangementAbbrev(ArrangementOrder.ArrangementOrderEnum o) => o switch
        {
            ArrangementOrder.ArrangementOrderEnum.Line => "Ln",
            ArrangementOrder.ArrangementOrderEnum.ShieldWall => "SW",
            ArrangementOrder.ArrangementOrderEnum.Loose => "Lse",
            ArrangementOrder.ArrangementOrderEnum.Square => "Sq",
            ArrangementOrder.ArrangementOrderEnum.Circle => "Cir",
            ArrangementOrder.ArrangementOrderEnum.Column => "Col",
            ArrangementOrder.ArrangementOrderEnum.Scatter => "Sct",
            ArrangementOrder.ArrangementOrderEnum.Skein => "Skn",
            _ => "--"
        };

        // Move hero to front or back of their current formation rank.
        private static void SetHeroFormationPosition(Agent heroAgent, string position,
            Action<string> onSuccess, Action<string> onFailure)
        {
            var formation = heroAgent.Formation;
            if (formation == null) { onFailure("No formation"); return; }

            var unit = heroAgent as IFormationUnit;
            var arrangement = formation.Arrangement;
            int rankCount = arrangement.RankCount;
            int fileCount = arrangement.UnitCount / Math.Max(1, rankCount);

            if (Mission.Current.IsSiegeBattle) { onFailure("Cannot reposition in siege battles"); return; }
            if (rankCount <= 1 && fileCount <= 1) { onFailure("Formation too small to reposition"); return; }

            try
            {
                // Collect non-hero, non-named agents only (don't swap with another named character)
                var candidates = arrangement.GetAllUnits()
                    .Select(u => u as Agent)
                    .Where(a => a != null && a != heroAgent && a.IsActive() && a.GetHero() == null)
                    .ToList();

                if (candidates.Count == 0) { onFailure("No troops to swap with"); return; }

                Agent swapTarget = null;

                if (position == "front")
                {
                    // Pick a random agent from the frontmost file-wide slice
                    int frontRank = candidates.Min(a => ((IFormationUnit)a).FormationRankIndex);
                    var frontCandidates = candidates
                        .Where(a => ((IFormationUnit)a).FormationRankIndex == frontRank)
                        .ToList();
                    swapTarget = frontCandidates.Count > 0
                        ? frontCandidates[MBRandom.RandomInt(frontCandidates.Count)]
                        : null;
                }
                else // back
                {
                    int backRank = candidates.Max(a => ((IFormationUnit)a).FormationRankIndex);
                    var backCandidates = candidates
                        .Where(a => ((IFormationUnit)a).FormationRankIndex == backRank)
                        .ToList();
                    swapTarget = backCandidates.Count > 0
                        ? backCandidates[MBRandom.RandomInt(backCandidates.Count)]
                        : null;
                }

                if (swapTarget == null) { onFailure("No valid swap target found"); return; }

                arrangement.SwitchUnitLocations(unit, swapTarget as IFormationUnit);
                onSuccess($"Moved to {position} (rank {((IFormationUnit)heroAgent).FormationRankIndex})");
            }
            catch (Exception e)
            {
                onFailure($"Reposition failed: {e.Message}");
            }
        }
    }
}