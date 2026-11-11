"""Exercise the Shokuho missile-collision prefix with the pinned Harmony distribution.

Usage: python tools/test-shokuho-missile-collision.py OUTPUT_DIR HARMONY_DLL
"""
from pathlib import Path
import shutil
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
out = Path(sys.argv[1]).resolve()
out.mkdir(parents=True, exist_ok=True)
harmony = Path(sys.argv[2]).resolve()
for dll in harmony.parent.glob("*.dll"):
    shutil.copy2(dll, out / dll.name)

source = (root / "BannerlordTwitch/BLTAdoptAHero/Behaviors/BLTHeroPowersMissionBehavior.cs").read_text(encoding="utf-8-sig")
start = source.rindex("        [UsedImplicitly", 0, source.index("private static void HandleMissileCollisionReactionPrefix"))
end = source.index("\n        [UsedImplicitly", start + 1)
prefix = source[start:end].replace("UsedImplicitly, ", "")
(out / "Program.cs").write_text("""
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;

sealed class Agent { }
sealed class MissionObject { }
struct MatrixFrame { }
struct Vec3 { }
sealed class Mission
{
    public enum MissileCollisionReaction { Stick }
    public sealed class Missile { }
    private readonly Dictionary<int, Missile> _missiles;
    public BLTHeroPowersMissionBehavior Behavior;
    public int NativeCollisions;

    public Mission(Dictionary<int, Missile> missiles) => _missiles = missiles;
    public T GetMissionBehavior<T>() where T : class => Behavior as T;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void HandleMissileCollisionReaction(int missileIndex, MissileCollisionReaction collisionReaction,
        MatrixFrame attachLocalFrame, bool isAttachedFrameLocal, Agent attackerAgent, Agent attachedAgent,
        bool attachedToShield, sbyte attachedBoneIndex, MissionObject attachedMissionObject, Vec3 bounceBackVelocity,
        Vec3 bounceBackAngularVelocity, int forcedSpawnIndex)
    {
        NativeCollisions++;
    }

    public void Hit(int index) => HandleMissileCollisionReaction(
        index, MissileCollisionReaction.Stick, default, false, new Agent(), null, false, -1, null, default, default, -1);
}

[HarmonyPatch]
sealed class BLTHeroPowersMissionBehavior
{
    public int PowerCalls;
    public Mission.Missile LastMissile;
    private void MissileCollision(Mission.MissileCollisionReaction reaction, Agent attacker, Agent target,
        sbyte bone, bool shield, MatrixFrame frame, Mission.Missile missile)
    {
        PowerCalls++;
        LastMissile = missile;
    }
PREFIX
}

static class Test
{
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static void Main()
    {
        new Harmony("blt.shokuho.missile-regression").PatchAll(typeof(Test).Assembly);
        var missile = new Mission.Missile();
        var powers = new BLTHeroPowersMissionBehavior();
        var mission = new Mission(new Dictionary<int, Mission.Missile> { { 5, missile }, { 6, null } });
        mission.Behavior = powers;
        mission.Hit(5);
        Check(powers.PowerCalls == 1 && powers.LastMissile == missile, "Tracked missile must reach the correct behavior.");
        mission.Hit(6);
        mission.Hit(99);
        mission.Behavior = null;
        mission.Hit(5);
        var uninitialized = new Mission(null) { Behavior = powers };
        uninitialized.Hit(5);
        Check(powers.PowerCalls == 1, "Missing missiles and missing behaviors must skip BLT.");
        Check(mission.NativeCollisions == 4 && uninitialized.NativeCollisions == 1,
            "Native collision must run for all cases.");
        Console.WriteLine("PASS: real Harmony field binding, tracked missile dispatch and native collision continuation.");
    }
}
""".replace("PREFIX", prefix), encoding="utf-8")
csc = Path("C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe")
subprocess.run([str(csc), "/nologo", "/optimize+", "/langversion:9", f"/out:{out / 'Test.exe'}",
                f"/reference:{out / '0Harmony.dll'}", str(out / "Program.cs")], check=True)
subprocess.run([str(out / "Test.exe")], cwd=out, check=True)
