global using BannerlordTwitch.Util;
using BLTAdoptAHero.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace JetBrains.Annotations { public sealed class UsedImplicitlyAttribute : Attribute { } }
namespace BLTAdoptAHero.Actions.Util { }
namespace BannerlordTwitch.Localization
{
    public sealed class LocDisplayNameAttribute : Attribute { public LocDisplayNameAttribute(string text) { } }
    public sealed class LocDescriptionAttribute : Attribute { public LocDescriptionAttribute(string text) { } }
}
namespace TaleWorlds.Localization
{
    public class TextObject
    {
        public string Value;
        public TextObject(string value) { Value = value; }
        public override string ToString() => Value;
        public static implicit operator TextObject(string value) => new(value);
    }
}
namespace TaleWorlds.ObjectSystem
{
    public struct MBGUID { }
    public class MBObjectBase { public string StringId { get; set; } = Guid.NewGuid().ToString(); public bool IsReady { get; set; } }
    public class MBObjectManager
    {
        public static MBObjectManager Instance = new();
        public Dictionary<(Type, string), object> Objects = new();
        public T RegisterObject<T>(T value) where T : MBObjectBase { Objects[(typeof(T), value.StringId)] = value; return value; }
        public void UnregisterObject<T>(T value) where T : MBObjectBase => Objects.Remove((typeof(T), value.StringId));
        public T GetObject<T>(string id) => Objects.TryGetValue((typeof(T), id), out var value) ? (T)value : default;
    }
}
namespace TaleWorlds.Core
{
    public class BasicCultureObject : MBObjectBase { public TaleWorlds.Localization.TextObject Name; }
    public enum WeaponClass { Dagger, OneHandedSword, TwoHandedSword, OneHandedAxe, TwoHandedAxe, Mace, TwoHandedMace, OneHandedPolearm, TwoHandedPolearm, LowGripPolearm, Bow, Crossbow, Sling, Arrow, Bolt, SlingStone, ThrowingKnife, ThrowingAxe, Javelin, Stone }
    public enum EquipmentIndex { Weapon0, Weapon1, Weapon2, Weapon3, ExtraWeaponSlot, NumAllWeaponSlots, Horse = 10 }
    public class WeaponComponentData
    {
        public WeaponClass WeaponClass;
        public int SwingDamage = 100, ThrustDamage = 50, MissileDamage, SwingSpeed = 80, ThrustSpeed = 70;
        public bool IsMeleeWeapon = true, IsRangedWeapon, IsAmmo, IsShield;
    }
    public class ItemObject : MBObjectBase
    {
        public TaleWorlds.Localization.TextObject Name = new("weapon");
        public BasicCultureObject Culture;
        public WeaponDesign WeaponDesign;
        public WeaponComponentData PrimaryWeapon = new();
        public List<WeaponComponentData> AlternateModes = new();
        public IEnumerable<WeaponComponentData> Weapons => (PrimaryWeapon == null ? Enumerable.Empty<WeaponComponentData>() : new[] { PrimaryWeapon }).Concat(AlternateModes);
        public bool IsCraftedByPlayer, NotMerchandise, Usable = true, Compatible = true;
        public float Weight = 2, Tierf = 5;
        public BannerlordTwitch.Helpers.EquipmentType Type;
        public object ItemType => Type;
    }
    public class ItemModifier : MBObjectBase
    {
        public TaleWorlds.Localization.TextObject Name;
        public int Damage, Speed, MissileSpeed, Armor;
        public short HitPoints, StackCount;
        public float MountSpeed, Maneuver, ChargeDamage, MountHitPoints;
    }
    public struct EquipmentElement
    {
        public ItemObject Item;
        public ItemModifier ItemModifier;
        public bool IsEmpty => Item == null;
        public EquipmentElement(ItemObject item, ItemModifier modifier = null) { Item = item; ItemModifier = modifier; }
    }
    public class Equipment
    {
        private readonly EquipmentElement[] slots = new EquipmentElement[12];
        public EquipmentElement this[EquipmentIndex i] { get => slots[(int)i]; set => slots[(int)i] = value; }
        public EquipmentElement Horse => this[EquipmentIndex.Horse];
    }
    public class CraftingPiece : MBObjectBase
    {
        public enum PieceTypes { Blade, Guard, Handle, Pommel }
        public PieceTypes PieceType;
        public BasicCultureObject Culture;
        public bool IsValid = true, IsHiddenOnDesigner;
        public int PieceTier = 5;
        public float Weight = .5f;
    }
    public class PieceData { public CraftingPiece.PieceTypes PieceType; public int Order; }
    public class WeaponDescription { public WeaponClass WeaponClass; }
    public class CraftingTemplate : MBObjectBase
    {
        public static List<CraftingTemplate> All = new();
        public List<CraftingPiece> Pieces = new();
        public PieceData[] BuildOrders;
        public WeaponDescription[] WeaponDescriptions;
        public TaleWorlds.Localization.TextObject TemplateName = new("crafted sword");
        public object ItemModifierGroup;
        public BannerlordTwitch.Helpers.EquipmentType Type = BannerlordTwitch.Helpers.EquipmentType.TwoHandedSword;
    }
    public class WeaponDesignElement
    {
        public CraftingPiece CraftingPiece;
        public bool IsValid => CraftingPiece?.IsValid == true;
        public static WeaponDesignElement GetInvalidPieceForType(CraftingPiece.PieceTypes type) => new();
        public static WeaponDesignElement CreateUsablePiece(CraftingPiece piece, int scale) => new() { CraftingPiece = piece };
    }
    public class WeaponDesign
    {
        public CraftingTemplate Template;
        public WeaponDesignElement[] UsedPieces;
        public string HashedCode;
        public WeaponDesign(CraftingTemplate template, TaleWorlds.Localization.TextObject name, WeaponDesignElement[] pieces, string id)
        { Template = template; UsedPieces = pieces; HashedCode = id; }
    }
    public static class Crafting
    {
        public static int Generated;
        public static void GenerateItem(WeaponDesign design, TaleWorlds.Localization.TextObject name, BasicCultureObject culture, object modifiers, ref ItemObject item, string id)
        {
            Generated++;
            item ??= new();
            item.StringId = id ?? design.Template.StringId;
            item.WeaponDesign = new(design.Template, name, design.UsedPieces, id);
            item.Culture = culture;
            item.Name = name;
            item.IsCraftedByPlayer = true;
            item.Type = design.Template.Type;
            item.Tierf = (float)design.UsedPieces.Where(p => p.IsValid).Average(p => p.CraftingPiece.PieceTier);
            item.Weight = design.UsedPieces.Where(p => p.IsValid).Sum(p => p.CraftingPiece.Weight);
        }
    }
    public static class MBRandom { private static Random rng = new(41); public static int RandomInt(int count) => rng.Next(count); }
}
namespace TaleWorlds.CampaignSystem
{
    public class CultureObject : BasicCultureObject { public bool IsMainCulture = true; }
    public interface IDataStore { bool IsLoading { get; } bool IsSaving { get; } bool SyncData<T>(string key, ref T value); }
    public class Store : IDataStore
    {
        public bool IsLoading { get; set; }
        public bool IsSaving => !IsLoading;
        public Dictionary<string, object> Data = new();
        public bool SyncData<T>(string key, ref T value) { if (IsLoading) { if (!Data.TryGetValue(key, out var saved)) return false; value = (T)saved; } else Data[key] = value; return true; }
    }
    public abstract class CampaignBehaviorBase { public abstract void RegisterEvents(); public abstract void SyncData(IDataStore store); }
    public class Campaign
    {
        public static Campaign Current = new();
        public BLTAdoptAHero.BLTCustomItemsCampaignBehavior Custom = new();
        public T GetCampaignBehavior<T>() => (T)(object)Custom;
    }
    public class Hero
    {
        public enum CharacterStates { Active, Prisoner }
        public CharacterStates HeroState;
        public CultureObject Culture;
        public Equipment BattleEquipment = new(), CivilianEquipment = new();
        public BLTAdoptAHero.HeroClassDef Class;
        public string Name = "Viewer";
        public object CharacterObject => this;
    }
    public class CampaignEventDispatcher
    {
        public static CampaignEventDispatcher Instance = new();
        public int Crafted;
        public void OnNewItemCrafted(ItemObject item, object hero, bool flag)
        {
            // Native CraftingCampaignBehavior dereferences the culture before persisting the design.
            _ = item.Culture.StringId;
            Crafted++;
        }
    }
}
namespace TaleWorlds.MountAndBlade { public class Mission { public static Mission Current; } }
namespace BannerlordTwitch.Helpers
{
    public enum EquipmentType { None, Dagger, OneHandedSword, TwoHandedSword, OneHandedAxe, TwoHandedAxe, OneHandedMace, TwoHandedMace, OneHandedLance, TwoHandedLance, OneHandedGlaive, TwoHandedGlaive, Bow, Crossbow, Sling, Arrows, Bolts, SlingStones, ThrowingKnives, ThrowingAxes, ThrowingJavelins, Shield, Stone, Num }
    public static class EquipmentTypeHelpers
    {
        public static WeaponClass GetWeaponClass(EquipmentType type) => type switch
        {
            EquipmentType.OneHandedMace => WeaponClass.Mace, EquipmentType.OneHandedLance or EquipmentType.OneHandedGlaive => WeaponClass.OneHandedPolearm,
            EquipmentType.TwoHandedLance or EquipmentType.TwoHandedGlaive => WeaponClass.TwoHandedPolearm,
            EquipmentType.ThrowingKnives => WeaponClass.ThrowingKnife, EquipmentType.ThrowingAxes => WeaponClass.ThrowingAxe, EquipmentType.ThrowingJavelins => WeaponClass.Javelin,
            _ => Enum.TryParse(type.ToString(), out WeaponClass parsed) ? parsed : WeaponClass.Stone
        };
        public static bool AnyWeaponMatches(this ItemObject item, Func<WeaponComponentData, bool> predicate) => item.Weapons?.Any(predicate) == true;
        public static bool IsEquipmentType(this ItemObject item, EquipmentType type) => item.Type == type;
        public static EquipmentType GetEquipmentType(this ItemObject item) => item.Type;
    }
    public static class CampaignHelpers
    {
        public static List<ItemObject> AllItems = new();
        public static List<CultureObject> AllCultures = new();
        public static IEnumerable<CultureObject> MainCultures => AllCultures.Where(c => c.IsMainCulture);
    }
}
namespace BannerlordTwitch.Util
{
    public static class Log { public static void Error(string message) { } public static void ShowInformation(string message, object hero) { } }
    public static class Extensions
    {
        public static BLTAdoptAHero.HeroClassDef GetClass(this Hero hero) => hero.Class;
        public static bool IsEqualTo(this EquipmentElement a, EquipmentElement b) => a.Item == b.Item && a.ItemModifier == b.ItemModifier;
        public static string GetModifiedItemName(this EquipmentElement item) => item.ItemModifier?.Name?.ToString().Replace("{ITEMNAME}", item.Item.Name.ToString()) ?? item.Item?.Name.ToString();
        public static IEnumerable<(EquipmentElement element, EquipmentIndex index)> YieldEquipmentSlots(this Equipment equipment)
        { for (int i = 0; i < 12; i++) yield return (equipment[(EquipmentIndex)i], (EquipmentIndex)i); }
        public static IEnumerable<(EquipmentElement element, EquipmentIndex index)> YieldFilledWeaponSlots(this Equipment equipment)
            => equipment.YieldEquipmentSlots().Where(s => (int)s.index < 5 && !s.element.IsEmpty);
        public static void SetName(this ItemModifier modifier, TaleWorlds.Localization.TextObject value) => modifier.Name = value;
        public static void SetDamageModifier(this ItemModifier modifier, int value) => modifier.Damage = value;
        public static void SetSpeedModifier(this ItemModifier modifier, int value) => modifier.Speed = value;
        public static void SetMissileSpeedModifier(this ItemModifier modifier, int value) => modifier.MissileSpeed = value;
        public static void SetArmorModifier(this ItemModifier modifier, int value) => modifier.Armor = value;
        public static void SetHitPointsModifier(this ItemModifier modifier, short value) => modifier.HitPoints = value;
        public static void SetStackCountModifier(this ItemModifier modifier, short value) => modifier.StackCount = value;
        public static void SetMountSpeedModifier(this ItemModifier modifier, float value) => modifier.MountSpeed = value;
        public static void SetManeuverModifier(this ItemModifier modifier, float value) => modifier.Maneuver = value;
        public static void SetChargeDamageModifier(this ItemModifier modifier, float value) => modifier.ChargeDamage = value;
        public static void SetMountHitPointsModifier(this ItemModifier modifier, float value) => modifier.MountHitPoints = value;
    }
}
namespace BannerlordTwitch { public class ReplyContext { public string Args; } }
namespace BLTAdoptAHero
{
    public class HeroClassDef { public List<BannerlordTwitch.Helpers.EquipmentType> Weapons = new(); public bool Mounted; public IEnumerable<(EquipmentIndex index, BannerlordTwitch.Helpers.EquipmentType type)> IndexedSlots => IndexedWeapons; public IEnumerable<(EquipmentIndex index, BannerlordTwitch.Helpers.EquipmentType type)> IndexedWeapons => Weapons.Select((type, i) => ((EquipmentIndex)i, type)); }
    public abstract class HeroCommandHandlerBase
    {
        public virtual Type HandlerConfigType => null;
        protected abstract void ExecuteInternal(Hero hero, BannerlordTwitch.ReplyContext context, object config, Action<string> success, Action<string> failure);
        public string TestConfigured(Hero hero, string args, object config) { string result = null; ExecuteInternal(hero, new() { Args = args }, config, s => result = s, s => result = s); return result; }
        public string Test(Hero hero, string args) { string result = null; ExecuteInternal(hero, new() { Args = args }, GlobalForgeConfig.Config, s => result = s, s => result = s); return result; }
    }
    public class GlobalForgeConfig : ForgeSettings { public static GlobalForgeConfig Config = new(); public static GlobalForgeConfig Get() => Config; }
    public static class BLTAdoptAHeroModule { public static Common CommonConfig = new(); public class Common { public HashSet<string> RestrictedItemIds = new(); public int CustomItemLimit = 10; } }
    public static class EquipHero { public static bool CanUseItem(Hero hero, ItemObject item, bool overrideAbility, bool mounted) => item.Compatible && (overrideAbility || item.Usable); }
    public class BLTAdoptAHeroCampaignBehavior
    {
        public static BLTAdoptAHeroCampaignBehavior Current = new();
        public Dictionary<Hero, List<EquipmentElement>> Inventories = new();
        public int Gold = 10000000, SpentGold;
        public bool Auction, FailCharge, FailAdd;
        public List<EquipmentElement> GetCustomItems(Hero hero) { if (!Inventories.ContainsKey(hero)) Inventories[hero] = new(); return Inventories[hero]; }
        public void AddCustomItem(Hero hero, EquipmentElement item) { GetCustomItems(hero).Add(item); if (FailAdd) throw new Exception("injected inventory failure"); }
        public bool IsItemBeingAuctioned(EquipmentElement item) => Auction;
        public int GetHeroGold(Hero hero) => Gold;
        public void CommitEnchantmentPurchase(Hero hero, int cost, Action apply, Action rollback)
        {
            if (cost < 0 || Gold < cost) throw new InvalidOperationException("Insufficient gold.");
            int gold = Gold, spent = SpentGold;
            int nextSpent = checked(spent + cost);
            EnchantmentPolicy.Commit(apply, () => { Gold -= cost; SpentGold = nextSpent; if (FailCharge) throw new Exception("injected charge failure"); }, rollback, () => { Gold = gold; SpentGold = spent; });
        }
    }
}

