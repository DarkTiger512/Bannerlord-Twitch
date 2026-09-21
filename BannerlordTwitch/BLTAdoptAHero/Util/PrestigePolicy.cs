using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;

namespace BLTAdoptAHero.Util
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class PrestigeSettings
    {
        [Category("General"), DisplayName("Enable Prestige"), Description("Enable prestige progression and permanent perks.")]
        public bool Enabled { get; set; } = true;
        [Category("Requirements"), DisplayName("Base Kills"), Description("Personal campaign battle kills required for the first prestige.")]
        public int BaseKills { get; set; } = 500;
        [Category("Requirements"), DisplayName("Additional Kills Per Prestige"), Description("Extra kills required for each completed prestige.")]
        public int KillsPerPrestige { get; set; } = 250;
        [Category("Requirements"), DisplayName("Base Gold"), Description("Gold required for the first prestige.")]
        public int BaseGold { get; set; } = 5000000;
        [Category("Requirements"), DisplayName("Additional Gold Per Prestige"), Description("Extra gold required for each completed prestige.")]
        public int GoldPerPrestige { get; set; } = 2500000;
        [Category("Perks"), DisplayName("Ranks Per Perk"), Description("Maximum ranks for each perk, from 1 to 10.")]
        public int RankCap { get; set; } = 10;
        [Category("Perks"), DisplayName("Might Per Rank"), Description("Outgoing damage bonus per rank: 0.02 means 2%.")]
        public double MightPerRank { get; set; } = .02;
        [Category("Perks"), DisplayName("Resilience Per Rank"), Description("Incoming damage reduction per rank: 0.02 means 2%.")]
        public double ResiliencePerRank { get; set; } = .02;
        [Category("Perks"), DisplayName("Vitality Per Rank"), Description("Additional hit points per rank.")]
        public double VitalityPerRank { get; set; } = 5;
        [Category("Perks"), DisplayName("Fortune Per Rank"), Description("Battle gold bonus per rank: 0.02 means 2%.")]
        public double FortunePerRank { get; set; } = .02;
        [Category("Perks"), DisplayName("Insight Per Rank"), Description("BLT experience bonus per rank: 0.02 means 2%.")]
        public double InsightPerRank { get; set; } = .02;
        [Category("Reset"), DisplayName("Starting Level"), Description("Hero level after prestige.")]
        public int StartingLevel { get; set; } = 1;
        [Category("Reset"), DisplayName("Starting Attributes"), Description("Value of each attribute after prestige.")]
        public int StartingAttributes { get; set; } = 2;
        [Category("Reset"), DisplayName("Starting Combat Skills"), Description("Melee and ranged skill levels after prestige.")]
        public int StartingCombatSkills { get; set; } = 50;
        [Category("Reset"), DisplayName("Starting Movement Skills"), Description("Riding and athletics levels after prestige.")]
        public int StartingMovementSkills { get; set; } = 25;
        [Category("Reset"), DisplayName("Starting Other Skills"), Description("Other skill levels after prestige.")]
        public int StartingOtherSkills { get; set; }
        [Category("Reset"), DisplayName("Starting Equipment Tier"), Description("Equipment tier after prestige, from 0 to 6.")]
        public int StartingEquipmentTier { get; set; } = 1;
        [Category("Reset"), DisplayName("Starting Gold"), Description("Hero gold after prestige.")]
        public int StartingGold { get; set; } = 50000;

        public override string ToString() => "Prestige requirements, perks and reset";

        public bool IsValid() => BaseKills > 0 && KillsPerPrestige >= 0 && BaseGold > 0 && GoldPerPrestige >= 0
            && RankCap > 0 && RankCap <= 10 && StartingLevel >= 1 && StartingAttributes >= 1 && StartingAttributes <= 10
            && StartingCombatSkills >= 1 && StartingCombatSkills <= 300 && StartingMovementSkills >= 0 && StartingMovementSkills <= 300
            && StartingOtherSkills >= 0 && StartingOtherSkills <= 300 && StartingGold >= 0 && StartingEquipmentTier >= 0 && StartingEquipmentTier <= 6
            && new[] { MightPerRank, ResiliencePerRank, VitalityPerRank, FortunePerRank, InsightPerRank }
                .All(x => !double.IsNaN(x) && !double.IsInfinity(x) && x >= 0)
            && ResiliencePerRank * RankCap < 1;
    }

    public class PrestigeProgress
    {
        public int Count { get; set; }
        public Dictionary<string, int> Ranks { get; set; } = new();
        public int Rank(string perk) => Ranks != null && Ranks.TryGetValue(perk, out int rank) ? Math.Max(0, rank) : 0;
    }

    public static class PrestigePolicy
    {
        public static readonly string[] Perks = { "might", "resilience", "vitality", "fortune", "insight" };
        public static long RequiredKills(PrestigeSettings settings, int count) => settings.BaseKills + (long)settings.KillsPerPrestige * Math.Max(0, count);
        public static long RequiredGold(PrestigeSettings settings, int count) => settings.BaseGold + (long)settings.GoldPerPrestige * Math.Max(0, count);
        public static int Maximum(PrestigeSettings settings) => Math.Min(50, settings.RankCap * Perks.Length);
        public static bool CanChoose(PrestigeSettings settings, PrestigeProgress progress, string perk) =>
            Perks.Contains(perk) && progress.Count < Maximum(settings) && progress.Rank(perk) < settings.RankCap;
        public static int ScalePositive(int amount, params double[] factors)
        {
            if (amount <= 0) return amount;
            double result = amount;
            foreach (double factor in factors) result *= factor;
            return double.IsNaN(result) ? amount : (int)Math.Min(int.MaxValue, Math.Max(0, Math.Floor(result + 1e-7)));
        }
        public static bool QualifyingKill(bool realBattle, bool human, bool enemy, bool personal, bool defeated) =>
            realBattle && human && enemy && personal && defeated;
    }

    // Transient previews are deliberately never saved. Consuming first prevents replay after a failure.
    public class PrestigeConfirmation
    {
        private string perk;
        private int count;
        private DateTime expires;
        public void Preview(string selected, int prestige, DateTime now) { perk = selected; count = prestige; expires = now.AddSeconds(60); }
        public bool Consume(string selected, int prestige, DateTime now)
        {
            bool valid = perk == selected && count == prestige && now < expires;
            perk = null;
            return valid;
        }
    }
}
