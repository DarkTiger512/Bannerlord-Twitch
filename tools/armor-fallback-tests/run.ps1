$ErrorActionPreference='Stop'
$source=Get-Content "$PSScriptRoot/../../BannerlordTwitch/BLTAdoptAHero/Actions/EquipHero.cs" -Raw
function Extract([string]$signature) {
    $start=$source.IndexOf($signature); if($start -lt 0){throw "Missing production member: $signature"}
    $open=$source.IndexOf('{',$start); $depth=1; $end=$open+1
    while($depth -gt 0) { if($source[$end] -eq '{'){$depth++}; if($source[$end] -eq '}'){$depth--}; $end++ }
    $source.Substring($start,$end-$start)
}
$members=@('public static bool UpgradeEquipment(', 'private static void UpgradeCivilian(', 'public static bool HeroShouldUseHorse(', 'public enum MountFamilyType', '[Flags]', 'public static ItemObject FindRandomTieredEquipment(', 'public static ItemObject SelectRandomItemNearestTier(', 'private static ItemObject FindLastResortArmor(', 'private static bool CanUseItemIgnoringRace(') | ForEach-Object { Extract $_ }
$start=$source.IndexOf('public static bool CanUseItem(')
$end=$source.IndexOf(';',$start)+1
$members += $source.Substring($start,$end-$start)
$generated="using System; using System.Collections.Generic; using System.Linq; using TaleWorlds.Core; using TaleWorlds.CampaignSystem; using BannerlordTwitch.Util; using BLTAdoptAHero.Util;`npublic static partial class EquipHero {`n"+($members -join "`n")+"`n}"
# Fixture tuple elements are unnamed; the native helper uses named tuple elements.
$generated=$generated.Replace('s.itemType == itemType','s.Item2 == itemType')
Set-Content "$PSScriptRoot/Generated.cs" $generated
& dotnet run --project "$PSScriptRoot/EquipmentTests.csproj" -v:quiet
if($LASTEXITCODE -ne 0){throw 'Armour fallback regression tests failed'}
