using System;
using System.Collections.Generic;
using System.Linq;
using BannerlordTwitch.Helpers;
using BLTAdoptAHero.Actions;
using BLTAdoptAHero.Behaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace BLTAdoptAHero.Util
{
    internal static class AdoptHeirService
    {
        // Commands are dispatched through BLT's campaign-thread action queue.
        internal static string Execute(Hero owner, int configuredCost)
        {
            var campaign = Campaign.Current;
            var behavior = campaign?.GetCampaignBehavior<BLTHeirBehavior>()
                ?? throw new InvalidOperationException("The campaign heir system is unavailable.");
            if (Mission.Current != null || owner.IsPrisoner || owner.PartyBelongedTo?.MapEvent != null)
                throw new InvalidOperationException("Create your heir on the campaign map while free and outside battle.");
            var existing = behavior.GetValidHeir(owner);
            if (existing != null) return $"Your heir is already {existing.Name}. No child created or gold charged.";
            if (!owner.IsAlive || owner.IsDisabled || owner.Clan == null)
                throw new InvalidOperationException("Your hero must be alive and belong to a clan.");

            var blt = BLTAdoptAHeroCampaignBehavior.Current;
            behavior.PendingOffspringOperations.TryGetValue(owner, out var pending);
            if (pending == null)
            {
                var spouse = owner.Spouse;
                int adultAge = campaign.Models.AgeModel.HeroComesOfAge;
                if (spouse == null || !spouse.IsAlive || spouse.IsDisabled || spouse.Spouse != owner ||
                    spouse.IsFemale == owner.IsFemale || owner.Age < adultAge || spouse.Age < adultAge)
                    throw new InvalidOperationException("You need a living adult spouse of the opposite sex.");
                if (spouse.IsPrisoner || spouse.PartyBelongedTo?.MapEvent != null)
                    throw new InvalidOperationException("Your spouse must be free and outside battle.");
                Hero mother = owner.IsFemale ? owner : spouse;
                Hero father = owner.IsFemale ? spouse : owner;
                if (mother.IsPregnant)
                    throw new InvalidOperationException("Your spouse or hero is already pregnant. Wait for that birth first.");
                if (mother.CharacterObject.Race != father.CharacterObject.Race)
                    throw new InvalidOperationException("Native offspring creation does not support these parents' different races.");
                int limit = FamilyManagement.BabyCommandLimit;
                if (owner.Children.Count(c => !c.IsDead && c.Clan == owner.Clan) >= limit)
                    throw new InvalidOperationException($"You have reached the family baby-command limit ({limit}).");
                if (configuredCost < 0) throw new InvalidOperationException("GoldCost must be zero or greater.");
                if (blt.GetHeroGold(owner) < configuredCost)
                    throw new InvalidOperationException($"Creating your heir costs {configuredCost:N0} BLT gold.");
                NativeOffspringAdapter.Validate(mother, father);
                pending = new BLTHeirBehavior.PendingOffspring
                {
                    Mother = mother, Father = father, GoldCost = configuredCost
                };
                behavior.PendingOffspringOperations.Add(owner, pending);
            }

            if (pending.Version != 1)
                throw new InvalidOperationException("This pending heir operation requires a newer BLT version.");
            if (blt.GetHeroGold(owner) < pending.GoldCost)
                throw new InvalidOperationException($"Completing your pending heir costs {pending.GoldCost:N0} BLT gold.");
            NativeOffspringAdapter.Validate(pending.Mother, pending.Father);

            if (!pending.CreationComplete)
            {
                if (pending.CreationStarted)
                    throw new InvalidOperationException("Native child creation was interrupted. The pending record was retained to prevent duplicate children; inspect the campaign log before recovery. No gold charged.");
                pending.CreationStarted = true;
                behavior.CreatingOffspring = pending;
                var previousChildren = new HashSet<Hero>(pending.Mother.Children.Concat(pending.Father.Children));
                try
                {
                    pending.Child = HeroCreator.DeliverOffSpring(pending.Mother, pending.Father,
                        MBRandom.RandomFloat < campaign.Models.PregnancyModel.DeliveringFemaleOffspringProbability);
                    pending.CreationComplete = pending.Child != null;
                }
                finally
                {
                    behavior.CreatingOffspring = null;
                    // A mod may throw before the native HeroCreated event reaches BLT.
                    if (pending.Child == null)
                    {
                        var created = pending.Mother.Children.Concat(pending.Father.Children).Distinct()
                            .Where(c => !previousChildren.Contains(c) && c.Mother == pending.Mother && c.Father == pending.Father).ToList();
                        if (created.Count == 1) pending.Child = created[0];
                    }
                }
            }
            Hero child = pending.Child;
            if (child == null || !child.IsAlive || child.IsDisabled || child.IsAdopted() ||
                child.Mother != pending.Mother || child.Father != pending.Father)
                throw new InvalidOperationException("The pending child is unavailable or no longer eligible. No gold charged.");
            child.Clan = owner.Clan;
            EnsureWandererSkills(child);

            if (!pending.BirthComplete)
            {
                if (pending.BirthStarted)
                    throw new InvalidOperationException("A birth event was interrupted by a campaign listener. The child is retained, but the event cannot safely be repeated. No gold charged; check the campaign log.");
                pending.BirthStarted = true;
                CampaignEventDispatcher.Instance.OnGivenBirth(pending.Mother, new List<Hero> { child }, 0);
                pending.BirthComplete = true;
            }
            NativeOffspringAdapter.GrowUp(child, pending);
            EnsureWandererSkills(child);
            blt.SetIsCreatedHero(child, true);
            // Native listeners can alter campaign state; recheck before committing.
            if (!owner.IsAlive || owner.IsDisabled || !owner.IsAdopted() || owner.Clan != child.Clan ||
                blt.GetHeroGold(owner) < pending.GoldCost || behavior.GetValidHeir(owner) != null)
                throw new InvalidOperationException("Your hero, gold or heir reservation changed during creation. The child is retained and no gold was charged.");
            string reply = $"{child.Name} is now your adult child and future heir! Your current hero stays active; use !heir after retirement or death. Cost: {pending.GoldCost:N0} BLT gold.";
            if (!behavior.TryReserve(owner, child))
                throw new InvalidOperationException("The adult child could not be reserved as your heir. No gold charged; retry after resolving the conflict.");
            int originalGold = blt.GetHeroGold(owner);
            try { blt.ChangeHeroGold(owner, -pending.GoldCost); }
            catch
            {
                behavior.RemoveReservation(owner);
                blt.SetHeroGold(owner, originalGold);
                throw;
            }
            behavior.PendingOffspringOperations.Remove(owner);
            return reply;
        }

        private static void EnsureWandererSkills(Hero child)
        {
            // Protect both retained newborns and completed adults against zero-skill removal on load.
            if (!child.IsWanderer) return;
            var skills = CampaignHelpers.AllSkillObjects.ToList();
            if (skills.Count > 0 && skills.All(s => child.GetSkillValue(s) == 0))
                child.HeroDeveloper.SetInitialSkillLevel(skills[0], 1);
        }
    }
}
