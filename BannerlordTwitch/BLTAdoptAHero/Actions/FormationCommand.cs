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
using TaleWorlds.MountAndBlade;
using BLTAdoptAHero;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("{=TESTING}FormationCommand"),
     LocDescription("{=TESTING}Show and change hero formation / issue detachment orders"),
     UsedImplicitly]
    public class FormationCommand : HeroCommandHandlerBase
    {
        public class Settings : IDocumentable
        {
            [LocDisplayName("{=TESTING}Respect class"),
             LocCategory("General", "{=TESTING}General"),
             LocDescription("{=TESTING}When on, heroes can only switch to formations that match their physical class"),
             PropertyOrder(1), UsedImplicitly]
            public bool Filter { get; set; } = true;

            [LocDisplayName("{=TESTING}Detachments"),
             LocCategory("General", "{=TESTING}General"),
             LocDescription("{=TESTING}Allow detachment sub-commands"),
             PropertyOrder(2), UsedImplicitly]
            public bool Detach { get; set; } = true;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.Value("<strong>Usage:</strong> [number | front | back | keyword]");
                generator.Value("");
                generator.Value("<strong>Formation movement (attached):</strong>");
                generator.Value("  (no args)  – show formation list");
                generator.Value("  [number]   – switch to that numbered formation");
                generator.Value("  front      – move to the front rank");
                generator.Value("  back       – move to the back rank");
                generator.Value("");
                generator.Value("<strong>Detachment commands:</strong>");
                generator.Value("  detach     – leave formation, go independent");
                generator.Value("  attach     – return to formation");
                generator.Value("  status     – show current detachment order");
                generator.Value("  charge     – hunt and engage nearest enemy");
                generator.Value("  hold       – stand ground and fight nearby");
                generator.Value("  follow     – shadow the parent formation");
                generator.Value("  flank      – reach the enemy's closest flank, then charge");
                generator.Value("  gate       – attack / defend nearest gate");
                generator.Value("  walls      – scale / hold walls (re-issue to cycle targets)");
            }
        }

        public override Type HandlerConfigType => typeof(Settings);

        private static readonly HashSet<string> DetachKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "detach",
            "attach",
            "status",
            "charge",
            "hold",
            "follow",
            "flank",
            "gate",
            "walls",
        };

        private static readonly HashSet<string> PositionKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "front",
            "back"
        };

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (config is not Settings settings) return;

            if (adoptedHero == null) { onFailure(AdoptAHero.NoHeroMessage); return; }
            if (Mission.Current == null) { onFailure("No active mission"); return; }
            if (MissionHelpers.InTournament()) { onFailure("Not available in tournaments"); return; }

            var agent = adoptedHero.GetAgent();
            if (agent == null) { onFailure("Hero is not on the battlefield"); return; }

            var formation = agent.Formation;
            if (formation == null) { onFailure("Hero has no formation"); return; }

            string keyword = (context.Args?.Split(' ')[0].Trim() ?? "").ToLowerInvariant();

            // ── Detachment commands ──────────────────────────────────────────
            if (DetachKeywords.Contains(keyword))
            {
                if (!settings.Detach)
                { onFailure("Detachment commands are disabled"); return; }

                var behavior = BLTHeroDetachmentBehavior.Current;
                if (behavior == null)
                { onFailure("Detachment system is not active"); return; }

                if (keyword is not "status" and not "attach")
                { onFailure("Cannot issue detachment commands during deployment"); return; }

                ExecuteDetachmentCommand(keyword, agent, behavior, onSuccess, onFailure);
                return;
            }

            // ── Formation number switch ──────────────────────────────────────
            ExecuteFormationSwitch(agent, formation, keyword, settings, onSuccess, onFailure);
        }

        // ── Detachment dispatch ───────────────────────────────────────────────

        private static void ExecuteDetachmentCommand(string keyword, Agent agent,
            BLTHeroDetachmentBehavior behavior,
            Action<string> onSuccess, Action<string> onFailure)
        {
            bool requiresDetached = keyword is not "detach" and not "status";

            if (requiresDetached && !behavior.IsDetached(agent))
            { onFailure("Hero is not detached — use 'detach' first"); return; }

            string error = keyword switch
            {
                "detach" => behavior.Detach(agent),
                "attach" => behavior.Attach(agent),
                "status" => HandleStatus(agent, behavior, onSuccess),
                "charge" => behavior.Charge(agent),
                "hold" => behavior.Hold(agent),
                "follow" => behavior.Follow(agent),
                "flank" => behavior.Flank(agent),
                "gate" => behavior.Gate(agent),
                "walls" => behavior.Walls(agent),
                _ => "Unknown detachment command"
            };

            if (error != null)
                onFailure(error);
            else if (keyword != "status")
                onSuccess(FriendlyOrderName(keyword));
        }

        private static string HandleStatus(Agent agent, BLTHeroDetachmentBehavior behavior,
            Action<string> onSuccess)
        {
            onSuccess(behavior.GetStatus(agent));
            return null;
        }

        private static string FriendlyOrderName(string keyword) => keyword switch
        {
            "detach" => "Detached from formation",
            "attach" => "Reattached to formation",
            "charge" => "Charging nearest enemy",
            "hold" => "Holding position",
            "follow" => "Following formation",
            "flank" => "Flanking enemy formation",
            "gate" => "Targeting gate",
            "walls" => "Moving to walls",
            _ => keyword
        };

        // ── Formation number switch ───────────────────────────────────────────

        private static void ExecuteFormationSwitch(Agent agent, Formation currentFormation,
            string keyword, Settings settings,
            Action<string> onSuccess, Action<string> onFailure)
        {
            var query = currentFormation.QuerySystem;
            var heroClass = query switch
            {
                _ when query.IsInfantryFormation => FormationClass.Infantry,
                _ when query.IsRangedFormation => FormationClass.Ranged,
                _ when query.IsCavalryFormation => FormationClass.Cavalry,
                _ when query.IsRangedCavalryFormation => FormationClass.HorseArcher,
                _ => FormationClass.Infantry
            };

            var formationList = agent.Team.FormationsIncludingSpecialAndEmpty
                .Where(f => f.CountOfUnits > 0 &&
                            (!settings.Filter || f.PhysicalClass == heroClass))
                .OrderBy(f => f.Index)
                .ToList();

            int currentPos = formationList.FindIndex(f => f.Index == currentFormation.Index) + 1;

            if (string.IsNullOrEmpty(keyword) || !int.TryParse(keyword, out int number))
            {
                onSuccess($"{heroClass} {currentPos}/{formationList.Count} ({currentFormation.CountOfUnits} troops) | {BuildFormationList(formationList, currentFormation)}");
                return;
            }

            if (agent.IsDetachedFromFormation)
            { onFailure("Reattach before switching formations"); return; }

            if (number < 1 || number > formationList.Count)
            { onFailure($"Enter 1–{formationList.Count}"); return; }

            var target = formationList[number - 1];
            if (target == currentFormation)
            { onSuccess($"Already in formation {number}"); return; }

            TransferHeroToFormation(agent, target);
            onSuccess($"Moved to formation {number} ({target.CountOfUnits} troops)");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void TransferHeroToFormation(Agent heroAgent, Formation target)
        {
            var old = heroAgent.Formation;
            heroAgent.Formation = target;
            old?.Team.TriggerOnFormationsChanged(old);
            target.Team.TriggerOnFormationsChanged(target);
            Log.Trace($"{heroAgent.Name} → {target.FormationIndex.GetName()}");
        }

        private static string BuildFormationList(List<Formation> formations, Formation current)
        {
            var sb = new StringBuilder();
            int n = 1;
            foreach (var f in formations)
            {
                string marker = f == current ? "*" : "";
                sb.Append($"{n}{marker}:{f.CountOfUnits}[{BuildCompact(f)}] ");
                n++;
            }
            return sb.ToString().TrimEnd();
        }

        private static string BuildCompact(Formation f)
        {
            string order = MovementLabel(f.GetReadonlyMovementOrderReference().OrderEnum);
            string arrng = ArrangementLabel(f.ArrangementOrder.OrderEnum);
            string target = "";

            if (f.TargetFormation != null && f.TargetFormation.CountOfUnits > 0)
            {
                float dist = (f.TargetFormation.GetAveragePositionOfUnits(true, true) - f.GetAveragePositionOfUnits(true, true)).Length;
                target = $"→{ClassLabel(f.TargetFormation.QuerySystem)}@{dist:0}m";
            }

            return $"{order}/{arrng}{target}";
        }

        private static string ClassLabel(FormationQuerySystem q) => q switch
        {
            _ when q.IsInfantryFormation => "Inf",
            _ when q.IsRangedFormation => "Rng",
            _ when q.IsCavalryFormation => "Cav",
            _ when q.IsRangedCavalryFormation => "HA",
            _ => "?"
        };

        private static string MovementLabel(MovementOrder.MovementOrderEnum o) => o switch
        {
            MovementOrder.MovementOrderEnum.Charge => "Chrg",
            MovementOrder.MovementOrderEnum.ChargeToTarget => "Chrg",
            MovementOrder.MovementOrderEnum.Advance => "Adv",
            MovementOrder.MovementOrderEnum.FallBack => "Fall",
            MovementOrder.MovementOrderEnum.Retreat => "Rtr",
            MovementOrder.MovementOrderEnum.Stop => "Hold",
            MovementOrder.MovementOrderEnum.Invalid => "Hold",
            MovementOrder.MovementOrderEnum.Follow => "Flw",
            MovementOrder.MovementOrderEnum.FollowEntity => "Flw",
            MovementOrder.MovementOrderEnum.Move => "Mov",
            _ => "?"
        };

        private static string ArrangementLabel(ArrangementOrder.ArrangementOrderEnum o) => o switch
        {
            ArrangementOrder.ArrangementOrderEnum.Line => "Line",
            ArrangementOrder.ArrangementOrderEnum.ShieldWall => "Wall",
            ArrangementOrder.ArrangementOrderEnum.Loose => "Lse",
            ArrangementOrder.ArrangementOrderEnum.Square => "Sqr",
            ArrangementOrder.ArrangementOrderEnum.Circle => "Cir",
            ArrangementOrder.ArrangementOrderEnum.Column => "Col",
            ArrangementOrder.ArrangementOrderEnum.Scatter => "Sct",
            _ => "?"
        };
    }
}