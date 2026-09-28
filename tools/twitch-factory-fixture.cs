using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BannerlordTwitch;
using TwitchLib.Api;
using TwitchLib.Api.Core.Internal;
using TwitchLib.Api.Core.HttpCallHandlers;
using TwitchLib.Client;
using TwitchLib.Client.Enums;
using TwitchLib.EventSub.Websockets;
class Test {
 static string blt,taom;
 static int Main(string[] args) {
  blt=args[0]; taom=args[1];
  AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{
   var n=new AssemblyName(e.Name);
   var dir=n.Name=="Microsoft.Extensions.Logging.Abstractions" && n.Version.Major<=5 ? taom : blt;
   var p=Path.Combine(dir,n.Name+".dll");return File.Exists(p)?Assembly.LoadFrom(p):null;
  };
  Assembly.LoadFrom(Path.Combine(taom,"Microsoft.Extensions.Logging.Abstractions.dll"));
  try { Run(); return 0; }catch(Exception e){Console.WriteLine(e);return 1;}
 }
 [MethodImpl(MethodImplOptions.NoInlining)] static void Original(){new TwitchAPI();}
 [MethodImpl(MethodImplOptions.NoInlining)] static void Run(){
  try {Original();throw new Exception("Expected old constructor to fail");}catch(MissingMethodException){Console.WriteLine("PASS: original constructor reproduces MissingMethodException with TAOM logging");}
  var handler=TwitchLibraryFactory.Create<TwitchHttpClientHandler>((object)null);
  var http=TwitchLibraryFactory.Create<TwitchHttpClient>((object)null);
  var api=TwitchLibraryFactory.Create<TwitchAPI>(null,null,null,http);
  var bot=TwitchLibraryFactory.Create<TwitchClient>(null,ClientProtocol.WebSocket,null);
  var events=TwitchLibraryFactory.Create<EventSubWebsocketClient>((object)null);
  if(api.Settings==null||api.Helix==null)throw new Exception("API not initialized");
  Console.WriteLine("PASS: API, HTTP handlers, chat client and EventSub constructors with mixed logging versions; no network connections made");
 }
}
