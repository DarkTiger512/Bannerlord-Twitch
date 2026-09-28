from pathlib import Path
import subprocess,sys,os
root=Path(__file__).resolve().parents[1]
out=Path(sys.argv[1]).resolve();out.mkdir(parents=True,exist_ok=True)
game=Path(sys.argv[2]) if len(sys.argv)>2 else Path(r'C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord')
blt=game/'Modules/BannerlordTwitch/bin/Win64_Shipping_Client'
taom=game/'Modules/TAOM.Dependencies/bin/Win64_Shipping_Client'
csc=Path(r'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe')
refs=['TwitchLib.Api','TwitchLib.Api.Core','TwitchLib.Api.Core.Interfaces','TwitchLib.Api.Helix','TwitchLib.Client','TwitchLib.Client.Enums','TwitchLib.Communication','TwitchLib.EventSub.Websockets','Microsoft.Extensions.Logging.Abstractions']
exe=out/'FactoryTest.exe'
subprocess.run([str(csc),'/nologo',f'/out:{exe}',r'/r:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\Facades\netstandard.dll',*[f'/r:{blt/(r+".dll")}' for r in refs],str(root/'tools/twitch-factory-fixture.cs'),str(root/'BannerlordTwitch/BannerlordTwitch/Twitch/TwitchLibraryFactory.cs')],check=True)
subprocess.run([str(exe),str(blt),str(taom)],check=True)
s=(root/'BannerlordTwitch/BannerlordTwitch/BLTModule.cs').read_text(encoding='utf-8-sig')
a=s.index('        public static bool RestartTwitchService()');b=s.index('\n    }',a)
method=s[a:b]
fixture='''using System;
class BLTModule {
 public static TwitchService TwitchService;
 METHOD
 static void Main(){
  foreach(var error in new Exception[]{new InvalidOperationException("missing configuration"),new MissingMethodException("dependency mismatch")}){
   TwitchService.Failure=error;
   if(RestartTwitchService()||TwitchService!=null||InformationManager.Pauses!=0)throw new Exception("Failure blocked startup");
  }
  TwitchService.Failure=null;
  if(!RestartTwitchService()||TwitchService==null||InformationManager.Messages!=2)throw new Exception("Retry failed");
  Console.WriteLine("PASS: actual startup method reports failures without a modal pause and allows retry");
 }
}
class TwitchService:IDisposable{public static Exception Failure;public TwitchService(){if(Failure!=null)throw Failure;}public void Dispose(){}}
static class Text{public static string Translate(this string s)=>s;}
static class InformationManager{public static int Messages,Pauses;public static void DisplayMessage(InformationMessage m){Messages++;}public static void ShowInquiry(object data,bool pause){Pauses++;}}
class InformationMessage{public InformationMessage(string text){}}
class InquiryData{public InquiryData(string a,string b,bool c,bool d,string e,string f,Action g,Action h){}}
static class Log{public static void Exception(string message,Exception ex){}}
'''.replace('METHOD',method)
cs=out/'StartupTest.cs';cs.write_text(fixture)
exe=out/'StartupTest.exe'
subprocess.run([str(csc),'/nologo',f'/out:{exe}',str(cs)],check=True)
subprocess.run([str(exe)],check=True)
