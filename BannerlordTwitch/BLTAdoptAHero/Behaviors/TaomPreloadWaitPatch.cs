using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Util;
using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace BLTAdoptAHero.Behaviors
{
    // Restore the 5.5.10 diagnostics build's guard for TAOM resource readiness stalls.
    [HarmonyPatch]
    internal static class TaomPreloadWaitPatch
    {
        private static MethodInfo target;
        private static FieldInfo meshesField, physicsField;

        [HarmonyPrepare]
        private static bool Prepare()
        {
            if (AccessTools.TypeByName("TAOM.SubModule") == null) return false;
            var type = AccessTools.TypeByName("TaleWorlds.MountAndBlade.View.PreloadHelper");
            if (type == null) return false;
            target = AccessTools.Method(type, "WaitForMeshesToBeLoaded", Type.EmptyTypes);
            meshesField = AccessTools.Field(type, "_uniqueMetaMeshes");
            physicsField = AccessTools.Field(type, "_uniqueDynamicPhysicsShapeName");
            bool compatible = target != null && target.ReturnType == typeof(void)
                && meshesField?.FieldType == typeof(HashSet<(MetaMesh, bool, bool)>)
                && physicsField?.FieldType == typeof(HashSet<string>);
            Log.Info("PRELOAD_GUARD " + (compatible ? "compatible timeoutMs=15000" : "not installed: incompatible engine API"));
            return compatible;
        }

        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => target;

        [HarmonyPrefix]
        private static bool Prefix(object __instance)
        {
            if (Mission.Current == null) return true;
            var meshes = (HashSet<(MetaMesh, bool, bool)>)meshesField.GetValue(__instance);
            var physics = (HashSet<string>)physicsField.GetValue(__instance);
            // Native resource checks must remain on the mission thread.
            var entries = meshes.Select(m => (Mesh: m.Item1, Name: m.Item1.GetName())).ToArray();
            var shapes = physics.ToArray();
            var pending = new List<string>();
            int pendingCount = 0;
            bool ready = ResourcePreloadWait.Run(() =>
            {
                pending.Clear();
                pendingCount = 0;
                foreach (var entry in entries)
                {
                    if (entry.Mesh.CheckResources() != 0)
                    {
                        pendingCount++;
                        if (pending.Count < 64) pending.Add("mesh=" + entry.Name);
                    }
                }
                foreach (var shape in shapes)
                {
                    if (PhysicsShape.GetFromResource(shape, true) == null)
                    {
                        pendingCount++;
                        if (pending.Count < 64) pending.Add("physics=" + shape);
                    }
                }
                return pendingCount;
            });
            Log.Info(ready
                ? "PRELOAD_READY meshes=" + entries.Length + " physics=" + shapes.Length
                : "PRELOAD_TIMEOUT TAOM readiness wait exceeded 15 seconds; continuing with "
                    + pendingCount + " unresolved resources. Assets may be missing. " + string.Join("; ", pending));
            return false;
        }
    }
}
