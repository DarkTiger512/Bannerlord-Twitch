using System;
using System.Collections.Generic;
using System.Linq;
using BannerlordTwitch.SaveSystem;
using BLTAdoptAHero.Actions.Util;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using BLTAdoptAHero.Util;

namespace BLTAdoptAHero
{
    public class BLTCustomItemsCampaignBehavior : CampaignBehaviorBase
    {
        public static BLTCustomItemsCampaignBehavior Current => Campaign.Current?.GetCampaignBehavior<BLTCustomItemsCampaignBehavior>();

        private class ItemModifierData
        {
            [UsedImplicitly]
            public string Name { get; set; }
            [UsedImplicitly]
            public string StringId { get; set; }
            [UsedImplicitly]
            public int Damage { get; set; }
            [UsedImplicitly]
            public int Speed { get; set; }
            [UsedImplicitly]
            public int MissileSpeed { get; set; }
            [UsedImplicitly]
            public int Armor { get; set; }
            [UsedImplicitly]
            public short HitPoints { get; set; }
            [UsedImplicitly]
            public short StackCount { get; set; }
            [UsedImplicitly]
            public float MountSpeed { get; set; }
            [UsedImplicitly]
            public float Maneuver { get; set; }
            [UsedImplicitly]
            public float ChargeDamage { get; set; }
            [UsedImplicitly]
            public float MountHitPoints { get; set; }
            [UsedImplicitly]
            public string CustomName { get; set; }

            public List<EnchantmentEntry> Enchantments { get; set; } = new();
            public ForgeMetadata Forge { get; set; }

            public void Apply(ItemModifier toModifier)
            {
                toModifier.SetName(new(CustomName ?? Name));
                toModifier.StringId = StringId;
                toModifier.SetDamageModifier(Damage);
                toModifier.SetSpeedModifier(Speed);
                toModifier.SetMissileSpeedModifier(MissileSpeed);
                toModifier.SetArmorModifier(Armor);
                toModifier.SetHitPointsModifier(HitPoints);
                toModifier.SetStackCountModifier(StackCount);
                toModifier.SetMountSpeedModifier(MountSpeed);
                toModifier.SetManeuverModifier(Maneuver);
                toModifier.SetChargeDamageModifier(ChargeDamage);
                toModifier.SetMountHitPointsModifier(MountHitPoints);
            }
        }

        private Dictionary<ItemModifier, ItemModifierData> customItemModifiers = new();

        public override void RegisterEvents() { }

        public override void SyncData(IDataStore dataStore)
        {
            using var scopedJsonSync = new ScopedJsonSync(dataStore, nameof(BLTCustomItemsCampaignBehavior));
            if (dataStore.IsLoading)
            {
                var savedModiferList = new List<ItemModifier>();
                dataStore.SyncData("ModifierList", ref savedModiferList);
                var savedModiferDataList = new List<ItemModifierData>();
                scopedJsonSync.SyncDataAsJson("ModifierData", ref savedModiferDataList);

                // ItemModifier is hashed by string id, so we need to initialize them BEFORE putting them into the dictionary
                customItemModifiers = new();
                foreach (var (modifier, data) in savedModiferList
                    .Zip(savedModiferDataList, (modifier, data) => (modifier, data)))
                {
                    modifier.StringId = data.StringId;
                    var registeredModifier = MBObjectManager.Instance.RegisterObject(modifier);
                    registeredModifier.IsReady = true;
                    data.Apply(registeredModifier);
                    customItemModifiers.Add(registeredModifier, data);
                }
            }
            else
            {
                var savedModiferList = customItemModifiers.Keys.ToList();
                dataStore.SyncData("ModifierList", ref savedModiferList);
                var savedModiferDataList = customItemModifiers.Values.ToList();
                scopedJsonSync.SyncDataAsJson("ModifierData", ref savedModiferDataList);
            }
        }

        public ItemModifier CreateArmorModifier(string modifiedName, int armorModifier) =>
            RegisterModifier(new()
            {
                Name = modifiedName,
                Armor = armorModifier,
            });

        public ItemModifier CreateWeaponModifier(string modifiedName, int damageModifier, int speedModifier, int missileSpeedModifier, short stackSizeModifier) =>
            RegisterModifier(new()
            {
                Name = modifiedName,
                Damage = damageModifier,
                MissileSpeed = missileSpeedModifier,
                Speed = speedModifier,
                StackCount = stackSizeModifier,
            });

