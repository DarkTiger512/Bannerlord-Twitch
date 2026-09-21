using System;
using System.Collections.Generic;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.SaveSystem;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using BannerlordTwitch.Helpers;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace BLTAdoptAHero.Behaviors
{
    public sealed class CursedArtifactBehavior : CampaignBehaviorBase
    {
        public static CursedArtifactBehavior Current => Campaign.Current?.GetCampaignBehavior<CursedArtifactBehavior>();

        private CurseRecord active;
        private List<CurseHistoryEntry> history = new();
        private readonly CursedBattleParticipation participation = new();
        private Mission playedMission;
        private bool missionResolved;
        private double lastTriggerDay = -100000;

        public CurseRecord Active => active?.Status is CurseLifecycle.Active or CurseLifecycle.CompletedPendingReward ? active : null;
        public IReadOnlyList<CurseHistoryEntry> History => history;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, (victim, _, _, _) =>
            {
                if (victim?.StringId == Active?.HeroId) Fail("the cursed hero died");
            });
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, () =>
            {
                participation.Clear();
                playedMission = null;
                missionResolved = false;
                RestoreLookupFailure();
                TryGrantPendingReward();
            });
        }

        public override void SyncData(IDataStore dataStore)
        {
            using var sync = new ScopedJsonSync(dataStore, nameof(CursedArtifactBehavior));
            sync.SyncDataAsJson("ActiveV1", ref active);
            sync.SyncDataAsJson("HistoryV1", ref history);
            dataStore.SyncData("LastTriggerDay", ref lastTriggerDay);
            history ??= new List<CurseHistoryEntry>();
            if (active != null) active.ProcessedBattleIds ??= new HashSet<string>(StringComparer.Ordinal);
        }

        public bool IsCursed(Hero hero) => hero != null && Active?.Status == CurseLifecycle.Active && Active.HeroId == hero.StringId;
        public int BattleProgress(Hero hero) => IsCursed(hero) ? active.QualifyingWins : 0;
        public float OutgoingDamageMultiplier(Hero hero) => IsCursed(hero)
            ? CursedArtifactPolicy.OutgoingMultiplier(BLTAdoptAHeroModule.EventConfig?.CursedArtifactOutgoingPenaltyPercent ?? 20f) : 1f;
        public float IncomingDamageMultiplier(Hero hero) => IsCursed(hero)
            ? CursedArtifactPolicy.IncomingMultiplier(BLTAdoptAHeroModule.EventConfig?.CursedArtifactIncomingIncreasePercent ?? 25f) : 1f;
        public bool IsEligible(Hero hero) => hero != null && !hero.IsDead && hero.IsActive && hero.IsAdopted()
            && !string.IsNullOrWhiteSpace(BLTAdoptAHeroCampaignBehavior.Current?.GetHeroOwner(hero));

        public void MarkMissionParticipant(Agent agent)
        {
            var hero = agent.GetAdoptedHero();
            var battle = MobileParty.MainParty?.MapEvent;
            if (!IsCursed(hero) || agent.Team?.IsValid != true || battle == null || !QualifyingType(battle.EventType)
                || MissionHelpers.InTournament() || MissionHelpers.InArenaPracticeMission() || MissionHelpers.InTrainingFieldMission()) return;
            if (playedMission != Mission.Current) missionResolved = false;
            playedMission = Mission.Current;
            participation.Mark(battle, (int)agent.Team.Side);
        }

        public void EndMission(Mission mission)
        {
            if (playedMission != mission) return;
            // An unresolved sortie must not earn a win from a later auto-resolve of the same map event.
            missionResolved = mission.MissionResult?.BattleResolved == true
                && mission.MissionResult.BattleState != BattleState.DefenderPullBack;
            if (!missionResolved) participation.Clear();
            playedMission = null;
        }

        private void OnDailyTick()
        {
            RestoreLookupFailure();
            var cfg = BLTAdoptAHeroModule.EventConfig;
            if (Active?.Status == CurseLifecycle.CompletedPendingReward) { TryGrantPendingReward(); return; }
            if (cfg?.RandomEventsEnabled != true || cfg.CursedArtifactEnabled != true) return;
            if (Active != null) return;

            double day = CampaignTime.Now.ToDays;
            if (day - lastTriggerDay < CursedArtifactPolicy.ClampCooldown(cfg.CursedArtifactCooldownDays)) return;
            float chance = CursedArtifactPolicy.ClampChance(cfg.CursedArtifactDailyChancePercent);
            float roll = MBRandom.RandomFloat * 100f;
            if (cfg.CursedArtifactDiagnostics) Log.Info($"[Cursed Artifact] daily roll {roll:0.00}/{chance:0.00}");
            if (roll >= chance) return;

            var eligible = BLTAdoptAHeroCampaignBehavior.GetAllAdoptedHeroes().Where(IsEligible).OrderBy(h => h.StringId).ToList();
            if (eligible.Count == 0) return;
            var hero = eligible[MBRandom.RandomInt(eligible.Count)];
            active = new CurseRecord { HeroId = hero.StringId, Owner = BLTAdoptAHeroCampaignBehavior.Current.GetHeroOwner(hero), StartedAt = CampaignTime.Now.ToString() };
            lastTriggerDay = day;
            Log.LogFeedEvent("{=BLTCurseStarted}A cursed artifact has bound itself to @{Owner}! Win {RequiredWins} campaign battles to transform it into a legendary weapon."
                .Translate(("Owner", active.Owner), ("RequiredWins", CursedArtifactPolicy.ClampRequiredWins(cfg.CursedArtifactRequiredWins))));
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (mapEvent == null || !participation.Matches(mapEvent)) return;
            try
            {
                if (Active?.Status != CurseLifecycle.Active || !QualifyingType(mapEvent.EventType)) return;
                bool resolved = missionResolved || (playedMission?.MissionResult?.BattleResolved == true
                    && playedMission.MissionResult.BattleState != BattleState.DefenderPullBack);
                if (!participation.Complete(active, mapEvent, (int)(mapEvent.Winner?.MissionSide ?? BattleSideEnum.None),
                    resolved, BLTAdoptAHeroModule.EventConfig.CursedArtifactRequiredWins))
                { Diagnostic("rejected unresolved battle, loss, or duplicate callback"); return; }
                Log.LogFeedEvent("{=BLTCurseProgress}@{Owner} won a cursed battle ({Wins}/{RequiredWins})."
                    .Translate(("Owner", active.Owner), ("Wins", active.QualifyingWins),
                        ("RequiredWins", CursedArtifactPolicy.ClampRequiredWins(BLTAdoptAHeroModule.EventConfig.CursedArtifactRequiredWins))));
                if (active.Status == CurseLifecycle.CompletedPendingReward) TryGrantPendingReward();
            }
            finally { participation.Clear(); playedMission = null; missionResolved = false; }
        }

        private static bool QualifyingType(MapEvent.BattleTypes type) => type is MapEvent.BattleTypes.FieldBattle
            or MapEvent.BattleTypes.Siege or MapEvent.BattleTypes.Hideout or MapEvent.BattleTypes.Raid
            or MapEvent.BattleTypes.SallyOut or MapEvent.BattleTypes.SiegeOutside;

        private void TryGrantPendingReward()
        {
            if (active?.Status != CurseLifecycle.CompletedPendingReward) return;
            try
            {
                Hero hero = ResolveHero();
                if (hero == null) { NotifyPendingReward(); return; }
                if (hero.IsDead) { Fail("the cursed hero died"); return; }
                var cfg = BLTAdoptAHeroModule.EventConfig;
                var modifier = new RandomItemModifierDef
                {
                    Power = 1f,
                    WeaponDamage = new RangeInt(cfg.CursedArtifactWeaponBonus, cfg.CursedArtifactWeaponBonus),
                    WeaponSpeed = new RangeInt(cfg.CursedArtifactWeaponBonus, cfg.CursedArtifactWeaponBonus),
                    WeaponMissileSpeed = new RangeInt(cfg.CursedArtifactWeaponBonus, cfg.CursedArtifactWeaponBonus),
                    ThrowingStack = new RangeInt(0, 0)
                };
                ItemObject item = string.IsNullOrEmpty(active.RewardItemId) ? null : MBObjectManager.Instance.GetObject<ItemObject>(active.RewardItemId);
                ItemModifier itemModifier = string.IsNullOrEmpty(active.RewardModifierId) ? null : MBObjectManager.Instance.GetObject<ItemModifier>(active.RewardModifierId);
                if (item == null || itemModifier == null)
                {
                    var generated = RewardHelpers.GenerateRewardType(RewardHelpers.RewardType.Weapon, 6, hero, hero.GetClass(), true,
                        modifier, "Cursed Legacy", 1f);
                    if (generated.item == null || generated.modifier == null) { NotifyPendingReward(); return; }
                    item = generated.item;
                    itemModifier = generated.modifier;
                    active.RewardItemId = item.StringId;
                    active.RewardModifierId = itemModifier.StringId;
                    active.RewardSlot = (int)generated.slot;
                }
                bool Stored() => BLTAdoptAHeroCampaignBehavior.Current.GetCustomItems(hero)
                    .Any(i => i.Item == item && i.ItemModifier == itemModifier);
                if (!Stored()) RewardHelpers.AssignCustomReward(hero, item, itemModifier, (EquipmentIndex)active.RewardSlot);
                if (!Stored()) { NotifyPendingReward(); return; }
                active.Status = CurseLifecycle.Completed;
                active.FinishedAt = CampaignTime.Now.ToString();
                AddHistory(active, null);
                string owner = active.Owner;
                active = null;
                Log.LogFeedEvent("{=BLTCurseCompleted}@{Owner} broke the curse and received the legendary Cursed Legacy!"
                    .Translate(("Owner", owner)));
            }
            catch (Exception ex) { Log.Error($"[Cursed Artifact] reward pending after failure: {ex}"); NotifyPendingReward(); }
        }

        private void NotifyPendingReward()
        {
            if (active?.Status != CurseLifecycle.CompletedPendingReward || active.PendingRewardNotified) return;
            Log.LogFeedEvent("{=BLTCurseRewardPending}@{Owner} broke the curse! The weapon reward is pending and will be retried automatically."
                .Translate(("Owner", active.Owner)));
            active.PendingRewardNotified = true;
        }

        private Hero ResolveHero()
        {
            return ResolveCampaignHero(Active?.HeroId);
        }

        private static Hero ResolveCampaignHero(string heroId)
        {
            if (string.IsNullOrWhiteSpace(heroId)) return null;
            // Runtime campaign heroes are not reliably registered in MBObjectManager.
            // Match the saved identity, never the viewer name or a newly adopted replacement.
            return CampaignHelpers.AllHeroes.FirstOrDefault(h => h.StringId == heroId)
                ?? MBObjectManager.Instance.GetObject<Hero>(heroId);
        }

        private void RestoreLookupFailure()
        {
            if (Active != null) return;
            var failed = history.LastOrDefault();
            // This exact legacy reason was used only after completing all required wins,
            // before any reward was generated. Do not revive real deaths or other failures.
            if (failed?.Status != CurseLifecycle.Failed || failed.Reason != "the cursed hero is no longer available") return;
            var hero = ResolveCampaignHero(failed.HeroId);
            if (hero == null || hero.IsDead) return;
            active = new CurseRecord
            {
                HeroId = failed.HeroId, Owner = failed.Owner, QualifyingWins = failed.Wins,
                Status = CurseLifecycle.CompletedPendingReward
            };
            failed.Status = CurseLifecycle.CompletedPendingReward;
            failed.Reason = "Recovered reward after legacy campaign hero lookup failure";
            Log.LogFeedEvent($"@{active.Owner}: restored the completed curse; retrying the earned weapon reward.");
        }

        private void Fail(string reason)
        {
            if (Active == null) return;
            active.Status = CurseLifecycle.Failed;
            active.FinishedAt = CampaignTime.Now.ToString();
            active.FailureReason = reason;
            AddHistory(active, reason);
            Log.LogFeedEvent("{=BLTCurseFailedReason}@{Owner}: the cursed artifact event ended because {Reason}. No reward was granted."
                .Translate(("Owner", active.Owner), ("Reason", reason)));
            active = null;
            participation.Clear();
            playedMission = null;
            missionResolved = false;
        }

        private void AddHistory(CurseRecord record, string reason) => history.Add(new CurseHistoryEntry
        {
            HeroId = record.HeroId, Owner = record.Owner, Status = record.Status, Wins = record.QualifyingWins,
            FinishedAt = record.FinishedAt, Reason = reason
        });

        private static void Diagnostic(string message)
        {
            if (BLTAdoptAHeroModule.EventConfig?.CursedArtifactDiagnostics == true) Log.Info($"[Cursed Artifact] {message}");
        }
    }
}
