using System.Collections.Generic;
using System.Linq;
using BannerlordTwitch.SaveSystem;
using BannerlordTwitch.Util;
using TaleWorlds.CampaignSystem;

namespace BLTAdoptAHero.Behaviors
{
    public class BLTHeirBehavior : CampaignBehaviorBase
    {
        public Dictionary<Hero, (Hero heir, bool flag)> heirList = new();
        public HashSet<Hero> _heirs = new();
        public Dictionary<Hero, PendingOffspring> PendingOffspringOperations = new();

        public sealed class PendingOffspring
        {
            public int Version { get; set; } = 1;
            public Hero Mother { get; set; }
            public Hero Father { get; set; }
            public Hero Child { get; set; }
            public int GoldCost { get; set; }
            public bool CreationStarted { get; set; }
            public bool CreationComplete { get; set; }
            public bool BirthStarted { get; set; }
            public bool BirthComplete { get; set; }
            public bool AdulthoodStarted { get; set; }
            public bool AdulthoodComplete { get; set; }
        }

        // Only populated while native creation runs on the campaign thread.
        internal PendingOffspring CreatingOffspring;

        public bool IsPendingOffspring(Hero hero) => hero != null &&
            PendingOffspringOperations.Values.Any(p => p.Child == hero);

        public bool IsUnavailableForAdoption(Hero hero) => IsPendingOffspring(hero) ||
            heirList.Values.Any(p => p.heir == hero);

        public bool CanReserve(Hero owner, Hero heir) => owner != null && heir != null &&
            owner != heir && heir.IsAlive && !heir.IsDisabled && !heir.IsAdopted() &&
            heir.Clan != null && heir.Clan == owner.Clan &&
            heir.Age >= Campaign.Current.Models.AgeModel.HeroComesOfAge &&
            !heirList.Any(p => p.Key != owner && p.Value.heir == heir);

        public Hero GetValidHeir(Hero owner)
        {
            if (owner == null || !heirList.TryGetValue(owner, out var entry)) return null;
            if (CanReserve(owner, entry.heir)) return entry.heir;
            RemoveReservation(owner);
            return null;
        }

        public bool TryReserve(Hero owner, Hero heir)
        {
            if (!CanReserve(owner, heir)) return false;
            RemoveReservation(owner);
            heirList[owner] = (heir, owner.IsClanLeader);
            _heirs.Add(heir);
            return true;
        }

        public void RemoveReservation(Hero owner)
        {
            if (owner == null || !heirList.TryGetValue(owner, out var entry)) return;
            heirList.Remove(owner);
            if (!heirList.Values.Any(p => p.heir == entry.heir)) _heirs.Remove(entry.heir);
        }

        public override void RegisterEvents()
        {
            CampaignEvents.HeroCreated.AddNonSerializedListener(this, (hero, bornNaturally) =>
            {
                if (CreatingOffspring != null && hero.Mother == CreatingOffspring.Mother &&
                    hero.Father == CreatingOffspring.Father && CreatingOffspring.Child == null)
                    CreatingOffspring.Child = hero;
            });
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, () =>
            {
                _heirs = heirList.Values.Where(v => v.heir != null).Select(v => v.heir).ToHashSet();
            });

            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, (victim, killer, actionDetail, showNotification) =>
            {
                if (_heirs.Contains(victim))
                {
                    _heirs.Remove(victim);
                    foreach (var key in heirList.Where(h => h.Value.heir == victim).Select(h => h.Key).ToList())
                    {
                        RemoveReservation(key);
                        Log.ShowInformation($"{key.Name}'s heir has died. Select a new one");
                    }
                }
            });

            CampaignEvents.HeroComesOfAgeEvent.AddNonSerializedListener(this, hero =>
            {
                var pending = PendingOffspringOperations.Values.FirstOrDefault(p => p.Child == hero);
                if (pending != null)
                {
                    // Natural aging or another mod may mature a retained child before retry.
                    // Do not replay an event whose complete listener chain we did not observe.
                    pending.AdulthoodStarted = true;
                    return;
                }
                var father = hero.Father;
                var mother = hero.Mother;
                if (father == null && mother == null) return;
                if (father != null && father.IsAdopted() && GetValidHeir(father) == null && TryReserve(father, hero))
                {
                    return;
                }
                if (mother != null && mother.IsAdopted() && GetValidHeir(mother) == null)
                    TryReserve(mother, hero);
            });

            CampaignEvents.OnClanLeaderChangedEvent.AddNonSerializedListener(this, (Hero leader, Hero newLeader) =>
            {
                var kv1 = heirList.FirstOrDefault(h => h.Value.heir == leader);
                if (kv1.Key != null && leader.IsAdopted())
                {
                    var entry = heirList[kv1.Key];
                    entry.flag = true;       
                    heirList[kv1.Key] = entry; 
                }

                var kv2 = heirList.FirstOrDefault(h => h.Value.heir == newLeader);
                if (kv2.Key != null && newLeader.IsAdopted())
                {
                    var entry = heirList[kv2.Key];
                    entry.flag = false;      
                    heirList[kv2.Key] = entry; 
                }
            });


        }
        public override void SyncData(IDataStore dataStore)
        {
            using var scopedJsonSync = new ScopedJsonSync(dataStore, nameof(BLTHeirBehavior));
            scopedJsonSync.SyncDataAsJson("HeirData", ref heirList);
            scopedJsonSync.SyncDataAsJson("PendingOffspringV1", ref PendingOffspringOperations);
            heirList ??= new();
            PendingOffspringOperations ??= new();
        }
    }
}
