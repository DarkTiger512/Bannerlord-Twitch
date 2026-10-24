"""Exercise production training/refund methods against an isolated campaign fixture.
Usage: python tools/test-training-refund.py OUTPUT_DIR [SOURCE_CHECKOUT]
"""
from pathlib import Path
import subprocess
import sys

root = Path(sys.argv[2]).resolve() if len(sys.argv) > 2 else Path(__file__).resolve().parents[1]
out = Path(sys.argv[1]).resolve()
out.mkdir(parents=True, exist_ok=True)

def method(source, signature):
    start = source.index(signature)
    pos = source.index('{', start)
    end, depth = pos + 1, 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]

base = root / 'BannerlordTwitch/BLTAdoptAHero'
campaign = (base / 'Behaviors/BLTAdoptAHeroCampaignBehavior.cs').read_text(encoding='utf-8-sig')
training = (base / 'Behaviors/TrainingBehavior.cs').read_text(encoding='utf-8-sig')
party = (base / 'Actions/PartyManagement.cs').read_text(encoding='utf-8-sig')
fixture = (Path(__file__).parent / 'training-refund-fixture.cs.txt').read_text()
fixture = fixture.replace('/* GOLD METHODS */', '\n'.join(method(campaign, s) for s in (
    '        public int ChangeHeroGold(', '        public int RefundHeroGold(')))
fixture = fixture.replace('/* TRAINING METHODS */', '\n'.join(method(training, s) for s in (
    '        public TrainingEntry GetEntry(', '        public void AddFund(',
    '        public int CancelFund(', '        public static int ComputeDailyBudget(')))
fixture = fixture.replace('/* COMMAND */', method(party, '        private static void HandleTrain('))
(out / 'Program.cs').write_text(fixture)
csc = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
subprocess.run([csc, '/nologo', '/langversion:9', '/out:' + str(out / 'Test.exe'), str(out / 'Program.cs')], check=True)
subprocess.run([str(out / 'Test.exe')], check=True)
