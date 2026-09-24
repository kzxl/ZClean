<#
    publish.ps1 — Publish script for ZClean (Dual Mode: Full & Lite)
    Adheres to AgentOption .NET Publish Release standard & ZeroUniverse rules.
#>
[CmdletBinding()]
param(
    [ValidateSet('Full', 'Lite', 'All')]
    [string]$Mode = 'All',
    [ValidateSet('UI', 'CLI', 'All')]
    [string]$Target = 'All',
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot
$UiProj = Join-Path $Root "src\ZClean.UI\ZClean.UI.csproj"
$CliProj = Join-Path $Root "src\ZClean.Cli\ZClean.Cli.csproj"
$Dist = Join-Path $Root "publish"

if ($Mode -eq 'Full' -or $Mode -eq 'All') {
    Write-Host ">>> Publishing ZClean FULL (Self-Contained Single File)..." -ForegroundColor Cyan
    
    if ($Target -eq 'UI' -or $Target -eq 'All') {
        $outUiFull = Join-Path $Dist "ui-full"
        dotnet publish $UiProj -c $Configuration -r $Runtime --self-contained true `
            -p:PublishSingleFile=true `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:EnableCompressionInSingleFile=true `
            -o $outUiFull
        Write-Host "  ✔ UI Full generated at: $outUiFull\ZClean.exe" -ForegroundColor Green
    }
    
    if ($Target -eq 'CLI' -or $Target -eq 'All') {
        $outCliFull = Join-Path $Dist "cli-full"
        dotnet publish $CliProj -c $Configuration -r $Runtime --self-contained true `
            -p:PublishSingleFile=true `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:EnableCompressionInSingleFile=true `
            -o $outCliFull
        Write-Host "  ✔ CLI Full generated at: $outCliFull\zeroclean.exe" -ForegroundColor Green
    }
}

if ($Mode -eq 'Lite' -or $Mode -eq 'All') {
    Write-Host ">>> Publishing ZClean LITE (Framework-Dependent Single File)..." -ForegroundColor Cyan
    
    if ($Target -eq 'UI' -or $Target -eq 'All') {
        $outUiLite = Join-Path $Dist "ui-lite"
        dotnet publish $UiProj -c $Configuration -r $Runtime --self-contained false `
            -p:PublishSingleFile=true `
            -o $outUiLite
        Write-Host "  ✔ UI Lite generated at: $outUiLite\ZClean.exe" -ForegroundColor Green
    }
    
    if ($Target -eq 'CLI' -or $Target -eq 'All') {
        $outCliLite = Join-Path $Dist "cli-lite"
        dotnet publish $CliProj -c $Configuration -r $Runtime --self-contained false `
            -p:PublishSingleFile=true `
            -o $outCliLite
        Write-Host "  ✔ CLI Lite generated at: $outCliLite\zeroclean.exe" -ForegroundColor Green
    }
}

Write-Host ">>> ZClean publish completed successfully!" -ForegroundColor Green
