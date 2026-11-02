"""Compare committed Classic/TOAM trees with exact, documented exceptions."""
import argparse,json,subprocess,zipfile,io
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('classic',nargs='?',default='DT/parity-classic');p.add_argument('toam',nargs='?',default='DT/parity-toam');a=p.parse_args()
def git(*args):return subprocess.check_output(['git',*args])
trees={ref:zipfile.ZipFile(io.BytesIO(git('-c','core.autocrlf=false','archive','--format=zip',ref))) for ref in [a.classic,a.toam]}
def files(ref):return {p for p in trees[ref].namelist() if not p.endswith('/')}
def raw(ref,path):return trees[ref].read(path)
def read(ref,path):return raw(ref,path).decode('utf-8-sig').replace('\r\n','\n')
exceptions=json.loads(read(a.classic,'tools/parity-exceptions.json'))
assert exceptions==json.loads(read(a.toam,'tools/parity-exceptions.json'))
x,y=files(a.classic),files(a.toam);diff=[]
for f in sorted(x|y):
 if f not in x or f not in y:diff.append(f);continue
 left,right=raw(a.classic,f),raw(a.toam,f)
 if left.replace(b'\r\n',b'\n').removeprefix(b'\xef\xbb\xbf') != right.replace(b'\r\n',b'\n').removeprefix(b'\xef\xbb\xbf'):diff.append(f)
assert set(diff)==set(exceptions), {'unlisted':sorted(set(diff)-set(exceptions)),'stale':sorted(set(exceptions)-set(diff))}
f='BannerlordTwitch/BLTAdoptAHero/Actions/EquipHero.cs'
def paid(s):return s[s.index('        protected override void ExecuteInternal('):s.index('        internal static void RemoveAllEquipment(')]
assert paid(read(a.classic,f))==paid(read(a.toam,f))
for ref in [a.classic,a.toam]:
 assert '<ModuleVersion>5.5.21</ModuleVersion>' in read(ref,'BannerlordTwitch/BLTProperties.targets')
 assert 'NativeForgeAdapter' not in read(ref,'BannerlordTwitch/BLTAdoptAHero/Util/ForgeAssets.cs')
 assert 'UnequipHero' in read(ref,'BannerlordTwitch/BannerlordTwitch/Settings/ForgeCommandDefaults.cs')
print('PASS: full-tree parity; '+str(len(diff))+' exact documented variant exceptions; shared equipment commands and release invariants')
