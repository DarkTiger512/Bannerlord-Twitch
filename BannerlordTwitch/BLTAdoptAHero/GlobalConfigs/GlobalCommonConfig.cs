using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.Annotations;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.UI;
using BannerlordTwitch.Util;
using BannerlordTwitch.Helpers;
using BLTAdoptAHero.Achievements;
using BLTAdoptAHero.Actions.Util;
using BLTAdoptAHero.Actions.Upgrades;
using BLTAdoptAHero.UI;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;
using TaleWorlds.CampaignSystem;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using YamlDotNet.Serialization;
using static BLTAdoptAHero.Actions.UpgradeAction;

namespace BLTAdoptAHero
{
    [CategoryOrder("General", 1),
     CategoryOrder("Training", 2),
     CategoryOrder("Battle", 3),
     CategoryOrder("Death", 4),
     CategoryOrder("Income", 5),
     CategoryOrder("Upgrades", 6),
     CategoryOrder("XP", 7),
     CategoryOrder("Kill Rewards", 8),
     CategoryOrder("Battle End Rewards", 9),
     CategoryOrder("Kill Streak Rewards", 10),
     CategoryOrder("Achievements", 11),
     CategoryOrder("Shouts", 12),
     LocDisplayName("{=GlobalCommonConfig_Name}Common Config")]
    internal class GlobalCommonConfig : IUpdateFromDefault, IDocumentable, INotifyPropertyChanged
    {
        #region Static
        private const string ID = "Adopt A Hero - General Config";

        internal static void Register() => ActionManager.RegisterGlobalConfigType(ID, typeof(GlobalCommonConfig));
        internal static GlobalCommonConfig Get() => ActionManager.GetGlobalConfig<GlobalCommonConfig>(ID);
        internal static GlobalCommonConfig Get(BannerlordTwitch.Settings fromSettings) => fromSettings.GetGlobalConfig<GlobalCommonConfig>(ID);

        public CapitalConfig CapitalConfig { get; set; } = new CapitalConfig();
        #endregion

