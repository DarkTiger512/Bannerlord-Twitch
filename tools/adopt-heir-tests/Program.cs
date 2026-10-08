using BLTAdoptAHero;
using BLTAdoptAHero.Actions;
using BLTAdoptAHero.Behaviors;
using BLTAdoptAHero.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.MountAndBlade;
using BannerlordTwitch;

static class Program
{
 static int assertions;
 static void Check(bool condition,string message){assertions++;if(!condition)throw new Exception(message);}
 static void Fails(Action action,string text){try{action();throw new Exception("Expected failure: "+text);}catch(InvalidOperationException e){Check(e.Message.Contains(text,StringComparison.OrdinalIgnoreCase),e.Message);}}
 static (Hero owner,Hero spouse,BLTHeirBehavior behavior) Setup(bool female=false)
 {
  CampaignEvents.Reset();Campaign.Current=new();CampaignEventDispatcher.Instance=new();HeroCreator.Created=0;HeroCreator.FailAfterCreate=false;Mission.Current=null;FamilyManagement.BabyCommandLimit=3;
  BLTAdoptAHeroCampaignBehavior.Current=new();
  var b=new BLTHeirBehavior();Campaign.Current.Behaviors.Add(b);Campaign.Current.Behaviors.Add(new AgingCampaignBehavior());b.RegisterEvents();
  var clan=new Clan();var h=new Hero{Adopted=true,IsFemale=female,Clan=clan,Name="Viewer",IsActive=true};
  var s=new Hero{IsFemale=!female,Clan=clan,Name="Spouse",IsActive=true};h.Spouse=s;s.Spouse=h;
  BLTAdoptAHeroCampaignBehavior.Current.SetHeroGold(h,1000);return(h,s,b);
 }
 static void Main()
 {
  foreach(bool female in new[]{false,true})
  {
   var(h,s,b)=Setup(female);Campaign.Current.Models.AgeModel.HeroComesOfAge=30;h.SetBirthDay(CampaignTime.YearsFromNow(-40));s.SetBirthDay(CampaignTime.YearsFromNow(-40));s.Adopted=true;
   AdoptHeirService.Execute(h,200);var child=b.GetValidHeir(h);
   Check(child!=null&&child.Age==30&&child.IsActive,"modded adulthood");
   Check(child.Mother==(female?h:s)&&child.Father==(female?s:h),"native parentage");
   Check(b.GetValidHeir(s)==null&&b._heirs.SetEquals(new[]{child}),"exclusive caller reservation");
   Check(BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(h)==800&&h.Adopted&&!child.Adopted,"future heir only, cost");
   Check(!Campaign.Current.GetCampaignBehavior<AgingCampaignBehavior>().Contains(child),"native aging removed");
   Check(CampaignEventDispatcher.Instance.Births==1&&CampaignEventDispatcher.Instance.Adults==1,"single native events");
   h.Spouse=null;AdoptHeirService.Execute(h,999999);Check(HeroCreator.Created==1&&BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(h)==800,"existing heir returned before spouse/gold checks");
   var saved=new Store(true);b.SyncData(saved);var loaded=new BLTHeirBehavior();loaded.SyncData(saved.Load());Check(loaded.GetValidHeir(h)==child,"heir identity survives JSON persistence");
   Check(loaded.IsUnavailableForAdoption(child),"reserved child unavailable for ordinary adoption");
   Check(loaded.TryReserve(s,child)==false,"double reservation rejected");loaded.RemoveReservation(h);Check(!loaded._heirs.Contains(child),"exact cleanup");
  }
  foreach(var scenario in new[]{"pregnant","limit","race","spouse","minor","prisoner","mission","battle","poor","negative","templates"})
  {
   var(h,s,b)=Setup();int cost=0;
   switch(scenario){case "pregnant":s.IsPregnant=true;break;case "limit":FamilyManagement.BabyCommandLimit=0;break;case "race":s.CharacterObject.Race=1;break;case "spouse":h.Spouse=null;break;case "minor":s.SetBirthDay(CampaignTime.YearsFromNow(-5));break;case "prisoner":h.IsPrisoner=true;break;case "mission":Mission.Current=new();break;case "battle":s.PartyBelongedTo=new(){MapEvent=new()};break;case "poor":cost=1001;break;case "negative":cost=-1;break;case "templates":Campaign.Current.Models.HeroCreationModel.Missing=true;break;}
   try{AdoptHeirService.Execute(h,cost);throw new Exception("Unexpected success: "+scenario);}catch(InvalidOperationException){}
   Check(HeroCreator.Created==0&&BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(h)==1000,"free preflight failure "+scenario);
  }
  {
   var(h,s,b)=Setup();Campaign.Current.Models.EquipmentSelectionModel.Missing=true;
   Fails(()=>AdoptHeirService.Execute(h,100),"equipment");var child=h.Children.Single();Check(child.Age==0,"failed equipment restores birthday");
   var saved=new Store(true);b.SyncData(saved);var loaded=new BLTHeirBehavior();loaded.SyncData(saved.Load());
   Check(loaded.PendingOffspringOperations[h].Child==child&&loaded.PendingOffspringOperations[h].BirthComplete,"pending child and phase persistence");
   Campaign.Current.Behaviors.Remove(b);Campaign.Current.Behaviors.Add(loaded);loaded.RegisterEvents();Campaign.Current.Models.EquipmentSelectionModel.Missing=false;
   AdoptHeirService.Execute(h,999);Check(HeroCreator.Created==1&&CampaignEventDispatcher.Instance.Births==1&&CampaignEventDispatcher.Instance.Adults==1,"resume without duplicates");
   Check(BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(h)==900&&loaded.PendingOffspringOperations.Count==0,"original price and pending cleanup");
  }
  {
   var(h,s,b)=Setup();Campaign.Current.Models.EquipmentSelectionModel.Missing=true;
   Fails(()=>AdoptHeirService.Execute(h,0),"equipment");var child=h.Children.Single();
   Check(b.IsUnavailableForAdoption(child),"pending child unavailable for adoption");
   child.SetBirthDay(CampaignTime.YearsFromNow(-18));CampaignEventDispatcher.Instance.OnHeroComesOfAge(child);
   Campaign.Current.Models.EquipmentSelectionModel.Missing=false;
   Fails(()=>AdoptHeirService.Execute(h,0),"interrupted");
   Check(CampaignEventDispatcher.Instance.Adults==1,"external adulthood cannot be replayed on retry");
  }
  foreach(var stage in new[]{"creation","birth","adult"})
  {
   var(h,s,b)=Setup();HeroCreator.FailAfterCreate=stage=="creation";CampaignEventDispatcher.Instance.FailBirth=stage=="birth";CampaignEventDispatcher.Instance.FailAdult=stage=="adult";
   try{AdoptHeirService.Execute(h,50);throw new Exception("Expected native exception");}catch(Exception e) when(e.Message.Contains("listener")){}
   Check(b.PendingOffspringOperations[h].Child==h.Children.Single(),"child captured during "+stage+" failure");
   var births=CampaignEventDispatcher.Instance.Births;var adults=CampaignEventDispatcher.Instance.Adults;
   Fails(()=>AdoptHeirService.Execute(h,50),"interrupted");
   Check(HeroCreator.Created==1&&CampaignEventDispatcher.Instance.Births==births&&CampaignEventDispatcher.Instance.Adults==adults,"no event replay "+stage);
   Check(BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(h)==1000,"no charge "+stage);
  }
  {
   var(h,s,b)=Setup();BLTAdoptAHeroCampaignBehavior.Current.FailCharge=true;
   try{AdoptHeirService.Execute(h,50);throw new Exception("Expected charge failure");}catch(Exception e) when(e.Message=="charge failure"){}
   Check(b.GetValidHeir(h)==null&&BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(h)==1000,"charge rollback");
   BLTAdoptAHeroCampaignBehavior.Current.FailCharge=false;AdoptHeirService.Execute(h,50);
   Check(HeroCreator.Created==1&&CampaignEventDispatcher.Instance.Adults==1&&BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(h)==950,"charge retry no duplication");
  }
  {
   var(h,s,b)=Setup();AdoptHeirService.Execute(h,0);var child=b.GetValidHeir(h);child.IsAlive=false;CampaignEvents.HeroKilledEvent.Fire(child,null,0,false);
   Check(b.GetValidHeir(h)==null&&!b._heirs.Contains(child),"dead heir cleanup");
   var legacy=new Store(true);var legacyData=new Dictionary<Hero,(Hero heir,bool flag)>{{h,(s,true)}};
   using(var sync=new BannerlordTwitch.SaveSystem.ScopedJsonSync(legacy,nameof(BLTHeirBehavior)))sync.SyncDataAsJson("HeirData",ref legacyData);
   var fresh=new BLTHeirBehavior();fresh.SyncData(legacy.Load());Check(fresh.PendingOffspringOperations.Count==0&&fresh.heirList[h].heir==s&&fresh.heirList[h].flag,"old save retains original key and flag without optional data");
  }
  {
   var admin=new Command{ID=Guid.NewGuid(),Handler="AdoptHeir",Name="myheir",Enabled=false,HandlerConfig=new object()};
   var defaults=new[]{new Command{ID=Guid.NewGuid(),Handler="AdoptHeir",Name="adoptheir",Enabled=true}};var list=new List<Command>{admin};
   Check(!ForgeCommandDefaults.AddMissing(list,defaults)&&ReferenceEquals(list[0],admin)&&!admin.Enabled,"preserve renamed disabled admin command");
   list.Clear();Check(ForgeCommandDefaults.AddMissing(list,defaults)&&list.Count==1,"non-destructive default addition");
   list.Clear();list.Add(new(){Name="ADOPTHEIR",Handler="Other"});Check(!ForgeCommandDefaults.AddMissing(list,defaults),"name collision preserved");
  }
  {
   var(h,s,b)=Setup();var blt=BLTAdoptAHeroCampaignBehavior.Current;blt.Active=h;h.IsClanLeader=true;
   AdoptHeirService.Execute(h,0);var child=b.GetValidHeir(h);
   var command=(BannerlordTwitch.Rewards.ICommandHandler)new HeirCommand();
   var settings=Activator.CreateInstance(command.HandlerConfigType,true);var context=new ReplyContext{UserName="viewer"};
   command.Execute(context,settings);
   Check(blt.Active==h&&blt.ItemInheritanceCalls==0&&blt.GoldInheritanceCalls==0,"heir preview does not inherit early");
   blt.Active=null;blt.Retired=h;h.Adopted=false;h.IsClanLeader=false;
   command.Execute(context,settings);
   Check(blt.Active==child&&child.Adopted&&child.Name=="viewer","existing heir command adopts grown child");
   Check(TaleWorlds.CampaignSystem.Actions.ChangeClanLeaderAction.NewLeader==child,"leadership uses preserved heir reference");
   Check(b.heirList.Count==0&&!b._heirs.Contains(child),"adoption cleans exact reservation");
   Check(blt.ItemInheritanceCalls==1&&blt.GoldInheritanceCalls==1&&blt.GetHeroGold(child)==100,"inheritance called once");
   command.Execute(context,settings);Check(blt.ItemInheritanceCalls==1&&blt.GoldInheritanceCalls==1,"repeated heir command does not inherit twice");
   Check(blt.CreatedHeroes.Contains(child),"child supports normal created-hero retirement policy");
  }
  {
   var(h,s,b)=Setup();var command=(BannerlordTwitch.Rewards.ICommandHandler)new HeirCommand();
   var settings=Activator.CreateInstance(command.HandlerConfigType,true);var context=new ReplyContext{UserName="missing"};
   command.Execute(context,settings);Check(BannerlordTwitch.Rewards.ActionManager.Reply.Contains("No current"),"missing ancestor fails without null dictionary key");
   BannerlordTwitch.Rewards.ActionManager.Completed=false;BannerlordTwitch.Rewards.ActionManager.Cancelled=false;
   ((BannerlordTwitch.Rewards.IRewardHandler)command).Enqueue(context,settings);
   Check(BannerlordTwitch.Rewards.ActionManager.Cancelled&&!BannerlordTwitch.Rewards.ActionManager.Completed,"failed reward cancelled");
  }
  Console.WriteLine($"Adopt-heir production service/adapter/persistence: {assertions} assertions passed.");
 }
 sealed class Store : IDataStore
 {
  public Dictionary<string,object> Values=new();public bool IsSaving{get;}public bool IsLoading=>!IsSaving;
  public Store(bool save){IsSaving=save;}public Store Load()=>new(false){Values=Values};
  public bool SyncData<T>(string key,ref T data){if(IsSaving){Values[key]=data;return true;}if(!Values.TryGetValue(key,out var v))return false;data=(T)v;return true;}
 }
}
