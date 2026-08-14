using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.UI;
using BannerlordTwitch.Util;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using YamlDotNet.Serialization;

namespace BLTAdoptAHero
{
    [CategoryOrder("General", 1),
     CategoryOrder("Equipment", 2),
     CategoryOrder("Balancing", 3),
     CategoryOrder("Round Type", 4),
     CategoryOrder("Round Rewards", 5),
     CategoryOrder("Rewards", 6),
     CategoryOrder("Betting", 7),
     CategoryOrder("Prize", 8),
     CategoryOrder("Prize Tier", 9),
     CategoryOrder("Custom Prize", 10),
     LocDisplayName("{=GlobalTournamentConfig_Name}Tournament Config")]
    internal class GlobalTournamentConfig : IDocumentable
    {
        #region Static
        private const string ID = "Adopt A Hero - Tournament Config";
        internal static void Register() => ActionManager.RegisterGlobalConfigType(ID, typeof(GlobalTournamentConfig));
        internal static GlobalTournamentConfig Get() => ActionManager.GetGlobalConfig<GlobalTournamentConfig>(ID);
        internal static GlobalTournamentConfig Get(BannerlordTwitch.Settings fromSettings) => fromSettings.GetGlobalConfig<GlobalTournamentConfig>(ID);
        #endregion

