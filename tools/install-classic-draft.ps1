#requires -Version 7.0
[CmdletBinding()]
param(
 [Parameter(Mandatory)][string]$StageDirectory,
 [Parameter(Mandatory)][string]$ReleaseDirectory,
 [Parameter(Mandatory)][string]$ExpectedCommit,
 [string]$GameModulesDirectory='C:/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules'
)
$ErrorActionPreference='Stop'
$modules=@('BannerlordTwitch','BLTAdoptAHero','BLTBuffet','BLTConfigure')
$releaseRoot=[IO.Path]::GetFullPath($ReleaseDirectory)
$gameRoot=[IO.Path]::GetFullPath($GameModulesDirectory)
$stage=[IO.Path]::GetFullPath($StageDirectory)
$backup=[IO.Path]::GetFullPath((Join-Path $releaseRoot 'installed-modules-backup'))
if (Get-Process -ErrorAction SilentlyContinue | Where-Object {$_.ProcessName -match '^(Bannerlord|TaleWorlds)'} ) { throw 'Close Bannerlord and its launcher before installing.' }
if (Test-Path -LiteralPath $backup) { throw 'Backup already exists; refusing to overwrite it.' }
function ChildPath([string]$root,[string]$relative) {
 $full=[IO.Path]::GetFullPath((Join-Path $root $relative))
 if (!$full.StartsWith($root.TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Path escaped expected root.' }
 return $full
}
foreach($module in $modules) {
 $installed=ChildPath $gameRoot $module
 $source=ChildPath $stage $module
 if (!(Test-Path -LiteralPath "$source/SubModule.xml")) {throw "Missing draft module: $module"}
 if ((Test-Path -LiteralPath $installed) -and ((Get-Item -LiteralPath $installed).Attributes -band [IO.FileAttributes]::ReparsePoint)) {throw 'Refusing to replace a redirected module directory.'}
}
New-Item -ItemType Directory -Path $backup | Out-Null
$draftMetadata=Get-Content -LiteralPath (Join-Path $stage 'release-metadata.json') -Raw | ConvertFrom-Json
if ($draftMetadata.sourceCommit -ne $ExpectedCommit) { throw 'Draft source mismatch' }
$defaultYaml=Join-Path $stage 'BannerlordTwitch/Bannerlord-Twitch-v4.yaml'
if ((Get-Content -LiteralPath $defaultYaml -Raw) -notmatch '(?m)^ConfigurationGeneration: 1\r?$') {throw 'Missing clean default configuration generation.'}
$preserved=[Collections.Generic.List[string]]::new()
$moved=[Collections.Generic.List[string]]::new()
$copied=[Collections.Generic.List[string]]::new()
try {
foreach($module in $modules) {
 $installed=ChildPath $gameRoot $module
 $saved=ChildPath $backup $module
 # Both resolved locations have been checked before moving a module tree.
 if (Test-Path -LiteralPath $installed) {
  Move-Item -LiteralPath $installed -Destination $saved
  $moved.Add($module)
 }
}
 foreach($module in $modules) {
  $installed=ChildPath $gameRoot $module
  $copied.Add($module)
  Copy-Item -LiteralPath (ChildPath $stage $module) -Destination $installed -Recurse
  $saved=ChildPath $backup $module
  foreach($file in @(if (Test-Path -LiteralPath $saved) {Get-ChildItem -LiteralPath $saved -Recurse -File | Where-Object {$_.Name -eq 'Bannerlord-Twitch-Auth.yaml' -or $_.Name -eq 'config.json'}})) {
   $relative=[IO.Path]::GetRelativePath($saved,$file.FullName)
   $dest=ChildPath $installed $relative
   New-Item -ItemType Directory -Path (Split-Path $dest) -Force | Out-Null
   Copy-Item -LiteralPath $file.FullName -Destination $dest -Force
   if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $dest).Hash) {throw 'Configuration preservation failed.'}
   $preserved.Add("$module/$relative".Replace('\','/'))
  }
 }
 $checked=0
 foreach($module in $modules) {
  $source=ChildPath $stage $module
  foreach($file in Get-ChildItem -LiteralPath $source -Recurse -File) {
   $relative=[IO.Path]::GetRelativePath($source,$file.FullName)
   if ($preserved.Contains("$module/$relative".Replace('\','/'))) {continue}
   $dest=ChildPath (ChildPath $gameRoot $module) $relative
   if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $dest).Hash) {throw "Installed file mismatch: $module/$relative"}
   $checked++
  }
  Copy-Item -LiteralPath "$stage/release-metadata.json" -Destination (ChildPath (ChildPath $gameRoot $module) 'testdraft-metadata.json')
 }
 [ordered]@{sourceCommit=$ExpectedCommit;configurationGeneration=1;resetOldProfilesOnLoad=$true;installedAt=(Get-Date -Format o);modules=$modules;gameModules=$gameRoot;backup=$backup;verifiedDraftFiles=$checked;preservedConfiguration=$preserved.ToArray();manualTestStatus='awaiting owner testing'} | ConvertTo-Json -Depth 5 | Set-Content "$releaseRoot/installation.json"
 Write-Output "Installed four draft modules; verified $checked files and preserved $($preserved.Count) authentication/service files; installed the clean default YAML. Backup: $backup"
} catch {
 $failed=[IO.Path]::GetFullPath("$releaseRoot/failed-install")
 New-Item -ItemType Directory -Path $failed -Force | Out-Null
 foreach($module in $copied) {
  $installed=ChildPath $gameRoot $module
  if(Test-Path -LiteralPath $installed){Move-Item -LiteralPath $installed -Destination (ChildPath $failed $module)}
 }
 foreach($module in $moved) {
  Move-Item -LiteralPath (ChildPath $backup $module) -Destination (ChildPath $gameRoot $module)
 }
 throw
}
