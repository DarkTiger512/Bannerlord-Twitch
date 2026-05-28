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
    [LocDisplayName("{=BLTFormationCommandName}Formation Command"),
     LocDescription("{=BLTFormationCommandDesc}Show and change your hero formation"),
     UsedImplicitly]
    public class FormationCommand : HeroCommandHandlerBase
    {
        public class Settings : IDocumentable
        {
            [LocDisplayName("{=BLTFormationRespectClassName}Respect class"),
             LocCategory("General", "{=TESTING}General"),
             LocDescription("{=BLTFormationRespectClassDesc}Turn off to allow any formation; otherwise infantry can only change to other infantry formations"),
             PropertyOrder(1), UsedImplicitly]
            public bool Filter { get; set; } = true;

            [LocDisplayName("{=BLTFormationDetachmentsName}Detachments"),
             LocCategory("General", "{=TESTING}General"),
             LocDescription("{=BLTFormationDetachmentsDesc}Allow detached hero commands"),
             PropertyOrder(2), UsedImplicitly]
            public bool Detach { get; set; } = true;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.Value("{=BLTFormationUsageNumber}<strong>Usage:</strong> number".Translate());
                generator.Value("{=BLTFormationUsageFrontBack}- front/back".Translate());
                generator.Value("{=BLTFormationUsageDetachAttach}- detach/attach".Translate());
                generator.Value("{=BLTFormationUsageDetached}- (while detached): charge/hold/follow/gate/walls".Translate());
            }
        }

        public override Type HandlerConfigType => typeof(Settings);

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (config is not Settings settings) return;
            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }

            if (Mission.Current == null)
            {
                onFailure("{=TESTING}No mission!".Translate());
                return;
            }

            if (Mission.Current.IsNavalBattle)
            {
                onFailure("{=BLTFormationNoNaval}Cannot change formation in naval battle".Translate());
                return;
            }
            if (MissionHelpers.InTournament())
            {
                onFailure("{=BLTFormationNoTournament}Cannot change formation in tournament".Translate());
                return;
            }
            
            var splitArgs = context.Args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string num = splitArgs.Length > 0 ? splitArgs[0] : "";

            var agent = adoptedHero.GetAgent();
            if (agent == null)
            {
                onFailure("{=BLTFormationNoHero}No hero".Translate());
                return;
            }

            Formation currentFormation = agent.Formation;
            if (currentFormation == null)
            {
                onFailure("{=BLTFormationNoFormation}No formation".Translate());
                return;
            }
           
            var behavior = BLTHeroDetachmentBehavior.Current;
            string command = GetFormationCommand(num);
            var keywords = new[] { "detach", "attach", "charge", "hold", "follow", "gate", "walls" };
            if (keywords.Contains(command))
            {      
                if (!settings.Detach) { onFailure("{=BLTFormationDetachOff}Detach commands are off".Translate()); return; }
                if (behavior == null) { onFailure("{=BLTFormationDetachInactive}Detachment system not active".Translate()); return; }
                if (!Mission.Current.IsDeploymentFinished) { onFailure("{=BLTFormationNoDetachDeploying}Cannot detach while deploying".Translate()); return; }

                string error = command switch
                {
                    "detach" => behavior.Detach(agent),
                    "attach" => behavior.Attach(agent),
                    "charge" => behavior.Charge(agent),
                    "hold" => behavior.Hold(agent),
                    "follow" => behavior.Follow(agent),
                    "gate" => behavior.TargetDoor(agent),
                    "walls" => behavior.Walls(agent),
                    _ => "Unknown command"
                };

                if (error != null) onFailure(error);
                else onSuccess("{=BLTFormationCommandOk}{command} ok".Translate(("command", GetFormationCommandDisplayName(command))));
                return;
            }

            if (command == "front" || command == "back")
            {
                if (agent.IsDetachedFromFormation)
                {
                    onFailure("{=BLTFormationAttachBeforeMoving}Reattach before moving".Translate());
                    return;
                }
                BLTSummonBehavior.MarkManualFormationOverride(agent);
                SetHeroFormationPosition(agent, command, onSuccess, onFailure);
                return;
            }

            var query = currentFormation.QuerySystem;
            FormationClass formType = query switch
            {
                _ when query.IsInfantryFormationReadOnly => FormationClass.Infantry,
                _ when query.IsRangedFormationReadOnly => FormationClass.Ranged,
                _ when query.IsCavalryFormationReadOnly => FormationClass.Cavalry,
                _ when query.IsRangedCavalryFormationReadOnly => FormationClass.HorseArcher,
                _ => FormationClass.Infantry
            };

            if (settings.Filter)
            {
                var allFormations = agent.Team.FormationsIncludingSpecialAndEmpty
                    .Where(f => f.PhysicalClass == formType && f.CountOfUnits > 0)
                    .OrderBy(f => f.Index);

                var indexes = allFormations.Select(f => f.Index).OrderBy(i => i).ToList();

                var sb = new StringBuilder();
                int number = 1;

                foreach (var f in allFormations)
                {
                    int troops = f.CountOfUnits;
                    string order = BuildCompact(f);
                    sb.Append($"{number}:{troops}[{order}], ");
                    number++;
                }

                int count = indexes.Count;
                int position = indexes.IndexOf(currentFormation.Index) + 1;

                if (string.IsNullOrEmpty(num) || !int.TryParse(num, out int numb))
                {
                    onSuccess($"{GetFormationClassDisplayName(formType)} {position}/{count} {currentFormation.CountOfUnits} | {sb}");
                    return;
                }
                if (agent.IsDetachedFromFormation)
                {
                    onFailure("{=BLTFormationAttachBeforeChanging}Reattach before changing formations".Translate());
                    return;
                }
                if (numb > count || numb <= 0)
                {
                    onFailure("{=BLTFormationInvalidNumber}Invalid number".Translate());
                    return;
                }

                var newformation = allFormations.ElementAt(numb - 1);
                TransferHeroToFormation(agent, newformation);

                onSuccess("{=BLTFormationMoved}Moved. {troops} troops".Translate(("troops", newformation.CountOfUnits)));
            }
            else
            {
                var allFormations = agent.Team.FormationsIncludingSpecialAndEmpty
                    .Where(f => f.CountOfUnits > 0)
                    .OrderBy(f => f.Index);

                var indexes = allFormations.Select(f => f.Index).OrderBy(i => i).ToList();

                var sb = new StringBuilder();
                int number = 1;

                foreach (var f in allFormations)
                {
                    var q = f.QuerySystem;
                    string type = q switch
                    {
                        _ when q.IsInfantryFormationReadOnly => GetFormationClassDisplayName(FormationClass.Infantry),
                        _ when q.IsRangedFormationReadOnly => GetFormationClassDisplayName(FormationClass.Ranged),
                        _ when q.IsCavalryFormationReadOnly => GetFormationClassDisplayName(FormationClass.Cavalry),
                        _ when q.IsRangedCavalryFormationReadOnly => GetFormationClassDisplayName(FormationClass.HorseArcher),
                        _ => "{=BLTFormationClassUnknown}unknown".Translate()
                    };

                    int troops = f.CountOfUnits;
                    string order = BuildCompact(f);

                    sb.Append($"{number}:{type}({troops})[{order}], ");
                    number++;
                }

                int count = indexes.Count;
                int position = indexes.IndexOf(currentFormation.Index) + 1;

                if (string.IsNullOrEmpty(num) || !int.TryParse(num, out int numb))
                {
                    onSuccess($"{GetFormationClassDisplayName(formType)} {position}/{count} {currentFormation.CountOfUnits} | {sb}");
                    return;
                }
                if (agent.IsDetachedFromFormation)
                {
                    onFailure("{=BLTFormationAttachBeforeChanging}Reattach before changing formations".Translate());
                    return;
                }
                if (numb > count || numb <= 0)
                {
                    onFailure("{=BLTFormationInvalidNumber}Invalid number".Translate());
                    return;
                }

                var newformation = allFormations.ElementAt(numb - 1);
                TransferHeroToFormation(agent, newformation);

                onSuccess("{=BLTFormationMoved}Moved. {troops} troops".Translate(("troops", newformation.CountOfUnits)));
            }
        }

        private void TransferHeroToFormation(Agent heroAgent, Formation target)
        {
            if (heroAgent == null || target == null) return;

            var oldFormation = heroAgent.Formation;
            heroAgent.Formation = target;
            BLTSummonBehavior.MarkManualFormationOverride(heroAgent);

            oldFormation?.Team.TriggerOnFormationsChanged(oldFormation);
            target.Team.TriggerOnFormationsChanged(target);

            Log.Trace($"{heroAgent.Name} transferred to {target.FormationIndex.GetName()}");
        }


        string BuildCompact(Formation f)
        {
            var m = f.GetReadonlyMovementOrderReference().OrderEnum;
            var a = f.ArrangementOrder.OrderEnum;

            string dist = "";
            if (f.TargetFormation != null)
            {
                var q = f.TargetFormation.QuerySystem;
                var myPos = f.CachedAveragePosition;
                var targetPos = f.TargetFormation.CachedAveragePosition;
                float pos = (targetPos - myPos).Length;
                string type = q switch
                {
                    _ when q.IsInfantryFormationReadOnly => GetFormationClassDisplayName(FormationClass.Infantry),
                    _ when q.IsRangedFormationReadOnly => GetFormationClassDisplayName(FormationClass.Ranged),
                    _ when q.IsCavalryFormationReadOnly => GetFormationClassDisplayName(FormationClass.Cavalry),
                    _ when q.IsRangedCavalryFormationReadOnly => GetFormationClassDisplayName(FormationClass.HorseArcher),
                    _ => "{=BLTFormationClassUnknown}unknown".Translate()
                };

                string targetLabel = "{=BLTFormationTarget}Target".Translate();
                dist += $"-{targetLabel}:{type}-{pos:0}";
            }

            return $"{M(m)}-{A(a)}{dist}";
        }

        string M(MovementOrder.MovementOrderEnum o) => o switch
        {
            MovementOrder.MovementOrderEnum.Charge => "{=BLTFormationOrderCharge}Charge".Translate(),
            MovementOrder.MovementOrderEnum.ChargeToTarget => "{=BLTFormationOrderCharge}Charge".Translate(),
            MovementOrder.MovementOrderEnum.Advance => "{=BLTFormationOrderAdvance}Advance".Translate(),
            MovementOrder.MovementOrderEnum.FallBack => "{=BLTFormationOrderRetreat}Retreat".Translate(),
            MovementOrder.MovementOrderEnum.Retreat => "{=BLTFormationOrderRetreat}Retreat".Translate(),
            MovementOrder.MovementOrderEnum.Invalid => "{=BLTFormationOrderHold}Hold".Translate(),
            MovementOrder.MovementOrderEnum.Stop => "{=BLTFormationOrderHold}Hold".Translate(),
            MovementOrder.MovementOrderEnum.Follow => "{=BLTFormationOrderFollow}Follow".Translate(),
            MovementOrder.MovementOrderEnum.FollowEntity => "{=BLTFormationOrderFollow}Follow".Translate(),
            MovementOrder.MovementOrderEnum.Move => "{=BLTFormationOrderMove}Move".Translate(),
            _ => "?"
        };

        string A(ArrangementOrder.ArrangementOrderEnum o) => o switch
        {
            ArrangementOrder.ArrangementOrderEnum.Line => "{=BLTFormationArrangementLine}Line".Translate(),
            ArrangementOrder.ArrangementOrderEnum.ShieldWall => "{=BLTFormationArrangementWall}Wall".Translate(),
            ArrangementOrder.ArrangementOrderEnum.Loose => "{=BLTFormationArrangementLoose}Loose".Translate(),
            ArrangementOrder.ArrangementOrderEnum.Square => "{=BLTFormationArrangementSquare}Square".Translate(),
            ArrangementOrder.ArrangementOrderEnum.Circle => "{=BLTFormationArrangementCircle}Circle".Translate(),
            ArrangementOrder.ArrangementOrderEnum.Column => "{=BLTFormationArrangementColumn}Column".Translate(),
            ArrangementOrder.ArrangementOrderEnum.Scatter => "{=BLTFormationArrangementScatter}Scatter".Translate(),
            _ => "--"
        };

        private string GetFormationClassDisplayName(FormationClass formationClass) => formationClass switch
        {
            FormationClass.Infantry => "{=BLTFormationClassInfantry}Infantry".Translate(),
            FormationClass.Ranged => "{=BLTFormationClassRanged}Ranged".Translate(),
            FormationClass.Cavalry => "{=BLTFormationClassCavalry}Cavalry".Translate(),
            FormationClass.HorseArcher => "{=BLTFormationClassHorseArcher}Horse archer".Translate(),
            _ => formationClass.ToString()
        };

        private string GetFormationCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return "";

            command = command.Trim();

            if (MatchesCommand(command, "{=BLTFormationSubFront}front".Translate(), "front")) return "front";
            if (MatchesCommand(command, "{=BLTFormationSubBack}back".Translate(), "back")) return "back";
            if (MatchesCommand(command, "{=BLTFormationSubDetach}detach".Translate(), "detach")) return "detach";
            if (MatchesCommand(command, "{=BLTFormationSubAttach}attach".Translate(), "attach")) return "attach";
            if (MatchesCommand(command, "{=BLTFormationSubCharge}charge".Translate(), "charge")) return "charge";
            if (MatchesCommand(command, "{=BLTFormationSubHold}hold".Translate(), "hold")) return "hold";
            if (MatchesCommand(command, "{=BLTFormationSubFollow}follow".Translate(), "follow")) return "follow";
            if (MatchesCommand(command, "{=BLTFormationSubGate}gate".Translate(), "gate")) return "gate";
            if (MatchesCommand(command, "{=BLTFormationSubWalls}walls".Translate(), "walls")) return "walls";

            return command.ToLowerInvariant();
        }

        private bool MatchesCommand(string command, string translatedCommand, string defaultCommand)
            => command.Equals(defaultCommand, StringComparison.OrdinalIgnoreCase)
               || command.Equals(translatedCommand, StringComparison.OrdinalIgnoreCase);

        private string GetFormationCommandDisplayName(string command) => command switch
        {
            "front" => "{=BLTFormationSubFront}front".Translate(),
            "back" => "{=BLTFormationSubBack}back".Translate(),
            "detach" => "{=BLTFormationSubDetach}detach".Translate(),
            "attach" => "{=BLTFormationSubAttach}attach".Translate(),
            "charge" => "{=BLTFormationSubCharge}charge".Translate(),
            "hold" => "{=BLTFormationSubHold}hold".Translate(),
            "follow" => "{=BLTFormationSubFollow}follow".Translate(),
            "gate" => "{=BLTFormationSubGate}gate".Translate(),
            "walls" => "{=BLTFormationSubWalls}walls".Translate(),
            _ => command
        };

        private void SetHeroFormationPosition(Agent heroAgent, string position, Action<string> onSuccess, Action<string> onFailure)
        {
            var formation = heroAgent.Formation;
            if (formation == null) { onFailure("{=BLTFormationNoFormation}No formation".Translate()); return; }

            var unit = heroAgent as IFormationUnit;
            if (unit == null) { onFailure("{=BLTFormationNotUnit}Not a formation unit".Translate()); return; }

            var arrangement = formation.Arrangement;

            try
            {
                switch (position.ToLowerInvariant())
                {
                    case "front":
                        {
                            var candidate = arrangement.GetAllUnits()
                                .Select(u => u as Agent)
                                .Where(a => a != null && a != heroAgent && a.GetHero() == null)
                                .OrderBy(a => ((IFormationUnit)a).FormationRankIndex)
                                .ThenBy(a => ((IFormationUnit)a).FormationFileIndex)
                                .Take((int)arrangement.Width).SelectRandom();

                            if (candidate == null) { onFailure("{=BLTFormationNoTroop}No troop found".Translate()); break; }

                            arrangement.SwitchUnitLocations(candidate, unit);
                            onSuccess("{=BLTFormationMovedFront}Moved to front".Translate());
                            break;
                        }

                    case "back":
                        {
                            var candidate = arrangement.GetAllUnits()
                                .Select(u => u as Agent)
                                .Where(a => a != null && a != heroAgent && a.GetHero() == null)
                                .OrderByDescending(a => ((IFormationUnit)a).FormationRankIndex)
                                .ThenBy(a => ((IFormationUnit)a).FormationFileIndex)
                                .Take((int)arrangement.Width).SelectRandom();

                            if (candidate == null) { onFailure("{=BLTFormationNoTroop}No troop found".Translate()); break; }

                            arrangement.SwitchUnitLocations(candidate, unit);
                            onSuccess("{=BLTFormationMovedBack}Moved to back".Translate());
                            break;
                        }
                }
            }
            catch (Exception e)
            {
                onFailure("{=BLTFormationUnsupported}Formation type does not support this operation ({message})".Translate(("message", e.Message)));
            }
        }
    }
}
