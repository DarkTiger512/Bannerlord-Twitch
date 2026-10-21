"""Compile production diagnostics and exercise them in harmless synthetic processes.
Usage: python tools/test-passive-diagnostics.py OUTPUT_DIR HARMONY_DLL
"""
from pathlib import Path
import shutil
import struct
import subprocess
import sys
import time
import zipfile

root = Path(__file__).resolve().parents[1]
out = Path(sys.argv[1]).resolve()
out.mkdir(parents=True, exist_ok=True)
csc = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
shutil.copy2(sys.argv[2], out / '0Harmony.dll')
subprocess.run([csc, '/nologo', '/target:winexe', '/platform:x64', '/langversion:9',
    '/r:System.IO.Compression.dll', '/r:System.IO.Compression.FileSystem.dll',
    '/out:' + str(out / 'BLT.Diagnostics.exe'), str(root / 'tools/diagnostic-watchdog.cs')], check=True)
(out / 'Fixture.cs').write_text((root / 'tools/passive-diagnostics-fixture.cs.txt').read_text())
subprocess.run([csc, '/nologo', '/platform:x64', '/langversion:9', '/r:' + str(out / '0Harmony.dll'),
    '/out:' + str(out / 'Fixture.exe'), str(out / 'Fixture.cs'),
    str(root / 'BannerlordTwitch/BannerlordTwitch/Util/FreezeDiagnostics.cs')], check=True)

def wait_for(path, seconds=20):
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        if path.exists():
            return path
        time.sleep(.25)
    raise AssertionError('Missing expected file: ' + str(path))

checks = 0
(out / 'Policy.cs').write_text('''using System; using System.Reflection;
class Policy { static void Main() {
var t=Assembly.LoadFrom("BLT.Diagnostics.exe").GetType("DiagnosticWatchdog");
var flags=BindingFlags.NonPublic|BindingFlags.Static;
var should=t.GetMethod("ShouldCapture",flags); var space=t.GetMethod("EnoughSpace",flags);
long now=DateTime.UtcNow.Ticks; long sec=TimeSpan.TicksPerSecond; long gb=1024L*1024*1024;
object[][] cases={new object[]{now-179*sec,now,0,false},new object[]{now-180*sec,now,0,true},
new object[]{0L,now,0,false},new object[]{now+sec,now,0,false},new object[]{now-500*sec,now,2,false}};
foreach(var c in cases) if((bool)should.Invoke(null,new object[]{c[0],c[1],c[2]})!=(bool)c[3]) throw new Exception("heartbeat policy");
object[][] budgets={new object[]{gb,50*gb,0L,true},new object[]{gb,2*gb,0L,false},new object[]{gb,50*gb,31*gb,false},new object[]{-1L,50*gb,0L,false}};
foreach(var c in budgets) if((bool)space.Invoke(null,new object[]{c[0],c[1],c[2]})!=(bool)c[3]) throw new Exception("storage policy");
Console.WriteLine("9 threshold, capture cap and storage policy checks passed");
}}''')
subprocess.run([csc, '/nologo', '/out:' + str(out / 'Policy.exe'), str(out / 'Policy.cs')], check=True)
subprocess.run([str(out / 'Policy.exe')], cwd=out, check=True)
checks += 9
for mode in ('hooks', 'unarmed'):
    host = subprocess.Popen([str(out / 'Fixture.exe'), str(out), mode])
    session = Path(wait_for(out / ('host-' + mode + '.txt')).read_text())
    assert host.wait(timeout=20) == 0
    time.sleep(2)
    assert not list(session.glob('*.dmp*')), 'Healthy/unarmed session triggered dump'
    checks += 1
    if mode == 'hooks':
        text = wait_for(session / 'exceptions.txt').read_text(encoding='utf-8-sig')
        assert 'Synthetic diagnostic test exception' in text and 'blt.passive-diagnostics' in text
        assert 'renderReady=0' in (session / 'heartbeat.txt').read_text()
        checks += 2

# An abrupt exit does not run the managed exit handler; preserve evidence without claiming a native cause.
host = subprocess.Popen([str(out / 'Fixture.exe'), str(out), 'abrupt'])
session = Path(wait_for(out / 'host-abrupt.txt').read_text())
wait_for(session / 'watcher.log')
host.terminate()
host.wait(timeout=10)
wait_for(session / 'BLT-incident.zip')
assert not (session / 'exit.txt').exists()
assert not list(session.glob('*.dmp*'))
checks += 2

if '--quick' in sys.argv[3:]:
    print(f'{checks} quick checks passed: hooks preserve exceptions, healthy/startup runs, abrupt-exit bundle and capture/storage limits')
    sys.exit(0)

host = subprocess.Popen([str(out / 'Fixture.exe'), str(out), 'stalled'])
session = Path(wait_for(out / 'host-stalled.txt').read_text())
first = wait_for(session / 'capture1.dmp', 35)
second = wait_for(session / 'capture2.dmp', 55)
for path in (first, second):
    with path.open('rb') as f:
        header = f.read(32)
        assert header[:4] == b'MDMP'
        count, directory = struct.unpack_from('<II', header, 8)
        flags = struct.unpack_from('<Q', header, 24)[0]
        assert flags & 2, 'Full-memory flag absent'
        f.seek(directory)
        streams = [struct.unpack('<III', f.read(12))[0] for _ in range(count)]
        assert 9 in streams and 3 in streams and 4 in streams, 'Missing full-memory, thread or module stream'
        f.seek(0)
        assert 'BLT_FULL_MEMORY_DIAGNOSTIC_SENTINEL_20261021'.encode('utf-16le') in f.read()
    checks += 1
assert host.poll() is None, 'Watchdog terminated the target'
assert first.stat().st_mtime + 19 <= second.stat().st_mtime, 'Captures are not separated'
assert 'shaderSampleAgeSeconds=' in (session / 'watcher.log').read_text()
checks += 3
assert host.wait(timeout=90) == 0
wait_for(session / 'BLT-incident.zip')
with zipfile.ZipFile(session / 'BLT-incident.zip') as archive:
    assert archive.testzip() is None
    assert any(name.startswith('events-') for name in archive.namelist())
    assert 'incident-state-1.txt' in archive.namelist()
assert len(list(session.glob('capture*.dmp'))) == 2
checks += 2
print(f'{checks} integration checks passed: real Harmony, healthy/unarmed sessions, abrupt exit, policy limits, external watchdog, two full snapshot dumps, target survival and valid incident ZIP')
