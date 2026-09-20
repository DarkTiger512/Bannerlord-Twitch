using System.Reflection;
using System.Text.Json;
using System.Collections.Concurrent;
using BannerlordTwitch;
using BannerlordTwitch.Integration;
using BannerlordTwitch.Util;
void Assert(bool value,string message){if(!value)throw new Exception(message);}
ManagedIntegrationClient Client()=>new(new AuthSettings(),"42",Array.Empty<Command>());
async Task Message(ManagedIntegrationClient client,string kind,Guid id,DateTimeOffset timestamp){
 var json=JsonSerializer.Serialize(new {v=1,id,kind,channelId="42",timestamp,user=new {id="",name="Tester",roles=new[]{"viewer"}},data=new{actionId="command.summon",args=new{},commandLine="summon"}});
 await (Task)typeof(ManagedIntegrationClient).GetMethod("HandleMessageAsync",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(client,new object[]{json,CancellationToken.None});
}
using(var client=Client()){
 var action=Guid.NewGuid();var command=Guid.NewGuid();var seen=new List<Guid>();
 client.ActionRequested+=r=>seen.Add(r.RequestId);client.CommandRequested+=r=>seen.Add(r.RequestId);
 await Message(client,"action.request",action,DateTimeOffset.UtcNow);
 await Message(client,"command.request",command,DateTimeOffset.UtcNow);
 Assert(seen.SequenceEqual(new[]{action,command}),"Real connector failed to dispatch wire-format requests");
 Assert(client.IsRequestPending(action),"Request must have a deadline before game dispatch");
 var stale=Guid.NewGuid();await Message(client,"action.request",stale,DateTimeOffset.UtcNow.AddMinutes(-2));
 Assert(!client.IsRequestPending(stale)&&seen.Count==2,"Expired request must terminate without execution");
 await client.SendActionResultAsync(action,new[]{"done"});Assert(!client.IsRequestPending(action),"Reply did not complete request");
}
using(var client=Client()){
 var calls=0;IntegrationIdentityProvider.Reconcile=(_,_)=>calls++;
 await Message(client,"viewer.subscribe",Guid.NewGuid(),DateTimeOffset.UtcNow);
 Assert(MainThreadSync.Queue.Count>0,"Expected queued viewer reconciliation");
 client.Dispose();MainThreadSync.Drain();Assert(calls==0,"Disposed connector ran queued game access");
 await client.SendActionResultAsync(Guid.NewGuid(),new[]{"late"});
}
for(var n=0;n<100;n++){
 var lifecycle=new IntegrationRequestLifecycle();var id=Guid.NewGuid();lifecycle.TryAccept(id,out _);
 Parallel.Invoke(()=>lifecycle.TryComplete(id),()=>lifecycle.TryExpire(id),()=>lifecycle.Dispose());
 Assert(!lifecycle.IsPending(id)&&!lifecycle.TryAccept(Guid.NewGuid(),out _),"Disposed lifecycle accepted work");
}
Console.WriteLine("PASS real connector action/command dispatch, early deadline, stale request completion, queued shutdown cancellation, late replies, and 100 concurrent completion/disposal races.");
