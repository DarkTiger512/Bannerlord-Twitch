using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Util;

namespace HarmonyLib { public static class AccessTools { public static bool Installed = true; public static Type TypeByName(string name) => Installed ? typeof(AccessTools) : null; } }
namespace TaleWorlds.Core
{
    public enum EquipmentIndex { Weapon0, Weapon1, Weapon2, Weapon3, Head, Body, Legs, Gloves, Cape, Horse, HorseHarness, WeaponItemBeginSlot = Weapon0 }
    [Flags] public enum ItemFlags { None = 0, NotUsableByFemale = 1, NotUsableByMale = 2 }
    public class SkillObject { }
    public class ItemObject
    {
        public enum ItemUsageSetFlags { RequiresNoShield, RequiresMount, RequiresNoMount }
        public enum ItemTypeEnum { Invalid, OneHandedWeapon, TwoHandedWeapon, Polearm, Bow, Crossbow, Sling, Arrows, Bolts, SlingStones, Thrown, Shield, Horse, HorseHarness, BodyArmor, HeadArmor, LegArmor, HandArmor, Cape }
        public string StringId;
        public int Tier;
        public ItemTypeEnum ItemType;
        public ItemTypeEnum Type => ItemType;
        public bool NotMerchandise, IsCivilian = true;
        public int Difficulty;
        public ItemFlags ItemFlags;
        public SkillObject RelevantSkill;
        public CultureObject Culture;
        public HorseComponent HorseComponent;
        public ArmorComponent ArmorComponent;
        public static ItemTypeEnum GetAmmoTypeForItemType(ItemTypeEnum type) => type switch { ItemTypeEnum.Bow => ItemTypeEnum.Arrows, ItemTypeEnum.Crossbow => ItemTypeEnum.Bolts, ItemTypeEnum.Sling => ItemTypeEnum.SlingStones, ItemTypeEnum.Thrown => ItemTypeEnum.Thrown, _ => ItemTypeEnum.Invalid };
    }
    public class HorseComponent { public bool IsMount = true; public Monster Monster = new(); }
    public class Monster { public int FamilyType = 1; }
    public class ArmorComponent { public int FamilyType = 1; }
    public struct EquipmentElement
    {
        public ItemObject Item;
        public object ItemModifier;
        public bool IsEmpty => Item == null;
        public static EquipmentElement Invalid => default;
        public EquipmentElement(ItemObject item) { Item = item; ItemModifier = null; }
        public EquipmentElement(EquipmentElement other) { Item = other.Item; ItemModifier = other.ItemModifier; }
    }
    public class Equipment
    {
        private readonly EquipmentElement[] slots = new EquipmentElement[11];
        public EquipmentElement this[EquipmentIndex i] { get => slots[(int)i]; set => slots[(int)i] = value; }
    }
    public static class DefaultSkills { public static SkillObject Riding = new(), Athletics = new(); }
}
namespace TaleWorlds.CampaignSystem
{
    public class Campaign { public static Campaign Current = new(); }
    public class CultureObject { }
    public class CharacterObject
    {
        public static List<CharacterObject> All = new();
        public int Race;
        public bool IsHero, IsTemplate, IsFemale;
        public List<Equipment> BattleEquipments = new(), CivilianEquipments = new();
    }
    public class Hero
    {
        public CultureObject Culture;
        public CharacterObject CharacterObject = new();
        public Equipment BattleEquipment = new(), CivilianEquipment = new();
        public int Tier, Gold = 1000;
        public HeroClassDef Class = new();
        public List<EquipmentElement> CustomItems = new();
        public int GetSkillValue(SkillObject skill) => 100;
    }
}
namespace BannerlordTwitch.Util
{
    public static class Extensions
    {
        public static T SelectRandom<T>(this IEnumerable<T> source) => source.FirstOrDefault();
        public static string Translate(this string text, params (string, object)[] args) => text;
        public static bool HasAnyFlag(this ItemFlags flags, ItemFlags other) => (flags & other) != 0;
        public static bool IsEquipmentType(this ItemObject item, EquipmentType type) => (type == EquipmentType.OneHandedSword && item.ItemType == ItemObject.ItemTypeEnum.OneHandedWeapon && !item.StringId.Contains("mace")) || (type == EquipmentType.Bow && item.ItemType == ItemObject.ItemTypeEnum.Bow) || (type == EquipmentType.Arrows && item.ItemType == ItemObject.ItemTypeEnum.Arrows);
        public static IEnumerable<(EquipmentElement element, EquipmentIndex index)> YieldEquipmentSlots(this Equipment e)
            => Enumerable.Range(0, 11).Select(i => (e[(EquipmentIndex)i], (EquipmentIndex)i));
        public static IEnumerable<(EquipmentElement element, EquipmentIndex index)> YieldFilledEquipmentSlots(this Equipment e) => e.YieldEquipmentSlots().Where(x => !x.element.IsEmpty);
        public static IEnumerable<(EquipmentElement element, EquipmentIndex index)> YieldWeaponSlots(this Equipment e) => e.YieldEquipmentSlots().Take(4);
        public static IEnumerable<(EquipmentElement element, EquipmentIndex index)> YieldFilledWeaponSlots(this Equipment e) => e.YieldWeaponSlots().Where(x => !x.element.IsEmpty);
    }
}
public enum EquipmentType { None, OneHandedSword, Dagger, TwoHandedSword, OneHandedAxe, TwoHandedAxe, OneHandedMace, TwoHandedMace, OneHandedLance, TwoHandedLance, OneHandedGlaive, TwoHandedGlaive, Bow, Crossbow, Sling, ThrowingKnives, ThrowingAxes, ThrowingJavelins, Stone, Arrows, Bolts, Shield }
public class HeroClassDef { public bool Mounted, UseHorse, UseCamel; public string Name = "Test"; public EquipmentType[] SlotItems = { EquipmentType.OneHandedSword }; }
public static class SkillGroup
{
    public static (EquipmentIndex, ItemObject.ItemTypeEnum)[] ArmorIndexType = { (EquipmentIndex.Body, ItemObject.ItemTypeEnum.BodyArmor), (EquipmentIndex.Head, ItemObject.ItemTypeEnum.HeadArmor), (EquipmentIndex.Legs, ItemObject.ItemTypeEnum.LegArmor), (EquipmentIndex.Gloves, ItemObject.ItemTypeEnum.HandArmor), (EquipmentIndex.Cape, ItemObject.ItemTypeEnum.Cape) };
    public static (SkillObject skill, ItemObject.ItemTypeEnum itemType)[] SkillItemPairs = { (new(), ItemObject.ItemTypeEnum.OneHandedWeapon) };
    public static (SkillObject skill, ItemObject.ItemTypeEnum itemType)[] MeleeSkillItemPairs = SkillItemPairs;
}
public static class CampaignHelpers { public static List<ItemObject> AllItems = new(); }
public static class BLTAdoptAHeroModule { public static Config CommonConfig = new(); public class Config { public HashSet<string> RestrictedItemIds = new(); } }
public class BLTCustomItemsCampaignBehavior { public static BLTCustomItemsCampaignBehavior Current = new(); public bool IsRegistered(object modifier) => modifier != null; }
public class BLTAdoptAHeroCampaignBehavior
{
    public static BLTAdoptAHeroCampaignBehavior Current = new();
    public List<EquipmentElement> GetCustomItems(Hero h) => h.CustomItems;
    public int GetEquipmentTier(Hero h) => h.Tier;
    public void SetEquipmentTier(Hero h, int tier) => h.Tier = tier;
    public void SetEquipmentClass(Hero h, HeroClassDef c) { }
    public HeroClassDef GetClass(Hero h) => h.Class;
    public int GetHeroGold(Hero h) => h.Gold;
    public void ChangeHeroGold(Hero h, int amount, bool isSpending) => h.Gold += amount;
}
public class Settings { public int? StartingEquipmentTier = 1; public bool ReequipInsteadOfUpgrade; public int GetTierCost(int tier) => 100; }
public static class Naming { public static string NotEnoughGold(int a, int b) => "Not enough gold"; }
public static partial class EquipHero
{
    public static bool IsItemUsableMounted(Hero h, ItemObject i) => true;
    public static bool WeaponIsSwingable(ItemObject i) => true;
    public static bool WeaponRequires(ItemObject i, object flag) => false;
}public static class Program
{
    static int checks;
    static void Check(bool value, string description) { if (!value) throw new Exception(description); Console.WriteLine("PASS " + description); checks++; }
    static ItemObject Item(string id, int tier, ItemObject.ItemTypeEnum type = ItemObject.ItemTypeEnum.BodyArmor) => new() { StringId = id, Tier = tier, ItemType = type };
    static void Reset()
    {
        Campaign.Current = new(); CharacterObject.All.Clear(); CampaignHelpers.AllItems.Clear();
        HarmonyLib.AccessTools.Installed = true; TaomEquipmentCompatibility.Reset();
        BLTAdoptAHeroModule.CommonConfig.RestrictedItemIds.Clear();
    }
    static void Donor(int race, params ItemObject[] items)
    {
        var c = new CharacterObject { Race = race };
        foreach (var item in items) { var e = new Equipment(); e[EquipmentIndex.Body] = new(item); c.BattleEquipments.Add(e); }
        CharacterObject.All.Add(c); CampaignHelpers.AllItems.AddRange(items);
    }
    static Hero Hero(int race, ItemObject initial = null)
    {
        var h = new Hero { CharacterObject = new() { Race = race, IsHero = true } };
        h.BattleEquipment[EquipmentIndex.Body] = new(initial); h.CivilianEquipment[EquipmentIndex.Body] = new(initial); return h;
    }
    public static void Main()
    {
        foreach (var (slot, type) in SkillGroup.ArmorIndexType)
        {
            Reset(); var mirkwood = new CultureObject();
            var foreign = Item("troll-tier6", 5, type); Donor(8, foreign);
            var h = Hero(77);
            EquipHero.UpgradeEquipment(h, 5, h.Class, true, mirkwood, true);
            Check(h.BattleEquipment[slot].Item == foreign, $"{slot}: empty unknown-race catalogue receives exact Tier 6 foreign armour");
            Check(h.CivilianEquipment[slot].Item == foreign, $"{slot}: civilian fallback fills armour");
            EquipHero.UpgradeEquipment(h, 5, h.Class, true);
            Check(h.BattleEquipment[slot].Item == foreign, $"{slot}: subsequent normal re-equip retains armour");
            foreign.Tier=1;
            EquipHero.UpgradeEquipment(h, 5, h.Class, true);
            Check(h.BattleEquipment[slot].Item == foreign, $"{slot}: distant-tier fallback fills otherwise empty slot");
            var original=h.BattleEquipment[slot]; CampaignHelpers.AllItems.Clear();
            EquipHero.UpgradeEquipment(h, 5, h.Class, true);
            Check(h.BattleEquipment[slot].Item == original.Item, $"{slot}: missing assets preserve previous battle armour");
            Check(h.CivilianEquipment[slot].Item == original.Item, $"{slot}: missing assets preserve civilian armour");
            Reset(); var blocked=Item("blocked",5,type); Donor(8,blocked);
            BLTAdoptAHeroModule.CommonConfig.RestrictedItemIds.Add("blocked"); h=Hero(77);
            EquipHero.UpgradeEquipment(h,5,h.Class,true);
            Check(h.BattleEquipment[slot].IsEmpty, $"{slot}: administrator item restrictions remain enforced");
            Reset(); var compatible=Item("elf-tier5",4,type); var alien=Item("troll-tier6",5,type); Donor(77,compatible); Donor(8,alien); h=Hero(77);
            EquipHero.UpgradeEquipment(h,5,h.Class,true,mirkwood,true);
            Check(h.BattleEquipment[slot].Item==compatible, $"{slot}: compatible normal fallback precedes foreign race fallback");
            Reset(); alien=Item("troll-tier6",5,type); Donor(8,alien); h=Hero(77);
            EquipHero.UpgradeEquipment(h,0,h.Class,true,enforceTierCap:true);
            Check(h.BattleEquipment[slot].IsEmpty, $"{slot}: starting equipment tier cap stays enforced");
        }
        Reset(); var weapon=Item("troll_sword",5,ItemObject.ItemTypeEnum.OneHandedWeapon); Donor(8,weapon);
        Check(!EquipHero.CanUseItem(Hero(77),weapon,false,false), "Creature weapon race restrictions unchanged");
        foreach (var family in new[] { 1, 2 })
        {
            Reset();
            var mount=Item("mount",5,ItemObject.ItemTypeEnum.Horse);
            mount.HorseComponent=new() { Monster=new() { FamilyType=family } };
            Donor(77,mount);
            var saddle=Item("foreign-saddle",5,ItemObject.ItemTypeEnum.HorseHarness);
            saddle.ArmorComponent=new() { FamilyType=family };
            Donor(8,saddle);
            var wrong=Item("wrong-family",5,ItemObject.ItemTypeEnum.HorseHarness);
            wrong.ArmorComponent=new() { FamilyType=family == 1 ? 2 : 1 };
            Donor(8,wrong);
            var rider=Hero(77); rider.Class.Mounted=true;
            rider.Class.UseHorse=family == 1; rider.Class.UseCamel=family == 2;
            EquipHero.UpgradeEquipment(rider,5,rider.Class,true,new CultureObject(),true);
            Check(rider.BattleEquipment[EquipmentIndex.HorseHarness].Item==saddle,
                $"Mount family {family}: exact-tier saddle ignores rider race/culture but fits mount");
            saddle.Tier=0;
            EquipHero.UpgradeEquipment(rider,5,rider.Class,true);
            Check(rider.BattleEquipment[EquipmentIndex.HorseHarness].Item==saddle,
                $"Mount family {family}: distant-tier saddle fallback and repeat re-equip work");
            var modifier=new object(); rider.BattleEquipment[EquipmentIndex.HorseHarness]=new(saddle) { ItemModifier=modifier };
            CampaignHelpers.AllItems.Remove(saddle);
            EquipHero.UpgradeEquipment(rider,5,rider.Class,true);
            Check(rider.BattleEquipment[EquipmentIndex.HorseHarness].Item==saddle
                && rider.BattleEquipment[EquipmentIndex.HorseHarness].ItemModifier==modifier,
                $"Mount family {family}: previous fitting saddle and modifier survive missing assets");
            rider.BattleEquipment[EquipmentIndex.HorseHarness]=new(wrong);
            EquipHero.UpgradeEquipment(rider,5,rider.Class,true);
            Check(rider.BattleEquipment[EquipmentIndex.HorseHarness].IsEmpty,
                $"Mount family {family}: wrong-family old saddle is never restored");
            CampaignHelpers.AllItems.Add(saddle); saddle.Tier=5;
            BLTAdoptAHeroModule.CommonConfig.RestrictedItemIds.Add(saddle.StringId);
            EquipHero.UpgradeEquipment(rider,5,rider.Class,true);
            Check(rider.BattleEquipment[EquipmentIndex.HorseHarness].IsEmpty,
                $"Mount family {family}: blocked saddle is not selected");
            BLTAdoptAHeroModule.CommonConfig.RestrictedItemIds.Clear();
            rider.BattleEquipment[EquipmentIndex.HorseHarness]=new(saddle);
            CampaignHelpers.AllItems.Remove(saddle);
            var newMount=Item("new-family-mount",5,ItemObject.ItemTypeEnum.Horse);
            newMount.HorseComponent=new() { Monster=new() { FamilyType=family == 1 ? 2 : 1 } };
            Donor(77,newMount); TaomEquipmentCompatibility.Reset();
            CampaignHelpers.AllItems.Remove(mount);
            rider.Class.UseHorse=family != 1; rider.Class.UseCamel=family != 2;
            EquipHero.UpgradeEquipment(rider,5,rider.Class,true);
            Check(rider.BattleEquipment[EquipmentIndex.HorseHarness].Item==wrong,
                $"Mount family {family}: switching mount family selects its fitting saddle");
        }
        Console.WriteLine($"{checks} checks passed.");
    }
}



