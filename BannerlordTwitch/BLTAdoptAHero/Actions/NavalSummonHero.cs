using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace BLTAdoptAHero.Actions
{
    internal class NavalSummonHero : SummonHero
    {
        public static void SummonInNavalBattle(Hero adoptedHero, Settings settings, ReplyContext context,
        Action<string> onSuccess, Action<string> onFailure)
        {
            if (!Mission.Current.IsNavalBattle)
            {
                onFailure("Not a naval battle!");
                return;
            }
            using var balanceJoin = BLTSummonBehavior.Current.BeginBalanceJoin(adoptedHero, settings.OnPlayerSide);
            if (balanceJoin == null)
            {
                onFailure("{=BLTBalanceJoinBlocked}You cannot switch sides or submit another join while one is pending.".Translate());
                return;
            }
            var heroSummonState = BLTSummonBehavior.Current.GetHeroSummonState(adoptedHero);
            if (heroSummonState != null && heroSummonState.WasPlayerSide != settings.OnPlayerSide)
            {
                onFailure("{=2D2T6xP6}You cannot switch sides, you traitor!".Translate());
                return;
            }
            if (heroSummonState != null
                && BLTAdoptAHeroModule.CommonConfig.AllowDeath
                && heroSummonState.State == AgentState.Killed)
            {
                onFailure("{=RBTDviuM}You cannot be summoned, you DIED!".Translate());
                return;
            }

            // Check again that the hero is alive, as this method is run on a later tick from the previous one
            if (heroSummonState is { State: AgentState.Active })
            {
                onFailure("{=YMiZAluP}You cannot be summoned, you are already here!".Translate());
                return;
            }

            if (heroSummonState?.InCooldown == true)
            {
                onFailure("{=kyUh29ij}{CoolDown}s cooldown remaining"
                    .Translate(("CoolDown", heroSummonState.CooldownRemaining.ToString("0"))));
                return;
            }

            Team targetTeam = settings.OnPlayerSide ? Mission.Current.PlayerTeam : Mission.Current.PlayerEnemyTeam;
            if (targetTeam == null)
            {
                onFailure("Could not determine mission team for chosen side.");
                return;
            }

            var agentsLogic = OptionalNavalApi.TryCreate(Mission.Current);

            if (agentsLogic == null)
            {
                onFailure("Naval spawn logic not available on this mission.");
                return;
            }

            var ships = Mission.Current.MissionObjects
                .Where(s => agentsLogic.IsShip(s) && agentsLogic.GetTeam(s) == targetTeam)
                .OrderBy(s => agentsLogic.GetCapacity(s))
                .ToList();

            if (!ships.Any())
            {
                onFailure("No deployable ships available for that side.");
                return;
            }
                var party = adoptedHero.GetMapEventParty() ?? settings.OnPlayerSide switch
                {
                    true when Mission.Current.PlayerTeam?.ActiveAgents.Any() == true => PartyBase.MainParty,
                    false when Mission.Current.PlayerEnemyTeam?.ActiveAgents.Any() == true => Mission.Current
                        .PlayerEnemyTeam?.TeamAgents?.Select(a => a.Origin?.BattleCombatant as PartyBase)
                        .Where(p => p != null)
                        .SelectRandom(),
                    _ => null
                };

                if (party == null)
                {
                    onFailure("{=jtqEqonE}Could not find a party for you to join!".Translate());
                    return;
                }

            Agent spawnedAgent;
            foreach (var ship in ships)
            {
                Action cancelReservation = null;
                try
                {

                    agentsLogic.SetIgnoreTroopCapacities(ship, true);
                    agentsLogic.SetDesiredTroopCountOfShip(ship, agentsLogic.GetCapacity(ship) + 100);


                    TeamSideEnum teamSide = targetTeam.TeamSide;

                    IAgentOriginBase heroOrigin =
                        agentsLogic.FindTroopOrigin(teamSide, o => o.Troop != null)
                        ?? agentsLogic.FindTroopOrigin(teamSide, o => o.Troop != null && o.Troop.IsHero);

                    if (heroOrigin == null)
                    {
#if DEBUG
                        Log.Trace("Missing origin");
#endif
                        continue;
                    }

                    // Resolve the engine's single-origin removal before enqueueing. A failed
                    // attempt must not leave a hero waiting to spawn on a later reinforcement tick.
                    var reservedOrigin = new SimpleAgentOrigin(adoptedHero.CharacterObject, settings.OnPlayerSide);
                    cancelReservation = agentsLogic.CreateReservationCleanup(teamSide, reservedOrigin, ship);
                    if (cancelReservation == null) continue;
                    if (!agentsLogic.AddReservedTroopToShip(reservedOrigin, ship)) continue;
                    agentsLogic.SpawnNextBatch(teamSide, false, null);
                    spawnedAgent = adoptedHero.GetAgent();
                    if (spawnedAgent == null)
                    {
#if DEBUG
                        Log.Trace("Failed to spawn hero on the ship.");
#endif
                        continue;
                    }
                    break;
                }
                catch
                {
                    if (adoptedHero.GetAgent() != null) break;
                    continue;
                }
                finally
                {
                    if (adoptedHero.GetAgent() == null) cancelReservation?.Invoke();
                }
            }

            try
            {
                agentsLogic.AssignTroops(targetTeam.TeamSide, true);
            }
            catch (Exception ex)
            {
                if (adoptedHero.GetAgent() == null)
                { onFailure($"Naval spawn flow failed: {ex.Message}"); return; }
            }

            spawnedAgent = adoptedHero.GetAgent();
            if (spawnedAgent == null)
            {
                onFailure("Failed to spawn hero on the ship.");
                return;
            }

            bool firstSummon = heroSummonState == null;
            if (firstSummon)
            {
                var originalParty = adoptedHero.PartyBelongedTo;
                int oldHP = adoptedHero.HitPoints;
                bool wasLeader = adoptedHero.PartyBelongedTo?.LeaderHero == adoptedHero;
                if (originalParty?.Party != party)
                {
                    originalParty?.Party?.AddMember(adoptedHero.CharacterObject, -1);
                    party.AddMember(adoptedHero.CharacterObject, 1);
                }

                balanceJoin.OnFailure = () =>
                {
                    BLTAdoptAHeroCustomMissionBehavior.Current.RemoveListeners(adoptedHero);
                    if (originalParty?.Party != party)
                    {
                        party.AddMember(adoptedHero.CharacterObject, -1);
                        originalParty?.Party?.MemberRoster.AddToCounts(adoptedHero.CharacterObject, 1, insertAtFront: wasLeader);
                        if (wasLeader) originalParty?.PartyComponent.ChangePartyLeader(adoptedHero);
                    }
                };

                BLTAdoptAHeroCustomMissionBehavior.Current.AddListeners(adoptedHero,
                    onSlowTick: dt =>
                    {
                        if (settings.HealPerSecond != 0)
                        {
                            var activeAgent = Mission.Current?.Agents?.FirstOrDefault(a =>
                                a.IsActive() && a.Character == adoptedHero.CharacterObject);
                            if (activeAgent?.IsActive() == true)
                            {
                                Log.Trace($"[{nameof(SummonHero)}] healing {adoptedHero}");
                                activeAgent.Health = Math.Min(activeAgent.HealthLimit,
                                    activeAgent.Health + settings.HealPerSecond * dt);
                            }
                        }
                    },
                    onMissionOver: () =>
                    {

                        if (adoptedHero.PartyBelongedTo != originalParty)
                        {
                            if (originalParty?.Party?.MemberRoster != null && originalParty?.Party?.MemberRoster.TotalHealthyCount > 0)
                                adoptedHero.HitPoints = oldHP;
                            party.AddMember(adoptedHero.CharacterObject, -1);
                            originalParty?.Party?.MemberRoster.AddToCounts(adoptedHero.CharacterObject, 1, insertAtFront: wasLeader);
                            // Make sure to reassign the hero as party leader if they were previously
                            if (wasLeader)
                            {
                                originalParty?.PartyComponent.ChangePartyLeader(adoptedHero);
                            }
                            Log.Trace($"[{nameof(SummonHero)}] moving {adoptedHero} from {party} back to {originalParty?.Party?.ToString() ?? "no party"}");
                        }

                        if (!balanceJoin.Succeeded) return;

                        // No rewards when defender pulled back to keep
                        if (Mission.Current?.MissionResult != null && Mission.Current.MissionResult?.BattleState != BattleState.DefenderPullBack)
                        {
                            var results = new List<string>();
                            float finalRewardScaling =
                                    (settings.OnPlayerSide
                                        ? BLTAdoptAHeroCommonMissionBehavior.Current.PlayerSideRewardMultiplier
                                        : BLTAdoptAHeroCommonMissionBehavior.Current.EnemySideRewardMultiplier)
                                ;

                            if (settings.OnPlayerSide == Mission.Current.MissionResult.PlayerVictory)
                            {
                                int actualGold = BLTAdoptAHeroCampaignBehavior.BattleGold(adoptedHero, BLTAdoptAHeroModule.CommonConfig.WinGold, finalRewardScaling) + settings.GoldCost;
                                if (actualGold > 0)
                                {
                                    BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, actualGold);
                                    results.Add(finalRewardScaling != 1
                                        ? $"{Naming.Inc}{actualGold}{Naming.Gold} (x{finalRewardScaling:0.00})"
                                        : $"{Naming.Inc}{actualGold}{Naming.Gold}");
                                }

                                if (BLTAdoptAHeroModule.CommonConfig.WinXP > 0)
                                {
                                    (bool success, string description) = SkillXP.ImproveSkill(adoptedHero,
                                        BLTAdoptAHeroModule.CommonConfig.WinXP, SkillsEnum.All, auto: true, rewardMultiplier: finalRewardScaling * BLTSummonBehavior.BalanceFactor(adoptedHero));
                                    if (success)
                                    {
                                        results.Add(finalRewardScaling != 1
                                            ? $"{description} (x{finalRewardScaling:0.00})"
                                            : description);
                                    }
                                }
                            }
                            else
                            {
                                if (BLTAdoptAHeroModule.CommonConfig.LoseGold != 0)
                                {
                                    var delta = BLTAdoptAHeroModule.CommonConfig.LoseGold;
                                    if (delta < 0) delta = -BLTAdoptAHeroCampaignBehavior.BattleGold(adoptedHero, -delta, finalRewardScaling);
                                    BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -delta);

                                    var sign = delta > 0 ? Naming.Dec : Naming.Inc;
                                    var amount = Math.Abs(delta);

                                    results.Add($"{sign}{amount}{Naming.Gold}");
                                }

                                int xp = BLTAdoptAHeroModule.CommonConfig.LoseXP;
                                if (xp > 0)
                                {
                                    (bool success, string description) = SkillXP.ImproveSkill(adoptedHero, xp,
                                        SkillsEnum.All, auto: true, rewardMultiplier: finalRewardScaling * BLTSummonBehavior.BalanceFactor(adoptedHero));
                                    if (success)
                                    {
                                        results.Add(finalRewardScaling != 1
                                            ? $"{description} (x{finalRewardScaling:0.00})"
                                            : description);
                                    }
                                }
                            }

                            if (results.Any())
                            {
                                Log.LogFeedResponse(context.UserName, results.ToArray());
                            }
                        }
                    },
                    replaceExisting: false
                );

                heroSummonState = BLTSummonBehavior.Current.AddHeroSummonState(adoptedHero, settings.OnPlayerSide, party, forced: false, settings.WithRetinue);
            }

            if (firstSummon) BLTSummonBehavior.Current.OnAgentBuild(spawnedAgent, null);
            balanceJoin.Commit();
            //BLTRemoveAgentsBehavior.Current.Add(adoptedHero);

            foreach (var t in Mission.Current.Teams)
            {
                t.QuerySystem.Expire();
            }
            foreach (var formation in Mission.Current.Teams.SelectMany(t => t.FormationsIncludingSpecialAndEmpty))
            {
                formation.SetSpawnIndex(0);
            }

            if (MBRandom.RandomInt(0, 100) < (int)Math.Round(settings.ShoutPercent * 100))
            {
                Log.ShowInformation(!string.IsNullOrEmpty(context.Args)
                    ? context.Args
                    : GetShouts(settings).SelectRandomWeighted(shout => shout.Weight)?.Text?.ToString() ?? "...",
                adoptedHero.CharacterObject, settings.AlertSound);
            }

            if (settings.GoldCost != 0)
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.GoldCost);

            Log.ShowInformation(!string.IsNullOrEmpty(context.Args)
                ? context.Args
                : GetShouts(settings).SelectRandomWeighted(shout => shout.Weight)?.Text?.ToString() ?? "...",
                adoptedHero.CharacterObject, settings.AlertSound);

            onSuccess("{=BLTBalanceBoarded}You have boarded a ship!".Translate() + " " + BLTSummonBehavior.Current.LockedBalanceSummary(adoptedHero));
        }
    }
}
