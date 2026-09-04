using System;
using System.Collections.Generic;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.SaveSystem;
using BannerlordTwitch.Util;
using BLTAdoptAHero.UI;
using BLTAdoptAHero.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace BLTAdoptAHero.Behaviors
{
    public sealed class StreamObjectivesBehavior : CampaignBehaviorBase
    {
        public static StreamObjectivesBehavior Current => Campaign.Current?.GetCampaignBehavior<StreamObjectivesBehavior>();
        private StreamObjectiveState active;
        private List<StreamObjectiveState> history = new();

        public StreamObjectiveState Active => active?.Status == StreamObjectiveStatus.Active ? active : null;
        public IReadOnlyList<StreamObjectiveState> History => history;

        public override void RegisterEvents()
        {
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, Publish);
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, (victim, _, _, _) =>
            {
                if (victim?.IsAdopted() == true && Active?.Kind == StreamObjectiveKind.Survive)
                    ResetSurvival(victim);
            });
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
        }

        public override void SyncData(IDataStore dataStore)
        {
            using var sync = new ScopedJsonSync(dataStore, nameof(StreamObjectivesBehavior));
            sync.SyncDataAsJson("Active", ref active);
            sync.SyncDataAsJson("History", ref history);
            history ??= new List<StreamObjectiveState>();
            StreamObjectivePolicy.RestoreCollections(active);
        }

        public bool Start(StreamObjectiveStart definition, string moderator, out string message)
        {
            if (!Enabled(out message)) return false;
            if (Active != null) { message = "A stream objective is already active. Complete or stop it first."; return false; }
            active = new StreamObjectiveState
            {
                Kind = definition.Kind, Target = definition.Target, RequiredHeroes = definition.RequiredHeroes,
                RequiredBattles = definition.RequiredBattles, GoldReward = definition.Gold, XPReward = definition.XP,
                CultureId = definition.CultureId, StartedBy = moderator, StartedAt = CampaignTime.Now.ToString()
            };
            message = $"Stream objective started: {StreamObjectivePolicy.Describe(active)}. Reward: {active.GoldReward} gold, {active.XPReward} XP.";
            Log.LogFeedEvent(message);
            Publish();
            return true;
        }

        public bool Cancel(out string message)
        {
            if (Active == null) { message = "No stream objective is active."; return false; }
            active.Status = StreamObjectiveStatus.Cancelled;
            active.FinishedAt = CampaignTime.Now.ToString();
            AddHistory(active);
            message = $"Stream objective cancelled at {ProgressText(active)}. No rewards were granted.";
            Log.LogFeedEvent(message);
            active = null;
            Publish();
            return true;
        }

        public string Status(string owner = null)
        {
            if (Active == null) return "No stream objective is active.";
            string own = "";
            if (!string.IsNullOrWhiteSpace(owner) && Active.Contributors.TryGetValue(owner, out var c))
                own = Active.Kind == StreamObjectiveKind.Survive ? $" Your streak: {c.SurvivalStreak}/{Active.RequiredBattles}." : $" Your contribution: {c.Amount}.";
            return $"{StreamObjectivePolicy.Describe(Active)}: {ProgressText(Active)}. Reward: {Active.GoldReward} gold, {Active.XPReward} XP.{own}";
        }

        public void RecordKill(Hero hero, Agent killed)
        {
            if (hero?.IsAdopted() != true || Active == null) return;
            bool matches = Active.Kind == StreamObjectiveKind.Kills ||
                           Active.Kind == StreamObjectiveKind.Cavalry && killed?.Character is CharacterObject troop &&
                           (troop.IsMounted || troop.DefaultFormationClass == FormationClass.HorseArcher);
            if (!matches) return;
            Record(hero, $"kill:{Mission.Current?.CurrentTime ?? 0:F3}:{hero.StringId}:{killed?.Index ?? -1}");
        }

        public void RecordTournamentWin(Hero hero)
        {
            if (Active?.Kind == StreamObjectiveKind.Tournaments && hero?.IsAdopted() == true)
                Record(hero, $"tournament:{CampaignTime.Now.ToHours:F3}:{hero.StringId}");
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (Active == null || mapEvent == null || mapEvent.Winner == null) return;
            var participants = ParticipatingAdoptedHeroes(mapEvent).Distinct().ToList();
            var winners = participants
                .Where(h => h.PartyBelongedTo?.MapEventSide == mapEvent.Winner).Distinct().ToList();
            string eventId = $"battle:{mapEvent.GetHashCode()}:{CampaignTime.Now.ToHours:F3}";
            if (Active.Kind == StreamObjectiveKind.Battles)
            {
                if (winners.Count == 0) return;
                var first = winners[0];
                string firstOwner = BLTAdoptAHeroCampaignBehavior.Current.GetHeroOwner(first);
                if (!StreamObjectivePolicy.AddProgress(Active, eventId, firstOwner, first.StringId, first.Name?.ToString())) return;
                foreach (var hero in winners.Skip(1))
                    StreamObjectivePolicy.AddContributor(Active, BLTAdoptAHeroCampaignBehavior.Current.GetHeroOwner(hero),
                        hero.StringId, hero.Name?.ToString());
                Changed();
            }
            else if (Active.Kind == StreamObjectiveKind.Survive)
            {
                if (participants.Count == 0) return;
                var survival = participants.Select(h =>
                {
                    string owner = BLTAdoptAHeroCampaignBehavior.Current.GetHeroOwner(h);
                    return (owner, h.StringId, h.Name?.ToString(), h.IsDead);
                });
                if (StreamObjectivePolicy.RecordSurvival(Active, eventId, survival)) Changed();
            }
        }

        private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner,
            Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            if (Active?.Kind != StreamObjectiveKind.Captures || settlement == null || capturerHero?.IsAdopted() != true) return;
            if (!settlement.IsTown && !settlement.IsCastle) return;
            if (!string.IsNullOrWhiteSpace(Active.CultureId) &&
                !string.Equals(settlement.Culture?.StringId, Active.CultureId, StringComparison.OrdinalIgnoreCase)) return;
            Record(capturerHero, $"capture:{settlement.StringId}:{newOwner?.StringId}:{CampaignTime.Now.ToHours:F3}");
        }

        private static IEnumerable<Hero> ParticipatingAdoptedHeroes(MapEvent mapEvent)
        {
            foreach (var party in mapEvent.InvolvedParties.Select(p => p.MobileParty).Where(p => p != null))
            {
                if (party.LeaderHero?.IsAdopted() == true) yield return party.LeaderHero;
                foreach (var hero in party.MemberRoster.GetTroopRoster()
                    .Select(e => e.Character?.HeroObject).Where(h => h?.IsAdopted() == true))
                    yield return hero;
            }
        }

        private void ResetSurvival(Hero hero)
        {
            string owner = BLTAdoptAHeroCampaignBehavior.Current.GetHeroOwner(hero);
            if (!string.IsNullOrWhiteSpace(owner) && Active.Contributors.TryGetValue(owner, out var c))
            { c.SurvivalStreak = 0; Active.Progress = Active.Contributors.Values.Count(x => x.SurvivalStreak >= Active.RequiredBattles); Changed(); }
        }

        private void Record(Hero hero, string eventId)
        {
            string owner = BLTAdoptAHeroCampaignBehavior.Current.GetHeroOwner(hero);
            if (!StreamObjectivePolicy.AddProgress(Active, eventId, owner, hero.StringId, hero.Name?.ToString())) return;
            Changed();
        }

        private void Changed()
        {
            int milestone = StreamObjectivePolicy.Milestone(Active);
            if (BLTAdoptAHeroModule.CommonConfig.StreamObjectivesMilestones && milestone > Active.LastMilestone && milestone < 100)
            { Active.LastMilestone = milestone; Log.LogFeedEvent($"Stream objective is {milestone}% complete: {ProgressText(Active)}."); }
            if (StreamObjectivePolicy.IsComplete(Active)) Complete();
            Publish();
        }

        private void Complete()
        {
            if (Active == null || Active.RewardsGranted) return;
            Active.Status = StreamObjectiveStatus.Completed;
            Active.FinishedAt = CampaignTime.Now.ToString();
            Active.RewardsGranted = true;
            int rewarded = 0;
            foreach (var contribution in Active.Contributors.Values.Where(c => c.Amount > 0))
            {
                Hero hero = BLTAdoptAHeroCampaignBehavior.Current.GetAdoptedHero(contribution.Owner);
                if (hero == null || hero.IsDead) continue;
                if (Active.GoldReward > 0) BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, Active.GoldReward);
                if (Active.XPReward > 0) SkillXP.ImproveSkill(hero, Active.XPReward, SkillsEnum.All, auto: true);
                rewarded++;
            }
            Log.LogFeedEvent($"Stream objective completed! {rewarded} contributors received {Active.GoldReward} gold and {Active.XPReward} XP.");
            AddHistory(Active);
            Publish();
        }

        private void AddHistory(StreamObjectiveState item)
        {
            history.Add(item);
            int max = Math.Max(0, BLTAdoptAHeroModule.CommonConfig?.StreamObjectivesHistorySize ?? 10);
            if (history.Count > max) history.RemoveRange(0, history.Count - max);
        }

        private bool Enabled(out string message)
        {
            if (Campaign.Current == null) { message = "Stream objectives require an active campaign."; return false; }
            if (BLTAdoptAHeroModule.CommonConfig?.StreamObjectivesEnabled != true) { message = "Stream objectives are disabled in settings."; return false; }
            message = null; return true;
        }

        private static string ProgressText(StreamObjectiveState state) => state.Kind == StreamObjectiveKind.Survive
            ? $"{state.Progress}/{state.RequiredHeroes} heroes qualified"
            : $"{state.Progress}/{state.Target}";

        private static void Publish() => StreamObjectivesHub.Publish(Current?.active);
    }
}
