"""Build and qualify Classic/TOAM drafts from committed source exports (never deploys)."""
import argparse,concurrent.futures,hashlib,json,os,shutil,subprocess,zipfile,xml.etree.ElementTree as ET
from pathlib import Path
p=argparse.ArgumentParser()
p.add_argument('--classic-ref',default='DT/parity-classic');p.add_argument('--toam-ref',default='DT/parity-toam')
p.add_argument('--output',required=True);p.add_argument('--packages',required=True);p.add_argument('--harmony',required=True)
p.add_argument('--toam-logging',required=True,help='Directory containing representative TOAM Logging.Abstractions 5.x')
a=p.parse_args();out=Path(a.output).resolve();out.mkdir(parents=True,exist_ok=False);packages=Path(a.packages).resolve();harmony=Path(a.harmony).resolve()
ms=r'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe'
modules=['BannerlordTwitch','BLTAdoptAHero','BLTBuffet','BLTConfigure']
def git(*x):return subprocess.check_output(['git',*x]).decode().strip()
def run(name,base,args,limit=300):
 log=out/'logs'/f'{name}.log';log.parent.mkdir(exist_ok=True)
 with log.open('w',encoding='utf-8') as f:r=subprocess.run([str(x) for x in args],cwd=base,stdout=f,stderr=subprocess.STDOUT,timeout=limit)
 if r.returncode:raise RuntimeError(f'{name} failed; see {log}')
 print('PASS '+name,flush=True)
