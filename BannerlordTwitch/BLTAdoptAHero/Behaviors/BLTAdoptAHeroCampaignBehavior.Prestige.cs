using System;
using System.Collections.Generic;
using System.Linq;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Achievements;
using BLTAdoptAHero.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace BLTAdoptAHero
{
    public partial class BLTAdoptAHeroCampaignBehavior
    {
        private Dictionary<string, PrestigeProgress> viewerPrestige = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Hero, PrestigeConfirmation> prestigePreviews = new();
        private readonly Dictionary<Hero, string> prestigePreviewSettings = new();
        private readonly HashSet<Hero> prestigeInProgress = new();
        public static PrestigeSettings PrestigeConfig => PrestigeCommand.CurrentSettings;
        public PrestigeProgress GetPrestige(Hero hero)
        {
            string owner = hero == null ? null : GetHeroData(hero).Owner;
            if (string.IsNullOrEmpty(owner)) return new PrestigeProgress();
            if (!viewerPrestige.TryGetValue(owner, out var progress)) viewerPrestige[owner] = progress = new PrestigeProgress();
            return progress;
        }
        public int GetPrestigeKills(Hero hero) => GetHeroData(hero).PrestigeKills;
        public void RecordPrestigeKill(Hero hero)
        {
            if (PrestigeConfig.Enabled && GetHeroData(hero).PrestigeKills < int.MaxValue) GetHeroData(hero).PrestigeKills++;
        }
        public double PrestigeBonus(Hero hero, string perk)
        {
            var c = PrestigeConfig;
            if (hero == null || !c.Enabled || !c.IsValid()) return 0;
            double step = perk switch { "might" => c.MightPerRank, "resilience" => c.ResiliencePerRank,
                "vitality" => c.VitalityPerRank, "fortune" => c.FortunePerRank, "insight" => c.InsightPerRank, _ => 0 };
            return Math.Min(GetPrestige(hero).Rank(perk), c.RankCap) * step;
        }
        public static int BattleGold(Hero hero, int amount, double scaling = 1) => PrestigePolicy.ScalePositive(amount,
            scaling, BLTSummonBehavior.BalanceFactor(hero), 1 + (Current?.PrestigeBonus(hero, "fortune") ?? 0));
        public static bool IsPrestigeBattle => Mission.Current != null && Campaign.Current != null
            && !MissionHelpers.InTournament() && !MissionHelpers.InArenaPracticeMission() && !MissionHelpers.InTrainingFieldMission()
            && !MissionHelpers.InFriendlyMission() && Mission.Current.Mode == MissionMode.Battle;
        public string PrestigeBlock(Hero hero)
        {
            var c = PrestigeConfig;
            if (!c.Enabled) return "{=BLTPrestigeDisabled}Prestige is disabled.".Translate();
            if (!c.IsValid()) return "{=BLTPrestigeConfig}Prestige settings are invalid; contact the streamer.".Translate();
            if (hero.IsDead || GetHeroData(hero).IsRetiredOrDead) return "{=BLTPrestigeInactive}An active hero is required.".Translate();
            if (GetPrestige(hero).Count >= PrestigePolicy.Maximum(c)) return "{=BLTPrestigeMax}All prestige ranks are complete.".Translate();
            if (Mission.Current != null || PlayerEncounter.Current != null || hero.PartyBelongedTo?.MapEvent != null)
                return "{=BLTPrestigeBattle}Finish the current mission or encounter first.".Translate();
            if (BLTTournamentQueueBehavior.Current?.TournamentQueue.Any(q => q.Hero == hero) == true)
                return "{=BLTPrestigeQueue}Leave the tournament queue first.".Translate();
            if (AuctionInProgress) return "{=BLTPrestigeAuction}Wait for the item auction to finish.".Translate();
            if (prestigeInProgress.Contains(hero)) return "{=BLTPrestigeBusy}Prestige is already in progress.".Translate();
            if (GetPrestigeKills(hero) < PrestigePolicy.RequiredKills(c, GetPrestige(hero).Count)
                || GetHeroGold(hero) < PrestigePolicy.RequiredGold(c, GetPrestige(hero).Count))
                return "{=BLTPrestigeRequirements}You need more qualifying kills or gold.".Translate();
            return null;
        }
        public string PrestigeResetSummary() => "{=BLTPrestigeReset}Resets level, XP, skills, attributes, focus, perks, gold, equipment, custom items, both retinues and achievement unlocks. Keeps identity, class, family, property, relationships and lifetime statistics. Starts at level {LEVEL}, attributes {ATTR}, no focus/unspent points, melee/ranged {COMBAT}, riding/athletics {MOVE}, other skills {OTHER}, gold {GOLD}, equipment tier {TIER}."
            .Translate(("LEVEL", PrestigeConfig.StartingLevel), ("ATTR", PrestigeConfig.StartingAttributes), ("COMBAT", PrestigeConfig.StartingCombatSkills), ("MOVE", PrestigeConfig.StartingMovementSkills), ("OTHER", PrestigeConfig.StartingOtherSkills), ("GOLD", PrestigeConfig.StartingGold), ("TIER", PrestigeConfig.StartingEquipmentTier));
        public string PrestigeStatus(Hero hero) => "{=BLTPrestigeStatus}Prestige {COUNT}/{MAX}: {KILLS}/{NEEDKILLS} personal battle kills, {GOLD}/{NEEDGOLD} gold. Perks: {PERKS}. {BLOCK}"
            .Translate(("COUNT", GetPrestige(hero).Count), ("MAX", PrestigePolicy.Maximum(PrestigeConfig)),
                ("KILLS", GetPrestigeKills(hero)), ("NEEDKILLS", PrestigePolicy.RequiredKills(PrestigeConfig, GetPrestige(hero).Count)),
                ("GOLD", GetHeroGold(hero)), ("NEEDGOLD", PrestigePolicy.RequiredGold(PrestigeConfig, GetPrestige(hero).Count)),
                ("PERKS", string.Join(", ", PrestigePolicy.Perks.Select(p => $"{p} {GetPrestige(hero).Rank(p)}/{PrestigeConfig.RankCap}"))),
                ("BLOCK", PrestigeBlock(hero) ?? "Ready. !prestige perks"));
        public string PreviewPrestige(Hero hero, string perk)
        {
            string blocked = PrestigeBlock(hero);
            if (blocked != null) return blocked;
            if (!PrestigePolicy.CanChoose(PrestigeConfig, GetPrestige(hero), perk)) return "{=BLTPrestigePerkUnavailable}Unknown or fully ranked perk. Use !prestige perks.".Translate();
            var preview = new PrestigeConfirmation();
            preview.Preview(perk, GetPrestige(hero).Count, DateTime.UtcNow);
            prestigePreviews[hero] = preview;
            prestigePreviewSettings[hero] = Newtonsoft.Json.JsonConvert.SerializeObject(PrestigeConfig);
            return PrestigeResetSummary() + " " + "{=BLTPrestigeConfirmPrompt}Choose {PERK}: type !prestige confirm {PERK} within 60 seconds.".Translate(("PERK", perk));
        }
        public bool ConfirmPrestige(Hero hero, string perk, out string reply)
        {
            reply = PrestigeBlock(hero);
            if (reply != null) return false;
            if (!PrestigePolicy.CanChoose(PrestigeConfig, GetPrestige(hero), perk) || !prestigePreviews.TryGetValue(hero, out var preview)
                || !prestigePreviewSettings.TryGetValue(hero, out var settingsPreview) || settingsPreview != Newtonsoft.Json.JsonConvert.SerializeObject(PrestigeConfig)
                || !preview.Consume(perk, GetPrestige(hero).Count, DateTime.UtcNow))
            { reply = "{=BLTPrestigePreviewExpired}Preview missing or expired. Use !prestige choose <perk> again.".Translate(); return false; }
            prestigePreviews.Remove(hero);
            prestigePreviewSettings.Remove(hero);
            var old = GetHeroData(hero);
            var progress = GetPrestige(hero);
            var previousRanks = new Dictionary<string, int>(progress.Ranks);
            int previousCount = progress.Count;
            PrestigeHeroSnapshot snapshot = null;
            heroAchievementPassivePowers.TryGetValue(hero, out var oldPowers);
            prestigeInProgress.Add(hero);
            try
            {
                snapshot = new PrestigeHeroSnapshot(hero);
                // Swap the BLT record so rollback restores every list and history without lossy reconstruction.
                heroData[hero] = new HeroData { Owner = old.Owner, Iteration = old.Iteration, IsCreatedHero = old.IsCreatedHero,
                    LegacyName = old.LegacyName, ClassID = old.ClassID, EquipmentClassID = old.ClassID,
                    EquipmentTier = PrestigeConfig.StartingEquipmentTier - 1, Gold = PrestigeConfig.StartingGold,
                    LifetimeStats = old.LifetimeStats ?? old.AchievementStats };
                var dev = hero.HeroDeveloper;
                dev.ClearHero();
                snapshot.RestoreTraits();
                var combat = SkillGroup.GetSkills(SkillsEnum.Melee).Concat(SkillGroup.GetSkills(SkillsEnum.Ranged)).ToList();
                foreach (var skill in CampaignHelpers.AllSkillObjects)
                    dev.SetInitialSkillLevel(skill, combat.Contains(skill) ? PrestigeConfig.StartingCombatSkills
                        : skill == DefaultSkills.Riding || skill == DefaultSkills.Athletics ? PrestigeConfig.StartingMovementSkills : PrestigeConfig.StartingOtherSkills);
                foreach (var attribute in TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObjectTypeList<CharacterAttribute>())
                    dev.AddAttribute(attribute, PrestigeConfig.StartingAttributes - hero.GetAttributeValue(attribute), false);
                // InitializeHeroDeveloper allocates random focus/attributes and unlocks perks.
                // SetInitialSkillLevel already initializes each skill's XP consistently.
                dev.SetInitialLevel(PrestigeConfig.StartingLevel);
                hero.Level = PrestigeConfig.StartingLevel;
                dev.ClearUnspentPoints();
                foreach (var skill in CampaignHelpers.AllSkillObjects) dev.RemoveFocus(skill, dev.GetFocus(skill));
                hero.BattleEquipment.FillFrom(new Equipment());
                hero.CivilianEquipment.FillFrom(new Equipment(Equipment.EquipmentType.Civilian));
                if (PrestigeConfig.StartingEquipmentTier > 0)
                    EquipHero.UpgradeEquipment(hero, PrestigeConfig.StartingEquipmentTier - 1, hero.GetClass(), true, customKeepFilter: _ => false, restrictedItemIds: BLTAdoptAHeroModule.CommonConfig.RestrictedItemIds);
                heroAchievementPassivePowers.Remove(hero);
                progress.Count++;
                progress.Ranks[perk] = progress.Rank(perk) + 1;
                SetHeroAdoptedName(hero, old.Owner);
                hero.HitPoints = hero.MaxHitPoints;
                reply = "{=BLTPrestigeSuccess}Prestige complete! {NAME}: {PERK} rank {RANK}.".Translate(("NAME", hero.Name), ("PERK", perk), ("RANK", progress.Rank(perk)));
                return true;
            }
            catch (Exception ex)
            {
                heroData[hero] = old;
                progress.Count = previousCount;
                progress.Ranks = previousRanks;
                if (oldPowers != null) heroAchievementPassivePowers[hero] = oldPowers;
                snapshot?.Restore();
                Log.Exception("Prestige reset", ex);
                reply = "{=BLTPrestigeFailed}Prestige failed; your previous progression was restored. Contact the streamer.".Translate();
                return false;
            }
            finally { prestigeInProgress.Remove(hero); }
        }
    }
}
