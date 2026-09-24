// Uses real native clan caches and the production postfix, with only adoption
// detection substituted. This is not a full engine save/load integration test.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using BLTAdoptAHero.Util;

namespace BLTAdoptAHero
{
    public static class HeroExtensions
    {
        internal static readonly HashSet<Hero> Adopted = new HashSet<Hero>();
        public static bool IsAdopted(this Hero hero) { return Adopted.Contains(hero); }
    }
}

internal static class ClanMembershipTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int checks;

    private static void Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
        {
            var name = new AssemblyName(e.Name).Name;
            var path = name == "0Harmony" ? args[1] : Path.Combine(args[0], name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        Run();
    }

    private static T Empty<T>() { return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
    private static void Set(object obj, string field, object value)
    {
        obj.GetType().GetField(field, Flags).SetValue(obj, value);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
    private static void Reset(Clan clan)
    {
        typeof(Clan).GetMethod("InitMembers", Flags).Invoke(clan, null);
    }
    private static Clan NewClan(string id)
    {
        var clan = Empty<Clan>();
        clan.StringId = id;
        Reset(clan);
        return clan;
    }
    private static Hero NewHero(Clan clan, Occupation occupation, bool adopted)
    {
        var hero = Empty<Hero>();
        Set(hero, "_characterObject", Empty<CharacterObject>());
        Set(hero, "_clan", clan);
        Set(hero, "<Occupation>k__BackingField", occupation);
        if (adopted) BLTAdoptAHero.HeroExtensions.Adopted.Add(hero);
        return hero;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run()
    {
        var harmony = new HarmonyLib.Harmony("blt.clan-membership.regression");
        harmony.PatchAll(typeof(ViewerClanMembershipPatch).Assembly);
        var targetMethod = typeof(Hero).GetMethod("PreAfterLoad", Flags);
        Check(HarmonyLib.Harmony.GetPatchInfo(targetMethod).Postfixes.Any(p => p.owner == harmony.Id),
            "Production postfix registers against installed Hero.PreAfterLoad.");
        foreach (var id in new[] { "player", "npc" })
        {
            var clan = NewClan(id);
            var hero = NewHero(clan, Occupation.Wanderer, true);
            // Match a saved non-noble's state immediately after native cache reset.
            Check(hero.Clan == clan && !clan.Heroes.Contains(hero), "Reproduce missing roster with retained clan.");
            for (int reload = 0; reload < 3; reload++)
            {
                Reset(clan);
                ViewerClanMembershipPatch.Postfix(hero);
                ViewerClanMembershipPatch.Postfix(hero);
                Check(clan.Heroes.Count == 1 && clan.AliveLords.Count == 1, "Restore once across repeated loads/callbacks.");
                Check(hero.Clan == clan && hero.Occupation == Occupation.Wanderer && hero.CompanionOf == null,
                    "Preserve membership and unpaid title; do not turn viewers into companions.");
                Check(hero.PartyBelongedTo == null, "Do not place the hero in a party.");
            }
            Set(hero, "_clan", null); // A save made before joining, or after leaving.
            Reset(clan);
            ViewerClanMembershipPatch.Postfix(hero);
            Check(clan.Heroes.Count == 0 && hero.Clan == null, "Do not resurrect membership after leave.");

            var other = NewClan(id + "-other");
            Set(hero, "_clan", other);
            ViewerClanMembershipPatch.Postfix(hero);
            Check(clan.Heroes.Count == 0 && other.Heroes.Contains(hero), "Respect changed clan in loaded save.");

            Set(hero, "<Occupation>k__BackingField", Occupation.Lord);
            ViewerClanMembershipPatch.Postfix(hero);
            Check(other.Heroes.Count == 1, "Buying title does not duplicate existing member.");
            Reset(other);
            typeof(Clan).GetMethod("OnLordAdded", Flags).Invoke(other, new object[] { hero });
            ViewerClanMembershipPatch.Postfix(hero);
            Check(other.Heroes.Count == 1, "Native noble reconstruction stays unchanged.");
        }

        var target = NewClan("target");
        ViewerClanMembershipPatch.Postfix(NewHero(target, Occupation.Wanderer, false));
        Check(target.Heroes.Count == 0, "Do not alter unadopted NPCs.");
        var companion = NewHero(target, Occupation.Wanderer, true);
        var companionClan = NewClan("companion-clan");
        Set(companion, "_companionOf", companionClan);
        ViewerClanMembershipPatch.Postfix(companion);
        Check(target.Heroes.Count == 0 && companionClan.Heroes.Count == 0, "Leave companion reconstruction to native code.");
        var neutral = NewClan("neutral");
        ViewerClanMembershipPatch.Postfix(NewHero(neutral, Occupation.Wanderer, true));
        Check(neutral.Heroes.Count == 0, "Do not populate neutral clan.");

        var kingdom = Empty<Kingdom>();
        Set(kingdom, "_heroesCache", new TaleWorlds.Library.MBList<Hero>());
        Set(target, "_kingdom", kingdom);
        var member = NewHero(target, Occupation.Wanderer, true);
        ViewerClanMembershipPatch.Postfix(member);
        ViewerClanMembershipPatch.Postfix(member);
        Check(kingdom.Heroes.Count == 1 && kingdom.Heroes.Contains(member), "Restore kingdom membership once too.");

        var deadClan = NewClan("dead-member");
        var dead = NewHero(deadClan, Occupation.Wanderer, true);
        typeof(Hero).GetFields(Flags).Single(f => f.FieldType == typeof(Hero.CharacterStates))
            .SetValue(dead, Hero.CharacterStates.Dead);
        ViewerClanMembershipPatch.Postfix(dead);
        ViewerClanMembershipPatch.Postfix(dead);
        Check(deadClan.Heroes.Count == 1 && deadClan.DeadLords.Count == 1 && deadClan.AliveLords.Count == 0,
            "Restore deceased members to the dead cache, never resurrect them.");
        Console.WriteLine("Passed " + checks + " native clan roster regression checks.");
    }
}
