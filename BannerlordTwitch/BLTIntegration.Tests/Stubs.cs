using BannerlordTwitch.Integration;
namespace BannerlordTwitch {
 public class AuthSettings {
  public bool IntegrationConfigured => false;
  public string IntegrationServiceUrl {get;set;}="https://example.invalid";
  public string IntegrationCredential {get;set;}
  public string IntegrationPairingCode {get;set;}
  public string IntegrationChannelId {get;set;}
  public string IntegrationInstallationId {get;set;}
  public string ClientID {get;set;}
  public string AccessToken {get;set;}
  public static void Save(AuthSettings _){}
 }
 public class Command { public string Name {get;set;} public string Handler {get;set;} public string Help {get;set;} public bool ModeratorOnly {get;set;} public bool HideHelp {get;set;} public bool Enabled {get;set;} public object HandlerConfig {get;set;} }
 public static class Settings { public static bool GameStarted {get;set;} }
}
namespace BannerlordTwitch.Util {
 public static class MainThreadSync {public static readonly Queue<Action> Queue=new();public static void Post(Action action)=>Queue.Enqueue(action);public static void Drain(){while(Queue.TryDequeue(out var action))action();}}
 public static class Log {public static void Info(string s)=>Console.WriteLine(s);public static void Error(string s)=>Console.WriteLine(s);public static void LogFeedSystem(string s){} }
}
namespace BannerlordTwitch.Integration {
 public class IntegrationActionCatalog {public static IntegrationActionCatalog Load()=>new();public string ManifestJson=>"{}"; public bool TryGet(string id,out IntegrationActionDefinition d){d=null;return false;}public string BuildLegacyArguments(IntegrationActionDefinition d, Dictionary<string,System.Text.Json.JsonElement> args)=>"";}
}