def build(variant,ref):
 sha=git('rev-parse',ref);base=out/('C' if variant=='Classic' else 'T');base.mkdir();archive=out/(variant+'-source.zip')
 subprocess.run(['git','archive','--format=zip','--output='+str(archive),sha],check=True)
 with zipfile.ZipFile(archive) as z:z.extractall(base)
 # Dependency files are copied locally so this script requires no junction privileges.
 prefix=chr(92)*2+'?'+chr(92) if os.name=='nt' else ''
 shutil.copytree(prefix+str(packages),prefix+str(base/'BannerlordTwitch/packages'),ignore=shutil.ignore_patterns('obj','.git','TwitchLib.3.5.3'))
 sol=base/'BannerlordTwitch'
 run(variant+'-build-all-modules',base,[ms,str(sol/'BannerlordTwitch.sln'),'/restore','/p:UseBannerlordReferenceAssemblies=true','/p:RestoreSources='+str(out),'/p:Configuration=Release','/p:SolutionDir='+str(sol)+os.sep,'/p:DeployToGame=false','/p:CreatePackage=false','/p:TAOM_HARMONY_DLL='+str(harmony),'/v:minimal','/nologo'])
 tests=[('policies','BannerlordTwitch/BLTAdoptAHero.Tests/BLTAdoptAHero.Tests.csproj'),('forge','tools/forge-tests/ForgeTests.csproj'),('heirs','tools/adopt-heir-tests/AdoptHeirTests.csproj')]
 for name,path in tests:run(variant+'-'+name,base,['dotnet','run','--project',path,'-p:RestoreSources='+str(out),'-p:NuGetAudit=false'])
 for name in ['unequip','training-refund','empty-equipment','automatic-retinue','kingdom-decision-queue','passive-diagnostics']:
  args=['python','tools/test-'+name+'.py',out/(variant+'-'+name)]
  if name in ['kingdom-decision-queue','passive-diagnostics']:args.append(harmony)
  run(variant+'-'+name,base,args)
 built=sol/'build/Release'
 run(variant+'-twitch',base,['python','tools/test-twitch-startup.py',out/(variant+'-twitch'),built/'BannerlordTwitch/bin/Win64_Shipping_Client',a.toam_logging if variant=='TOAM' else built/'BannerlordTwitch/bin/Win64_Shipping_Client'])
 run(variant+'-overlay',base,['node','--test','BannerlordTwitch/BannerlordTwitch/Twitch/RandomOverlay/security.test.js'])
 if variant=='TOAM':
  run('TOAM-equipment',base,['python','tools/test-taom-equipment.py',out/'TOAM-equipment'])
  for enabled in ['false','true']:
   run('TOAM-preload-'+enabled,base,['dotnet','run','--project','tools/preload-wait-tests/PreloadWaitTests.csproj','-p:RestoreSources='+str(out),'-p:WithTaom='+enabled])
 # Inspect actual CLR identities without loading game dependencies.
 inspector=out/(variant+'-inspect.cs')
 inspector.write_text('using System; using System.Reflection; class Inspect { static void Main(string[] a) { foreach(var p in a) { var x=Assembly.ReflectionOnlyLoadFrom(p); if(x.GetName().Version.ToString(3)!="5.5.21") throw new Exception("assembly version"); foreach(var r in x.GetReferencedAssemblies()) if(r.Name.StartsWith("NavalDLC")) throw new Exception("mandatory NavalDLC reference"); Console.WriteLine(x.GetName()); } } }',encoding='utf-8')
 exe=out/(variant+'-inspect.exe')
 run(variant+'-inspector-build',base,[str(Path(ms).parent/'Roslyn/csc.exe'),'/nologo','/out:'+str(exe),inspector])
 run(variant+'-assembly-identities',base,[exe,*[built/m/'bin/Win64_Shipping_Client'/(m+'.dll') for m in modules]])
 stage=out/(variant+'-stage');stage.mkdir()
 for m in modules:
  shutil.copytree(built/m,stage/m,ignore=shutil.ignore_patterns('*.pdb','*.user','*Auth*.yaml'))
  manifest=ET.parse(stage/m/'SubModule.xml');assert manifest.find('./Version').get('value')=='v5.5.21'
  assert manifest.find('./Id').get('value')==m
  required={x.get('Id') for x in manifest.findall('./DependedModules/DependedModule')}
  provider='TAOM.Dependencies' if variant=='TOAM' else 'Bannerlord.Harmony'
  assert provider in required and 'NavalDLC' not in required
  assert manifest.find('./DependedModuleMetadatas/DependedModuleMetadata[@id="NavalDLC"]').get('optional')=='true'
  assert (stage/m/'bin/Win64_Shipping_Client'/f'{m}.dll').exists()
 assert (stage/'BannerlordTwitch/bin/Win64_Shipping_Client/BLT.Diagnostics.exe').exists()
 for f in stage.rglob('*'):
  if not f.is_file():continue
  assert not f.name.lower().startswith(('navaldlc','0harmony'))
  assert not (f.suffix.lower() in ['.pdb','.pem','.key','.pfx','.user'] or f.name.lower().startswith('.env') or 'auth' in f.stem.lower() and f.suffix.lower() in ['.yaml','.yml'])
 metadata={'variant':variant,'version':'5.5.21','bannerlordVersion':'1.4.8','sourceCommit':sha,'status':'draft','liveCampaignValidation':'pending','harmonyCompileSha256':hashlib.sha256(harmony.read_bytes()).hexdigest(),'optionalDlcRuntime':'not tested: complete game/DLC runtime unavailable'}
 (stage/'release-metadata.json').write_text(json.dumps(metadata,indent=2),encoding='utf-8')
 for name in ['RELEASE-5.5.21.md','CAMPAIGN-5.5.21-CHECKLIST.md','PASSIVE-DIAGNOSTICS.md','PARITY-5.5.21.md']:
  shutil.copy2(base/'docs'/name,stage/name)
 package=out/f'BLT-5.5.21-{variant}-{sha[:8]}-DRAFT.zip'
 with zipfile.ZipFile(package,'w',zipfile.ZIP_DEFLATED) as z:
  for f in stage.rglob('*'):
   if f.is_file():z.write(f,f.relative_to(stage).as_posix())
 extracted=out/(variant+'-verified')
 with zipfile.ZipFile(package) as z:assert z.testzip() is None;z.extractall(extracted)
 for f in stage.rglob('*'):
  if f.is_file():assert hashlib.sha256(f.read_bytes()).digest()==hashlib.sha256((extracted/f.relative_to(stage)).read_bytes()).digest()
 metadata['package']=package.name;metadata['sha256']=hashlib.sha256(package.read_bytes()).hexdigest()
 return metadata
try:
 run('parity',Path.cwd(),['python','tools/verify-classic-toam-parity.py',a.classic_ref,a.toam_ref])
 for name in ['forge','adopt-heir']:
  run(name+'-parity',Path.cwd(),['node','tools/verify-'+name+'-parity.mjs',a.classic_ref,a.toam_ref])
 with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
  records=list(pool.map(lambda x:build(*x),[('Classic',a.classic_ref),('TOAM',a.toam_ref)]))
 (out/'release-manifest.json').write_text(json.dumps({'packages':records,'releaseGate':'DRAFT: Classic and TOAM live campaign qualification pending'},indent=2),encoding='utf-8')
 (out/'SHA256SUMS.txt').write_text(''.join(r['sha256']+'  '+r['package']+'\n' for r in records),encoding='utf-8')
 (out/'VALIDATION.md').write_text('# Draft validation\n\nAutomated builds and listed test suites passed for both committed source exports. ZIP integrity and all extracted hashes match. See logs and release-manifest.json.\n\nPending: live campaigns for both variants, real TOAM dependency/module loading, optional-DLC runtime binding and naval mission play. No installation, publication or cross-variant save certification performed.\n',encoding='utf-8')
except Exception as e:
 (out/'VALIDATION.md').write_text('# Incomplete draft validation\n\n'+str(e)+'\nDo not distribute incomplete artifacts.',encoding='utf-8');raise
