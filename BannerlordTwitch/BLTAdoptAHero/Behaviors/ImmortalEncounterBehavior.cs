using System;
using System.Collections.Generic;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.SaveSystem;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Util;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using Helpers;

namespace BLTAdoptAHero.Behaviors
{
    public sealed class ImmortalEncounterBehavior : CampaignBehaviorBase
    {
        public static ImmortalEncounterBehavior Current => Campaign.Current?.GetCampaignBehavior<ImmortalEncounterBehavior>();

        private ImmortalEncounterState state;
        private double lastSuccessfulTriggerDay = -100000;
        private bool cleaningUp;
        private bool healthCancellationPending;

        public bool BattleActive => state?.Phase is RandomEventLifecycle.BattlePending or RandomEventLifecycle.Active;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnCampaignTick);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, RegisterDialogs);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnPartyDestroyed);
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, OnGameLoadFinished);
        }

        public override void SyncData(IDataStore dataStore)
        {
            using var sync = new ScopedJsonSync(dataStore, nameof(ImmortalEncounterBehavior));
            sync.SyncDataAsJson("StateV1", ref state);
            dataStore.SyncData("LastSuccessfulTriggerDay", ref lastSuccessfulTriggerDay);
            if (state != null)
            {
                state.ParticipantHeroIds ??= new HashSet<string>(StringComparer.Ordinal);
                state.RewardedHeroIds ??= new HashSet<string>(StringComparer.Ordinal);
            }
        }

        public bool CanSummon(bool onPlayerSide, out string reason)
        {
            reason = null;
            if (!BattleActive || onPlayerSide) return true;
            reason = "Enemy-side summons are disabled during the Immortal Encounter.";
            return false;
        }

        public void MarkMissionParticipant(Hero hero, bool onPlayerSide)
        {
            if (!BattleActive || !onPlayerSide || hero?.IsAdopted() != true) return;
            if (RandomEventPolicy.RecordParticipant(state, hero.StringId))
                Diagnostic($"registered player-side participant {hero.StringId}");
        }

        private void OnDailyTick()
        {
            var cfg = BLTAdoptAHeroModule.EventConfig;
            if (cfg?.RandomEventsEnabled != true || cfg.ImmortalEncounterEnabled != true || !Eligible(out _)) return;
            double day = CampaignTime.Now.ToDays;
            if (!RandomEventPolicy.CanRoll(day, lastSuccessfulTriggerDay, cfg.ImmortalEncounterCooldownDays, state != null && RandomEventPolicy.IsActive(state.Phase))) return;
            float chance = RandomEventPolicy.ClampChance(cfg.ImmortalEncounterDailyChancePercent);
            float roll = MBRandom.RandomFloat * 100f;
            Diagnostic($"daily roll {roll:0.000}/{chance:0.000}");
            if (RandomEventPolicy.RollSucceeds(roll, chance)) TryStart(false, out _);
        }

        private bool Eligible(out string reason)
        {
            var cfg = BLTAdoptAHeroModule.EventConfig;
            if (state != null && RandomEventPolicy.IsActive(state.Phase)) { reason = "an Immortal Encounter is already active"; return false; }
            if (Hero.MainHero?.IsAlive != true) { reason = "the main hero is not alive"; return false; }
            if (!PlayerHealthyEnough()) { reason = "the player must have more than 20% health"; return false; }
            if (Hero.MainHero.Level < (cfg?.ImmortalEncounterMinimumPlayerLevel ?? 10)) { reason = "the main hero level is too low"; return false; }
            if (MobileParty.MainParty?.IsActive != true) { reason = "the main party is unavailable"; return false; }
            if (Mission.Current != null || PlayerEncounter.Current != null) { reason = "a mission or encounter is active"; return false; }
            if (Campaign.Current?.ConversationManager?.IsConversationInProgress == true) { reason = "a conversation is active"; return false; }
            if (MobileParty.MainParty.CurrentSettlement != null || MobileParty.MainParty.IsGarrison || MobileParty.MainParty.BesiegedSettlement != null || MobileParty.MainParty.MapEvent != null)
            { reason = "the main party is in a settlement, siege, or map event"; return false; }
            reason = null;
            return true;
        }

        private bool TryStart(bool manual, out string result)
        {
            if (BLTAdoptAHeroModule.EventConfig?.RandomEventsEnabled != true)
            {
                result = "{=BLTRandomEventsDisabled}Random events are disabled.".Translate();
                return false;
            }
            if (!Eligible(out result)) return false;
            Clan clan = null;
            Hero hero = null;
            MobileParty party = null;
            state = new ImmortalEncounterState { Phase = RandomEventLifecycle.Preparing, StartedDay = CampaignTime.Now.ToDays };
            try
            {
                var cfg = BLTAdoptAHeroModule.EventConfig;
                string suffix = $"{CampaignTime.Now.ToHours:F0}_{MBRandom.RandomInt(1000000)}";
                var culture = Hero.MainHero.Culture;
                clan = Clan.CreateClan($"blt_immortal_clan_{suffix}");
                string clanName = SafeName(cfg.ImmortalEncounterCultName, "Cult of the Eternal");
                clan.ChangeClanName(new TextObject(clanName), new TextObject(clanName));
                clan.Culture = culture;
                clan.Banner = Banner.CreateRandomBanner();
                clan.Kingdom = null;
                clan.SetInitialHomeSettlement(Settlement.All.FirstOrDefault(s => s.Culture == culture) ?? Settlement.All.FirstOrDefault());
                clan.IsNoble = true;
                var template = CampaignHelpers.GetWandererTemplates(culture).FirstOrDefault(t => !t.IsFemale)
                    ?? CampaignHelpers.GetWandererTemplates(culture).FirstOrDefault()
                    ?? CampaignHelpers.AllWandererTemplates.FirstOrDefault();
                if (template == null) throw new InvalidOperationException("no loaded hero template was available");
                hero = HeroCreator.CreateSpecialHero(template, null, null, null, -1);
                hero.SetName(new TextObject(SafeName(cfg.ImmortalEncounterName, "The Immortal")), new TextObject(SafeName(cfg.ImmortalEncounterName, "The Immortal")));
                hero.Clan = clan;
                clan.SetLeader(hero);
                hero.CompanionOf = null;
                CampaignEventDispatcher.Instance.OnClanCreated(clan, false);
                var spawn = Settlement.All.OrderBy(s => s.GetPosition2D.Distance(MobileParty.MainParty.GetPosition2D)).FirstOrDefault();
                if (spawn == null) throw new InvalidOperationException("no settlement spawn position was available");
                party = MobilePartyHelper.SpawnLordParty(hero, spawn.GatePosition, 1f);
                if (party == null) throw new InvalidOperationException("Bannerlord could not create the cult party");
                party.InitializeMobilePartyAtPosition(MobileParty.MainParty.Position);
                int size = RandomEventPolicy.CalculateArmySize(MobileParty.MainParty.Party.EstimatedStrength,
                    cfg.ImmortalEncounterArmyStrengthPercent, 50, 500);
                AddTroops(party, culture, size, 4, 6);
                if (party.MemberRoster.TotalManCount == 0) throw new InvalidOperationException("no valid cult troops were available");
                party.Ai.SetDoNotMakeNewDecisions(true);
                state.ClanId = clan.StringId;
                state.HeroId = hero.StringId;
                state.PartyId = party.StringId;
                state.Phase = RandomEventLifecycle.AwaitingResponse;
                EncounterManager.StartPartyEncounter(MobileParty.MainParty.Party, party.Party);
                lastSuccessfulTriggerDay = CampaignTime.Now.ToDays;
                result = "{=BLTImmortalStarted}Immortal Encounter started with {TroopCount} troops."
                    .Translate(("TroopCount", party.MemberRoster.TotalManCount));
                Diagnostic(result + (manual ? " (manual)" : string.Empty));
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[Immortal Encounter] construction failed: {ex}");
                Rollback(party, hero);
                state = null;
                result = "{=BLTImmortalStartFailed}Immortal Encounter could not start: {Error}"
                    .Translate(("Error", ex.Message));
                return false;
            }
        }

        private void RegisterDialogs(CampaignGameStarter starter)
        {
            starter.AddDialogLine("blt_immortal_start", "start", "blt_immortal_challenge",
                "{=BLTImmortalDialogChallenge}You bring strange warriors from beyond this world. Let us discover whether they can bleed.", IsImmortalConversation, null, 1000);
            starter.AddDialogLine("blt_immortal_taunt", "blt_immortal_challenge", "blt_immortal_response",
                "{=BLTImmortalDialogReward}Face me and my faithful. Triumph, and those who stand with you will be richly rewarded.", null, null, 100);
            starter.AddPlayerLine("blt_immortal_accept", "blt_immortal_response", "blt_immortal_accepted", "{=BLTImmortalDialogAccept}I accept your challenge.", null, null, 100);
            starter.AddPlayerLine("blt_immortal_refuse", "blt_immortal_response", "blt_immortal_refused", "{=BLTImmortalDialogRefuse}No. Leave us.", null, null, 100);
            starter.AddDialogLine("blt_immortal_accept_response", "blt_immortal_accepted", "close_window", "{=BLTImmortalDialogDraw}Then draw your weapon.", null, AcceptBattle, 100);
            starter.AddDialogLine("blt_immortal_refuse_response", "blt_immortal_refused", "close_window", "{=BLTImmortalDialogAnotherDay}Another day, mortal.", null, RefuseBattle, 100);
        }

        private bool IsImmortalConversation() => state?.Phase == RandomEventLifecycle.AwaitingResponse && Hero.OneToOneConversationHero?.StringId == state.HeroId;

        private static bool PlayerHealthyEnough() => Hero.MainHero?.IsAlive == true
            && RandomEventPolicy.CanStartImmortalBattle(Hero.MainHero.HitPoints, Hero.MainHero.MaxHitPoints);

        private void AcceptBattle()
        {
            if (state?.Phase != RandomEventLifecycle.AwaitingResponse) return;
            if (!PlayerHealthyEnough())
            {
                // Let the closing dialog release its hero/party before destroying them.
                healthCancellationPending = true;
                PlayerEncounter.LeaveEncounter = true;
                Log.LogFeedEvent("{=BLTImmortalLowHealth}The Immortal withdrew: you must have more than 20% health to fight.".Translate());
                return;
            }
            try
            {
                state.Phase = RandomEventLifecycle.BattlePending;
                PlayerEncounter.StartBattle();
                state.Phase = RandomEventLifecycle.Active;
            }
            catch (Exception ex) { Abort($"battle start failed: {ex.Message}"); }
        }

        private void OnCampaignTick(float dt)
        {
            if (!healthCancellationPending || Mission.Current != null
                || Campaign.Current?.ConversationManager?.IsConversationInProgress == true) return;
            healthCancellationPending = false;
            if (PlayerEncounter.Current != null && PlayerEncounter.EncounteredMobileParty?.StringId == state?.PartyId)
                PlayerEncounter.Finish(false);
            Complete(false, "player health is at or below 20%");
        }

        private void RefuseBattle()
        {
            if (state?.Phase != RandomEventLifecycle.AwaitingResponse) return;
            Complete(false, "challenge refused");
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (!BattleActive || mapEvent == null || !mapEvent.InvolvedParties.Any(p => p.MobileParty?.StringId == state.PartyId)) return;
            bool playerWon = mapEvent.Winner != null && MobileParty.MainParty?.Party?.MapEventSide == mapEvent.Winner;
            if (playerWon) RewardParticipants();
            Complete(playerWon, playerWon ? "player victory" : "loss or retreat");
        }

        private void RewardParticipants()
        {
            int reward = RandomEventPolicy.ClampReward(BLTAdoptAHeroModule.EventConfig.ImmortalEncounterGoldReward);
            foreach (string heroId in state.ParticipantHeroIds.ToList())
            {
                if (!RandomEventPolicy.RecordReward(state, heroId)) continue;
                var hero = MBObjectManager.Instance.GetObject<Hero>(heroId);
                if (hero?.IsAlive == true) BLTAdoptAHeroCampaignBehavior.Current?.ChangeHeroGold(hero, reward);
            }
        }

        private void OnPartyDestroyed(MobileParty party, PartyBase _) { if (!cleaningUp && party?.StringId == state?.PartyId) Complete(false, "temporary party destroyed"); }
        private void OnHeroKilled(Hero victim, Hero _, KillCharacterAction.KillCharacterActionDetail __, bool ___) { if (!cleaningUp && victim?.StringId == state?.HeroId) Complete(false, "the Immortal died"); }
        private void OnGameLoadFinished() { if (state != null && RandomEventPolicy.IsActive(state.Phase)) Abort("active encounter UI cannot be resumed safely after loading"); }

        private void Complete(bool victory, string reason)
        {
            healthCancellationPending = false;
            Diagnostic($"completed ({reason})");
            if (state != null) state.Phase = RandomEventLifecycle.Resolved;
            CleanupObjects();
            state = null;
            if (victory) Log.LogFeedEvent("{=BLTImmortalDefeated}The Immortal was defeated. Player-side BLT participants received their reward.".Translate());
        }

        private void Abort(string reason)
        {
            healthCancellationPending = false;
            Log.Error($"[Immortal Encounter] aborted: {reason}");
            if (state != null) state.Phase = RandomEventLifecycle.Failed;
            CleanupObjects();
            state = null;
        }

        private void CleanupObjects()
        {
            if (cleaningUp) return;
            cleaningUp = true;
            try
            {
                var party = ResolveParty(state?.PartyId);
                var hero = ResolveHero(state?.HeroId);
                Rollback(party, hero);
            }
            finally { cleaningUp = false; }
        }

        private static void Rollback(MobileParty party, Hero hero)
        {
            if (party?.IsActive == true) DestroyPartyAction.Apply(null, party);
            if (hero?.IsAlive == true) KillCharacterAction.ApplyByRemove(hero, true);
        }

        private static MobileParty ResolveParty(string id) => string.IsNullOrWhiteSpace(id) ? null : MobileParty.All.FirstOrDefault(p => p.StringId == id);
        private static Hero ResolveHero(string id) => string.IsNullOrWhiteSpace(id) ? null : MBObjectManager.Instance.GetObject<Hero>(id);
        private static string SafeName(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

        private static void AddTroops(MobileParty party, CultureObject culture, int total, int minTier, int maxTier)
        {
            var troops = CharacterObject.All.Where(c => !c.IsHero && c.Occupation == Occupation.Soldier && c.Tier >= minTier && c.Tier <= maxTier && c.Culture == culture).ToList();
            if (troops.Count == 0) troops = CharacterObject.All.Where(c => !c.IsHero && c.Occupation == Occupation.Soldier && c.Tier >= minTier && c.Tier <= maxTier).ToList();
            if (troops.Count == 0 && culture?.BasicTroop != null) troops.Add(culture.BasicTroop);
            for (int remaining = total, i = 0; remaining > 0 && troops.Count > 0; i++)
            {
                int count = Math.Max(1, remaining / Math.Max(1, Math.Min(4, troops.Count) - i));
                count = Math.Min(count, remaining);
                party.MemberRoster.AddToCounts(troops[i % troops.Count], count);
                remaining -= count;
            }
        }

        private static void Diagnostic(string message) { if (BLTAdoptAHeroModule.EventConfig?.ImmortalEncounterDiagnostics == true) Log.Info($"[Immortal Encounter] {message}"); }

        [CommandLineFunctionality.CommandLineArgumentFunction("trigger_immortal_event", "blt")]
        [UsedImplicitly]
        public static string TriggerImmortalEvent(List<string> _)
        {
            if (Current == null) return "{=BLTNoActiveCampaign}No active campaign.".Translate();
            Current.TryStart(true, out string result);
            return result;
        }
    }
}