        #region User Editable
        #region General
        [LocDisplayName("{=GlobalCommonConfig_Category_General_SubBoost_Name}Sub Boost"),
         LocCategory("General", "{=GlobalCommonConfig_Category_General}General"),
         LocDescription("{=GlobalCommonConfig_Category_General_SubBoost_Desc}Multiplier applied to all rewards for subscribers (less or equal to 1 means no boost). NOTE: This is only partially implemented, it works for bot commands only currently."),
         PropertyOrder(1), Document, UsedImplicitly,
         Range(0.5, 10), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor))]
        public float SubBoost { get; set; } = 1;

        [LocDisplayName("{=GlobalCommonConfig_Category_General_CustomRewardModifiers_Name}Custom Reward Modifiers"),
         LocCategory("General", "{=GlobalCommonConfig_Category_General}General"),
         LocDescription("{=GlobalCommonConfig_Category_General_CustomRewardModifiers_Desc}The specification for custom item rewards, applies to tournament prize and achievement rewards"),
         PropertyOrder(2), ExpandableObject, UsedImplicitly]
        public RandomItemModifierDef CustomRewardModifiers { get; set; } = new();

        [LocDisplayName("{=GlobalCommonConfig_Category_General_CustomItemLimit_Name}Custom Inventory Item Limit"),
         LocCategory("General", "{=GlobalCommonConfig_Category_General}General"),
         LocDescription("{=GlobalCommonConfig_Category_General_CustomItemLimit_Desc}Maximum custom inventory items allowed. This only applies when smithing, other rewards will always be added to inventory (but they will contribute to the limit). If you set this high then inventory management and console spam may become a problem."),
         PropertyOrder(3), UsedImplicitly]
        public int CustomItemLimit { get; set; } = 8;

        [LocDisplayName("{=GlobalCommonConfig_Category_General_RestrictedItems_Name}Restricted Items"),
         LocCategory("General", "{=GlobalCommonConfig_Category_General}General"),
         LocDescription("{=GlobalCommonConfig_Category_General_RestrictedItems_Desc}Comma-separated list of ItemObject StringIds to exclude from equipment selection in equip, smith, and rewards (e.g., 'battanian_noble_sword,empire_spear_1')"),
         PropertyOrder(4), UsedImplicitly]
        public string RestrictedItems { get; set; } = "";

        [LocDisplayName("{=Abc123}Forbidden Races"),
         LocCategory("General", "{=C5T5nnix}General"),
         LocDescription("{=Desc123}List of race IDs that are forbidden. Usage: 0,1,2"),
         PropertyOrder(4)]
        public string ForbiddenRaces { get; set; } = "";

        [LocDisplayName("{=Abc123}Forbidden Cultures"),
         LocCategory("General", "{=C5T5nnix}General"),
         LocDescription("{=Desc123}List of cultures that are forbidden. Usage: Vlandia,Battania"),
         PropertyOrder(4)]
        public string ForbiddenCultures { get; set; } = "";

        [LocDisplayName("{=}Custom Companion Limit"),
         LocCategory("General", "{=C5T6nnix}General"),
         LocDescription("{=}Flat number increase to companion limit"),
         PropertyOrder(5), UsedImplicitly]
        public int CustomCompanionLimit { get; set; } = 7;

        [LocDisplayName("{=GlobalCommonConfig_Category_General_BLTChildAgeMult_Name}BLT children aging multiplier"),
         LocCategory("General", "{=GlobalCommonConfig_Category_General}General"),
         LocDescription("{=GlobalCommonConfig_Category_General_BLTChildAgeMult_Desc}Multiplier to BLT children age"),
         PropertyOrder(6), UsedImplicitly]
        public int BLTChildAgeMult { get; set; } = 3;

        [LocDisplayName("{=BLTAdoptAHero_ShowCampaignMap}Show Campaign Map Overlay"),
         LocDescription("{=BLTAdoptAHero_ShowCampaignMap_Desc}Enable or disable the campaign map overlay that shows in the top portion of the overlay. The map automatically hides during missions."),
        LocCategory("General", "{=C5T6nnix}General"),
         PropertyOrder(7)]
        public bool ShowCampaignMapOverlay { get; set; } = true;

        [LocDisplayName("{=GlobalCommonConfig_Category_General_MapOverlayMinSpacing_Name}Overlay Map Settlement town radius"),
         LocDescription("{=GlobalCommonConfig_Category_General_MapOverlayMinSpacing_Desc}Overlay Map Settlement town radius"),
        LocCategory("General", "{=GlobalCommonConfig_Category_General}General"),
         PropertyOrder(8)]
        public float MapTownRadius { get; set; } = 2.15f;

        [LocDisplayName("{=BLTAdoptAHero_ShowCampaignMap}Overlay Map Settlement castle length"),
         LocDescription("{=BLTAdoptAHero_ShowCampaignMap_Desc}Overlay Map Settlement castle length"),
        LocCategory("General", "{=C5T6nnix}General"),
         PropertyOrder(9)]
        public float MapCastleLength { get; set; } = 2.5f;

        [LocDisplayName("{=GlobalCommonConfig_Category_General_UncapFoodStocks_Name}Uncap Maximum Foodstocks in Settlements"),
         LocCategory("General", "{=GlobalCommonConfig_Category_General}General"),
         LocDescription("{=GlobalCommonConfig_Category_General_UncapFoodStocks_Desc}Enable or disable the vanilla maximum of 300 foodstocks in towns and castles for all settlements."),
         PropertyOrder(10)]
        public bool UncapFoodStocks { get; set; } = false;

        [LocDisplayName("{=GlobalCommonConfig_Category_General_HearthPerVillageTier_Name}Hearth Per Village Tier"),
         LocCategory("General", "{=GlobalCommonConfig_Category_General}General"),
         LocDescription("{=GlobalCommonConfig_Category_General_HearthPerVillageTier_Desc}How much hearth is required per village prosperity level (affects food and goods production)."),
         PropertyOrder(11)]
        public float HearthPerVillageTier { get; set; } = 200f;

        [LocDisplayName("{=GlobalCommonConfig_Category_General_BLTArmyMinLifetimeDays_Name}Minimum BLT-Led Army Lifetime"),
         LocCategory("General", "{=GlobalCommonConfig_Category_General}General"),
         LocDescription("{=GlobalCommonConfig_Category_General_BLTArmyMinLifetimeDays_Desc}Minimum days a BLT-led army will persist before being allowed to disband."),
         PropertyOrder(12)]
        public float BLTArmyMinLifetimeDays { get; set; } = 30f;

        [LocDisplayName("{=GlobalCommonConfig_Category_General_LockBLTArmyCohesion_Name}Lock BLT Army Cohesion"),
         LocCategory("General", "{=GlobalCommonConfig_Category_General}General"),
         LocDescription("{=GlobalCommonConfig_Category_General_LockBLTArmyCohesion_Desc}When enabled, standard armies led by adopted heroes also have their cohesion locked at 100 and are exempt from automatic dispersion checks. Mercenary armies always have this applied regardless."),
         PropertyOrder(13), UsedImplicitly]
        public bool LockBLTArmyCohesion { get; set; } = true;

        [LocDisplayName("{=GlobalCommonConfig_Category_General_AllowAIJoinBLT_Name}Allow AI Clans to Join BLT Kingdoms"),
         LocCategory("General", "{=GlobalCommonConfig_Category_General}General"),
         LocDescription("{=GlobalCommonConfig_Category_General_AllowAIJoinBLT_Desc}Allow AI clans to join BLT kingdoms."),
         PropertyOrder(14)]
        public bool AllowAIJoinBLT { get; set; } = true;

        [LocDisplayName("Non-blt party size effectiveness"),
         LocCategory("General", "{=GeneralCat}General"),
         LocDescription("Percentage effectiveness party size upgrades non-blt heroes get."),
         PropertyOrder(15), UsedImplicitly, DefaultValue(1f), Range(0f, 1f)]
        public float PartySizeEffectiveness { get; set; } = 1f;

        [YamlIgnore, Browsable(false)]
        public HashSet<string> RestrictedItemIds
        {
            get
            {
                return new HashSet<string>(
                    RestrictedItems
                        .Split(',')
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrWhiteSpace(s)),
                    StringComparer.OrdinalIgnoreCase
                );
            }
        }

        [YamlIgnore, Browsable(false)]
        public HashSet<int> RestrictedRacesIds 
        {
            get
            {
                return new HashSet<int>(
                    ForbiddenRaces
                            .Split(',')
                            .Select(s => s.Trim())
                            .Where(s => int.TryParse(s, out _))
                            .Select(int.Parse)                            
                );
            }
        }
        [YamlIgnore, Browsable(false)]
        public HashSet<string> RestrictedCulturesStrings
        {
            get
            {
                return new HashSet<string>(
                    ForbiddenCultures
                            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim())
                            .ToHashSet(StringComparer.OrdinalIgnoreCase)
                );
            }        
        }

        #endregion

        #region Training
        [LocDisplayName("Train Max Daily Spend"),
         LocCategory("Training", "Training"),
         LocDescription("Hard cap on gold spent on training per in-game day regardless of fund size. 0 = no cap."),
         PropertyOrder(1), UsedImplicitly]
        public int TrainMaxDailySpend { get; set; } = 100000;

        [LocDisplayName("Train Gold Cost Multiplier"),
         LocCategory("Training", "Training"),
         LocDescription("Multiplies the gold cost of troop upgrades when using !party train. 1.0 = same cost as player. (This multiplies a very low value, setting this lower than 100 may ruin your game's balance.)"),
         UIRange(0f, 1000f, 10f),
         Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         PropertyOrder(2), UsedImplicitly]
        public float TrainGoldCostMultiplier { get; set; } = 500f;
        #endregion

        #region Battle
        [LocDisplayName("{=GlobalCommonConfig_Category_Battle_StartWithFullHealth_Name}Start With Full Health"),
         LocCategory("Battle", "{=GlobalCommonConfig_Category_Battle}Battle"),
         LocDescription("{=GlobalCommonConfig_Category_Battle_StartWithFullHealth_Desc}Whether the hero will always start with full health"),
         PropertyOrder(1), Document, UsedImplicitly]
        public bool StartWithFullHealth { get; set; } = true;

        [LocDisplayName("{=GlobalCommonConfig_Category_Battle_StartHealthMultiplier_Name}Start Health Multiplier"),
         LocCategory("Battle", "{=GlobalCommonConfig_Category_Battle}Battle"),
         LocDescription("{=GlobalCommonConfig_Category_Battle_StartHealthMultiplier_Desc}Amount to multiply normal starting health by, to give heroes better staying power vs others"),
         PropertyOrder(2),
         Range(0.1, 10), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         Document, UsedImplicitly]
        public float StartHealthMultiplier { get; set; } = 2;

        [LocDisplayName("{=GlobalCommonConfig_Category_Battle_StartRetinueHealthMultiplier_Name}Start Retinue Health Multiplier"),
         LocCategory("Battle", "{=GlobalCommonConfig_Category_Battle}Battle"),
         LocDescription("{=GlobalCommonConfig_Category_Battle_StartRetinueHealthMultiplier_Desc}Amount to multiply normal retinue starting health by, to give retinue better staying power vs others"),
         PropertyOrder(3),
         Range(0.1, 10), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         Document, UsedImplicitly]
        public float StartRetinueHealthMultiplier { get; set; } = 2;

        [LocDisplayName("{=GlobalCommonConfig_Category_Battle_MoraleLossFactor_Name}Morale Loss Factor (not implemented)"),
         LocCategory("Battle", "{=GlobalCommonConfig_Category_Battle}Battle"),
         LocDescription("{=GlobalCommonConfig_Category_Battle_MoraleLossFactor_Desc}Reduces morale loss when summoned heroes die"),
         PropertyOrder(4),
         Range(0, 2), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         Document, UsedImplicitly]
        public float MoraleLossFactor { get; set; } = 0.5f;

        [LocDisplayName("{=GlobalCommonConfig_Category_Battle_RetinueUseHeroesFormation_Name}Retinue Use Heroes Formation"),
         LocCategory("Battle", "{=GlobalCommonConfig_Category_Battle}Battle"),
         LocDescription("{=GlobalCommonConfig_Category_Battle_RetinueUseHeroesFormation_Desc}Whether an adopted hero's retinue should spawn in the same formation as the hero (otherwise they will go into default formations)"),
         PropertyOrder(7), Document, UsedImplicitly]
        public bool RetinueUseHeroesFormation { get; set; }

        [LocDisplayName("{=GlobalCommonConfig_Category_Battle_SummonCooldownInSeconds_Name}Summon Cooldown In Seconds"),
         LocCategory("Battle", "{=GlobalCommonConfig_Category_Battle}Battle"),
         LocDescription("{=GlobalCommonConfig_Category_Battle_SummonCooldownInSeconds_Desc}Minimum time between summons for a specific hero"),
         PropertyOrder(5),
         Range(0, int.MaxValue),
         Document, UsedImplicitly]
        public int SummonCooldownInSeconds { get; set; } = 20;

        [Browsable(false), YamlIgnore]
        public bool CooldownEnabled => SummonCooldownInSeconds > 0;

        [LocDisplayName("{=GlobalCommonConfig_Category_Battle_SummonCooldownUseMultiplier_Name}Summon Cooldown Use Multiplier"),
         LocCategory("Battle", "{=GlobalCommonConfig_Category_Battle}Battle"),
         LocDescription("{=GlobalCommonConfig_Category_Battle_SummonCooldownUseMultiplier_Desc}How much to multiply the cooldown by each time summon is used. e.g. if Summon Cooldown is 20 seconds, and UseMultiplier is 1.1 (the default), then the first summon has a cooldown of 20 seconds, and the next 24 seconds, the 10th 52 seconds, and the 20th 135 seconds. See https://www.desmos.com/calculator/muej1o5eg5 for a visualization of this."),
         PropertyOrder(6),
         Range(1, 10), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         Document, UsedImplicitly]
        public float SummonCooldownUseMultiplier { get; set; } = 1.1f;

        [LocDisplayName("{=GlobalCommonConfig_Category_Battle_SummonCooldownExample_Name}Summon Cooldown Example"),
         LocCategory("Battle", "{=GlobalCommonConfig_Category_Battle}Battle"),
         LocDescription("{=GlobalCommonConfig_Category_Battle_SummonCooldownExample_Desc}Shows the consecutive cooldowns (in seconds) for 10 summons"),
         PropertyOrder(8), YamlIgnore, ReadOnly(true), UsedImplicitly]
        public string SummonCooldownExample => string.Join(", ",
            Enumerable.Range(1, 10)
                .Select(i => $"{i}: {GetCooldownTime(i):0}s"));

        [LocDisplayName("{=GlobalCommonConfig_Category_Battle_NametagEnabled_Name}Nametags"),
         LocCategory("Battle", "{=GlobalCommonConfig_Category_Battle}Battle"),
         LocDescription("{=GlobalCommonConfig_Category_Battle_NametagEnabled_Desc}Enable or disable nametags"),
         PropertyOrder(9), Document, UsedImplicitly]

        public bool NametagEnabled { get; set; } = true;

        [LocDisplayName("{=GlobalCommonConfig_Category_Battle_NametagWidth_Name}Nametag Width"),
         LocCategory("Battle", "{=GlobalCommonConfig_Category_Battle}Battle"),
         LocDescription("{=GlobalCommonConfig_Category_Battle_NametagWidth_Desc}Width of the nametag"),
         PropertyOrder(10),
         Range(50, float.MaxValue),
         Document, UsedImplicitly]
        public float NametagWidth { get; set; } = 150f;

        [LocDisplayName("{=GlobalCommonConfig_Category_Battle_NametagHeight_Name}Nametag Height"),
         LocCategory("Battle", "{=GlobalCommonConfig_Category_Battle}Battle"),
         LocDescription("{=GlobalCommonConfig_Category_Battle_NametagHeight_Desc}Height of the nametag"),
         PropertyOrder(10),
         Range(10, float.MaxValue),
         Document, UsedImplicitly]
        public float NametagHeight { get; set; } = 30f;

        [LocDisplayName("{=GlobalCommonConfig_Category_Battle_NametagFontsize_Name}Nametag Font Size"),
         LocCategory("Battle", "{=GlobalCommonConfig_Category_Battle}Battle"),
         LocDescription("{=GlobalCommonConfig_Category_Battle_NametagFontsize_Desc}Font size of the nametag text"),
         PropertyOrder(11),
         Range(15, float.MaxValue),
         Document, UsedImplicitly]
        public float NametagFontsize { get; set; } = 20f;

        [LocDisplayName("{=GlobalCommonConfig_Category_Battle_NametagKey_Name}Nametag Toggle Key"),
         LocCategory("Battle", "{=GlobalCommonConfig_Category_Battle}Battle"),
         LocDescription("{=GlobalCommonConfig_Category_Battle_NametagKey_Desc}Key used to toggle nametags (case sensitive)"),
         PropertyOrder(12),
         Document, UsedImplicitly]
        public string NametagKey { get; set; } = "H";

        [LocDisplayName("{=GlobalCommonConfig_Category_Battle_AutoFormationForHeroes_Name}Auto Formation For BLT Heroes"),
         LocCategory("Battle", "{=GlobalCommonConfig_Category_Battle}Battle"),
         LocDescription("{=GlobalCommonConfig_Category_Battle_AutoFormationForHeroes_Desc}Automatically assign adopted BLT heroes to predefined formations during battle. Field battles: infantry (4), ranged (5), cavalry (6), horse archers (7). Sieges: infantry (6), ranged (7). Manual formation commands override this for that hero until the mission ends."),
         PropertyOrder(14), Document, UsedImplicitly]
        public bool AutoFormationForHeroes { get; set; } = true;
        
        [LocDisplayName("{=GlobalCommonConfig_Category_Battle_EnableEnemyBltAttack_Name}Enable Enemy BLT Attack"),
         LocCategory("Battle", "{=GlobalCommonConfig_Category_Battle}Battle"),
         LocDescription("{=GlobalCommonConfig_Category_Battle_EnableEnemyBltAttack_Desc}Continuously moves all enemy adopted BLT heroes into the last formation and forces that formation to attack during field battles. Siege battles are ignored. Non-BLT units already assigned to that formation may also be sent into attack."),
         PropertyOrder(15), Document, UsedImplicitly]
        public bool EnableEnemyBltAttackCommand { get; set; } = false;
        
        
        [LocDisplayName("{=GlobalCommonConfig_Category_Battle_AutoSummonStreamerOnly_Name}Auto Summon Streamer Only"),
         LocCategory("Battle", "{=GlobalCommonConfig_Category_Battle}Battle"),
         LocDescription("{=GlobalCommonConfig_Category_Battle_AutoSummonStreamerOnly_Desc}Automatically summons only the streamer's adopted hero in field and siege battles."),
         PropertyOrder(9), Document, UsedImplicitly]
        public bool AutoSummonStreamerOnly { get; set; }

        [LocDisplayName("{=GlobalCommonConfig_Category_Battle_AutoSummonStreamerEnemySide_Name}Summon Streamer On Enemy Side"),
         LocCategory("Battle", "{=GlobalCommonConfig_Category_Battle}Battle"),
         LocDescription("{=GlobalCommonConfig_Category_Battle_AutoSummonStreamerEnemySide_Desc}If enabled, the streamer's hero is auto-summoned on the enemy side. If disabled, the hero is summoned on the player side."),
         PropertyOrder(10), Document, UsedImplicitly]
        public bool AutoSummonStreamerEnemySide { get; set; }
        
        #endregion

        #region Death
        [LocDisplayName("{=GlobalCommonConfig_Category_Death_AllowDeath_Name}Allow Death"),
         LocCategory("Death", "{=GlobalCommonConfig_Category_Death}Death"),
         LocDescription("{=GlobalCommonConfig_Category_Death_AllowDeath_Desc}Whether an adopted hero is allowed to die"),
         PropertyOrder(1), Document, UsedImplicitly]
        public bool AllowDeath { get; set; } = true;

        [LocDisplayName("{=4sNJRQyw}Minimum age"),
         LocCategory("Death", "{=dbU7WEKG}Death"),
         LocDescription("{=VbBUYOfc}Minimum age death before adopted hero can die"),
         PropertyOrder(2), Document, UsedImplicitly]
        public int MinimumAge { get; set; } = 30;

        [Browsable(false), UsedImplicitly]
        public float DeathChance { get; set; } = 0.1f;

        [LocDisplayName("{=GlobalCommonConfig_Category_Death_FinalDeathChancePercent_Name}Final Death Chance Percent"),
         LocCategory("Death", "{=GlobalCommonConfig_Category_Death}Death"),
         LocDescription("{=GlobalCommonConfig_Category_Death_FinalDeathChancePercent_Desc}Final death chance percent (includes vanilla chance)"),
         PropertyOrder(3),
         Range(0, 10), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         YamlIgnore, Document, UsedImplicitly]
        public float FinalDeathChancePercent
        {
            get => DeathChance * 10f;
            set => DeathChance = value * 0.1f;
        }

        [LocDisplayName("{=GlobalCommonConfig_Category_Death_ApplyDeathChanceToAllHeroes_Name}Apply Death Chance To All Heroes"),
         LocCategory("Death", "{=GlobalCommonConfig_Category_Death}Death"),
         LocDescription("{=GlobalCommonConfig_Category_Death_ApplyDeathChanceToAllHeroes_Desc}Whether to apply the Death Chance changes to all heroes, not just adopted ones"),
         PropertyOrder(5), Document, UsedImplicitly]
        public bool ApplyDeathChanceToAllHeroes { get; set; } = false;

        [LocDisplayName("{=GlobalCommonConfig_Category_Death_RetinueDeathChancePercent_Name}Retinue Death Chance Percent"),
         LocCategory("Death", "{=GlobalCommonConfig_Category_Death}Death"),
         LocDescription("{=GlobalCommonConfig_Category_Death_RetinueDeathChancePercent_Desc}Retinue death chance percent (this determines the chance that a killing blow will actually kill the retinue, removing them from the adopted hero's retinue list)"),
         PropertyOrder(6),
         Range(0, 100), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         YamlIgnore, Document, UsedImplicitly]
        public float RetinueDeathChancePercent
        {
            get => RetinueDeathChance * 100f;
            set => RetinueDeathChance = value * 0.01f;
        }

        [Browsable(false), UsedImplicitly]
        public float RetinueDeathChance { get; set; } = 0.025f;

        [LocDisplayName("{=GlobalCommonConfig_Category_Death_Retinue2DeathChancePercent_Name}Secondary Retinue Death Chance Percent"),
         LocCategory("Death", "{=GlobalCommonConfig_Category_Death}Death"),
         LocDescription("{=GlobalCommonConfig_Category_Death_Retinue2DeathChancePercent_Desc}Secondary retinue death chance percent (this determines the chance that a killing blow will actually kill the retinue, removing them from the adopted hero's retinue list)"),
         PropertyOrder(7),
         Range(0, 100), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         YamlIgnore, Document, UsedImplicitly]
        public float Retinue2DeathChancePercent
        {
            get => Retinue2DeathChance * 100f;
            set => Retinue2DeathChance = value * 0.01f;
        }

        [Browsable(false), UsedImplicitly]
        public float Retinue2DeathChance { get; set; } = 0.025f;
        #endregion

        #region Income
        [LocDisplayName("{=GlobalCommonConfig_Category_Income_GoldIncomeEnabled_Name}Enabled"),
         LocCategory("Income", "{=GlobalCommonConfig_Category_Income}Income"),
         LocDescription("{=GlobalCommonConfig_Category_Income_GoldIncomeEnabled_Desc}Enable daily BLT gold income"),
         PropertyOrder(1), UsedImplicitly]
        public bool GoldIncomeEnabled { get; set; } = true;

        // ---- Fiefs ----
        [LocDisplayName("{=GlobalCommonConfig_Category_Income_FiefIncomeEnabled_Name}Enable Fief Income"),
         LocCategory("Income", "{=GlobalCommonConfig_Category_Income}Income"),
         LocDescription("{=GlobalCommonConfig_Category_Income_FiefIncomeEnabled_Desc}Enable BLT gold from owned settlements"),
         PropertyOrder(1), UsedImplicitly]
        public bool FiefIncomeEnabled { get; set; } = true;

        [LocDisplayName("{=GlobalCommonConfig_Category_Income_TownBaseGold_Name}Town Base Gold"),
         LocCategory("Income", "{=GlobalCommonConfig_Category_Income}Income"),
         LocDescription("{=GlobalCommonConfig_Category_Income_TownBaseGold_Desc}Base BLT gold per town per day"),
         PropertyOrder(2), UsedImplicitly]
        public int TownBaseGold { get; set; } = 3000;

        [LocDisplayName("{=GlobalCommonConfig_Category_Income_CastleBaseGold_Name}Castle Base Gold"),
         LocCategory("Income", "{=GlobalCommonConfig_Category_Income}Income"),
         LocDescription("{=GlobalCommonConfig_Category_Income_CastleBaseGold_Desc}Base BLT gold per castle per day"),
         PropertyOrder(3), UsedImplicitly]
        public int CastleBaseGold { get; set; } = 1500;

        [LocDisplayName("{=GlobalCommonConfig_Category_Income_IncludeProsperity_Name}Include Prosperity"),
         LocCategory("Income", "{=GlobalCommonConfig_Category_Income}Income"),
         LocDescription("{=GlobalCommonConfig_Category_Income_IncludeProsperity_Desc}Add prosperity-based income"),
         PropertyOrder(4), UsedImplicitly]
        public bool IncludeProsperity { get; set; } = true;

        [LocDisplayName("{=GlobalCommonConfig_Category_Income_ProsperityMultiplier_Name}Prosperity Multiplier"),
         LocCategory("Income", "{=GlobalCommonConfig_Category_Income}Income"),
         LocDescription("{=GlobalCommonConfig_Category_Income_ProsperityMultiplier_Desc}Prosperity multiplier"),
         PropertyOrder(5), UsedImplicitly]
        public float ProsperityMultiplier { get; set; } = 1f;

        // ---- Mercenary ----
        [LocDisplayName("{=GlobalCommonConfig_Category_Income_MercenaryIncomeEnabled_Name}Enable Mercenary Income"),
         LocCategory("Income", "{=GlobalCommonConfig_Category_Income}Income"),
         LocDescription("{=GlobalCommonConfig_Category_Income_MercenaryIncomeEnabled_Desc}Enable BLT gold from mercenary contracts"),
         PropertyOrder(1), UsedImplicitly]
        public bool MercenaryIncomeEnabled { get; set; } = true;

        [LocDisplayName("{=GlobalCommonConfig_Category_Income_MercenaryMultiplier_Name}Mercenary Multiplier"),
         LocCategory("Income", "{=GlobalCommonConfig_Category_Income}Income"),
         LocDescription("{=GlobalCommonConfig_Category_Income_MercenaryMultiplier_Desc}Multiplier applied to mercenary contract value (used for BLT clans and their vassals)"),
         PropertyOrder(2), UsedImplicitly]
        public int MercenaryMultiplier { get; set; } = 10;

        [LocDisplayName("{=GlobalCommonConfig_Category_Income_MercenaryMaxIncome_Name}Mercenary Maximum Daily Income"),
         LocCategory("Income", "{=GlobalCommonConfig_Category_Income}Income"),
         LocDescription("{=GlobalCommonConfig_Category_Income_MercenaryMaxIncome_Desc}Maximum BLT daily income from mercenary contract"),
         PropertyOrder(2), UsedImplicitly]
        public int MercenaryMaxIncome { get; set; } = 2000;

        #endregion

        #region Upgrades
        [LocDisplayName("{=GlobalCommonConfig_Category_Upgrades_FiefUpgrades_Name}Fief Upgrades"),
         LocCategory("Upgrades", "{=GlobalCommonConfig_Category_Upgrades}Upgrades"),
         LocDescription("{=GlobalCommonConfig_Category_Upgrades_FiefUpgrades_Desc}List of available fief (settlement) upgrades"),
         PropertyOrder(1), UsedImplicitly,
         Editor(typeof(DefaultCollectionEditor), typeof(DefaultCollectionEditor))]
        public ObservableCollection<FiefUpgrade> FiefUpgrades { get; set; } = new()
        {
            new FiefUpgrade
            {
                ID = "fief_loyalty_1",
                Name = "Improved Administration",
                Description = "Better administration increases loyalty growth",
                GoldCost = 15000,
                LoyaltyDailyFlat = 0.5f
            },
            new FiefUpgrade
            {
                ID = "fief_prosperity_1",
                Name = "Trade Hub",
                Description = "Attract more merchants to boost prosperity",
                GoldCost = 20000,
                ProsperityDailyFlat = 1.0f
            },
            new FiefUpgrade
            {
                ID = "fief_security_1",
                Name = "Guard Posts",
                Description = "Additional guard posts improve security",
                GoldCost = 12000,
                SecurityDailyFlat = 0.5f
            },
            new FiefUpgrade
            {
                ID = "fief_militia_1",
                Name = "Militia Training",
                Description = "Train civilians as militia",
                GoldCost = 10000,
                MilitiaDailyFlat = 2.0f
            },
            new FiefUpgrade
            {
                ID = "fief_food_1",
                Name = "Granary Expansion",
                Description = "Larger granaries store more food",
                GoldCost = 8000,
                FoodDailyFlat = 5.0f
            }
        };

        [LocDisplayName("{=GlobalCommonConfig_Category_Upgrades_ClanUpgrades_Name}Clan Upgrades"),
         LocCategory("Upgrades", "{=GlobalCommonConfig_Category_Upgrades}Upgrades"),
         LocDescription("{=GlobalCommonConfig_Category_Upgrades_ClanUpgrades_Desc}List of available clan-wide upgrades"),
         PropertyOrder(2), UsedImplicitly,
         Editor(typeof(DefaultCollectionEditor), typeof(DefaultCollectionEditor))]
        public ObservableCollection<ClanUpgrade> ClanUpgrades { get; set; } = new()
        {
            new ClanUpgrade
            {
                ID = "clan_renown_1",
                Name = "Clan Prestige",
                Description = "Increase your clan's fame across the land",
                GoldCost = 30000,
                RenownDaily = 1.0f
            },
            new ClanUpgrade
            {
                ID = "clan_party_1",
                Name = "Recruitment Drive",
                Description = "Allow larger party sizes for all clan members",
                GoldCost = 40000,
                PartySizeBonus = 20
            },
            new ClanUpgrade
            {
                ID = "clan_settlements_1",
                Name = "Clan Development",
                Description = "Improve loyalty and prosperity in all clan settlements",
                GoldCost = 50000,
                LoyaltyDailyFlat = 0.3f,
                ProsperityDailyFlat = 0.5f
            }
        };

        [LocDisplayName("{=GlobalCommonConfig_Category_Upgrades_KingdomUpgrades_Name}Kingdom Upgrades"),
         LocCategory("Upgrades", "{=GlobalCommonConfig_Category_Upgrades}Upgrades"),
         LocDescription("{=GlobalCommonConfig_Category_Upgrades_KingdomUpgrades_Desc}List of available kingdom-wide upgrades"),
         PropertyOrder(3), UsedImplicitly,
         Editor(typeof(DefaultCollectionEditor), typeof(DefaultCollectionEditor))]
        public ObservableCollection<KingdomUpgrade> KingdomUpgrades { get; set; } = new()
        {
            new KingdomUpgrade
            {
                ID = "kingdom_influence_1",
                Name = "Royal Authority",
                Description = "Strengthen the ruler's influence",
                GoldCost = 100000,
                InfluenceCost = 500,
                InfluenceDaily = 2.0f
            },
            new KingdomUpgrade
            {
                ID = "kingdom_military_1",
                Name = "Kingdom Military Reform",
                Description = "Increase party sizes and militia across the kingdom",
                GoldCost = 150000,
                InfluenceCost = 1000,
                PartySizeBonus = 15,
                MilitiaDailyFlat = 1.0f
            },
            new KingdomUpgrade
            {
                ID = "kingdom_prosperity_1",
                Name = "Kingdom Prosperity Initiative",
                Description = "Boost prosperity and loyalty in all kingdom settlements",
                GoldCost = 200000,
                InfluenceCost = 1500,
                LoyaltyDailyFlat = 0.2f,
                ProsperityDailyFlat = 0.5f
            }
        };

        [LocDisplayName("{=BLT_BlockNegativesAtFloor}Block Negative Upgrades At Minimum"),
         LocCategory("Upgrades", "{=BLT_Upgrades}Upgrades"),
         LocDescription("{=BLT_BlockNegativesAtFloorDesc}When enabled, upgrade effects with negative values will not be applied if the stat they affect is already at zero (or would be pushed below zero). Disable this to allow negative upgrades to always apply regardless of the current stat value."),
         PropertyOrder(4), UsedImplicitly]
        public bool BlockNegativesAtFloor { get; set; } = true;
        #endregion

        #region XP
        [LocDisplayName("{=GlobalCommonConfig_Category_XP_UseRawXP_Name}Use Raw XP"),
         LocCategory("XP", "{=GlobalCommonConfig_Category_XP}XP"),
         LocDescription("{=GlobalCommonConfig_Category_XP_UseRawXP_Desc}Use raw XP values instead of adjusting by focus and attributes, also ignoring skill cap. This avoids characters getting stuck when focus and attributes are not well distributed."),
         PropertyOrder(1), Document, UsedImplicitly]
        public bool UseRawXP { get; set; } = true;

        [LocDisplayName("{=GlobalCommonConfig_Category_XP_RawXPSkillCap_Name}Raw XP Skill Cap"),
         LocCategory("XP", "{=GlobalCommonConfig_Category_XP}XP"),
         LocDescription("{=GlobalCommonConfig_Category_XP_RawXPSkillCap_Desc}Skill cap when using Raw XP. Skills will not go above this value. 330 is the vanilla XP skill cap."),
         PropertyOrder(2), Range(0, 1023), Document, UsedImplicitly]
        public int RawXPSkillCap { get; set; } = 330;
        #endregion

        #region Kill Rewards
        [LocDisplayName("{=GlobalCommonConfig_Category_KillRewards_GoldPerKill_Name}Gold Per Kill"),
         LocCategory("Kill Rewards", "{=GlobalCommonConfig_Category_KillRewards}Kill Rewards"),
         LocDescription("{=GlobalCommonConfig_Category_KillRewards_GoldPerKill_Desc}Gold the hero gets for every kill"),
         PropertyOrder(1), Document, UsedImplicitly]
        public int GoldPerKill { get; set; } = 5000;

        [LocDisplayName("{=GlobalCommonConfig_Category_KillRewards_XPPerKill_Name}XP Per Kill"),
         LocCategory("Kill Rewards", "{=GlobalCommonConfig_Category_KillRewards}Kill Rewards"),
         LocDescription("{=GlobalCommonConfig_Category_KillRewards_XPPerKill_Desc}XP the hero gets for every kill"),
         PropertyOrder(2), Document, UsedImplicitly]
        public int XPPerKill { get; set; } = 5000;

        [LocDisplayName("{=GlobalCommonConfig_Category_KillRewards_XPPerKilled_Name}XP Per Death"),
         LocCategory("Kill Rewards", "{=GlobalCommonConfig_Category_KillRewards}Kill Rewards"),
         LocDescription("{=GlobalCommonConfig_Category_KillRewards_XPPerKilled_Desc}XP the hero gets for being killed"),
         PropertyOrder(3), Document, UsedImplicitly]
        public int XPPerKilled { get; set; } = 2000;

        [LocDisplayName("{=GlobalCommonConfig_Category_KillRewards_HealPerKill_Name}Heal Per Kill"),
         LocCategory("Kill Rewards", "{=GlobalCommonConfig_Category_KillRewards}Kill Rewards"),
         LocDescription("{=GlobalCommonConfig_Category_KillRewards_HealPerKill_Desc}HP the hero gets for every kill"),
         PropertyOrder(4), Document, UsedImplicitly]
        public int HealPerKill { get; set; } = 20;

        [LocDisplayName("{=GlobalCommonConfig_Category_KillRewards_RetinueGoldPerKill_Name}Retinue Gold Per Kill"),
         LocCategory("Kill Rewards", "{=GlobalCommonConfig_Category_KillRewards}Kill Rewards"),
         LocDescription("{=GlobalCommonConfig_Category_KillRewards_RetinueGoldPerKill_Desc}Gold the hero gets for every kill their retinue gets"),
         PropertyOrder(5), Document, UsedImplicitly]
        public int RetinueGoldPerKill { get; set; } = 2500;

        [LocDisplayName("{=GlobalCommonConfig_Category_KillRewards_RetinueHealPerKill_Name}Retinue Heal Per Kill"),
         LocCategory("Kill Rewards", "{=GlobalCommonConfig_Category_KillRewards}Kill Rewards"),
         LocDescription("{=GlobalCommonConfig_Category_KillRewards_RetinueHealPerKill_Desc}HP the hero's retinue gets for every kill"),
         PropertyOrder(6), Document, UsedImplicitly]
        public int RetinueHealPerKill { get; set; } = 50;

        [LocDisplayName("{=GlobalCommonConfig_Category_KillRewards_RelativeLevelScaling_Name}Relative Level Scaling"),
         LocCategory("Kill Rewards", "{=GlobalCommonConfig_Category_KillRewards}Kill Rewards"),
         LocDescription("{=GlobalCommonConfig_Category_KillRewards_RelativeLevelScaling_Desc}How much to scale the kill rewards by, based on relative level of the two characters. If this is 0 (or not set) then the rewards are always as specified, if this is higher than 0 then the rewards increase if the killed unit is higher level than the hero, and decrease if it is lower. At a value of 0.5 (recommended) at level difference of 10 would give about 2.5 times the normal rewards for gold, xp and health."),
         PropertyOrder(7),
         Range(0, 1), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         Document, UsedImplicitly]
        public float RelativeLevelScaling { get; set; } = 0.5f;

        [LocDisplayName("{=GlobalCommonConfig_Category_KillRewards_LevelScalingCap_Name}Level Scaling Cap"),
         LocCategory("Kill Rewards", "{=GlobalCommonConfig_Category_KillRewards}Kill Rewards"),
         LocDescription("{=GlobalCommonConfig_Category_KillRewards_LevelScalingCap_Desc}Caps the maximum multiplier for the level difference, defaults to 5 if not specified"),
         PropertyOrder(8),
         Range(0, 10), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         Document, UsedImplicitly]
        public float LevelScalingCap { get; set; } = 5;

        [LocDisplayName("{=GlobalCommonConfig_Category_KillRewards_MinimumGoldPerKill_Name}Minimum Gold Per Kill"),
         LocCategory("Kill Rewards", "{=GlobalCommonConfig_Category_KillRewards}Kill Rewards"),
         LocDescription("{=GlobalCommonConfig_Category_KillRewards_MinimumGoldPerKill_Desc}Minimum percent gold earned per kill"),
         PropertyOrder(9),
         Range(0, 1), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         Document, UsedImplicitly]
        public float MinimumGoldPerKill { get; set; } = 0.5f;
        #endregion

        #region Battle End Rewards
        [LocDisplayName("{=GlobalCommonConfig_Category_BattleEndRewards_WinGold_Name}Win Gold"),
         LocCategory("Battle End Rewards", "{=GlobalCommonConfig_Category_BattleEndRewards}Battle End Rewards"),
         LocDescription("{=GlobalCommonConfig_Category_BattleEndRewards_WinGold_Desc}Gold won if the hero's side wins"),
         PropertyOrder(1), Document, UsedImplicitly]
        public int WinGold { get; set; } = 10000;

        [LocDisplayName("{=GlobalCommonConfig_Category_BattleEndRewards_WinXP_Name}Win XP"),
         LocCategory("Battle End Rewards", "{=GlobalCommonConfig_Category_BattleEndRewards}Battle End Rewards"),
         LocDescription("{=GlobalCommonConfig_Category_BattleEndRewards_WinXP_Desc}XP the hero gets if the hero's side wins"),
         PropertyOrder(2), Document, UsedImplicitly]
        public int WinXP { get; set; } = 10000;

        [LocDisplayName("{=GlobalCommonConfig_Category_BattleEndRewards_LoseGold_Name}Lose Gold"),
         LocCategory("Battle End Rewards", "{=GlobalCommonConfig_Category_BattleEndRewards}Battle End Rewards"),
         LocDescription("{=GlobalCommonConfig_Category_BattleEndRewards_LoseGold_Desc}Gold lost if the hero's side loses (negative relative to win gold)"),
         PropertyOrder(3), Document, UsedImplicitly]
        public int LoseGold { get; set; } = 5000;

        [LocDisplayName("{=GlobalCommonConfig_Category_BattleEndRewards_LoseXP_Name}Lose XP"),
         LocCategory("Battle End Rewards", "{=GlobalCommonConfig_Category_BattleEndRewards}Battle End Rewards"),
         LocDescription("{=GlobalCommonConfig_Category_BattleEndRewards_LoseXP_Desc}XP the hero gets if the hero's side loses"),
         PropertyOrder(4), Document, UsedImplicitly]
        public int LoseXP { get; set; } = 5000;

        [LocDisplayName("{=GlobalCommonConfig_Category_BattleEndRewards_DifficultyScalingOnPlayersSide_Name}Difficulty Scaling On Player's Side"),
         LocCategory("Battle End Rewards", "{=GlobalCommonConfig_Category_BattleEndRewards}Battle End Rewards"),
         LocDescription("{=GlobalCommonConfig_Category_BattleEndRewards_DifficultyScalingOnPlayersSide_Desc}Apply difficulty scaling to the player's side"),
         PropertyOrder(5), Document, UsedImplicitly]
        public bool DifficultyScalingOnPlayersSide { get; set; } = true;

        [LocDisplayName("{=GlobalCommonConfig_Category_BattleEndRewards_DifficultyScalingOnEnemySide_Name}Difficulty Scaling On Enemy Side"),
         LocCategory("Battle End Rewards", "{=GlobalCommonConfig_Category_BattleEndRewards}Battle End Rewards"),
         LocDescription("{=GlobalCommonConfig_Category_BattleEndRewards_DifficultyScalingOnEnemySide_Desc}Apply difficulty scaling to the enemy side"),
         PropertyOrder(6), Document, UsedImplicitly]
        public bool DifficultyScalingOnEnemySide { get; set; } = true;

        [LocDisplayName("{=GlobalCommonConfig_Category_BattleEndRewards_DifficultyScaling_Name}Difficulty Scaling"),
         LocCategory("Battle End Rewards", "{=GlobalCommonConfig_Category_BattleEndRewards}Battle End Rewards"),
         LocDescription("{=GlobalCommonConfig_Category_BattleEndRewards_DifficultyScaling_Desc}End reward difficulty scaling: determines how much higher difficulty battles increase the above rewards (0 to 1)"),
         Range(0, 1), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         PropertyOrder(7), Document, UsedImplicitly]
        public float DifficultyScaling { get; set; } = 1;

        [LocDisplayName("{=GlobalCommonConfig_Category_BattleEndRewards_DifficultyScalingMin_Name}Difficulty Scaling Min"),
         LocCategory("Battle End Rewards", "{=GlobalCommonConfig_Category_BattleEndRewards}Battle End Rewards"),
         LocDescription("{=GlobalCommonConfig_Category_BattleEndRewards_DifficultyScalingMin_Desc}Minimum difficulty scaling multiplier"),
         PropertyOrder(8),
         Range(0, 1), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         Document, UsedImplicitly]
        public float DifficultyScalingMin { get; set; } = 0.2f;

        [YamlIgnore, Browsable(false)]
        public float DifficultyScalingMinClamped => MathF.Clamp(DifficultyScalingMin, 0, 1);

        [LocDisplayName("{=GlobalCommonConfig_Category_BattleEndRewards_DifficultyScalingMax_Name}Difficulty Scaling Max"),
         LocCategory("Battle End Rewards", "{=GlobalCommonConfig_Category_BattleEndRewards}Battle End Rewards"),
         LocDescription("{=GlobalCommonConfig_Category_BattleEndRewards_DifficultyScalingMax_Desc}Maximum difficulty scaling multiplier"),
         PropertyOrder(9),
         Range(1, 10), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         Document, UsedImplicitly]
        public float DifficultyScalingMax { get; set; } = 3f;

        [YamlIgnore, Browsable(false)]
        public float DifficultyScalingMaxClamped => Math.Max(DifficultyScalingMax, 1f);
        #endregion

        #region Kill Streak Rewards
        [LocDisplayName("{=GlobalCommonConfig_Category_KillStreakRewards_KillStreaks_Name}Kill Streaks"),
         LocCategory("Kill Streak Rewards", "{=GlobalCommonConfig_Category_KillStreakRewards}Kill Streak Rewards"),
         LocDescription("{=GlobalCommonConfig_Category_KillStreakRewards_KillStreaks_Desc}Kill streak configuration list"),
         Editor(typeof(DefaultCollectionEditor), typeof(DefaultCollectionEditor)),
         PropertyOrder(1), UsedImplicitly]
        public ObservableCollection<KillStreakDef> KillStreaks { get; set; } = new();

        [LocDisplayName("{=GlobalCommonConfig_Category_KillStreakRewards_ShowKillStreakPopup_Name}Show Kill Streak Popup"),
         LocCategory("Kill Streak Rewards", "{=GlobalCommonConfig_Category_KillStreakRewards}Kill Streak Rewards"),
         LocDescription("{=GlobalCommonConfig_Category_KillStreakRewards_ShowKillStreakPopup_Desc}Whether to use the popup banner to announce kill streaks. If disabled, it will only print in the overlay."),
         PropertyOrder(2), UsedImplicitly]
        public bool ShowKillStreakPopup { get; set; } = true;

        [LocDisplayName("{=GlobalCommonConfig_Category_KillStreakRewards_KillStreakPopupAlertSound_Name}Kill Streak Popup Alert Sound"),
         LocCategory("Kill Streak Rewards", "{=GlobalCommonConfig_Category_KillStreakRewards}Kill Streak Rewards"),
         LocDescription("{=GlobalCommonConfig_Category_KillStreakRewards_KillStreakPopupAlertSound_Desc}Sound to play when kill streak popup is disabled."),
         PropertyOrder(3), UsedImplicitly]
        public Log.Sound KillStreakPopupAlertSound { get; set; } = Log.Sound.Horns2;

        [LocDisplayName("{=GlobalCommonConfig_Category_KillStreakRewards_ReferenceLevelReward_Name}Reference Level Reward"),
         LocCategory("Kill Streak Rewards", "{=GlobalCommonConfig_Category_KillStreakRewards}Kill Streak Rewards"),
         LocDescription("{=GlobalCommonConfig_Category_KillStreakRewards_ReferenceLevelReward_Desc}The level at which rewards normalize and begin to decrease (if relative level scaling is enabled)."),
         PropertyOrder(4), UsedImplicitly]
        public int ReferenceLevelReward { get; set; } = 15;
        #endregion

        #region Achievements
        [LocDisplayName("{=GlobalCommonConfig_Category_Achievements_Achievements_Name}Achievements"),
         LocCategory("Achievements", "{=GlobalCommonConfig_Category_Achievements}Achievements"),
         LocDescription("{=GlobalCommonConfig_Category_Achievements_Achievements_Desc}List of available achievements"),
         Editor(typeof(DefaultCollectionEditor), typeof(DefaultCollectionEditor)),
         PropertyOrder(1), UsedImplicitly]
        public ObservableCollection<AchievementDef> Achievements { get; set; } = new();
        #endregion

        #region Shouts
        [LocDisplayName("{=GlobalCommonConfig_Category_Shouts_Shouts_Name}Shouts"),
         LocCategory("Shouts", "{=GlobalCommonConfig_Category_Shouts}Shouts"),
         LocDescription("{=GlobalCommonConfig_Category_Shouts_Shouts_Desc}Custom shouts"),
         Editor(typeof(DefaultCollectionEditor), typeof(DefaultCollectionEditor)),
         PropertyOrder(1), UsedImplicitly]
        public ObservableCollection<Shout> Shouts { get; set; } = new();

        [LocDisplayName("{=GlobalCommonConfig_Category_Shouts_IncludeDefaultShouts_Name}Include Default Shouts"),
         LocCategory("Shouts", "{=GlobalCommonConfig_Category_Shouts}Shouts"),
         LocDescription("{=GlobalCommonConfig_Category_Shouts_IncludeDefaultShouts_Desc}Whether to include default shouts"),
         PropertyOrder(2), UsedImplicitly]
        public bool IncludeDefaultShouts { get; set; } = true;
        #endregion
        #endregion

        #region Public Interface
        [YamlIgnore, Browsable(false)]
        public float DifficultyScalingClamped => MathF.Clamp(DifficultyScaling, 0, 5);

        [YamlIgnore, Browsable(false)]
        public IEnumerable<AchievementDef> ValidAchievements => Achievements.Where(a => a.Enabled);

        public AchievementDef GetAchievement(Guid id) => ValidAchievements?.FirstOrDefault(a => a.ID == id);

        public float GetCooldownTime(int summoned)
           => (float)(Math.Pow(SummonCooldownUseMultiplier, Mathf.Max(0, summoned - 1)) * SummonCooldownInSeconds);
        #endregion

        #region IUpdateFromDefault
        public void OnUpdateFromDefault(BannerlordTwitch.Settings defaultSettings)
        {
            SettingsHelpers.MergeCollectionsSorted(
                KillStreaks,
                Get(defaultSettings).KillStreaks,
                (a, b) => a.ID == b.ID,
                (a, b) => a.KillsRequired.CompareTo(b.KillsRequired)
            );
            SettingsHelpers.MergeCollections(
                Achievements,
                Get(defaultSettings).Achievements,
                (a, b) => a.ID == b.ID
            );
        }
        #endregion

        #region IDocumentable
        public void GenerateDocumentation(IDocumentationGenerator generator)
        {
            generator.Div("common-config", () =>
            {
                generator.H1("{=GlobalCommonConfig_Doc_CommonConfig}Common Config".Translate());
                DocumentationHelpers.AutoDocument(generator, this);

                var killStreaks = KillStreaks.Where(k => k.Enabled).ToList();
                if (killStreaks.Any())
                {
                    generator.H2("{=GlobalCommonConfig_Doc_KillStreaks}Kill Streaks".Translate());
                    generator.Table("kill-streaks", () =>
                    {
                        generator.TR(() => generator
                            .TH("{=GlobalCommonConfig_Doc_Name}Name".Translate())
                            .TH("{=GlobalCommonConfig_Doc_KillsRequired}Kills Required".Translate())
                            .TH("{=GlobalCommonConfig_Doc_Reward}Reward".Translate())
                        );

                        foreach (var k in killStreaks.OrderBy(k => k.KillsRequired))
                        {
                            generator.TR(() =>
                                generator
                                    .TD(k.Name.ToString())
                                    .TD($"{k.KillsRequired}")
                                    .TD(() =>
                                    {
                                        if (k.GoldReward > 0) generator.P($"{k.GoldReward}{Naming.Gold}");
                                        if (k.XPReward > 0) generator.P($"{k.XPReward}{Naming.XP}");
                                    })
                            );
                        }
                    });
                }

                var achievements = ValidAchievements.Where(a => a.Enabled).ToList();
                if (achievements.Any())
                {
                    generator.H2("{=GlobalCommonConfig_Doc_Achievements}Achievements".Translate());
                    generator.Table("achievements", () =>
                    {
                        generator.TR(() => generator
                            .TH("{=GlobalCommonConfig_Doc_Name}Name".Translate())
                            .TH("{=GlobalCommonConfig_Doc_KillsRequired}Requirements".Translate())
                            .TH("{=GlobalCommonConfig_Doc_Reward}Reward".Translate())
                        );

                        foreach (var a in achievements.OrderBy(a => a.Name.ToString()))
                        {
                            generator.TR(() =>
                                generator
                                    .TD(a.Name.ToString())
                                    .TD(() =>
                                    {
                                        foreach (var r in a.Requirements)
                                        {
                                            if (r is IDocumentable d)
                                                d.GenerateDocumentation(generator);
                                            else
                                                generator.P(r.ToString());
                                        }
                                    })
                                    .TD(() =>
                                    {
                                        if (a.GoldGain > 0) generator.P($"{a.GoldGain}{Naming.Gold}");
                                        if (a.XPGain > 0) generator.P($"{a.XPGain}{Naming.XP}");
                                        if (a.GiveItemReward)
                                            generator.P($"{Naming.Item}: {a.ItemReward}");

                                        if (a.GivePassivePower)
                                        {
                                            generator.P(
                                                "power-title",
                                                a.PassivePowerReward.Name.ToString() + ":"
                                            );

                                            foreach (var power in a.PassivePowerReward.Powers)
                                            {
                                                if (power is IDocumentable docPower)
                                                    docPower.GenerateDocumentation(generator);
                                                else
                                                    generator.P(power.ToString());
                                            }
                                        }
                                    })
                            );
                        }
                    });
                }
            });
            new UpgradeSystemDocumentation().GenerateDocumentation(generator);


            var kingdoms = MapHub.CurrentMapData?.Kingdoms;
            if (kingdoms == null || kingdoms.Count == 0)
            {
                MapHub.UpdateMapData(true);
                kingdoms = MapHub.CurrentMapData?.Kingdoms;
            }

            if (kingdoms == null || kingdoms.Count == 0)
                return;

            generator.H1("Campaign Map");
            generator.H2("Legend".Translate());

            generator.Table("legend", () =>
            {
                var kingdoms = MapHub.CurrentMapData?.Kingdoms;
                if (kingdoms == null || kingdoms.Count == 0)
                    return;

                generator.H1("Campaign Map");
                generator.H2("{=GlobalCommonConfig_Doc_MapLegend}Map Legend".Translate());

                generator.Table("legend", () =>
                {
                    generator.TR(() =>
                    {
                        generator.TH("{=GlobalCommonConfig_Doc_Color}Color".Translate());
                        generator.TH("{=GlobalCommonConfig_Doc_Name}Name".Translate());
                    });

                    // Map
                    var settlements = MapHub.CurrentMapData?.Settlements;
                    if (settlements == null || settlements.Count == 0)
                    {
                        MapHub.UpdateMapData(true);
                        settlements = MapHub.CurrentMapData?.Settlements;
                    }

                    if (settlements == null || settlements.Count == 0)
                        return;

                    var segments = MapHub.CurrentMapData.Coastline;
                    generator.H2("Map");

                    generator.Div(() =>
                    {
                        // Outer container div with inline style
                        generator.P("<div style=\"position:relative; width:1500px; height:1000px;" +
                                    "background-color:#1f1f1f; border:3px solid #111; overflow:hidden; left:-25%; \">");

                        float margin = 35f;
                        float mapWidth = 1500f - 2 * margin;
                        float mapHeight = 1000f - 2 * margin;

                        float minX = settlements.Min(s => s.X);
                        float maxX = settlements.Max(s => s.X);
                        float minY = settlements.Min(s => s.Y);
                        float maxY = settlements.Max(s => s.Y);

                        var kingdomDict = MapHub.CurrentMapData.Kingdoms.ToDictionary(k => k.Id, k => k.Color1);
                        var kingdomBorderDict = MapHub.CurrentMapData.Kingdoms.ToDictionary(k => k.Id, k => k.Color2);

                        if (segments?.Count > 0)
                        {
                            float worldWidth = maxX - minX;
                            float worldHeight = maxY - minY;

                            if (worldWidth == 0) worldWidth = 1;
                            if (worldHeight == 0) worldHeight = 1;

                            foreach (var seg in segments)
                            {
                                float scaledX1 = (seg.X1 - minX) / worldWidth * mapWidth + margin;
                                float scaledY1 = (seg.Y1 - minY) / worldHeight * mapHeight + margin;

                                float scaledX2 = (seg.X2 - minX) / worldWidth * mapWidth + margin;
                                float scaledY2 = (seg.Y2 - minY) / worldHeight * mapHeight + margin;

                                generator.MapSegment(
                                    scaledX1,
                                    scaledY1,
                                    scaledX2,
                                    scaledY2
                                );
                            }
                        }

                        foreach (var s in settlements)
                        {
                            float scaledX = (s.X - minX) / (maxX - minX) * mapWidth + margin;
                            float scaledY = (s.Y - minY) / (maxY - minY) * mapHeight + margin;

                            generator.MapLabel(
                                scaledX,
                                scaledY,
                                s.Name,
                                s.Type,
                                s.KingdomId,
                                kingdomId => kingdomDict.TryGetValue(kingdomId, out var c) ? c : "#000080",
                                kingdomId => kingdomBorderDict.TryGetValue(kingdomId, out var c) ? c : "#000000"
                            );
                        }

                        generator.P("</div>"); // close container
                    });
                });
            });
        }
        #endregion

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
