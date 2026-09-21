using System.ComponentModel.DataAnnotations;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.Util;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero
{
    [LocDisplayName("Events"), CategoryOrder("Stream Objectives", 0), CategoryOrder("Random Events", 1),
     CategoryOrder("Cursed Artifact", 2), CategoryOrder("Immortal Encounter", 3), CategoryOrder("Priest's Crusade", 4)]
    internal sealed class GlobalEventConfig : IDocumentable
    {
        private const string ID = "Adopt A Hero - Events";
        internal static void Register() => ActionManager.RegisterGlobalConfigType(ID, typeof(GlobalEventConfig));
        internal static GlobalEventConfig Get() => ActionManager.GetGlobalConfig<GlobalEventConfig>(ID);

        [LocDisplayName("Enable Stream Objectives"), LocDescription("Allow moderators to launch community objectives from chat."),
         LocCategory("Stream Objectives", "Stream Objectives"), PropertyOrder(1)]
        public bool StreamObjectivesEnabled { get; set; } = true;

        [LocDisplayName("Show Objectives Overlay"), LocDescription("Show the active chat objective in the browser overlay."),
         LocCategory("Stream Objectives", "Stream Objectives"), PropertyOrder(2)]
        public bool StreamObjectivesOverlayEnabled { get; set; } = true;

        [LocDisplayName("Contributor Count"), LocDescription("Maximum contributors shown in the objectives panel."),
         LocCategory("Stream Objectives", "Stream Objectives"), PropertyOrder(3), Range(0, 10)]
        public int StreamObjectivesContributorCount { get; set; } = 3;

        [LocDisplayName("Milestone Announcements"), LocDescription("Announce progress at 25, 50, and 75 percent."),
         LocCategory("Stream Objectives", "Stream Objectives"), PropertyOrder(4)]
        public bool StreamObjectivesMilestones { get; set; } = true;

        [LocDisplayName("Objective History Size"), LocDescription("Number of completed or cancelled events retained in the campaign save."),
         LocCategory("Stream Objectives", "Stream Objectives"), PropertyOrder(5), Range(0, 50)]
        public int StreamObjectivesHistorySize { get; set; } = 10;

        [LocDisplayName("Overlay Width Percent"), LocDescription("Width of the objectives panel as a percentage of the browser source."),
         LocCategory("Stream Objectives", "Stream Objectives"), PropertyOrder(6), Range(20, 60)]
        public float StreamObjectivesOverlayWidthPercent { get; set; } = 38f;

        [LocDisplayName("Overlay Background Opacity"), LocDescription("Opacity of the objectives panel background."),
         LocCategory("Stream Objectives", "Stream Objectives"), PropertyOrder(7), Range(0, 1)]
        public float StreamObjectivesOverlayOpacity { get; set; } = .92f;

        [LocDisplayName("{=BLTRandomEventsEnabled}Enable Random Events"), LocDescription("{=BLTRandomEventsEnabledDesc}Master switch for all random campaign events. Disable this to prevent any new random events from starting."),
         LocCategory("Random Events", "Random Events"), PropertyOrder(1)]
        public bool RandomEventsEnabled { get; set; } = true;

        [LocDisplayName("{=BLTCursedArtifactEnabled}Enable Cursed Artifact"), LocDescription("{=BLTCursedArtifactEnabledDesc}Allow the automatic cursed-artifact campaign event."),
         LocCategory("Cursed Artifact", "Cursed Artifact"), PropertyOrder(1)]
        public bool CursedArtifactEnabled { get; set; } = true;

        [LocDisplayName("{=BLTDailyTriggerChance}Daily Trigger Chance %"), LocDescription("{=BLTCursedArtifactChanceDesc}Chance rolled once per campaign day when no curse is active and cooldown has elapsed."),
         LocCategory("Cursed Artifact", "Cursed Artifact"), PropertyOrder(2), Range(0, 100)]
        public float CursedArtifactDailyChancePercent { get; set; } = 1f;

        [LocDisplayName("{=BLTCooldownDays}Cooldown Days"), LocDescription("{=BLTCursedArtifactCooldownDesc}Minimum campaign days between successful curse triggers."),
         LocCategory("Cursed Artifact", "Cursed Artifact"), PropertyOrder(3), Range(0, 3650)]
        public int CursedArtifactCooldownDays { get; set; } = 30;

        [LocDisplayName("{=BLTRequiredBattleWins}Required Battle Wins"), LocDescription("{=BLTRequiredBattleWinsDesc}Qualifying campaign victories the cursed hero must personally participate in."),
         LocCategory("Cursed Artifact", "Cursed Artifact"), PropertyOrder(4), Range(1, 1000)]
        public int CursedArtifactRequiredWins { get; set; } = 5;

        [LocDisplayName("{=BLTOutgoingDamagePenalty}Outgoing Damage Penalty %"), LocDescription("{=BLTOutgoingDamagePenaltyDesc}Percentage less damage dealt by the cursed hero."),
         LocCategory("Cursed Artifact", "Cursed Artifact"), PropertyOrder(5), Range(0, 95)]
        public float CursedArtifactOutgoingPenaltyPercent { get; set; } = 20f;

        [LocDisplayName("{=BLTIncomingDamageIncrease}Incoming Damage Increase %"), LocDescription("{=BLTIncomingDamageIncreaseDesc}Percentage additional damage received by the cursed hero."),
         LocCategory("Cursed Artifact", "Cursed Artifact"), PropertyOrder(6), Range(0, 400)]
        public float CursedArtifactIncomingIncreasePercent { get; set; } = 25f;

        [LocDisplayName("{=BLTArtifactWeaponBonus}Artifact Weapon Bonus"), LocDescription("{=BLTArtifactWeaponBonusDesc}Fixed bounded damage, speed, and missile-speed modifier for Cursed Legacy weapons."),
         LocCategory("Cursed Artifact", "Cursed Artifact"), PropertyOrder(7), Range(0, 100)]
        public int CursedArtifactWeaponBonus { get; set; } = 35;

        [LocDisplayName("{=BLTDiagnosticLogging}Diagnostic Logging"), LocDescription("{=BLTCursedArtifactDiagnosticsDesc}Log trigger rolls and rejected or duplicate battle callbacks."),
         LocCategory("Cursed Artifact", "Cursed Artifact"), PropertyOrder(8)]
        public bool CursedArtifactDiagnostics { get; set; } = false;

        [LocDisplayName("{=BLTEnabled}Enabled"), LocDescription("{=BLTImmortalEnabledDesc}Allow the automatic Immortal Encounter campaign event."),
         LocCategory("Immortal Encounter", "Immortal Encounter"), PropertyOrder(1)]
        public bool ImmortalEncounterEnabled { get; set; } = true;

        [LocDisplayName("{=BLTDailyTriggerChance}Daily Trigger Chance %"), LocDescription("{=BLTDailyTriggerChanceDesc}Chance rolled once per eligible campaign day."),
         LocCategory("Immortal Encounter", "Immortal Encounter"), PropertyOrder(2), Range(0, 100)]
        public float ImmortalEncounterDailyChancePercent { get; set; } = .2f;

        [LocDisplayName("{=BLTCooldownDays}Cooldown Days"), LocDescription("{=BLTImmortalCooldownDesc}Minimum campaign days between successfully constructed encounters."),
         LocCategory("Immortal Encounter", "Immortal Encounter"), PropertyOrder(3), Range(0, 3650)]
        public int ImmortalEncounterCooldownDays { get; set; } = 90;

        [LocDisplayName("{=BLTMinimumPlayerLevel}Minimum Player Level"), LocDescription("{=BLTMinimumPlayerLevelDesc}Minimum main-hero level required."),
         LocCategory("Immortal Encounter", "Immortal Encounter"), PropertyOrder(4), Range(1, 100)]
        public int ImmortalEncounterMinimumPlayerLevel { get; set; } = 10;

        [LocDisplayName("{=BLTArmyStrength}Army Strength %"), LocDescription("{=BLTImmortalArmyStrengthDesc}Enemy troop count relative to the player's current party strength."),
         LocCategory("Immortal Encounter", "Immortal Encounter"), PropertyOrder(5), Range(1, 500)]
        public int ImmortalEncounterArmyStrengthPercent { get; set; } = 120;

        [LocDisplayName("{=BLTParticipantGoldReward}Participant Gold Reward"), LocDescription("{=BLTParticipantGoldRewardDesc}BLT gold awarded once to each adopted hero who fought on the player's side in a victory."),
         LocCategory("Immortal Encounter", "Immortal Encounter"), PropertyOrder(6), Range(0, 100000000)]
        public int ImmortalEncounterGoldReward { get; set; } = 100000;

        [LocDisplayName("{=BLTImmortalName}Immortal Name"), LocDescription("{=BLTImmortalNameDesc}Display name of the temporary challenger."),
         LocCategory("Immortal Encounter", "Immortal Encounter"), PropertyOrder(7)]
        public string ImmortalEncounterName { get; set; } = "The Immortal";

        [LocDisplayName("{=BLTCultName}Cult Name"), LocDescription("{=BLTCultNameDesc}Display name of the temporary challenger clan."),
         LocCategory("Immortal Encounter", "Immortal Encounter"), PropertyOrder(8)]
        public string ImmortalEncounterCultName { get; set; } = "Cult of the Eternal";

        [LocDisplayName("{=BLTDiagnosticLogging}Diagnostic Logging"), LocDescription("{=BLTImmortalDiagnosticsDesc}Log trigger rolls, eligibility failures, and lifecycle transitions."),
         LocCategory("Immortal Encounter", "Immortal Encounter"), PropertyOrder(9)]
        public bool ImmortalEncounterDiagnostics { get; set; } = false;

        [LocDisplayName("{=BLTEnabled}Enabled"), LocDescription("{=BLTPriestCrusadeEnabledDesc}Allow the automatic Priest's Crusade campaign event."),
         LocCategory("Priest's Crusade", "Priest's Crusade"), PropertyOrder(1)]
        public bool PriestCrusadeEnabled { get; set; } = true;

        [LocDisplayName("{=BLTDailyTriggerChance}Daily Trigger Chance %"), LocDescription("{=BLTDailyTriggerChanceDesc}Chance rolled once per eligible campaign day."),
         LocCategory("Priest's Crusade", "Priest's Crusade"), PropertyOrder(2), Range(0, 100)]
        public float PriestCrusadeDailyChancePercent { get; set; } = .5f;

        [LocDisplayName("{=BLTCooldownDays}Cooldown Days"), LocDescription("{=BLTPriestCrusadeCooldownDesc}Minimum campaign days after a successful crusade creation before another may occur."),
         LocCategory("Priest's Crusade", "Priest's Crusade"), PropertyOrder(3), Range(0, 3650)]
        public int PriestCrusadeCooldownDays { get; set; } = 60;

        [LocDisplayName("{=BLTMinimumKingdomTier}Minimum Kingdom Tier"), LocDescription("{=BLTMinimumKingdomTierDesc}Minimum tier of the player's clan while it belongs to a kingdom."),
         LocCategory("Priest's Crusade", "Priest's Crusade"), PropertyOrder(4), Range(0, 6)]
        public int PriestCrusadeMinimumKingdomTier { get; set; } = 2;

        [LocDisplayName("{=BLTArmyStrength}Army Strength %"), LocDescription("{=BLTPriestArmyStrengthDesc}Crusader troop count relative to target kingdom strength, clamped to 100-1000."),
         LocCategory("Priest's Crusade", "Priest's Crusade"), PropertyOrder(5), Range(1, 500)]
        public int PriestCrusadeArmyStrengthPercent { get; set; } = 80;

        [LocDisplayName("{=BLTPriestName}Priest Name"), LocDescription("{=BLTPriestNameDesc}Display name of the crusader leader."),
         LocCategory("Priest's Crusade", "Priest's Crusade"), PropertyOrder(6)]
        public string PriestCrusadePriestName { get; set; } = "Father Zealot";

        [LocDisplayName("{=BLTCrusaderClanName}Crusader Clan Name"), LocDescription("{=BLTCrusaderClanNameDesc}Display name of the persistent independent crusader clan."),
         LocCategory("Priest's Crusade", "Priest's Crusade"), PropertyOrder(7)]
        public string PriestCrusadeClanName { get; set; } = "Holy Crusaders";

        [LocDisplayName("{=BLTDiagnosticLogging}Diagnostic Logging"), LocDescription("{=BLTPriestDiagnosticsDesc}Log trigger rolls, eligibility failures, and resolution checks."),
         LocCategory("Priest's Crusade", "Priest's Crusade"), PropertyOrder(8)]
        public bool PriestCrusadeDiagnostics { get; set; } = false;

        public void GenerateDocumentation(IDocumentationGenerator generator)
        {
            generator.H1("Events");
            DocumentationHelpers.AutoDocument(generator, this);
        }
    }
}
