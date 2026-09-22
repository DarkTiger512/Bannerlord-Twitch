#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$ClassicRef = 'main',
    [string]$IntegrationRef = 'BLT/twitch-integration',
    [string]$OutputDirectory = '',
    [string]$GameDirectory = 'C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord',
    [string]$ManagedServiceUrl = 'https://bltrefreshed.evepirate.nl',
    [string]$MSBuildPath = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe',
    [string]$SevenZipPath = '',
    [string]$NuGetPackagesPath = '',
    [string]$NodeModulesPath = '',
    [string]$PolicyTestFramework = 'net9.0',
    [int]$BrowserPort = 5175
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = Split-Path $PSScriptRoot -Parent
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repo ('releases/draft-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Choose a new output directory; existing output is never overwritten.' }
if (!(Test-Path -LiteralPath $MSBuildPath)) { throw 'Visual Studio MSBuild is required. Pass -MSBuildPath.' }
if (!(Test-Path -LiteralPath (Join-Path $GameDirectory 'bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll'))) { throw 'Bannerlord assemblies are required.' }
$serviceUri = [Uri]$ManagedServiceUrl
if (!$serviceUri.IsAbsoluteUri -or $serviceUri.Scheme -ne 'https' -or $serviceUri.IsLoopback -or $serviceUri.UserInfo -or $serviceUri.Query) { throw 'A public HTTPS managed service URL without credentials is required.' }
if (!$SevenZipPath) {
    $SevenZipPath = @('C:\Program Files\7-Zip\7z.exe', 'C:\bin\7zr.exe', (Join-Path $repo 'releases/tooling/7zr.exe')) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (!$SevenZipPath) { throw 'Install 7-Zip or pass -SevenZipPath to an official 7zr.exe executable.' }
$SevenZipPath = (Resolve-Path -LiteralPath $SevenZipPath).Path
if (!$NuGetPackagesPath) { $NuGetPackagesPath = Join-Path $repo 'BannerlordTwitch/packages' }
if (!$NodeModulesPath) { $NodeModulesPath = Join-Path $repo 'TwitchExtension/frontend/node_modules' }
$NuGetPackagesPath = (Resolve-Path -LiteralPath $NuGetPackagesPath).Path
$NodeModulesPath = (Resolve-Path -LiteralPath $NodeModulesPath).Path
$modules = @('BannerlordTwitch', 'BLTAdoptAHero', 'BLTBuffet', 'BLTConfigure')
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$workspacePath = Join-Path ([IO.Path]::GetTempPath()) ('blt-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
$work = New-Item -ItemType Directory -Path $workspacePath
$work.FullName | Set-Content (Join-Path $OutputDirectory 'build-workspace.txt') -Encoding utf8
$logs = New-Item -ItemType Directory -Path (Join-Path $OutputDirectory 'logs')
$artifacts = [Collections.Generic.List[string]]::new()
$checks = [Collections.Generic.List[string]]::new()
$records = [Collections.Generic.List[object]]::new()
function Run([string]$Name, [string]$Directory, [string]$Exe, [string[]]$Arguments) {
    Write-Host $Name
    Push-Location $Directory
    try {
        & $Exe @Arguments *> (Join-Path $logs.FullName "$Name.log")
        if ($LASTEXITCODE -ne 0) { throw "$Name failed ($LASTEXITCODE). See logs/$Name.log." }
        $checks.Add("PASS: $Name")
    } finally { Pop-Location }
}
function Read-Git([string[]]$Arguments) {
    $result = & git.exe -C $repo @Arguments
    if ($LASTEXITCODE -ne 0) { throw 'Git command failed.' }
    return ($result -join "`n").Trim()
}
function WriteJson([string]$Path, $Value) { $Value | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $Path -Encoding utf8 }
function CheckBundle([string]$Directory, $Record, [string]$ExpectedConfig) {
    foreach ($module in $modules) {
        $moduleRoot = Join-Path $Directory $module
        [xml]$xml = Get-Content -LiteralPath (Join-Path $moduleRoot 'SubModule.xml') -Raw
        if ($xml.Module.Id.value -ne $module -or $xml.Module.Version.value -ne "v$($Record.version)") { throw "Unexpected module identity/version in $module" }
        if (!(Test-Path -LiteralPath (Join-Path $moduleRoot "bin/Win64_Shipping_Client/$module.dll"))) { throw "Missing $module assembly" }
    }
    $bundledHarmony = @(Get-ChildItem -LiteralPath $Directory -Recurse -File -Filter '0Harmony.dll')
    if ($bundledHarmony.Count -gt 0) { throw 'Unexpected 0Harmony.dll in package; Harmony must be supplied by the runtime dependency stack.' }
    $config = Get-Content -LiteralPath (Join-Path $Directory 'BannerlordTwitch/Bannerlord-Twitch-v4.yaml') -Raw
    if ($config.Replace("`r`n", "`n") -cne $ExpectedConfig.Replace("`r`n", "`n")) { throw 'Bundled branch configuration changed.' }
    if ($config -notmatch 'Prestige:\s+Enabled: true' -or $config -notmatch 'BattleBalance:\s+Enabled: true' -or $config -notmatch 'DifficultyScalingOnEnemySide: false' -or $config -notmatch 'DifficultyScalingOnPlayersSide: false') { throw 'Unexpected gameplay defaults.' }
    foreach ($file in Get-ChildItem -LiteralPath $Directory -Recurse -File) {
        if ($file.Name -match '(?i)(auth.*\.ya?ml$|^\.env|\.(pfx|pem|key|user|pdb)$)') { throw "Forbidden packaged file: $($file.Name)" }
        if ($file.Extension -in @('.yaml', '.json', '.xml', '.config', '.txt', '.js')) {
            $body = Get-Content -LiteralPath $file.FullName -Raw
            if ($body -match '-----BEGIN (?:RSA |OPENSSH |EC )?PRIVATE KEY-----|temporary-preview|\?balance=uneven') { throw "Private or preview material found: $($file.Name)" }
        }
    }
    $metadata = Get-Content -LiteralPath (Join-Path $Directory 'release-metadata.json') -Raw | ConvertFrom-Json
    if ($metadata.sourceCommit -ne $Record.sourceCommit -or $metadata.variant -ne $Record.variant) { throw 'Extracted metadata mismatch.' }
}
$savedVite = @{}
Get-ChildItem Env: | Where-Object Name -Like 'VITE_*' | ForEach-Object { $savedVite[$_.Name] = $_.Value; Remove-Item -LiteralPath "Env:$($_.Name)" }
try {
    $classicCommit = Read-Git @('rev-parse', '--verify', "$ClassicRef^{commit}")
    $integrationCommit = Read-Git @('rev-parse', '--verify', "$IntegrationRef^{commit}")
    Run 'shared-parity' $repo 'node' @('tools/verify-release-parity.mjs', '--classic-ref', $classicCommit, '--integration-ref', $integrationCommit)
    foreach ($variant in @('Classic', 'TwitchExtension')) {
        $sourceCommit = if ($variant -eq 'Classic') { $classicCommit } else { $integrationCommit }
        $checkout = New-Item -ItemType Directory -Path (Join-Path $work.FullName $variant)
        $archive = Join-Path $work.FullName "$variant-source.zip"
        Run "$variant-export" $repo 'git' @('archive', '--format=zip', "--output=$archive", $sourceCommit)
        Expand-Archive -LiteralPath $archive -DestinationPath $checkout.FullName
        $solution = Join-Path $checkout.FullName 'BannerlordTwitch'
        New-Item -ItemType Junction -Path (Join-Path $solution 'packages') -Target $NuGetPackagesPath | Out-Null
        [xml]$properties = Get-Content -LiteralPath (Join-Path $solution 'BLTProperties.targets') -Raw
        # Select explicit properties across MSBuild property groups without changing versioning.
        $version = $properties.SelectSingleNode("//*[local-name()='ModuleVersion']").InnerText
        $gameVersion = $properties.SelectSingleNode("//*[local-name()='GameVersion']").InnerText
        $record = [ordered]@{ variant=$variant; version=$version; bannerlordVersion=$gameVersion; sourceCommit=$sourceCommit; status='draft'; manualCampaignTests='not performed'; managedServiceUrl=$(if ($variant -eq 'TwitchExtension') { $ManagedServiceUrl } else { $null }) }
        $records.Add($record)
        foreach ($module in $modules) {
            Run "$variant-build-$module" $solution $MSBuildPath @("$module/$module.csproj", '/t:Build', '/p:Configuration=Release', "/p:SolutionDir=$solution/", "/p:BANNERLORD_GAME_DIR=$GameDirectory", '/p:DeployToGame=false', '/p:CreatePackage=false', "/p:ManagedServiceUrl=$ManagedServiceUrl", '/v:minimal', '/nologo')
        }
        $testProject = Join-Path $solution 'BLTAdoptAHero.Tests'
        Run "$variant-policy-restore" $checkout.FullName 'dotnet' @('restore', $testProject, "/p:TargetFramework=$PolicyTestFramework", "-p:RestoreSources=$($work.FullName)", '/v:quiet')
        Run "$variant-policy-build" $checkout.FullName 'dotnet' @('build', $testProject, '--no-restore', "/p:TargetFramework=$PolicyTestFramework", '/v:quiet')
        Run "$variant-policy-tests" $checkout.FullName 'dotnet' @((Join-Path $testProject "bin/Debug/$PolicyTestFramework/BLTAdoptAHero.Tests.dll"))
        $stage = New-Item -ItemType Directory -Path (Join-Path $work.FullName "$variant-stage")
        foreach ($module in $modules) {
            $built = Join-Path $solution "build/Release/$module"
            foreach ($file in Get-ChildItem -LiteralPath $built -File -Recurse) {
                if ($file.Name -match '(?i)(auth.*\.ya?ml$|^\.env|\.(pfx|pem|key|user|pdb)$)') { continue }
                $destination = Join-Path $stage.FullName "$module/$([IO.Path]::GetRelativePath($built, $file.FullName))"
                New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
                Copy-Item -LiteralPath $file.FullName -Destination $destination
            }
        }
        WriteJson (Join-Path $stage.FullName 'release-metadata.json') $record
        Copy-Item -LiteralPath (Join-Path $checkout.FullName 'docs/RELEASE-VARIANTS.md') -Destination (Join-Path $stage.FullName 'INSTALL-AND-RELEASE-NOTES.md')
        $expectedConfig = Get-Content -LiteralPath (Join-Path $solution 'BannerlordTwitch/_Module/Bannerlord-Twitch-v4.yaml') -Raw
        CheckBundle $stage.FullName $record $expectedConfig
        $name = "BLT-v$version-Bannerlord-v$gameVersion-$variant-$($sourceCommit.Substring(0,8))-DRAFT.7z"
        $package = Join-Path $OutputDirectory $name
        Run "$variant-archive" $stage.FullName $SevenZipPath @('a', '-t7z', '-mx=5', $package, '*')
        Run "$variant-archive-test" $stage.FullName $SevenZipPath @('t', $package)
        $extracted = Join-Path $work.FullName "$variant-extracted"
        Run "$variant-extract" $stage.FullName $SevenZipPath @('x', $package, "-o$extracted", '-y')
        CheckBundle $extracted $record $expectedConfig
        $checks.Add("PASS: $variant extracted package identity, metadata, config and credential/preview exclusions")
        $artifacts.Add($package)
        if ($variant -eq 'TwitchExtension') {
            $frontend = Join-Path $checkout.FullName 'TwitchExtension/frontend'
            New-Item -ItemType Junction -Path (Join-Path $frontend 'node_modules') -Target $NodeModulesPath | Out-Null
            # Reject reuse of a dependency tree from a different lockfile.
            $dependencySource = Split-Path $NodeModulesPath -Parent
            if ((Get-Content (Join-Path $frontend 'package-lock.json') -Raw).Replace("`r`n", "`n") -cne (Get-Content (Join-Path $dependencySource 'package-lock.json') -Raw).Replace("`r`n", "`n")) { throw 'Frontend dependency lock differs. Run npm ci for the selected integration ref and pass its node_modules path.' }
            $env:VITE_BLT_API_URL = $ManagedServiceUrl
            $env:VITE_BLT_LIVE_INTEGRATION = 'false'
            Run 'TwitchExtension-frontend-tests' $frontend 'npm.cmd' @('test')
            Run 'TwitchExtension-frontend-build' $frontend 'npm.cmd' @('run', 'build')
            $dist = Join-Path $frontend 'dist'
            foreach ($entry in @('index.html', 'viewer.html', 'config.html', 'live-config.html', 'action-manifest.json')) { if (!(Test-Path (Join-Path $dist $entry))) { throw "Missing frontend entry $entry" } }
            $javascript = (Get-ChildItem (Join-Path $dist 'assets') -Filter '*.js' | Get-Content -Raw) -join "`n"
            if (!$javascript.Contains($ManagedServiceUrl) -or $javascript -match 'temporary-preview|prestige-test|http://127\.0\.0\.1:5188') { throw 'Frontend contains preview or development service settings.' }
            WriteJson (Join-Path $dist 'release-metadata.json') $record
            $frontendPackage = Join-Path $OutputDirectory "BLT-v$version-TwitchFrontend-$($sourceCommit.Substring(0,8))-DRAFT.zip"
            Compress-Archive -Path (Join-Path $dist '*') -DestinationPath $frontendPackage
            $frontendExtracted = Join-Path $work.FullName 'frontend-extracted'
            Expand-Archive -LiteralPath $frontendPackage -DestinationPath $frontendExtracted
            $frontendFiles = @(Get-ChildItem $dist -Recurse -File)
            if ($frontendFiles.Count -ne @(Get-ChildItem $frontendExtracted -Recurse -File).Count) { throw 'Frontend zip file count mismatch.' }
            foreach ($file in $frontendFiles) {
                $copy = Join-Path $frontendExtracted ([IO.Path]::GetRelativePath($dist, $file.FullName))
                if ((Get-FileHash $file.FullName).Hash -ne (Get-FileHash $copy).Hash) { throw 'Frontend zip content mismatch.' }
            }
            $artifacts.Add($frontendPackage)
            $backend = Join-Path $checkout.FullName 'TwitchExtension/backend/BLT.ExtensionService.Tests'
            Run 'TwitchExtension-backend-tests' $checkout.FullName 'dotnet' @('test', $backend, '--verbosity', 'minimal', '-p:RestoreIgnoreFailedSources=true')
            if (Get-NetTCPConnection -LocalPort $BrowserPort -State Listen -ErrorAction SilentlyContinue) { throw "Browser test port $BrowserPort is in use. Select a free port with -BrowserPort." }
            $env:VITE_BLT_LIVE_INTEGRATION = 'true'
            $env:VITE_BLT_CHANNEL_ID = 'prestige-test'
            $env:VITE_BLT_API_URL = "http://127.0.0.1:$BrowserPort"
            $node = (Get-Command node).Source
            $server = Start-Process -FilePath $node -ArgumentList @('node_modules/vite/bin/vite.js', '--host', '127.0.0.1', '--port', "$BrowserPort", '--strictPort') -WorkingDirectory $frontend -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logs.FullName 'browser-server.log') -RedirectStandardError (Join-Path $logs.FullName 'browser-server-error.log')
            try {
                $ready = $false
                for ($attempt=0; $attempt -lt 30; $attempt++) {
                    if ($server.HasExited) { throw 'Browser test server exited.' }
                    try { $null = Invoke-WebRequest "http://127.0.0.1:$BrowserPort/"; $ready=$true; break } catch { Start-Sleep -Milliseconds 500 }
                }
                if (!$ready) { throw 'Browser test server did not become ready.' }
                # Generated only inside the isolated checkout; no tracked config is edited.
                $browserConfig = @"
import { defineConfig } from '@playwright/test';
export default defineConfig({testDir:'./e2e',testMatch:['balance.spec.ts','prestige.spec.ts'],workers:1,outputDir:'./draft-browser-results',use:{channel:'chrome',baseURL:'http://127.0.0.1:$BrowserPort',viewport:{width:1440,height:1000}}});
"@
                Set-Content (Join-Path $frontend 'playwright.draft.config.ts') $browserConfig -Encoding utf8
                Run 'TwitchExtension-browser-tests' $frontend 'node' @('node_modules/@playwright/test/cli.js', 'test', '--config', 'playwright.draft.config.ts')
            } finally { if (!$server.HasExited) { Stop-Process -Id $server.Id } }
        }
    }
    $artifacts | ForEach-Object { "{0}  {1}" -f (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path $_ -Leaf) } | Set-Content (Join-Path $OutputDirectory 'SHA256SUMS.txt') -Encoding utf8
    WriteJson (Join-Path $OutputDirectory 'release-manifest.json') @{ packages=$records; policyTestFramework=$PolicyTestFramework; archiverSha256=(Get-FileHash $SevenZipPath).Hash; checks=$checks.ToArray(); releaseGate='BLOCKED: disposable-campaign smoke tests not performed' }
    Copy-Item (Join-Path $repo 'docs/RELEASE-VARIANTS.md') (Join-Path $OutputDirectory 'RELEASE-NOTES.md')
    @("# Draft release validation", "", ($checks -join "`n"), "", 'NOT RELEASE-READY: disposable-campaign land/naval, save/reload, replacement, failed joins and actual reward checks remain pending on both variants. Cross-variant save compatibility is unverified.', '', 'No branch push, publication, Twitch upload, game deployment or VPS deployment was performed.') | Set-Content (Join-Path $OutputDirectory 'VALIDATION.md') -Encoding utf8
    Write-Host "Draft packages and validation: $OutputDirectory"
} catch {
    @('# Draft preparation incomplete', '', $_.Exception.Message, '', ($checks -join "`n"), '', 'Do not distribute artifacts from this incomplete run.') | Set-Content (Join-Path $OutputDirectory 'VALIDATION.md') -Encoding utf8
    throw
} finally {
    Get-ChildItem Env: | Where-Object Name -Like 'VITE_*' | ForEach-Object { Remove-Item -LiteralPath "Env:$($_.Name)" }
    foreach ($key in $savedVite.Keys) { Set-Item -LiteralPath "Env:$key" -Value $savedVite[$key] }
}
