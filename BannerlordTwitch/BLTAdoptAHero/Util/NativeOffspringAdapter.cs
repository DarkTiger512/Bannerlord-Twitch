using System;
using System.Collections;
using System.Reflection;
using BLTAdoptAHero.Behaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace BLTAdoptAHero.Util
{
    // Keep engine-version-sensitive aging internals here, with no mod-specific dependencies.
    internal static class NativeOffspringAdapter
    {
        private static readonly FieldInfo MinorHeroes = typeof(AgingCampaignBehavior).GetField(
            "_heroesYoungerThanHeroComesOfAge", BindingFlags.Instance | BindingFlags.NonPublic);

        private static IDictionary GetMinorHeroes()
        {
            var aging = Campaign.Current.GetCampaignBehavior<AgingCampaignBehavior>();
            return (aging != null ? MinorHeroes?.GetValue(aging) : null) as IDictionary
                ?? throw new InvalidOperationException("This campaign's native aging API is unsupported. No gold charged.");
        }

        internal static void Validate(Hero mother, Hero father)
        {
            GetMinorHeroes();
            var models = Campaign.Current.Models;
            if (models.AgeModel.HeroComesOfAge <= 0 || models.EquipmentSelectionModel == null ||
                models.HeroCreationModel == null || models.PregnancyModel == null ||
                mother == null || father == null)
                throw new InvalidOperationException("Native offspring templates or adulthood models are unavailable. No gold charged.");
            var male = models.HeroCreationModel.GetCharacterTemplateForOffspring(mother, father, false);
            var female = models.HeroCreationModel.GetCharacterTemplateForOffspring(mother, father, true);
            if (male == null || female == null)
                throw new InvalidOperationException("Native offspring templates are unavailable. No gold charged.");
            if (male.Race != mother.CharacterObject.Race || female.Race != mother.CharacterObject.Race)
                throw new InvalidOperationException("Native offspring templates do not support the parents' race. No gold charged.");
        }

        internal static void GrowUp(Hero child, BLTHeirBehavior.PendingOffspring pending)
        {
            if (!pending.AdulthoodComplete)
            {
                if (pending.AdulthoodStarted)
                    throw new InvalidOperationException("An adulthood event was interrupted by a campaign listener. The child is retained, but the event cannot safely be repeated. No gold charged; check the campaign log.");
                if (child.HomeSettlement == null)
                    throw new InvalidOperationException("The child has no native home settlement. No gold charged; retry after resolving the clan's settlement.");
                var models = Campaign.Current.Models;
                // Check modded equipment before allowing the native generic fallback to run.
                var originalBirthday = child.BirthDay;
                try
                {
                    child.SetBirthDay(CampaignTime.YearsFromNow(-models.AgeModel.HeroComesOfAge));
                    if (models.EquipmentSelectionModel.GetEquipmentForHeroComeOfAge(child, TaleWorlds.Core.Equipment.EquipmentType.Battle) == null ||
                        models.EquipmentSelectionModel.GetEquipmentForHeroComeOfAge(child, TaleWorlds.Core.Equipment.EquipmentType.Civilian) == null)
                        throw new InvalidOperationException("Adult equipment for this child is unavailable. No gold charged; restore the culture's equipment assets and retry.");
                }
                finally { child.SetBirthDay(originalBirthday); }
                GetMinorHeroes().Remove(child);
                child.SetBirthDay(CampaignTime.YearsFromNow(-models.AgeModel.HeroComesOfAge));
                pending.AdulthoodStarted = true;
                CampaignEventDispatcher.Instance.OnHeroComesOfAge(child);
                pending.AdulthoodComplete = true;
            }
            if (child.Age < Campaign.Current.Models.AgeModel.HeroComesOfAge || !child.IsActive)
                throw new InvalidOperationException("Native adulthood did not activate the child. No gold charged; check the campaign's adulthood listeners.");
        }
    }
}