        public ItemModifier CreateAmmoModifier(string modifiedName, int damageModifier, short stackModifier) =>
            RegisterModifier(new()
            {
                Name = modifiedName,
                Damage = damageModifier,
                StackCount = stackModifier,
            });

        public ItemModifier CreateMountModifier(string modifiedName, float maneuverModifier, float mountSpeedModifier, float chargeDamageModifier, float mountHitPointsModifier) =>
            RegisterModifier(new()
            {
                Name = modifiedName,
                Maneuver = maneuverModifier,
                MountSpeed = mountSpeedModifier,
                ChargeDamage = chargeDamageModifier,
                MountHitPoints = mountHitPointsModifier,
            });

        public ItemModifier CreateShieldModifier(string modifiedName, short HitPointsModifier) =>
            RegisterModifier(new()
            {
                Name = modifiedName,
                HitPoints = HitPointsModifier,
            });

        public ItemModifier CreateDummyModifier(string baseName) =>
            RegisterModifier(new()
            {
                Name = baseName,
                Armor = 0,
                Damage = 0,
                MissileSpeed = 0,
                Speed = 0,
                StackCount = 0,
                Maneuver = 0,
                MountSpeed = 0,
                ChargeDamage = 0,
                MountHitPoints = 0,
            });

        public bool IsRegistered(ItemModifier modifier) => modifier != null && customItemModifiers.ContainsKey(modifier);

        public int GetEnchantmentLevel(ItemModifier modifier) =>
            customItemModifiers.TryGetValue(modifier, out var data) ? data.Enchantments?.Count ?? 0 : 0;

        public ForgeMetadata GetForgeMetadata(ItemModifier modifier) =>
            modifier != null && customItemModifiers.TryGetValue(modifier, out var data) ? data.Forge?.Copy() : null;

        public void SetInitialForgeMetadata(ItemModifier modifier, ForgeMetadata metadata)
        {
            var data = customItemModifiers[modifier];
            if (data.Forge != null) throw new InvalidOperationException("Forge metadata already initialized.");
            data.Forge = metadata.Copy();
        }

        public void RemoveNewForgeModifier(ItemModifier modifier)
        {
            if (modifier == null || !customItemModifiers.Remove(modifier)) return;
            MBObjectManager.Instance.UnregisterObject(modifier);
        }

        public void Reforge(Hero hero, EquipmentElement item, ForgeStyle style, ForgeQuality quality, ForgeSettings settings, int cost)
        {
            var campaign = BLTAdoptAHeroCampaignBehavior.Current;
            if (!campaign.GetCustomItems(hero).Any(i => i.IsEqualTo(item)) || campaign.IsItemBeingAuctioned(item))
                throw new InvalidOperationException("Weapon ownership changed or the weapon is being auctioned.");
            var data = customItemModifiers[item.ItemModifier];
            var previous = data.Forge;
            var next = previous?.Copy() ?? new ForgeMetadata
            {
                NativeCrafted = item.Item.WeaponDesign != null,
                BaseItemId = item.Item.StringId,
                TemplateId = item.Item.WeaponDesign?.Template?.StringId,
                CultureId = item.Item.Culture?.StringId,
                BaselineDamage = data.Damage,
                BaselineSpeed = data.Speed
            };
            if (next.Version != 1) throw new InvalidOperationException("This weapon requires a newer forge version.");
            var contributions = ForgeService.Contributions(item.Item, style, quality, settings);
            int damage = data.Damage, speed = data.Speed;
            int newDamage = checked(damage - next.AppliedDamage + contributions.damage);
            int newSpeed = checked(speed - next.AppliedSpeed + contributions.speed);
            if (newDamage == damage && newSpeed == speed)
                throw new InvalidOperationException("This choice would not change the weapon's stats. No gold charged.");
            next.Style = style;
            next.Quality = quality;
            next.AppliedDamage = contributions.damage;
            next.AppliedSpeed = contributions.speed;
            campaign.CommitEnchantmentPurchase(hero, cost, () =>
            {
                item.ItemModifier.SetDamageModifier(newDamage);
                item.ItemModifier.SetSpeedModifier(newSpeed);
                data.Damage = newDamage;
                data.Speed = newSpeed;
                data.Forge = next;
            }, () =>
            {
                data.Damage = damage;
                data.Speed = speed;
                data.Forge = previous;
                item.ItemModifier.SetDamageModifier(damage);
                item.ItemModifier.SetSpeedModifier(speed);
            });
        }

