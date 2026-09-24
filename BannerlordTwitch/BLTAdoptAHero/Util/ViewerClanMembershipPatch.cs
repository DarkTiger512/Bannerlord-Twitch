using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace BLTAdoptAHero.Util
{
    // ClanManagement permits non-nobles to join while Buy Title is enabled.
    // Native clan assignment adds them to the lord/hero caches, but PreAfterLoad
    // only rebuilds those entries for lords (or separately registered companions).
    [HarmonyPatch(typeof(Hero), "PreAfterLoad")]
    internal static class ViewerClanMembershipPatch
    {
        private static readonly Action<Clan, Hero> AddLord =
            (Action<Clan, Hero>)Delegate.CreateDelegate(typeof(Action<Clan, Hero>),
                AccessTools.Method(typeof(Clan), "OnLordAdded", new[] { typeof(Hero) }));

        [HarmonyPostfix]
        internal static void Postfix(Hero __instance)
        {
            // Companions and nobles already follow native reconstruction. Never
            // give a hero a clan they did not have in the loaded save.
            if (__instance.CharacterObject.IsObsolete || !__instance.IsAdopted()
                || __instance.IsLord || __instance.CompanionOf != null)
                return;

            var clan = __instance.Clan;
            if (clan == null || clan.StringId == "neutral" || clan.Heroes.Contains(__instance))
                return;

            // Use the same callback as Hero.Clan's setter: this also restores
            // the appropriate alive/dead and kingdom caches without changing
            // occupation, companion status, parties, gold, or saved membership.
            AddLord(clan, __instance);
        }
    }
}
