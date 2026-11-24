using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using BannerlordTwitch.Util;

namespace BLTAdoptAHero
{
    internal static class CharacterReloadCompat
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var ext = AccessTools.TypeByName(
                    "CharacterReload.VM.RelationshipManager.Extensions.RelationshipExtensions");
                var clanVm = AccessTools.TypeByName("CharacterReload.VM.ClanEditorVM");

                PatchMethod(harmony, ext, "ExecutePeace");   // Relationship Manager path
                PatchMethod(harmony, clanVm, "ExecutePeace"); // Clan page War/Peace buttons
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] CharacterReload compat failed: {ex}");
            }
        }

        private static void PatchMethod(Harmony harmony, Type type, string name)
        {
            if (type == null) return;
            var method = AccessTools.Method(type, name);
            if (method == null) return;

            harmony.Patch(method,
                prefix: new HarmonyMethod(typeof(CharacterReloadCompat), nameof(Prefix)),
                finalizer: new HarmonyMethod(typeof(CharacterReloadCompat), nameof(Finalizer)));
        }

        private static void Prefix(out bool __state)
        {
            __state = AdoptedHeroFlags._allowDiplomacyAction;
            AdoptedHeroFlags._allowDiplomacyAction = true;
        }

        private static void Finalizer(bool __state)
        {
            AdoptedHeroFlags._allowDiplomacyAction = __state;
        }
    }
}