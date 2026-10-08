using TaleWorlds.CampaignSystem;
namespace BannerlordTwitch.Util { public static class Log { public enum Sound { Horns2 } public static void ShowInformation(string s,object o=null,Sound sound=default) {} public static void Info(string s) {} } }
namespace BannerlordTwitch {
 public sealed class Command { public Guid ID; public string Handler, Name; public bool Enabled; public object HandlerConfig; }
}
namespace BLTAdoptAHero.Actions { public static class FamilyManagement { public static int BabyCommandLimit = 3; } }
namespace BLTAdoptAHero {
 public static class HeroExtensions { public static bool IsAdopted(this Hero h) => h.Adopted; }
 public class BLTAdoptAHeroCampaignBehavior {
  public static BLTAdoptAHeroCampaignBehavior Current = new();
  public Hero Active,Retired; public int ItemInheritanceCalls,GoldInheritanceCalls;
  public Hero GetAdoptedHero(string user)=>Active;
  public Hero GetRetiredHero(string user)=>Retired;
  public void InitAdoptedHero(Hero h,string user){h.Adopted=true;Active=h;h.Name=user;}
  public List<TaleWorlds.Core.EquipmentElement> InheritCustomItems(Hero h,int max){ItemInheritanceCalls++;return new(){new()};}
  public int InheritGold(Hero h,float fraction){GoldInheritanceCalls++;Gold[h]+=100;return 100;}
  public void SetEquipmentTier(Hero h,int tier){}
  public void SetEquipmentClass(Hero h,HeroClassDef cls){}
  public Dictionary<Hero,int> Gold = new(); public bool FailCharge; public HashSet<Hero> CreatedHeroes=new();
  public void SetIsCreatedHero(Hero h,bool value){if(value)CreatedHeroes.Add(h);else CreatedHeroes.Remove(h);}
  public int GetHeroGold(Hero h) => Gold.GetValueOrDefault(h);
  public void SetHeroGold(Hero h,int g) => Gold[h]=g;
  public int ChangeHeroGold(Hero h,int g) { Gold[h]=GetHeroGold(h)+g; if(FailCharge) throw new Exception("charge failure"); return Gold[h]; }
 }
}
namespace TaleWorlds.ObjectSystem { public struct MBGUID {} public class MBObjectBase {} }
namespace TaleWorlds.MountAndBlade { public class Mission { public static Mission Current; } }
namespace TaleWorlds.Core {
 public static class MBRandom { public static float RandomFloat = .3f; public static int RandomInt(int a,int b)=>a; }
 public class Equipment { public enum EquipmentType { Battle, Civilian } }
}
namespace TaleWorlds.CampaignSystem {
 public interface IDataStore { bool IsSaving {get;} bool IsLoading {get;} bool SyncData<T>(string key,ref T data); }
 public abstract class CampaignBehaviorBase { public abstract void RegisterEvents(); public abstract void SyncData(IDataStore store); }
 public class Event { public List<Action> Listeners = new(); public void AddNonSerializedListener(object o,Action a)=>Listeners.Add(a); public void Fire(){foreach(var a in Listeners)a();} }
 public class Event<T> { public List<Action<T>> Listeners=new(); public void AddNonSerializedListener(object o,Action<T> a)=>Listeners.Add(a); public void Fire(T t){foreach(var a in Listeners)a(t);} }
 public class Event<T,U> { public List<Action<T,U>> Listeners=new(); public void AddNonSerializedListener(object o,Action<T,U> a)=>Listeners.Add(a); public void Fire(T t,U u){foreach(var a in Listeners)a(t,u);} }
 public class Event<T,U,V,W> { public List<Action<T,U,V,W>> Listeners=new(); public void AddNonSerializedListener(object o,Action<T,U,V,W> a)=>Listeners.Add(a); public void Fire(T t,U u,V v,W w){foreach(var a in Listeners)a(t,u,v,w);} }
 public static class CampaignEvents {
  public static Event OnGameLoadFinishedEvent=new(); public static Event<Hero,bool> HeroCreated=new();
  public static Event<Hero,Hero,int,bool> HeroKilledEvent=new(); public static Event<Hero> HeroComesOfAgeEvent=new();
  public static Event<Hero,Hero> OnClanLeaderChangedEvent=new();
  public static void Reset(){OnGameLoadFinishedEvent=new();HeroCreated=new();HeroKilledEvent=new();HeroComesOfAgeEvent=new();OnClanLeaderChangedEvent=new();}
 }
 public struct CampaignTime { public float Years; public static CampaignTime YearsFromNow(float age)=>new(){Years=age}; }
 public class CharacterObject { public int Race; }
 public class Clan { public object Home=new(); public List<Hero> Heroes=new(); }
 public class Party { public object MapEvent; }
 public class Hero : TaleWorlds.ObjectSystem.MBObjectBase {
  public string Name="Child"; public string FirstName=>Name; public List<Hero> Siblings=new(); public HeroDeveloper HeroDeveloper=new(); public int GetSkillValue(object skill)=>1; public bool IsFemale, IsPregnant, IsPrisoner, IsDisabled, Adopted, IsClanLeader, IsActive;
  public bool IsAlive=true; public bool IsDead=>!IsAlive;
  public Clan Clan; public Hero Mother,Father,Spouse; public Party PartyBelongedTo;
  public List<Hero> Children=new(); public CharacterObject CharacterObject=new();
  public CampaignTime BirthDay=new(){Years=-25}; public float Age=>-BirthDay.Years;
  public object HomeSettlement=>Clan?.Home;
  public void SetBirthDay(CampaignTime t)=>BirthDay=t;
 }
 public class AgeModel { public int HeroComesOfAge=18; }
 public class PregnancyModel { public float DeliveringFemaleOffspringProbability=.5f; }
 public class CreationModel { public bool Missing; public CharacterObject GetCharacterTemplateForOffspring(Hero m,Hero f,bool female)=>Missing?null:new(); }
 public class EquipmentModel { public bool Missing; public object GetEquipmentForHeroComeOfAge(Hero h,TaleWorlds.Core.Equipment.EquipmentType type)=>Missing?null:new(); }
 public class Models { public AgeModel AgeModel=new(); public PregnancyModel PregnancyModel=new(); public CreationModel HeroCreationModel=new(); public EquipmentModel EquipmentSelectionModel=new(); }
 public class Campaign {
  public static Campaign Current=new(); public Models Models=new(); public List<object> Behaviors=new();
  public T GetCampaignBehavior<T>() where T:class=>Behaviors.OfType<T>().FirstOrDefault();
 }
 public static class HeroCreator {
  public static int Created; public static bool FailAfterCreate;
  public static Hero DeliverOffSpring(Hero m,Hero f,bool female){
   Created++;var h=new Hero{Mother=m,Father=f,IsFemale=female,Clan=f.Clan,BirthDay=new(){Years=0}};
   m.Children.Add(h);f.Children.Add(h);Campaign.Current.GetCampaignBehavior<CampaignBehaviors.AgingCampaignBehavior>().Add(h);
   CampaignEvents.HeroCreated.Fire(h,true);if(FailAfterCreate)throw new Exception("creation listener");return h;
  }
 }
 public class CampaignEventDispatcher {
  public static CampaignEventDispatcher Instance=new(); public int Births, Adults; public bool FailBirth,FailAdult;
  public void OnGivenBirth(Hero mother,List<Hero> children,int stillborn){Births++;if(FailBirth)throw new Exception("birth listener");}
  public void OnHeroComesOfAge(Hero h){Adults++;if(FailAdult)throw new Exception("adult listener");h.IsActive=true;CampaignEvents.HeroComesOfAgeEvent.Fire(h);}
 }
}
namespace TaleWorlds.CampaignSystem.CampaignBehaviors {
 public class AgingCampaignBehavior { private Dictionary<Hero,int> _heroesYoungerThanHeroComesOfAge=new(); public void Add(Hero h)=>_heroesYoungerThanHeroComesOfAge[h]=0; public bool Contains(Hero h)=>_heroesYoungerThanHeroComesOfAge.ContainsKey(h); }
}
namespace BannerlordTwitch {
 public class ReplyContext { public string UserName,Args=""; }
}
namespace BannerlordTwitch.Rewards {
 public interface ICommandHandler { Type HandlerConfigType{get;} void Execute(BannerlordTwitch.ReplyContext c,object config); }
 public interface IRewardHandler { Type RewardConfigType{get;} void Enqueue(BannerlordTwitch.ReplyContext c,object config); }
 public static class ActionManager { public static string Reply; public static bool Completed,Cancelled; public static void NotifyComplete(BannerlordTwitch.ReplyContext c,string s){Completed=true;Reply=s;} public static void NotifyCancelled(BannerlordTwitch.ReplyContext c,string s){Cancelled=true;Reply=s;} public static void SendReply(BannerlordTwitch.ReplyContext c,string s)=>Reply=s; }
}
namespace BannerlordTwitch.Localization {
 public class LocDisplayNameAttribute : Attribute { public LocDisplayNameAttribute(string s){} }
 public class LocDescriptionAttribute : Attribute { public LocDescriptionAttribute(string s){} }
 public static class Translations { public static string Translate(this string s,params (string,object)[] args)=>s; }
}
namespace BannerlordTwitch.Helpers {
 public interface IDocumentable { void GenerateDocumentation(IDocumentationGenerator g); }
 public interface IDocumentationGenerator { void P(string text); }
 public static class CampaignHelpers { public static object[] AllSkillObjects={new()}; public static bool IsEncyclopediaBookmarked(Hero h)=>false; public static void AddEncyclopediaBookmarkToItem(Hero h){}  }

}
namespace BannerlordTwitch.UI { public class DefaultCollectionEditor {} }
namespace BLTAdoptAHero.Annotations { public class UsedImplicitlyAttribute : Attribute {} }
namespace Xceed.Wpf.Toolkit.PropertyGrid.Attributes { public class PropertyOrderAttribute : Attribute { public PropertyOrderAttribute(int n){} } }
namespace YamlDotNet.Serialization { public class YamlIgnoreAttribute : Attribute {} }
namespace TaleWorlds.CampaignSystem.CharacterCreationContent { }
namespace TaleWorlds.CampaignSystem.Settlements { }
namespace TaleWorlds.Localization { }
namespace TaleWorlds.Library { public static class MBMath { public static int ClampInt(int v,int min,int max)=>Math.Clamp(v,min,max); } }
namespace TaleWorlds.Core { public struct EquipmentElement { public string GetModifiedItemName()=>"custom sword"; } }
namespace TaleWorlds.CampaignSystem {
 public class HeroDeveloper { public void ClearHero(){} public void SetInitialSkillLevel(object skill,int v){} public void InitializeHeroDeveloper(){} }
}
namespace TaleWorlds.CampaignSystem.Actions {
 public static class ChangeClanLeaderAction { public static Hero NewLeader; public static void ApplyWithSelectedNewLeader(Clan clan,Hero h){if(h==null||clan==null)throw new Exception("null leader");NewLeader=h;} }
}
namespace BLTAdoptAHero {
 public class RangeFloat { public RangeFloat(float min,float max){} public float RandomInRange()=>25; }
 public enum SkillsEnum {None,OneHanded}
 public class SkillRangeDef { public SkillsEnum Skill; public int MinLevel,MaxLevel; }
 public static class SkillGroup { public static object[] GetSkills(SkillsEnum s)=>new[]{new object()}; }
 public static class Naming { public const string Gold="gold"; }
 public class HeroClassDef {}
 public static class EquipHero { public static void RemoveAllEquipment(Hero h){} public static void UpgradeEquipment(Hero h,int tier,HeroClassDef cls,bool replaceSameTier,bool enforceTierCap=false){} }
 public static class RandomExtension { public static T SelectRandom<T>(this IEnumerable<T> values)=>values.FirstOrDefault(); }
}
