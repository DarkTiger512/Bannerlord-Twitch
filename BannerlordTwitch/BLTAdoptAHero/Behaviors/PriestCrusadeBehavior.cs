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
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using Helpers;

namespace BLTAdoptAHero.Behaviors
{
    public sealed class PriestCrusadeBehavior : CampaignBehaviorBase
    {
        public static PriestCrusadeBehavior Current => Campaign.Current?.GetCampaignBehavior<PriestCrusadeBehavior>();

        private PriestCrusadeState state;
        private double lastSuccessfulTriggerDay = -100000;
        private bool rollingBack;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.MakePeace.AddNonSerializedListener(this, OnPeace);
            CampaignEvents.OnClanDestroyedEvent.AddNonSerializedListener(this, OnClanDestroyed);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnPartyDestroyed);
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, (hero, _, _, _) => { if (!rollingBack && hero?.StringId == state?.LeaderId) CheckResolution(); });
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, CheckResolution);
        }

        public override void SyncData(IDataStore dataStore)
        {
            using var sync = new ScopedJsonSync(dataStore, nameof(PriestCrusadeBehavior));
            sync.SyncDataAsJson("StateV1", ref state);
            dataStore.SyncData("LastSuccessfulTriggerDay", ref lastSuccessfulTriggerDay);
        }

        private void OnDailyTick()
        {
            if (state?.Phase == RandomEventLifecycle.Active) { CheckResolution(); return; }
            var cfg = BLTAdoptAHeroModule.EventConfig;
            if (cfg?.RandomEventsEnabled != true || cfg.PriestCrusadeEnabled != true || !Eligible(out _)) return;
            double day = CampaignTime.Now.ToDays;
            if (!RandomEventPolicy.CanRoll(day, lastSuccessfulTriggerDay, cfg.PriestCrusadeCooldownDays, state != null && RandomEventPolicy.IsActive(state.Phase))) return;
            float chance = RandomEventPolicy.ClampChance(cfg.PriestCrusadeDailyChancePercent);
            float roll = MBRandom.RandomFloat * 100f;
            Diagnostic($"daily roll {roll:0.000}/{chance:0.000}");
            if (RandomEventPolicy.RollSucceeds(roll, chance)) TryStart(false, out _);
        }

        private bool Eligible(out string reason)
        {
            if (state != null && RandomEventPolicy.IsActive(state.Phase)) { reason = "a previous crusade remains active"; return false; }
            var clan = Hero.MainHero?.Clan;
            var kingdom = clan?.Kingdom;
            if (Hero.MainHero?.IsAlive != true || clan?.IsEliminated == true || kingdom?.IsEliminated != false)
            { reason = "the main hero does not belong to a living kingdom"; return false; }
            if (clan.Tier < (BLTAdoptAHeroModule.EventConfig?.PriestCrusadeMinimumKingdomTier ?? 2))
            { reason = "the player's clan tier is too low"; return false; }
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
            Hero priest = null;
            MobileParty party = null;
            state = new PriestCrusadeState { Phase = RandomEventLifecycle.Preparing, StartedDay = CampaignTime.Now.ToDays };
            try
            {
                var cfg = BLTAdoptAHeroModule.EventConfig;
                var target = Hero.MainHero.Clan.Kingdom;
                var culture = Hero.MainHero.Culture;
                string suffix = $"{CampaignTime.Now.ToHours:F0}_{MBRandom.RandomInt(1000000)}";
                clan = Clan.CreateClan($"blt_crusader_clan_{suffix}");
                string clanName = SafeName(cfg.PriestCrusadeClanName, "Holy Crusaders");
                clan.ChangeClanName(new TextObject(clanName), new TextObject(clanName));
                clan.Culture = culture;
                clan.Banner = Banner.CreateRandomBanner();
                clan.Kingdom = null;
                clan.IsNoble = true;
                Settlement objective = FindObjective(target);
                clan.SetInitialHomeSettlement(objective ?? Settlement.All.FirstOrDefault());
                var template = CampaignHelpers.GetWandererTemplates(culture).FirstOrDefault(t => !t.IsFemale && t.Occupation == Occupation.Preacher)
                    ?? CampaignHelpers.GetWandererTemplates(culture).FirstOrDefault(t => !t.IsFemale)
                    ?? CampaignHelpers.AllWandererTemplates.FirstOrDefault();
                if (template == null) throw new InvalidOperationException("no loaded priest template was available");
                priest = HeroCreator.CreateSpecialHero(template, objective);
                priest.SetName(new TextObject(SafeName(cfg.PriestCrusadePriestName, "Father Zealot")), new TextObject(SafeName(cfg.PriestCrusadePriestName, "Father Zealot")));
                priest.Clan = clan;
                clan.SetLeader(priest);
                CampaignEventDispatcher.Instance.OnClanCreated(clan, false);
                var spawn = objective ?? Settlement.All.FirstOrDefault();
                if (spawn == null) throw new InvalidOperationException("no settlement spawn position was available");
                party = MobilePartyHelper.SpawnLordParty(priest, spawn.GatePosition, 1f);
                if (party == null) throw new InvalidOperationException("Bannerlord could not create the crusader party");
                int size = RandomEventPolicy.CalculateArmySize(target.CurrentTotalStrength, cfg.PriestCrusadeArmyStrengthPercent, 100, 1000);
                AddTroops(party, size);
                if (party.MemberRoster.TotalManCount == 0) throw new InvalidOperationException("no valid crusader troops were available");
                party.Ai.SetDoNotMakeNewDecisions(false);
                state.ClanId = clan.StringId;
                state.LeaderId = priest.StringId;
                state.PartyId = party.StringId;
                state.TargetKingdomId = target.StringId;
                DeclareWarAction.ApplyByDefault(clan, target);
                if (!clan.IsAtWarWith(target)) throw new InvalidOperationException("Bannerlord did not establish the declared war");
                IssueObjective(party, objective);
                state.Phase = RandomEventLifecycle.Active;
                lastSuccessfulTriggerDay = CampaignTime.Now.ToDays;
                result = "{=BLTPriestCrusadeStarted}{ClanName} began a crusade against {KingdomName} with {TroopCount} troops."
                    .Translate(("ClanName", clan.Name), ("KingdomName", target.Name), ("TroopCount", party.MemberRoster.TotalManCount));
                Log.LogFeedEvent(result);
                Diagnostic(result + (manual ? " (manual)" : string.Empty));
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[Priest's Crusade] construction failed: {ex}");
                Rollback(party, priest);
                state = null;
                result = "{=BLTPriestCrusadeStartFailed}Priest's Crusade could not start: {Error}"
                    .Translate(("Error", ex.Message));
                return false;
            }
        }

        private static Settlement FindObjective(Kingdom target)
        {
            var settlements = target.Settlements.Where(s => s.IsFortification).ToList();
            if (settlements.Count == 0) settlements = target.Settlements.ToList();
            return settlements.OrderBy(s => s.GetPosition2D.Distance(MobileParty.MainParty.GetPosition2D)).FirstOrDefault();
        }

        private static void IssueObjective(MobileParty party, Settlement target)
        {
            var village = target?.BoundVillages?.FirstOrDefault()?.Settlement;
            if (village != null)
                SetPartyAiAction.GetActionForRaidingSettlement(party, village, MobileParty.NavigationType.All, false, false);
            else if (target != null)
                SetPartyAiAction.GetActionForPatrollingAroundSettlement(party, target, MobileParty.NavigationType.All, false, false);
        }

        private static void AddTroops(MobileParty party, int total)
        {
            var troops = CharacterObject.All.Where(c => !c.IsHero && c.Occupation == Occupation.Soldier && c.Tier >= 1 && c.Tier <= 3).ToList();
            if (troops.Count == 0 && party.LeaderHero?.Culture?.BasicTroop != null) troops.Add(party.LeaderHero.Culture.BasicTroop);
            int varieties = Math.Min(8, troops.Count);
            for (int remaining = total, i = 0; remaining > 0 && varieties > 0; i++)
            {
                int count = Math.Max(1, remaining / Math.Max(1, varieties - i));
                count = Math.Min(count, remaining);
                party.MemberRoster.AddToCounts(troops[i % troops.Count], count);
                remaining -= count;
            }
        }

        private void OnPeace(IFaction first, IFaction second, MakePeaceAction.MakePeaceDetail _)
        {
            if (state?.Phase != RandomEventLifecycle.Active) return;
            if ((first?.StringId == state.ClanId && second?.StringId == state.TargetKingdomId) ||
                (second?.StringId == state.ClanId && first?.StringId == state.TargetKingdomId)) Resolve("peace was made");
        }

        private void OnClanDestroyed(Clan clan) { if (clan?.StringId == state?.ClanId) Resolve("the crusader clan was eliminated"); }
        private void OnPartyDestroyed(MobileParty party, PartyBase _) { if (!rollingBack && party?.StringId == state?.PartyId) CheckResolution(); }

        private void CheckResolution()
        {
            if (state?.Phase != RandomEventLifecycle.Active) return;
            Clan clan = ResolveClan(state.ClanId);
            Kingdom target = ResolveKingdom(state.TargetKingdomId);
            bool eliminated = clan == null || clan.IsEliminated;
            bool atWar = clan != null && target != null && clan.IsAtWarWith(target);
            bool viable = clan != null && MobileParty.All.Any(p => p.IsActive && p.ActualClan == clan && p.MemberRoster.TotalHealthyCount > 0);
            if (RandomEventPolicy.CrusadeResolved(eliminated, atWar, viable))
                Resolve(eliminated ? "the crusader clan was eliminated" : !atWar ? "the war ended" : "the crusaders have no viable forces");
        }

        private void Resolve(string reason)
        {
            if (state == null) return;
            state.Phase = RandomEventLifecycle.Resolved;
            Diagnostic($"resolved: {reason}");
            Log.LogFeedEvent("{=BLTPriestCrusadeResolved}Priest's Crusade has ended.".Translate());
            state = null;
        }

        private void Rollback(MobileParty party, Hero hero)
        {
            rollingBack = true;
            try
            {
                if (party?.IsActive == true) DestroyPartyAction.Apply(null, party);
                if (hero?.IsAlive == true) KillCharacterAction.ApplyByRemove(hero, true);
            }
            finally { rollingBack = false; }
        }

        private static Clan ResolveClan(string id) => string.IsNullOrWhiteSpace(id) ? null : Clan.All.FirstOrDefault(c => c.StringId == id);
        private static Kingdom ResolveKingdom(string id) => string.IsNullOrWhiteSpace(id) ? null : Kingdom.All.FirstOrDefault(k => k.StringId == id);
        private static string SafeName(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        private static void Diagnostic(string message) { if (BLTAdoptAHeroModule.EventConfig?.PriestCrusadeDiagnostics == true) Log.Info($"[Priest's Crusade] {message}"); }

        [CommandLineFunctionality.CommandLineArgumentFunction("trigger_priest_crusade", "blt")]
        [UsedImplicitly]
        public static string TriggerPriestCrusade(List<string> _)
        {
            if (Current == null) return "{=BLTNoActiveCampaign}No active campaign.".Translate();
            Current.TryStart(true, out string result);
            return result;
        }
    }
}