        #region User Editable
        #region General
        [LocDisplayName("{=GlobalTournamentConfig_Category_General_StartHealthMultiplier_Name}Start Health Multiplier"),
         LocCategory("General", "{=GlobalTournamentConfig_Category_General}General"),
         LocDescription("{=GlobalTournamentConfig_Category_General_StartHealthMultiplier_Desc}Amount to multiply normal starting health by"),
         PropertyOrder(1), Range(0.5, 10),
         Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)), UsedImplicitly, Document]
        public float StartHealthMultiplier { get; set; } = 2;

        [LocDisplayName("{=GlobalTournamentConfig_Category_General_DisableKillRewardsInTournament_Name}Disable Kill Rewards In Tournament"),
         LocCategory("General", "{=GlobalTournamentConfig_Category_General}General"),
         LocDescription("{=GlobalTournamentConfig_Category_General_DisableKillRewardsInTournament_Desc}Heroes won't get any kill rewards in tournaments"),
         PropertyOrder(2), Document, UsedImplicitly]
        public bool DisableKillRewardsInTournament { get; set; } = true;

        [LocDisplayName("{=GlobalTournamentConfig_Category_General_DisableTrackingKillsTournament_Name}Disable Tracking Kills In Tournament"),
         LocCategory("General", "{=GlobalTournamentConfig_Category_General}General"),
         LocDescription("{=GlobalTournamentConfig_Category_General_DisableTrackingKillsTournament_Desc}Tournament kills/deaths won't be counted towards achievements or kill streaks"),
         PropertyOrder(3), Document, UsedImplicitly]
        public bool DisableTrackingKillsTournament { get; set; } = true;
        #endregion

        #region Equipment
        [LocDisplayName("{=GlobalTournamentConfig_Category_Equipment_NoHorses_Name}No Horses"),
         LocCategory("Equipment", "{=GlobalTournamentConfig_Category_Equipment}Equipment"),
         LocDescription("{=GlobalTournamentConfig_Category_Equipment_NoHorses_Desc}Remove horses completely from BLT tournaments"),
         PropertyOrder(2), UsedImplicitly, Document]
        public bool NoHorses { get; set; } = true;

        [LocDisplayName("{=GlobalTournamentConfig_Category_Equipment_NoSpears_Name}No Spears"),
         LocCategory("Equipment", "{=GlobalTournamentConfig_Category_Equipment}Equipment"),
         LocDescription("{=GlobalTournamentConfig_Category_Equipment_NoSpears_Desc}Replaces all lances and spears with swords"),
         PropertyOrder(3), UsedImplicitly, Document]
        public bool NoSpears { get; set; } = true;

        [LocDisplayName("{=GlobalTournamentConfig_Category_Equipment_NormalizeArmor_Name}Normalize Armor"),
         LocCategory("Equipment", "{=GlobalTournamentConfig_Category_Equipment}Equipment"),
         LocDescription("{=GlobalTournamentConfig_Category_Equipment_NormalizeArmor_Desc}Replace all armor with fixed tier armor based on culture (see tier below)"),
         PropertyOrder(4), UsedImplicitly, Document]
        public bool NormalizeArmor { get; set; }

        [LocDisplayName("{=GlobalTournamentConfig_Category_Equipment_NormalizeArmorTier_Name}Normalize Armor Tier"),
         LocCategory("Equipment", "{=GlobalTournamentConfig_Category_Equipment}Equipment"),
         LocDescription("{=GlobalTournamentConfig_Category_Equipment_NormalizeArmorTier_Desc}Armor tier applied to all contestants (1 to 6) if normalization is enabled"),
         PropertyOrder(5), Range(1, 6), UsedImplicitly, Document]
        public int NormalizeArmorTier { get; set; } = 3;

        [LocDisplayName("{=GlobalTournamentConfig_Category_Equipment_RandomizeWeaponTypes_Name}Randomize Weapon Types"),
         LocCategory("Equipment", "{=GlobalTournamentConfig_Category_Equipment}Equipment"),
         LocDescription("{=GlobalTournamentConfig_Category_Equipment_RandomizeWeaponTypes_Desc}Randomize weapons each round based on participant classes"),
         PropertyOrder(6), UsedImplicitly, Document]
        public bool RandomizeWeaponTypes { get; set; } = true;
        
        [LocDisplayName("{=GlobalTournamentConfig_Category_Equipment_NoUnarmed_Name}No Unarmed"),
         LocCategory("Equipment", "{=GlobalTournamentConfig_Category_Equipment}Equipment"),
         LocDescription("{=GlobalTournamentConfig_Category_Equipment_NoUnarmed_Desc}Disable unarmed (fist) rounds in tournaments"),
         PropertyOrder(4), UsedImplicitly, Document]
        public bool NoUnarmed { get; set; } = false;

        [LocDisplayName("{=GlobalTournamentConfig_Category_Equipment_NoRanged_Name}No Ranged Weapons"),
         LocCategory("Equipment", "{=GlobalTournamentConfig_Category_Equipment}Equipment"),
         LocDescription("{=GlobalTournamentConfig_Category_Equipment_NoRanged_Desc}Disable bows, crossbows and throwing weapons in tournaments"),
         PropertyOrder(5), UsedImplicitly, Document]
        public bool NoRanged { get; set; } = false;

        [LocDisplayName("{=GlobalTournamentConfig_Category_Equipment_OverrideAmmo_Name}Override Ammo"),
         LocCategory("Equipment", "{=GlobalTournamentConfig_Category_Equipment}Equipment"),
         LocDescription("{=GlobalTournamentConfig_Category_Equipment_OverrideAmmo_Desc}Force all ranged ammo to a fixed amount"),
         PropertyOrder(6), UsedImplicitly, Document]
        public bool OverrideAmmo { get; set; } = false;

        [LocDisplayName("{=GlobalTournamentConfig_Category_Equipment_AmmoAmount_Name}Ammo Amount"),
         LocCategory("Equipment", "{=GlobalTournamentConfig_Category_Equipment}Equipment"),
         LocDescription("{=GlobalTournamentConfig_Category_Equipment_AmmoAmount_Desc}Amount of ammo for bows, crossbows and throwing weapons"),
         PropertyOrder(7), Range(0, 200), UsedImplicitly, Document]
        public int AmmoAmount { get; set; } = 20;
        #endregion

        #region Balancing
        [LocDisplayName("{=GlobalTournamentConfig_Category_Balancing_PreviousWinnerDebuffs_Name}Previous Winner Debuffs"),
         LocCategory("Balancing", "{=GlobalTournamentConfig_Category_Balancing}Balancing"),
         LocDescription("{=GlobalTournamentConfig_Category_Balancing_PreviousWinnerDebuffs_Desc}Applies skill debuffs to previous tournament winners"),
         Editor(typeof(DefaultCollectionEditor), typeof(DefaultCollectionEditor)), PropertyOrder(1), UsedImplicitly, Document]
        public ObservableCollection<SkillDebuffDef> PreviousWinnerDebuffs { get; set; } = new() { new() };
        #endregion

        #region Round Types
        public class Round1Def
        {
            [LocDisplayName("{=GlobalTournamentConfig_RoundType_Vanilla_Name}Vanilla"),
             LocDescription("{=GlobalTournamentConfig_RoundType_Vanilla_Desc}Allow the vanilla round setup"),
             PropertyOrder(1), UsedImplicitly, Document]
            public bool EnableVanilla { get; set; } = true;

            [LocDisplayName("{=GlobalTournamentConfig_RoundType_1Match4Teams_Name}1 Match 4 Teams"),
             LocDescription("{=GlobalTournamentConfig_Round1Def_1Match4Teams_Desc}Allow 1 match with 4 teams of 4"),
             PropertyOrder(3), UsedImplicitly, Document]
            public bool Enable1Match4Teams { get; set; }

            [LocDisplayName("{=GlobalTournamentConfig_RoundType_2Match2Teams_Name}2 Matches 2 Teams"),
             LocDescription("{=GlobalTournamentConfig_Round1Def_2Match2Teams_Desc}Allow 2 matches with 2 teams of 4"),
             PropertyOrder(4), UsedImplicitly, Document]
            public bool Enable2Match2Teams { get; set; }

            [LocDisplayName("{=GlobalTournamentConfig_RoundType_2Match4Teams_Name}2 Matches 4 Teams"),
             LocDescription("{=GlobalTournamentConfig_Round1Def_2Match4Teams_Desc}Allow 2 matches with 4 teams of 2"),
             PropertyOrder(5), UsedImplicitly, Document]
            public bool Enable2Match4Teams { get; set; }

            [LocDisplayName("{=GlobalTournamentConfig_RoundType_4Match2Teams_Name}4 Matches 2 Teams"),
             LocDescription("{=GlobalTournamentConfig_Round1Def_4Match2Teams_Desc}Allow 4 matches with 2 teams of 2"),
             PropertyOrder(6), UsedImplicitly, Document]
            public bool Enable4Match2Teams { get; set; }

            [LocDisplayName("{=GlobalTournamentConfig_RoundType_4Match4Teams_Name}4 Matches 4 Teams"),
             LocDescription("{=GlobalTournamentConfig_Round1Def_4Match4Teams_Desc}Allow 4 matches with 4 teams of 1"),
             PropertyOrder(7), UsedImplicitly, Document]
            public bool Enable4Match4Teams { get; set; }

            [LocDisplayName("{=GlobalTournamentConfig_RoundType_8Match2Teams_Name}8 Matches 2 Teams"),
             LocDescription("{=GlobalTournamentConfig_Round1Def_8Match2Teams_Desc}Allow 8 matches with 2 teams of 1"),
             PropertyOrder(8), UsedImplicitly, Document]
            public bool Enable8Match2Teams { get; set; }

            public override string ToString()
            {
                var enabled = new List<string>();
                if (EnableVanilla) enabled.Add("{=GlobalTournamentConfig_RoundType_Vanilla_Name}Vanilla".Translate());
                if (Enable1Match4Teams) enabled.Add("{=GlobalTournamentConfig_RoundType_1Match4Teams_Name}1 Match 4 Teams".Translate());
                if (Enable2Match2Teams) enabled.Add("{=GlobalTournamentConfig_RoundType_2Match2Teams_Name}2 Matches 2 Teams".Translate());
                if (Enable2Match4Teams) enabled.Add("{=GlobalTournamentConfig_RoundType_2Match4Teams_Name}2 Matches 4 Teams".Translate());
                if (Enable4Match2Teams) enabled.Add("{=GlobalTournamentConfig_RoundType_4Match2Teams_Name}4 Matches 2 Teams".Translate());
                if (Enable4Match4Teams) enabled.Add("{=GlobalTournamentConfig_RoundType_4Match4Teams_Name}4 Matches 4 Teams".Translate());
                if (Enable8Match2Teams) enabled.Add("{=GlobalTournamentConfig_RoundType_8Match2Teams_Name}8 Matches 2 Teams".Translate());
                return string.Join(", ", enabled);
            }

            public const int ParticipantCount = 16;
            public const int WinnerCount = ParticipantCount / 2;

            public TournamentRound GetRandomRound(TournamentRound vanilla, TournamentGame.QualificationMode qualificationMode)
            {
                var matches = new List<TournamentRound>();
                if (Enable1Match4Teams)
                    matches.Add(new(ParticipantCount, 1, 4, WinnerCount, qualificationMode));
                if (Enable2Match2Teams)
                    matches.Add(new(ParticipantCount, 2, 2, WinnerCount, qualificationMode));
                if (Enable2Match4Teams)
                    matches.Add(new(ParticipantCount, 2, 4, WinnerCount, qualificationMode));
                if (Enable4Match2Teams)
                    matches.Add(new(ParticipantCount, 4, 2, WinnerCount, qualificationMode));
                if (Enable4Match4Teams)
                    matches.Add(new(ParticipantCount, 4, 4, WinnerCount, qualificationMode));
                if (Enable8Match2Teams)
                    matches.Add(new(ParticipantCount, 8, 2, WinnerCount, qualificationMode));
                if (EnableVanilla || !matches.Any())
                    matches.Add(vanilla);
                return matches.SelectRandom();
            }
        }

        public class Round2Def
        {
            [LocDisplayName("{=GlobalTournamentConfig_RoundType_Vanilla_Name}Vanilla"),
             LocDescription("{=GlobalTournamentConfig_RoundType_Vanilla_Desc}Allow the vanilla round setup"),
             PropertyOrder(1), UsedImplicitly, Document]
            public bool EnableVanilla { get; set; } = true;

            [LocDisplayName("{=GlobalTournamentConfig_RoundType_1Match2Teams_Name}1 Match 2 Teams"),
             LocDescription("{=GlobalTournamentConfig_Round2Def_1Match2Teams_Desc}Allow 1 match with 2 teams of 4"),
             PropertyOrder(2), UsedImplicitly, Document]
            public bool Enable1Match2Teams { get; set; }

            [LocDisplayName("{=GlobalTournamentConfig_RoundType_1Match4Teams_Name}1 Match 4 Teams"),
             LocDescription("{=GlobalTournamentConfig_Round2Def_1Match4Teams_Desc}Allow 1 match with 4 teams of 2"),
             PropertyOrder(3), UsedImplicitly, Document]
            public bool Enable1Match4Teams { get; set; }

            [LocDisplayName("{=GlobalTournamentConfig_RoundType_2Match2Teams_Name}2 Matches 2 Teams"),
             LocDescription("{=GlobalTournamentConfig_Round2Def_2Match2Teams_Desc}Allow 2 matches with 2 teams of 2"),
             PropertyOrder(4), UsedImplicitly, Document]
            public bool Enable2Match2Teams { get; set; }

            [LocDisplayName("{=GlobalTournamentConfig_RoundType_2Match4Teams_Name}2 Matches 4 Teams"),
             LocDescription("{=GlobalTournamentConfig_Round2Def_2Match4Teams_Desc}Allow 2 matches with 4 teams of 1"),
             PropertyOrder(5), UsedImplicitly, Document]
            public bool Enable2Match4Teams { get; set; }

            [LocDisplayName("{=GlobalTournamentConfig_RoundType_4Match2Teams_Name}4 Matches 2 Teams"),
             LocDescription("{=GlobalTournamentConfig_Round2Def_4Match2Teams_Desc}Allow 4 matches with 2 teams of 1"),
             PropertyOrder(6), UsedImplicitly, Document]
            public bool Enable4Match2Teams { get; set; }

            public override string ToString()
            {
                var enabled = new List<string>();
                if (EnableVanilla) enabled.Add("{=GlobalTournamentConfig_RoundType_Vanilla_Name}Vanilla".Translate());
                if (Enable1Match2Teams) enabled.Add("{=GlobalTournamentConfig_RoundType_1Match2Teams_Name}1 Match 2 Teams".Translate());
                if (Enable1Match4Teams) enabled.Add("{=GlobalTournamentConfig_RoundType_1Match4Teams_Name}1 Match 4 Teams".Translate());
                if (Enable2Match2Teams) enabled.Add("{=GlobalTournamentConfig_RoundType_2Match2Teams_Name}2 Matches 2 Teams".Translate());
                if (Enable2Match4Teams) enabled.Add("{=GlobalTournamentConfig_RoundType_2Match4Teams_Name}2 Matches 4 Teams".Translate());
                if (Enable4Match2Teams) enabled.Add("{=GlobalTournamentConfig_RoundType_4Match2Teams_Name}4 Matches 2 Teams".Translate());
                return string.Join(", ", enabled);
            }

            public const int ParticipantCount = 8;
            public const int WinnerCount = ParticipantCount / 2;

            public TournamentRound GetRandomRound(TournamentRound vanilla, TournamentGame.QualificationMode qualificationMode)
            {
                var matches = new List<TournamentRound>();
                if (Enable1Match2Teams)
                    matches.Add(new(ParticipantCount, 1, 2, WinnerCount, qualificationMode));
                if (Enable1Match4Teams)
                    matches.Add(new(ParticipantCount, 1, 4, WinnerCount, qualificationMode));
                if (Enable2Match2Teams)
                    matches.Add(new(ParticipantCount, 2, 2, WinnerCount, qualificationMode));
                if (Enable2Match4Teams)
                    matches.Add(new(ParticipantCount, 2, 4, WinnerCount, qualificationMode));
                if (Enable4Match2Teams)
                    matches.Add(new(ParticipantCount, 4, 2, WinnerCount, qualificationMode));
                if (EnableVanilla || !matches.Any())
                    matches.Add(vanilla);
                return matches.SelectRandom();
            }
        }

        public class Round3Def
        {
            [LocDisplayName("{=GlobalTournamentConfig_RoundType_Vanilla_Name}Vanilla"),
             LocDescription("{=GlobalTournamentConfig_RoundType_Vanilla_Desc}Allow the vanilla round setup"),
             PropertyOrder(1), UsedImplicitly, Document]
            public bool EnableVanilla { get; set; } = true;

            [LocDisplayName("{=GlobalTournamentConfig_RoundType_1Match2Teams_Name}1 Match 2 Teams"),
             LocDescription("{=GlobalTournamentConfig_Round3Def_1Match2Teams_Desc}Allow 1 match with 2 teams of 2"),
             PropertyOrder(2), UsedImplicitly, Document]
            public bool Enable1Match2Teams { get; set; }

            [LocDisplayName("{=GlobalTournamentConfig_RoundType_1Match4Teams_Name}1 Match 4 Teams"),
             LocDescription("{=GlobalTournamentConfig_Round3Def_1Match4Teams_Desc}Allow 1 match with 4 teams of 1"),
             PropertyOrder(3), UsedImplicitly, Document]
            public bool Enable1Match4Teams { get; set; }

            [LocDisplayName("{=GlobalTournamentConfig_RoundType_2Match2Teams_Name}2 Matches 2 Teams"),
             LocDescription("{=GlobalTournamentConfig_Round3Def_2Match2Teams_Desc}Allow 2 matches with 2 teams of 1"),
             PropertyOrder(4), UsedImplicitly, Document]
            public bool Enable2Match2Teams { get; set; }

            public override string ToString()
            {
                var enabled = new List<string>();
                if (EnableVanilla) enabled.Add("{=GlobalTournamentConfig_RoundType_Vanilla_Name}Vanilla".Translate());
                if (Enable1Match2Teams) enabled.Add("{=GlobalTournamentConfig_RoundType_1Match2Teams_Name}1 Match 2 Teams".Translate());
                if (Enable1Match4Teams) enabled.Add("{=GlobalTournamentConfig_RoundType_1Match4Teams_Name}1 Match 4 Teams".Translate());
                if (Enable2Match2Teams) enabled.Add("{=GlobalTournamentConfig_RoundType_2Match2Teams_Name}2 Matches 2 Teams".Translate());
                return string.Join(", ", enabled);
            }

            public const int ParticipantCount = 4;
            public const int WinnerCount = ParticipantCount / 2;

            public TournamentRound GetRandomRound(TournamentRound vanilla, TournamentGame.QualificationMode qualificationMode)
            {
                var matches = new List<TournamentRound>();
                if (Enable1Match2Teams)
                    matches.Add(new(ParticipantCount, 1, 2, WinnerCount, qualificationMode));
                if (Enable1Match4Teams)
                    matches.Add(new(ParticipantCount, 1, 4, WinnerCount, qualificationMode));
                if (Enable2Match2Teams)
                    matches.Add(new(ParticipantCount, 2, 2, WinnerCount, qualificationMode));
                if (EnableVanilla || !matches.Any())
                    matches.Add(vanilla);
                return matches.SelectRandom();
            }
        }

        [LocDisplayName("{=GlobalTournamentConfig_Category_RoundType_Round1Type_Name}Round 1 Type"),
         LocCategory("Round Type", "{=GlobalTournamentConfig_Category_RoundType}Round Type"),
         LocDescription("{=GlobalTournamentConfig_Category_RoundType_Round1Type_Desc}Configuration for the first round"),
         PropertyOrder(1), ExpandableObject, UsedImplicitly, Document]
        public Round1Def Round1Type { get; set; } = new();

        [LocDisplayName("{=GlobalTournamentConfig_Category_RoundType_Round2Type_Name}Round 2 Type"),
         LocCategory("Round Type", "{=GlobalTournamentConfig_Category_RoundType}Round Type"),
         LocDescription("{=GlobalTournamentConfig_Category_RoundType_Round2Type_Desc}Configuration for the second round"),
         PropertyOrder(2), ExpandableObject, UsedImplicitly, Document]
        public Round2Def Round2Type { get; set; } = new();

        [LocDisplayName("{=GlobalTournamentConfig_Category_RoundType_Round3Type_Name}Round 3 Type"),
         LocCategory("Round Type", "{=GlobalTournamentConfig_Category_RoundType}Round Type"),
         LocDescription("{=GlobalTournamentConfig_Category_RoundType_Round3Type_Desc}Configuration for the third round"),
         PropertyOrder(3), ExpandableObject, UsedImplicitly, Document]
        public Round3Def Round3Type { get; set; } = new();

        #endregion

        #region Round Rewards
        public class RoundRewardsDef
        {
            [LocDisplayName("{=GlobalTournamentConfig_RoundRewards_WinGold_Name}Win Gold"),
             LocDescription("{=GlobalTournamentConfig_RoundRewards_WinGold_Desc}Gold awarded if the hero wins their match in the round"),
             PropertyOrder(1), UsedImplicitly, Document]
            public int WinGold { get; set; } = 10000;

            [LocDisplayName("{=GlobalTournamentConfig_RoundRewards_WinXP_Name}Win XP"),
             LocDescription("{=GlobalTournamentConfig_RoundRewards_WinXP_Desc}XP awarded if the hero wins their match in the round"),
             PropertyOrder(2), UsedImplicitly, Document]
            public int WinXP { get; set; } = 10000;

            [LocDisplayName("{=GlobalTournamentConfig_RoundRewards_LoseXP_Name}Lose XP"),
             LocDescription("{=GlobalTournamentConfig_RoundRewards_LoseXP_Desc}XP awarded if the hero loses their match in the round"),
             PropertyOrder(3), UsedImplicitly, Document]
            public int LoseXP { get; set; } = 2500;

            public override string ToString() =>
                "{=GlobalTournamentConfig_RoundRewards_WinGold_Name}Win Gold".Translate() +
                $" {WinGold}, " +
                "{=GlobalTournamentConfig_RoundRewards_WinXP_Name}Win XP".Translate() +
                $" {WinXP}, " +
                "{=GlobalTournamentConfig_RoundRewards_LoseXP_Name}Lose XP".Translate() +
                $" {LoseXP}";
        }

        [LocDisplayName("{=GlobalTournamentConfig_Category_RoundRewards_Round1Rewards_Name}Round 1 Rewards"),
         LocCategory("Round Rewards", "{=GlobalTournamentConfig_Category_RoundRewards}Round Rewards"),
         LocDescription("{=GlobalTournamentConfig_Category_RoundRewards_Round1Rewards_Desc}Rewards for round 1"),
         PropertyOrder(1), ExpandableObject, UsedImplicitly, Document]
        public RoundRewardsDef Round1Rewards { get; set; } = new() { WinGold = 5000, WinXP = 5000, LoseXP = 5000 };

        [LocDisplayName("{=GlobalTournamentConfig_Category_RoundRewards_Round2Rewards_Name}Round 2 Rewards"),
         LocCategory("Round Rewards", "{=GlobalTournamentConfig_Category_RoundRewards}Round Rewards"),
         LocDescription("{=GlobalTournamentConfig_Category_RoundRewards_Round2Rewards_Desc}Rewards for round 2"),
         PropertyOrder(2), ExpandableObject, UsedImplicitly, Document]
        public RoundRewardsDef Round2Rewards { get; set; } = new() { WinGold = 7500, WinXP = 7500, LoseXP = 7500 };

        [LocDisplayName("{=GlobalTournamentConfig_Category_RoundRewards_Round3Rewards_Name}Round 3 Rewards"),
         LocCategory("Round Rewards", "{=GlobalTournamentConfig_Category_RoundRewards}Round Rewards"),
         LocDescription("{=GlobalTournamentConfig_Category_RoundRewards_Round3Rewards_Desc}Rewards for round 3"),
         PropertyOrder(3), ExpandableObject, UsedImplicitly, Document]
        public RoundRewardsDef Round3Rewards { get; set; } = new() { WinGold = 10000, WinXP = 10000, LoseXP = 10000 };

        [LocDisplayName("{=GlobalTournamentConfig_Category_RoundRewards_Round4Rewards_Name}Round 4 Rewards"),
         LocCategory("Round Rewards", "{=GlobalTournamentConfig_Category_RoundRewards}Round Rewards"),
         LocDescription("{=GlobalTournamentConfig_Category_RoundRewards_Round4Rewards_Desc}Rewards for round 4"),
         PropertyOrder(4), ExpandableObject, UsedImplicitly, Document]
        public RoundRewardsDef Round4Rewards { get; set; } = new() { WinGold = 0, WinXP = 0, LoseXP = 0 };

        [YamlIgnore, Browsable(false)]
        public RoundRewardsDef[] RoundRewards => new[] { Round1Rewards, Round2Rewards, Round3Rewards, Round4Rewards };
        #endregion

        #region Rewards
        [LocDisplayName("{=GlobalTournamentConfig_Rewards_WinGold_Name}Win Gold"),
         LocCategory("Rewards", "{=GlobalTournamentConfig_Category_Rewards}Rewards"),
         LocDescription("{=GlobalTournamentConfig_Rewards_WinGold_Desc}Gold awarded if the hero wins the tournament"),
         PropertyOrder(1), UsedImplicitly, Document]
        public int WinGold { get; set; } = 50000;

        [LocDisplayName("{=GlobalTournamentConfig_Rewards_WinXP_Name}Win XP"),
         LocCategory("Rewards", "{=GlobalTournamentConfig_Category_Rewards}Rewards"),
         LocDescription("{=GlobalTournamentConfig_Rewards_WinXP_Desc}XP awarded if the hero wins the tournament"),
         PropertyOrder(2), UsedImplicitly, Document]
        public int WinXP { get; set; } = 50000;

        [LocDisplayName("{=GlobalTournamentConfig_Rewards_ParticipateXP_Name}Participate XP"),
         LocCategory("Rewards", "{=GlobalTournamentConfig_Category_Rewards}Rewards"),
         LocDescription("{=GlobalTournamentConfig_Rewards_ParticipateXP_Desc}XP awarded if the hero participates but does not win"),
         PropertyOrder(3), UsedImplicitly, Document]
        public int ParticipateXP { get; set; } = 10000;

        [LocDisplayName("{=GlobalTournamentConfig_Rewards_Prize_Name}Prize"),
         LocCategory("Rewards", "{=GlobalTournamentConfig_Category_Rewards}Rewards"),
         LocDescription("{=GlobalTournamentConfig_Rewards_Prize_Desc}Reward granted to the tournament winner"),
         PropertyOrder(4), ExpandableObject, Expand, UsedImplicitly, Document]
        public GeneratedRewardDef Prize { get; set; } = new()
        {
            ArmorWeight = 0.3f,
            WeaponWeight = 1f,
            MountWeight = 0.1f,
            Tier1Weight = 0,
            Tier2Weight = 0,
            Tier3Weight = 0,
            Tier4Weight = 0,
            Tier5Weight = 0,
            Tier6Weight = 1,
            CustomWeight = 1,
            CustomItemName = "{=GlobalTournamentConfig_Rewards_Prize_CustomItemName}Prize {ITEMNAME}",
            CustomItemPower = 1,
        };
        #endregion

        #region Betting
        [LocDisplayName("{=GlobalTournamentConfig_Betting_EnableBetting_Name}Enable Betting"),
         LocCategory("Betting", "{=GlobalTournamentConfig_Category_Betting}Betting"),
         LocDescription("{=GlobalTournamentConfig_Betting_EnableBetting_Desc}Enable or disable betting"),
         PropertyOrder(1), UsedImplicitly, Document]
        public bool EnableBetting { get; set; } = true;

        [LocDisplayName("{=GlobalTournamentConfig_Betting_BettingOnFinalOnly_Name}Betting On Final Only"),
         LocCategory("Betting", "{=GlobalTournamentConfig_Category_Betting}Betting"),
         LocDescription("{=GlobalTournamentConfig_Betting_BettingOnFinalOnly_Desc}Allow betting only on the final round"),
         PropertyOrder(2), UsedImplicitly, Document]
        public bool BettingOnFinalOnly { get; set; }
        #endregion
        #endregion

        #region IDocumentable
        public void GenerateDocumentation(IDocumentationGenerator generator)
        {
            //generator.Div("tournament-config", () =>
            //{
            //    generator.H1("{=AkDCrLgg}Tournament Config".Translate());
            //    DocumentationHelpers.AutoDocument(generator, this);
            //});
        }
        #endregion
    }

    #region SkillDebuffDef
    public class SkillDebuffDef : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        [LocDisplayName("{=GlobalTournamentConfig_SkillDebuffDef_Skill_Name}Skill"),
         LocDescription("{=GlobalTournamentConfig_SkillDebuffDef_Skill_Desc}Skill or skill group to modify (all skills in a group will be modified)"),
         PropertyOrder(1), UsedImplicitly, Document]
        public SkillsEnum Skill { get; set; } = SkillsEnum.All;

        [LocDisplayName("{=GlobalTournamentConfig_SkillDebuffDef_SkillReductionPercentPerWin_Name}Skill Reduction Percent Per Win"),
         LocDescription("{=GlobalTournamentConfig_SkillDebuffDef_SkillReductionPercentPerWin_Desc}Reduction to the skill per win (in %). See https://www.desmos.com/calculator/ajydvitcer for visualization of how skill will be modified."),
         PropertyOrder(2), UIRangeAttribute(0, 50, 0.5f),
         Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)), UsedImplicitly, Document]
        public float SkillReductionPercentPerWin { get; set; } = 3.2f;

        [LocDisplayName("{=GlobalTournamentConfig_SkillDebuffDef_FloorPercent_Name}Floor Percent"),
         LocDescription("{=GlobalTournamentConfig_SkillDebuffDef_FloorPercent_Desc}The lower limit (in %) that the skill(s) can be reduced to."),
         PropertyOrder(2), UIRangeAttribute(0, 100, 0.5f),
         Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)), UsedImplicitly, Document]
        public float FloorPercent { get; set; } = 65f;

        [LocDisplayName("{=GlobalTournamentConfig_SkillDebuffDef_Example_Name}Example"),
         LocDescription("{=GlobalTournamentConfig_SkillDebuffDef_Example_Desc}Shows the % reduction of the skill over 20 tournaments"),
         PropertyOrder(3), ReadOnly(true), YamlIgnore, UsedImplicitly]
        public string Example => string.Join(", ",
            Enumerable.Range(0, 20)
                .Select(i => $"{i}: {100 * SkillModifier(i):0}%"));

        public float SkillModifier(int wins)
        {
            return (float)(FloorPercent + (100 - FloorPercent) * Math.Pow(1f - SkillReductionPercentPerWin / 100f, wins * wins)) / 100f;
        }

        public SkillModifierDef ToModifier(int wins)
        {
            return new SkillModifierDef
            {
                Skill = Skill,
                ModifierPercent = SkillModifier(wins) * 100,
            };
        }

        public override string ToString()
        {
            return "{=GlobalTournamentConfig_SkillDebuffDef_Skill_Name}Skill".Translate() +
                   $": {Skill}, " +
                   "{=GlobalTournamentConfig_SkillDebuffDef_SkillReductionPercentPerWin_Name}Skill Reduction Percent Per Win".Translate() +
                   $": {SkillReductionPercentPerWin}%, " +
                   "{=GlobalTournamentConfig_SkillDebuffDef_FloorPercent_Name}Floor Percent".Translate() +
                   $": {FloorPercent}%";
        }
    }
    #endregion
}