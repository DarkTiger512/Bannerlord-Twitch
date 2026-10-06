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
    // Both 28440 dumps place the game thread in this engine wait, after BLT's tick returned.
    // Keep preload requests intact; bound only the readiness polling in TAOM missions.
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
            FreezeDiagnostics.Mark("PRELOAD_GUARD " + (compatible ? "compatible timeoutMs=15000" : "not installed: incompatible engine API"));
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
            // Snapshot on the campaign/mission thread; never check native resources on workers.
            var entries = meshes.Select(m => (Mesh: m.Item1, Name: m.Item1.GetName())).ToArray();
            var shapes = physics.ToArray();
            var pending = new List<string>();
            int pendingCount = 0;
            using (FreezeDiagnostics.Trace("preload.wait meshes=" + entries.Length + " physics=" + shapes.Length))
            {
                bool ready = ResourcePreloadWait.Run(() =>
                {
                    pending.Clear();
                    pendingCount = 0;
                    foreach (var entry in entries)
                    {
                        FreezeDiagnostics.Phase("preload.mesh " + entry.Name);
                        if (entry.Mesh.CheckResources() != 0)
                        {
                            pendingCount++;
                            if (pending.Count < 64) pending.Add("mesh=" + entry.Name);
                        }
                    }
                    foreach (var shape in shapes)
                    {
                        FreezeDiagnostics.Phase("preload.physics " + shape);
                        if (PhysicsShape.GetFromResource(shape, true) == null)
                        {
                            pendingCount++;
                            if (pending.Count < 64) pending.Add("physics=" + shape);
                        }
                    }
                    return pendingCount;
                });
                if (!ready)
                {
                    var message = "TAOM preload readiness wait timed out after 15 seconds; continuing with "
                        + pendingCount + " unresolved resource entries. Assets may be missing. "
                        + string.Join("; ", pending);
                    FreezeDiagnostics.Mark("PRELOAD_TIMEOUT " + message);
                    Log.Info(message);
                }
                else FreezeDiagnostics.Mark("PRELOAD_READY meshes=" + entries.Length + " physics=" + shapes.Length);
            }
            FreezeDiagnostics.Phase("preload.wait-returned");
            return false;
        }
    }
}
