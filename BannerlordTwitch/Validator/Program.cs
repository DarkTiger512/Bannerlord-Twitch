using System;
using System.Linq;
using System.Reflection;

internal class Program
{
    private static int Main(string[] args)
    {
        const string DllPath = @"C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.CampaignSystem.dll";

        var failures = new System.Collections.Generic.List<string>();
        Assembly asm;

        try
        {
            asm = Assembly.LoadFrom(DllPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FATAL: could not load assembly: {ex.Message}");
            return 1;
        }

        Type Req(string typeName)
        {
            var t = asm.GetType(typeName, throwOnError: false);
            if (t == null) failures.Add($"TYPE NOT FOUND: {typeName}");
            return t;
        }

        void ReqMethod(string typeName, string methodName, BindingFlags flags, Type[] paramTypes = null)
        {
            var t = Req(typeName);
            if (t == null) return;

            MethodInfo m = paramTypes != null
                ? t.GetMethod(methodName, flags, null, paramTypes, null)
                : t.GetMethod(methodName, flags);

            if (m == null)
                failures.Add($"METHOD NOT FOUND: {typeName}.{methodName}" +
                              (paramTypes != null ? $"({string.Join(",", paramTypes.Select(p => p.Name))})" : " (any overload)"));
        }

        void ReqCtor(string typeName, Type[] paramTypes)
        {
            var t = Req(typeName);
            if (t == null) return;

            var c = t.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                                      null, paramTypes, null);
            if (c == null)
                failures.Add($"CTOR NOT FOUND: {typeName}({string.Join(",", paramTypes.Select(p => p.Name))})");
        }

        void ReqProperty(string typeName, string propName, BindingFlags flags)
        {
            var t = Req(typeName);
            if (t == null) return;
            if (t.GetProperty(propName, flags) == null)
                failures.Add($"PROPERTY NOT FOUND: {typeName}.{propName}");
        }

        const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        // --- FactionDiscontinuationCampaignBehavior ---
        ReqMethod("TaleWorlds.CampaignSystem.CampaignBehaviors.FactionDiscontinuationCampaignBehavior",
            "FinalizeMapEvents", Instance);
        ReqMethod("TaleWorlds.CampaignSystem.CampaignBehaviors.FactionDiscontinuationCampaignBehavior",
            "DiscontinueClan", Instance);
        ReqMethod("TaleWorlds.CampaignSystem.CampaignBehaviors.FactionDiscontinuationCampaignBehavior",
            "CanClanBeDiscontinued", Instance);
        ReqMethod("TaleWorlds.CampaignSystem.CampaignBehaviors.FactionDiscontinuationCampaignBehavior",
            "DiscontinueKingdom", Instance);

        // --- ChangeKingdomAction ---
        ReqMethod("TaleWorlds.CampaignSystem.Actions.ChangeKingdomAction",
            "ApplyByJoinToKingdom", Static);
        ReqMethod("TaleWorlds.CampaignSystem.Actions.ChangeKingdomAction",
            "ApplyByJoinToKingdomByDefection", Static);
        ReqMethod("TaleWorlds.CampaignSystem.Actions.ChangeKingdomAction",
            "ApplyByLeaveKingdom", Static);
        ReqMethod("TaleWorlds.CampaignSystem.Actions.ChangeKingdomAction",
            "ApplyByLeaveWithRebellionAgainstKingdom", Static);
        ReqMethod("TaleWorlds.CampaignSystem.Actions.ChangeKingdomAction",
            "ApplyByLeaveByKingdomDestruction", Static);

        // --- DeclareWarDecision / ExpelClanFromKingdomDecision ctors ---
        ReqCtor("TaleWorlds.CampaignSystem.Election.DeclareWarDecision",
            new[] { Req("TaleWorlds.CampaignSystem.Clan"), Req("TaleWorlds.CampaignSystem.IFaction") });
        ReqCtor("TaleWorlds.CampaignSystem.Election.ExpelClanFromKingdomDecision",
            new[] { Req("TaleWorlds.CampaignSystem.Clan"), Req("TaleWorlds.CampaignSystem.Clan") });

        // --- KingdomDecisionProposalBehavior ---
        ReqMethod("TaleWorlds.CampaignSystem.CampaignBehaviors.KingdomDecisionProposalBehavior",
            "ConsiderWar", Instance);

        // --- Clan ---
        ReqMethod("TaleWorlds.CampaignSystem.Clan",
            "UpdateBannerColorsAccordingToKingdom", Instance);

        // --- DefaultMarriageModel ---
        ReqMethod("TaleWorlds.CampaignSystem.GameComponents.DefaultMarriageModel",
            "GetClanAfterMarriage", Instance);
        ReqMethod("TaleWorlds.CampaignSystem.GameComponents.DefaultMarriageModel",
            "IsSuitableForMarriage", Instance);

        // --- KillCharacterAction ---
        ReqMethod("TaleWorlds.CampaignSystem.Actions.KillCharacterAction",
            "ApplyInLabor", Static);
        ReqMethod("TaleWorlds.CampaignSystem.Actions.KillCharacterAction",
            "ApplyInternal", Static);

        // --- Food / Village ---
        ReqProperty("TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementFoodModel",
            "FoodStocksUpperLimit", Instance);
        ReqMethod("TaleWorlds.CampaignSystem.Settlements.Village",
            "GetHearthLevel", Instance);

        // --- MakePeaceAction ---
        ReqMethod("TaleWorlds.CampaignSystem.Actions.MakePeaceAction",
            "ApplyInternal", Static);

        // --- Army ---
        ReqMethod("TaleWorlds.CampaignSystem.Army", "CheckArmyDispersion", Instance);

        // --- Town ---
        ReqMethod("TaleWorlds.CampaignSystem.Settlements.Town",
            "GetDefenderParties", Instance);

        // --- MakeHeroFugitiveAction ---
        ReqMethod("TaleWorlds.CampaignSystem.Actions.MakeHeroFugitiveAction",
            "Apply", Static);

        // --- Kingdom ---
        ReqMethod("TaleWorlds.CampaignSystem.Kingdom",
            "CreateArmy", Instance);

        // --- AiPartyThinkBehavior ---
        ReqMethod("TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors.AiPartyThinkBehavior",
            "PartyHourlyAiTick", Instance);

        if (failures.Count == 0)
        {
            Console.WriteLine($"OK: all checked members found in {asm.GetName()}.");
            return 0;
        }

        Console.WriteLine($"FAILED: {failures.Count} member(s) missing or signature mismatch:");
        foreach (var f in failures) Console.WriteLine("  - " + f);

        Console.WriteLine();
        Console.WriteLine("--- Fuzzy discovery (searching whole assembly for likely renames) ---");

        void FuzzyFindType(string shortName)
        {
            var matches = asm.GetTypes().Where(t => t.Name == shortName || t.Name.Contains(shortName)).ToList();
            Console.WriteLine($"Types matching \"{shortName}\":");
            foreach (var t in matches) Console.WriteLine($"    {t.FullName}");
            if (matches.Count == 0) Console.WriteLine("    (none found — name likely changed substantially)");
        }

        void FuzzyFindMember(string typeName, string memberSubstring)
        {
            var t = asm.GetType(typeName, throwOnError: false);
            if (t == null) { Console.WriteLine($"  (can't search members — type {typeName} not found)"); return; }

            var allMembers = t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic |
                                           BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            var matches = allMembers
                .Where(m => m.Name.IndexOf(memberSubstring, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            Console.WriteLine($"Members on {typeName} containing \"{memberSubstring}\":");
            foreach (var m in matches) Console.WriteLine($"    {m.MemberType}: {m}");
            if (matches.Count == 0) Console.WriteLine("    (none — try searching base/derived types, or a broader substring)");
        }

        // Broaden: check MapEvent without DeclaredOnly, and check MapEventSide too
        void FuzzyFindMemberBroad(string typeName, string substring)
        {
            var t = asm.GetType(typeName, throwOnError: false);
            if (t == null) { Console.WriteLine($"  (type {typeName} not found)"); return; }

            var members = t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic |
                                        BindingFlags.Instance | BindingFlags.Static); // no DeclaredOnly this time
            var matches = members.Where(m => m.Name.IndexOf(substring, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            Console.WriteLine($"Members on {typeName} (incl. inherited) containing \"{substring}\":");
            foreach (var m in matches) Console.WriteLine($"    {m.MemberType}: {m}");
            if (matches.Count == 0) Console.WriteLine("    (none)");
        }

        FuzzyFindType("Army");

        return 1;
    }
}