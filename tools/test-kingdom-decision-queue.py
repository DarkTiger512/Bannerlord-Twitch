"""Exercise the production ballot queue patch using real Harmony and isolated game stubs.
Usage: python tools/test-kingdom-decision-queue.py OUTPUT_DIR HARMONY_DLL
"""
from pathlib import Path
import shutil
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
out = Path(sys.argv[1]).resolve()
out.mkdir(parents=True, exist_ok=True)
source = (root / 'BannerlordTwitch/BLTAdoptAHero/Behaviors/HarmonyPatches.cs').read_text(encoding='utf-8-sig')
start = source.index('        [HarmonyPatch(typeof(Kingdom), nameof(Kingdom.AddDecision),')
pos = source.index('{', source.index('internal static class BltKingdomDecisionQueuePatch', start))
end, depth = pos + 1, 1
while depth:
    depth += (source[end] == '{') - (source[end] == '}')
    end += 1
patch = source[start:end]
fixture = (root / 'tools/kingdom-decision-queue-fixture.cs.txt').read_text()
(out / 'Program.cs').write_text(fixture.replace('/* PRODUCTION PATCH */', patch))
shutil.copy2(sys.argv[2], out / '0Harmony.dll')
csc = Path('C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe')
subprocess.run([str(csc), '/nologo', '/optimize+', '/langversion:9',
                '/out:' + str(out / 'Test.exe'), '/reference:' + str(out / '0Harmony.dll'),
                str(out / 'Program.cs')], check=True)
subprocess.run([str(out / 'Test.exe')], cwd=out, check=True)
