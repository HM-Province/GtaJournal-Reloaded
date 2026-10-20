<#
.SYNOPSIS
  Публикует приложение и собирает установщики Inno Setup.

.EXAMPLE
  .\installer\build.ps1 -Version 1.2.0
  .\installer\build.ps1 -Version 1.2.0 -Arch x64,arm64

  Результат: artifacts\installer\GtaJournal-Setup-<версия>-<арх>.exe
#>
param(
    [Parameter(Mandatory)] [string] $Version,
    [ValidateSet('x64', 'x86', 'arm64')] [string[]] $Arch = @('x64', 'x86', 'arm64')
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'GtaJournal\GtaJournal.csproj'
$script = Join-Path $PSScriptRoot 'GtaJournal.iss'

$iscc = @(
    (Get-Command iscc.exe -ErrorAction SilentlyContinue).Source,
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) { throw 'Не найден Inno Setup 6 (ISCC.exe): https://jrsoftware.org/isdl.php' }

foreach ($a in $Arch) {
    $platform = if ($a -eq 'arm64') { 'ARM64' } else { $a }
    $publishDir = Join-Path $root "artifacts\publish\win-$a"
    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

    Write-Host "== Публикация win-$a ($Version)" -ForegroundColor Cyan
    dotnet publish $project -c Release -r "win-$a" -p:Platform=$platform -p:Version=$Version `
        -p:SelfContained=true -p:PublishSingleFile=false -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish win-$a завершился с ошибкой" }

    Write-Host "== Установщик win-$a" -ForegroundColor Cyan
    & $iscc "/DAppVersion=$Version" "/DArch=$a" "/DSourceDir=$publishDir" $script
    if ($LASTEXITCODE -ne 0) { throw "ISCC win-$a завершился с ошибкой" }
}

Get-ChildItem (Join-Path $root 'artifacts\installer') -Filter "GtaJournal-Setup-$Version-*.exe" | Select-Object Name, Length
