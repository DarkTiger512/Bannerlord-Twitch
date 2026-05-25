using System;
using System.Collections.Generic;
using System.Linq;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Util;
using BannerlordTwitch;
using BannerlordTwitch.Twitch;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace BLTAdoptAHero
{
    internal class BLTSummonBehavior : AutoMissionBehavior<BLTSummonBehavior>
    {
        public class RetinueState
        {
            public CharacterObject Troop;
            public Agent Agent;
            // We must record this separately, as the Agent.State is undefined once the Agent is deleted (the internal handle gets reused by the engine)
            public AgentState State;
            public bool Died;
        }

        private readonly Dictionary<Agent, bool> _retinueResolution = new();

        public class HeroSummonState
        {
            public Hero Hero;
            public bool WasPlayerSide;
            public bool SpawnWithRetinue;
            public PartyBase Party;
            public AgentState State;
            public Agent CurrentAgent;
            public float SummonTime;
            public int TimesSummoned = 0;
            public List<RetinueState> Retinue { get; set; } = new();
            public List<RetinueState> Retinue2 { get; set; } = new();

            public int ActiveRetinue => Retinue.Count(r => r.State == AgentState.Active);
            public int DeadRetinue => Retinue.Count(r => r.Died);

            public int ActiveRetinue2 => Retinue2.Count(r => r.State == AgentState.Active);
            public int DeadRetinue2 => Retinue2.Count(r => r.Died);

            private float CooldownTime => BLTAdoptAHeroModule.CommonConfig.CooldownEnabled
                ? BLTAdoptAHeroModule.CommonConfig.GetCooldownTime(TimesSummoned) : 0;

            public bool InCooldown => BLTAdoptAHeroModule.CommonConfig.CooldownEnabled && SummonTime + CooldownTime > CampaignHelpers.GetTotalMissionTime();
            public float CooldownRemaining => !BLTAdoptAHeroModule.CommonConfig.CooldownEnabled ? 0 : Math.Max(0, SummonTime + CooldownTime - CampaignHelpers.GetTotalMissionTime());
            public float CoolDownFraction => !BLTAdoptAHeroModule.CommonConfig.CooldownEnabled ? 1 : 1f - CooldownRemaining / CooldownTime;
        }

        private readonly List<HeroSummonState> heroSummonStates = new();
        private readonly List<Action> onTickActions = new();

        public HeroSummonState GetHeroSummonState(Hero hero)
            => heroSummonStates.FirstOrDefault(h => h.Hero == hero);

        public HeroSummonState GetHeroSummonStateForRetinue(Agent retinueAgent)
            => heroSummonStates.FirstOrDefault(h => h.Retinue.Any(r => r.Agent == retinueAgent));
        public HeroSummonState GetHeroSummonStateForRetinue2(Agent retinue2Agent)
            => heroSummonStates.FirstOrDefault(h => h.Retinue2.Any(r => r.Agent == retinue2Agent));
        public readonly Dictionary<Hero, (Agent killer, KillingBlow blow)> HeroDeathSpecifics = new();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hero"></param>
        /// <param name="playerSide"></param>
        /// <param name="party"></param>
        /// <param name="forced">Whether the player chose to summon, or was part of the battle without choosing it. This affects what statistics will be updated, so streaks etc. aren't broken</param>
        /// <returns></returns>
        public HeroSummonState AddHeroSummonState(Hero hero, bool playerSide, PartyBase party, bool forced, bool withRetinue)
        {
            var heroSummonState = new HeroSummonState
            {
                Hero = hero,
                WasPlayerSide = playerSide,
                Party = party,
                SummonTime = CampaignHelpers.GetTotalMissionTime(),
                SpawnWithRetinue = withRetinue,
            };
            heroSummonStates.Add(heroSummonState);

            BLTAdoptAHeroCampaignBehavior.Current.IncreaseParticipationCount(hero, playerSide, forced);

            return heroSummonState;
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            SafeCall(() =>
            {
                // We only use this for heroes in battle
                if (CampaignMission.Current.Location != null)
                    return;

                var adoptedHero = agent.GetAdoptedHero();
                if (adoptedHero == null)
                    return;

                var heroSummonState = GetHeroSummonState(adoptedHero)
                                   ?? AddHeroSummonState(adoptedHero,
                                       Mission != null
                                       && agent.Team != null
                                       && Mission.PlayerTeam?.IsValid == true
                                       && agent.Team.IsFriendOf(Mission.PlayerTeam),
                                       adoptedHero.GetMapEventParty(),
                                       forced: true,
                                       withRetinue: true);

                // First spawn, so spawn retinue also
                if (heroSummonState.TimesSummoned == 0 && heroSummonState.SpawnWithRetinue && RetinueAllowed())
                {
                    var formationClass = agent.Formation.FormationIndex;
                    SpawnRetinue(adoptedHero, ShouldBeMounted(formationClass), formationClass,
                        heroSummonState, heroSummonState.WasPlayerSide);
                }

                heroSummonState.CurrentAgent = agent;
                heroSummonState.State = AgentState.Active;
                heroSummonState.TimesSummoned++;
                heroSummonState.SummonTime = CampaignHelpers.GetTotalMissionTime();
                // If hero isn't registered yet then this must be a hero that is part of one of the involved parties
                // already
                HeroDeathSpecifics.Remove(adoptedHero);

            });
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            SafeCall(() =>
            {
                var heroSummonState = heroSummonStates.FirstOrDefault(h => h.CurrentAgent == affectedAgent);
                if (heroSummonState != null)
                {
                    heroSummonState.State = agentState;
                }
                var adoptedHero = affectedAgent.GetAdoptedHero();
                if (adoptedHero != null && (affectedAgent.State == AgentState.Unconscious || affectedAgent.State == AgentState.Killed))
                {
                    HeroDeathSpecifics[adoptedHero] = (affectorAgent, blow);
                }

                // Set the final retinue states
                var (retinueOwner, retinueState) = heroSummonStates
                    .Select(h
                        => (state: h, retinue: h.Retinue.FirstOrDefault(r => r.Agent == affectedAgent)))
                    .FirstOrDefault(h => h.retinue != null);
                var (retinue2Owner, retinue2State) = heroSummonStates
                    .Select(h
                        => (state: h, retinue2: h.Retinue2.FirstOrDefault(r => r.Agent == affectedAgent)))
                    .FirstOrDefault(h => h.retinue2 != null);

                bool inRetinue1 = retinueOwner != null;
                bool inRetinue2 = retinue2Owner != null;

                bool runRetinue1 = false;
                bool runRetinue2 = false;

                if (inRetinue1 && inRetinue2)
                {
                    if (!_retinueResolution.TryGetValue(affectedAgent, out var useRetinue1))
                    {
                        useRetinue1 = MBRandom.RandomInt(0, 2) == 0;
                        _retinueResolution[affectedAgent] = useRetinue1;
                    }

                    runRetinue1 = useRetinue1;
                    runRetinue2 = !useRetinue1;
                }
                else
                {
                    runRetinue1 = inRetinue1;
                    runRetinue2 = inRetinue2;
                }


                if (runRetinue1 && retinueState != null && BLTAdoptAHeroModule.CommonConfig.RetinueDeathChance != 0)
                {
                    if (retinueState.Died)
                        return;

                    if (BLTAdoptAHeroModule.CommonConfig.RetinueDeathChance != 0f &&
                        agentState == AgentState.Killed &&
                        MBRandom.RandomFloat < BLTAdoptAHeroModule.CommonConfig.RetinueDeathChance)
                    {
                        retinueState.Died = true;
                        BLTAdoptAHeroCampaignBehavior.Current.KillRetinue(
                            retinueOwner.Hero,
                            affectedAgent.Character);
                        if (retinueOwner.Hero.FirstName != null)
                        {
                            Log.LogFeedResponse(
                                retinueOwner.Hero.FirstName.ToString(),
                                $"Your {affectedAgent.Character} was killed in battle!");
                        }
                    }
                    retinueState.State = agentState;  // Always update state
                }

                if (runRetinue2 && retinue2State != null && BLTAdoptAHeroModule.CommonConfig.Retinue2DeathChance != 0)
                {
                    if (retinue2State.Died)
                        return;

                    if (BLTAdoptAHeroModule.CommonConfig.Retinue2DeathChance != 0f &&
                        agentState == AgentState.Killed &&
                        MBRandom.RandomFloat < BLTAdoptAHeroModule.CommonConfig.Retinue2DeathChance)
                    {
                        retinue2State.Died = true;
                        BLTAdoptAHeroCampaignBehavior.Current.KillRetinue2(
                            retinue2Owner.Hero,
                            affectedAgent.Character);
                        if (retinue2Owner.Hero.FirstName != null)
                        {
                            Log.LogFeedResponse(
                                retinue2Owner.Hero.FirstName.ToString(),
                                $"Your {affectedAgent.Character} was killed in battle!");
                        }
                    }
                    retinue2State.State = agentState;  // Always update state
                }

                if ((retinue2State != null && retinue2State.Died) || (retinueState != null && retinueState.Died))
                {
                    _retinueResolution.Remove(affectedAgent);
                }

            });
        }

        public void DoNextTick(Action action)
        {
            onTickActions.Add(action);
        }
        
        private float autoSummonTimer = 0f;
        private float autoFormationTimer = 0f;
        private float autoAttackTimer = 0f;

        public override void OnMissionTick(float dt)
        {
            SafeCall(() =>
            {
                var actionsToDo = onTickActions.ToList();
                onTickActions.Clear();
                foreach (var action in actionsToDo)
                {
                    action();
                }

                var mission = Mission.Current;
                if (mission == null || mission.IsNavalBattle)
                    return;
                
                if (BLTAdoptAHeroModule.CommonConfig.AutoFormationForHeroes && !IsDeploymentPhase())
                {
                    autoFormationTimer += dt;
                    if (autoFormationTimer >= 1f)
                    {
                        autoFormationTimer = 0f;
                        EnforceHeroFormationRules();
                    }
                }
                else
                {
                    autoFormationTimer = 0f;
                }
                
                if (BLTAdoptAHeroModule.CommonConfig.EnableEnemyBltAttackCommand
                    && !IsDeploymentPhase()
                    && !mission.IsSiegeBattle)
                {
                    autoAttackTimer += dt;
                    if (autoAttackTimer >= 1f)
                    {
                        autoAttackTimer = 0f;
                        EnforceEnemyBltAttackFormationRules();
                    }
                }
                else
                {
                    autoAttackTimer = 0f;
                }
                
                if (BLTAdoptAHeroModule.CommonConfig.AutoSummonStreamerOnly)
                {
                    AutoSummonHeroes(dt);
                }
                else
                {
                    autoSummonTimer = 0f;
                }
            });
        }

        private void EnforceHeroFormationRules()
        {
            if (Mission.Current?.PlayerTeam == null)
                return;

            var targetIndices = Mission.Current.IsSiegeBattle
                ? new[] { 6, 7 }
                : new[] { 4, 5, 6, 7 };

            foreach (var i in targetIndices)
            {
                var fc = (FormationClass)i;
                if (Mission.Current.PlayerTeam.GetFormation(fc) == null)
                {
                    Mission.Current.PlayerTeam.FormationsIncludingEmpty
                        .Add(new Formation(Mission.Current.PlayerTeam, i));
                }
            }

            foreach (var agent in Mission.Current.Agents)
            {
                if (!agent.IsActive() || !agent.IsHuman || !agent.IsAIControlled)
                    continue;

                if (agent.Character == null || !agent.Character.IsHero)
                    continue;

                if (agent.Team == null || !agent.Team.IsFriendOf(Mission.Current.PlayerTeam))
                    continue;

                var adoptedHero = agent.GetAdoptedHero();
                if (adoptedHero == null)
                    continue;

                FormationClass fClass;
                if (agent.HasMount && HasRanged(agent) && HasAmmo(agent))
                    fClass = FormationClass.HorseArcher;
                else if (HasRanged(agent) && HasAmmo(agent))
                    fClass = FormationClass.Ranged;
                else if (agent.HasMount)
                    fClass = FormationClass.Cavalry;
                else
                    fClass = FormationClass.Infantry;

                int index = Mission.Current.IsSiegeBattle
                    ? (fClass == FormationClass.Ranged ? 7 : 6)
                    : 4 + (int)fClass;

                var form = Mission.Current.PlayerTeam.GetFormation((FormationClass)index);
                if (form != null && agent.Formation != form)
                {
                    agent.Formation = form;
                }
            }
        }

        private bool HasAmmo(Agent agent)
        {
            if (agent.Equipment == null)
                return false;

            foreach (var index in new[]
            {
        EquipmentIndex.Weapon0,
        EquipmentIndex.Weapon1,
        EquipmentIndex.Weapon2,
        EquipmentIndex.Weapon3
    })
            {
                var item = agent.Equipment[index];
                if (item.IsAnyAmmo() && item.Amount > 0)
                    return true;
            }

            return false;
        }

        private bool HasRanged(Agent agent)
        {
            var eq = agent.SpawnEquipment;
            return eq?.HasWeaponOfClass(WeaponClass.Bow) == true
                || eq?.HasWeaponOfClass(WeaponClass.Crossbow) == true;
        }
        
        private void EnforceEnemyBltAttackFormationRules()
        {
            if (Mission.Current?.PlayerEnemyTeam == null)
                return;

            var enemyTeam = Mission.Current.PlayerEnemyTeam;

            const int targetIndex = 7;
            var targetClass = (FormationClass)targetIndex;

            if (enemyTeam.GetFormation(targetClass) == null)
            {
                enemyTeam.FormationsIncludingEmpty.Add(new Formation(enemyTeam, targetIndex));
            }

            var targetFormation = enemyTeam.GetFormation(targetClass);
            if (targetFormation == null)
                return;

            bool hasAnyBltHeroes = false;

            foreach (var agent in Mission.Current.Agents)
            {
                if (!agent.IsActive() || !agent.IsHuman || !agent.IsAIControlled)
                    continue;

                if (agent.Team != enemyTeam)
                    continue;

                if (agent.Character?.IsHero != true)
                    continue;

                var adoptedHero = agent.GetAdoptedHero();
                if (adoptedHero == null)
                    continue;

                hasAnyBltHeroes = true;

                if (agent.Formation != targetFormation)
                {
                    agent.Formation = targetFormation;
                }
            }

            if (!hasAnyBltHeroes)
                return;

            var controller = enemyTeam.MasterOrderController;
            if (controller == null)
                return;

            controller.ClearSelectedFormations();
            controller.SelectFormation(targetFormation);
            controller.SetOrder(OrderType.Charge);
        }

        private bool IsDeploymentPhase()
        {
            return Mission.Current?.Mode == MissionMode.Deployment;
        }
        
        private void AutoSummonHeroes(float dt)
        {
            autoSummonTimer += dt;
            if (autoSummonTimer < 3f)
                return;

            autoSummonTimer = 0f;

            var mission = Mission.Current;
            if (mission == null)
                return;

            if (!mission.IsFieldBattle && !mission.IsSiegeBattle)
                return;

            var cfg = BLTAdoptAHeroModule.CommonConfig;
            if (cfg == null || !cfg.AutoSummonStreamerOnly)
                return;

            var streamerLogin = TwitchSharedState.StreamerLogin;
            if (string.IsNullOrWhiteSpace(streamerLogin))
                return;

            var expectedHeroName = streamerLogin + " [BLT]";

            var targetHero = Hero.AllAliveHeroes
                .Where(h => h.IsAdopted())
                .FirstOrDefault(h =>
                    string.Equals(h.Name?.ToString(), expectedHeroName, StringComparison.OrdinalIgnoreCase));

            if (targetHero == null)
                return;

            var targetState = GetHeroSummonState(targetHero);

            if (targetState != null && (targetState.State == AgentState.Active || targetState.InCooldown))
                return;

            var summonCfg = new SummonHero.Settings
            {
                AllowFieldBattle = true,
                AllowSiegeBattle = true,
                AllowVillageBattle = false,
                AllowFriendlyMission = false,
                AllowHideOut = false,

                OnPlayerSide = !cfg.AutoSummonStreamerEnemySide,
                WithRetinue = true,
                AllowWhenDepleted = true,
                HealPerSecond = 2,
                GoldCost = 0,
                PreferredFormation = "Infantry"
            };

            var fakeContext = ReplyContext.FromUser(null, targetHero.Name.ToString(), "");

            try
            {
                SummonHero.AutoSummon(targetHero, summonCfg, fakeContext);
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoSummon] Exception during summon: {ex}");
            }
        }

        protected override void OnEndMission()
        {
            SafeCall(() =>
            {
                // Remove still living retinue troops from their parties
                foreach (var h in heroSummonStates)
                {
                    foreach (var r in h.Retinue.Where(r => r.State != AgentState.Killed))
                    {
                        h.Party?.MemberRoster?.AddToCounts(r.Troop, -1);
                    }
                }
            });
        }

        private static void SpawnRetinue(Hero adoptedHero, bool ownerIsMounted, FormationClass ownerFormationClass,
            HeroSummonState existingHero, bool onPlayerSide)
        {
            var retinueTroops = BLTAdoptAHeroCampaignBehavior.Current.GetRetinue(adoptedHero).ToList();
            var retinue2Troops = BLTAdoptAHeroCampaignBehavior.Current.GetRetinue2(adoptedHero).ToList();

            bool retinueMounted = Mission.Current.Mode != MissionMode.Stealth
                                  && !MissionHelpers.InSiegeMission()
                                  && (ownerIsMounted || !BLTAdoptAHeroModule.CommonConfig.RetinueUseHeroesFormation);
            var agent_name = AccessTools.Field(typeof(Agent), "_name");
            foreach (var retinueTroop in retinueTroops)
            {
                // Don't modify formation for non-player side spawn as we don't really care
                bool hasPrevFormation = Campaign.Current.PlayerFormationPreferences
                                            .TryGetValue(retinueTroop, out var prevFormation)
                                        && onPlayerSide
                                        && BLTAdoptAHeroModule.CommonConfig.RetinueUseHeroesFormation;

                if (onPlayerSide && BLTAdoptAHeroModule.CommonConfig.RetinueUseHeroesFormation)
                {
                    Campaign.Current.SetPlayerFormationPreference(retinueTroop, ownerFormationClass);
                }

                existingHero.Party.MemberRoster.AddToCounts(retinueTroop, 1);

                bool DeploymentFlag = Mission.Current.Mode is MissionMode.Deployment;
                var retinueAgent = SpawnAgent(onPlayerSide, retinueTroop, existingHero.Party,
                    retinueTroop.IsMounted && retinueMounted, false, !DeploymentFlag);

                existingHero.Retinue.Add(new()
                {
                    Troop = retinueTroop,
                    Agent = retinueAgent,
                    State = AgentState.Active,
                });

                agent_name.SetValue(retinueAgent, new TextObject($"{retinueAgent.Name} ({adoptedHero.FirstName})"));

                retinueAgent.BaseHealthLimit *= Math.Max(1, BLTAdoptAHeroModule.CommonConfig.StartRetinueHealthMultiplier);
                retinueAgent.HealthLimit *= Math.Max(1, BLTAdoptAHeroModule.CommonConfig.StartRetinueHealthMultiplier);
                retinueAgent.Health *= Math.Max(1, BLTAdoptAHeroModule.CommonConfig.StartRetinueHealthMultiplier);

                BLTAdoptAHeroCustomMissionBehavior.Current.AddListeners(retinueAgent,
                    onGotAKill: (killer, killed, state) =>
                    {
                        Log.Trace($"[{nameof(SummonHero)}] {retinueAgent.Name} killed {killed?.Name ?? "unknown"}");
                        BLTAdoptAHeroCommonMissionBehavior.Current.ApplyKillEffects(
                            adoptedHero, killer, killed, state,
                            BLTAdoptAHeroModule.CommonConfig.RetinueGoldPerKill,
                            BLTAdoptAHeroModule.CommonConfig.RetinueHealPerKill,
                            0, 1,
                            BLTAdoptAHeroModule.CommonConfig.RelativeLevelScaling,
                            BLTAdoptAHeroModule.CommonConfig.LevelScalingCap,
                            BLTAdoptAHeroModule.CommonConfig.MinimumGoldPerKill
                        );
                    }
                );

                if (hasPrevFormation)
                {
                    Campaign.Current.SetPlayerFormationPreference(retinueTroop, prevFormation);
                }
            }
            foreach (var retinue2Troop in retinue2Troops)
            {
                // Don't modify formation for non-player side spawn as we don't really care
                bool hasPrevFormation = Campaign.Current.PlayerFormationPreferences
                                            .TryGetValue(retinue2Troop, out var prevFormation)
                                        && onPlayerSide
                                        && BLTAdoptAHeroModule.CommonConfig.RetinueUseHeroesFormation;

                if (onPlayerSide && BLTAdoptAHeroModule.CommonConfig.RetinueUseHeroesFormation)
                {
                    Campaign.Current.SetPlayerFormationPreference(retinue2Troop, ownerFormationClass);
                }

                existingHero.Party.MemberRoster.AddToCounts(retinue2Troop, 1);

                bool DeploymentFlag = Mission.Current.Mode is MissionMode.Deployment;
                var retinue2Agent = SpawnAgent(onPlayerSide, retinue2Troop, existingHero.Party,
                    retinue2Troop.IsMounted && retinueMounted, false, !DeploymentFlag);

                existingHero.Retinue.Add(new()
                {
                    Troop = retinue2Troop,
                    Agent = retinue2Agent,
                    State = AgentState.Active,
                });

                agent_name.SetValue(retinue2Agent, new TextObject($"{retinue2Agent.Name} ({adoptedHero.FirstName})"));

                retinue2Agent.BaseHealthLimit *= Math.Max(1, BLTAdoptAHeroModule.CommonConfig.StartRetinueHealthMultiplier);
                retinue2Agent.HealthLimit *= Math.Max(1, BLTAdoptAHeroModule.CommonConfig.StartRetinueHealthMultiplier);
                retinue2Agent.Health *= Math.Max(1, BLTAdoptAHeroModule.CommonConfig.StartRetinueHealthMultiplier);

                BLTAdoptAHeroCustomMissionBehavior.Current.AddListeners(retinue2Agent,
                    onGotAKill: (killer, killed, state) =>
                    {
                        Log.Trace($"[{nameof(SummonHero)}] {retinue2Agent.Name} killed {killed?.Name ?? "unknown"}");
                        BLTAdoptAHeroCommonMissionBehavior.Current.ApplyKillEffects(
                            adoptedHero, killer, killed, state,
                            BLTAdoptAHeroModule.CommonConfig.RetinueGoldPerKill,
                            BLTAdoptAHeroModule.CommonConfig.RetinueHealPerKill,
                            0, 1,
                            BLTAdoptAHeroModule.CommonConfig.RelativeLevelScaling,
                            BLTAdoptAHeroModule.CommonConfig.LevelScalingCap,
                            BLTAdoptAHeroModule.CommonConfig.MinimumGoldPerKill
                        );
                    }
                );

                if (hasPrevFormation)
                {
                    Campaign.Current.SetPlayerFormationPreference(retinue2Troop, prevFormation);
                }
            }
        }

        public static Agent SpawnAgent(bool onPlayerSide, CharacterObject troop, PartyBase party, bool spawnWithHorse, bool isReinforcement = false, bool isAlarmed = true)
        {
            var agent = Mission.Current.SpawnTroop(
                new PartyAgentOrigin(party, troop)
                , isPlayerSide: onPlayerSide
                , hasFormation: true
                , spawnWithHorse: spawnWithHorse
                , isReinforcement: isReinforcement
                , formationTroopCount: 1
                , formationTroopIndex: 0
                , isAlarmed: isAlarmed
                , wieldInitialWeapons: true
                , initialPosition: null
                , initialDirection: null
            );
            agent.MountAgent?.FadeIn();
            agent.FadeIn();
            return agent;
        }

        public static bool ShouldBeMounted(FormationClass formationClass)
        {
            return Mission.Current.Mode != MissionMode.Stealth
                   && !MissionHelpers.InSiegeMission()
                   && Mission.Current?.IsNavalBattle == false
                   && formationClass is
                       FormationClass.Cavalry or
                       FormationClass.LightCavalry or
                       FormationClass.HeavyCavalry or
                       FormationClass.HorseArcher;
        }

        public static bool RetinueAllowed() => MissionHelpers.InSiegeMission() || MissionHelpers.InFieldBattleMission();
    }
}