        public (EnchantmentEntry change, bool success, int level) Enchant(ItemModifier modifier,
            EnchantmentStat stat, int gain, int failurePercent, int roll, Hero hero, int cost)
        {
            if (!customItemModifiers.TryGetValue(modifier, out var data))
                throw new InvalidOperationException("The item is not a registered custom item.");
            var history = data.Enchantments;
            var next = EnchantmentPolicy.Roll(history, stat, gain, failurePercent, roll, out var change, out var success);
            int damage = data.Damage, speed = data.Speed, missileSpeed = data.MissileSpeed;
            int delta = success ? change.Gain : -change.Gain;
            int newDamage = checked(damage + (change.Stat == EnchantmentStat.Damage ? delta : 0));
            int newSpeed = checked(speed + (change.Stat == EnchantmentStat.Speed ? delta : 0));
            int newMissileSpeed = checked(missileSpeed + (change.Stat == EnchantmentStat.MissileSpeed ? delta : 0));
            BLTAdoptAHeroCampaignBehavior.Current.CommitEnchantmentPurchase(hero, cost,
                () =>
                {
                    modifier.SetDamageModifier(newDamage);
                    modifier.SetSpeedModifier(newSpeed);
                    modifier.SetMissileSpeedModifier(newMissileSpeed);
                    data.Damage = newDamage;
                    data.Speed = newSpeed;
                    data.MissileSpeed = newMissileSpeed;
                    data.Enchantments = next;
                },
                () =>
                {
                    data.Damage = damage;
                    data.Speed = speed;
                    data.MissileSpeed = missileSpeed;
                    data.Enchantments = history;
                    modifier.SetDamageModifier(damage);
                    modifier.SetSpeedModifier(speed);
                    modifier.SetMissileSpeedModifier(missileSpeed);
                });
            return (change, success, next.Count);
        }

        public bool ItemCanBeNamed(ItemModifier itemModifier) => itemModifier != null && customItemModifiers.ContainsKey(itemModifier);

        public void NameItem(ItemModifier itemModifier, string name)
        {
            if (customItemModifiers.TryGetValue(itemModifier, out ItemModifierData modifierData))
            {
                itemModifier.SetName(new(name));
                modifierData.CustomName = name;
            }
        }

        private ItemModifier RegisterModifier(ItemModifierData modifierData)
        {
            modifierData.StringId = Guid.NewGuid().ToString();
            var modifier = new ItemModifier();
            modifierData.Apply(modifier);
            var registeredModifier = MBObjectManager.Instance.RegisterObject(modifier);
            customItemModifiers.Add(registeredModifier, modifierData);
            return registeredModifier;
        }

        // public void InitializeCraftingElements()
        // {
        //     List<ItemObject> list = new List<ItemObject>();
        //     foreach (KeyValuePair<ItemObject, CraftingCampaignBehavior.CraftedItemInitializationData> keyValuePair in this._craftedItemDictionary)
        //     {
        //         ItemObject itemObject = Crafting.InitializePreCraftedWeaponOnLoad(keyValuePair.Key, keyValuePair.Value.CraftedData, keyValuePair.Value.ItemName, keyValuePair.Value.Culture, keyValuePair.Value.OverrideData);
        //         if (itemObject == DefaultItems.Trash)
        //         {
        //             list.Add(keyValuePair.Key);
        //             if (MBObjectManager.Instance.GetObject(keyValuePair.Key.Id) != null)
        //             {
        //                 MBObjectManager.Instance.UnregisterObject(keyValuePair.Key);
        //             }
        //         }
        //         else
        //         {
        //             ItemObject.InitAsPlayerCraftedItem(ref itemObject);
        //         }
        //     }
        //     foreach (ItemObject key in list)
        //     {
        //         this._craftedItemDictionary.Remove(key);
        //     }
        //     foreach (KeyValuePair<Town, CraftingCampaignBehavior.CraftingOrderSlots> keyValuePair2 in this.CraftingOrders)
        //     {
        //         foreach (CraftingOrder craftingOrder in keyValuePair2.Value.Slots)
        //         {
        //             if (craftingOrder != null)
        //             {
        //                 craftingOrder.InitializeCraftingOrderOnLoad();
        //             }
        //         }
        //     }
        // }
    }
}
