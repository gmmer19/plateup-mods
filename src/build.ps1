param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$src = $PSScriptRoot
$root = Split-Path $src -Parent
$mods = Join-Path $root "Mods"

$projects = @(
    "DoorlessDining",
    "VisibleUpgradeChains",
    "HassleFreeShop",
    "LargeLayoutsOnly",
    "ExtraOptions",
    "MoreLoadingBayCarts",
    "RandomCustomerColors",
    "SoundSettingsPlus",
    "TrashToCash",
    "CountUp",
    "PatienceIndicator",
    "PlateupPrepGhost",
    "ModSettingsMenu",
    "StartingMealSelector",
    "CleaningBonus",
    "BetterTableAssignment",
    "Overstocked",
    "MoreUpgrades"
)

foreach ($project in $projects) {
    dotnet build (Join-Path $src "$project\$project.csproj") -c $Configuration -v minimal
}

New-Item -ItemType Directory -Force -Path (Join-Path $mods "00_HarmonyX") | Out-Null
Copy-Item (Join-Path $src "lib\0Harmony.dll") (Join-Path $mods "00_HarmonyX\0Harmony.dll") -Force

foreach ($project in $projects) {
    $target = Join-Path $mods $project
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Copy-Item (Join-Path $src "$project\bin\$Configuration\$project.dll") (Join-Path $target "$project.dll") -Force
}

Write-Host "Installed mods into $mods"