namespace BannerlordTwitch { public sealed class Command { public Guid ID { get; set; } = Guid.NewGuid(); public string Handler, Name, Help, Documentation; public bool Enabled; public object HandlerConfig; } }

namespace BannerlordTwitch.Util { public static class YamlHelpers { public static T ConvertObject<T>(object value) => Newtonsoft.Json.JsonConvert.DeserializeObject<T>(Newtonsoft.Json.JsonConvert.SerializeObject(value)); } }

namespace TaleWorlds.Library { }
namespace BLTAdoptAHero.Annotations { public sealed class UsedImplicitlyAttribute : Attribute { } }
namespace Xceed.Wpf.Toolkit.PropertyGrid.Attributes
{
    public sealed class CategoryOrderAttribute : Attribute { public CategoryOrderAttribute(string name, int order) { } }
    public sealed class PropertyOrderAttribute : Attribute { public PropertyOrderAttribute(int order) { } }
}
namespace BannerlordTwitch.Localization
{
    public sealed class LocCategoryAttribute : Attribute { public LocCategoryAttribute(string name, string label) { } }
    public static class TestTranslations
    {
        public static string Translate(this string text, params (string key, object value)[] args)
        {
            text = System.Text.RegularExpressions.Regex.Replace(text, @"^\{=[^}]*\}", "");
            foreach (var (key, value) in args) text = text.Replace("{" + key + "}", value?.ToString());
            return text;
        }
    }
}
namespace BannerlordTwitch.Util
{
    public interface IDocumentable { void GenerateDocumentation(IDocumentationGenerator generator); }
    public interface IDocumentationGenerator { void Value(string value); }
}
namespace BLTAdoptAHero
{
    public static class AdoptAHero { public static string NoHeroMessage = "No hero"; }
    public static class Naming { public static string Gold = "gold"; public static string NotEnoughGold(int cost, int gold) => "Not enough gold"; }
    public static class RewardHelpers { public static string GetItemNameAndModifiers(EquipmentElement item) => item.GetModifiedItemName(); }
}
