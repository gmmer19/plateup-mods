param(
    [Parameter(Mandatory = $true)]
    [string]$Source,
    [string]$Destination = (Join-Path $PSScriptRoot 'Mods'),
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

# Workshop ID -> normal mod name (all 34 mods from Mods.txt)
$map = @{
    '2898033283' = 'HarmonyX'
    '2898069883' = 'KitchenLib'
    '2949018507' = 'PreferenceSystem'
    '3306089551' = 'PlatePatch'
    '2901012380' = 'Kitchen Designer'
    '2913877103' = 'IngredientLib'
    '2924082510' = 'Cat Maid Outfit'
    '2922289918' = 'Doorless Dining'
    '2934088024' = 'ApplianceLib'
    '2901036336' = 'MMO Kitchen'
    '3013660590' = 'Modded Cosmetics Integration'
    '2927654156' = 'CountUp!'
    '2933854869' = 'American Breakfast'
    '2928449724' = 'TrashToCash'
    '2898176672' = 'Starting Meal Selector'
    '2925133454' = 'Visible Upgrade Chains'
    '2938492456' = 'Drag N Drop Designer'
    '2903290561' = 'PlateupPrepGhost'
    '2913962862' = 'Extra Options'
    '2922412179' = 'AutomationPlus'
    '3036483525' = 'Cleaning Bonus'
    '2927582154' = 'Hassle Free Shop'
    '2910617016' = 'Better Table Assignment'
    '3001717371' = 'Overstocked'
    '3363858163' = 'Patience Indicator Timer and Priority'
    '2911150641' = 'Colorblind+'
    '2900092542' = 'Save System'
    '2911432872' = 'More Loading bay carts'
    '2925086477' = 'More Upgrades'
    '2961930514' = 'Random Customer Colors'
    '2909998068' = 'Custom Difficulty'
    '2919786563' = 'SoundSettings+'
    '2956266506' = 'Stuff On Demand'
    '3514078362' = 'Large layouts only'
}

function Resolve-WorkshopRoot([string]$path) {
    $candidates = @(
        $path,
        (Join-Path $path '1599600'),
        (Join-Path $path 'content\1599600'),
        (Join-Path $path 'steamapps\workshop\content\1599600')
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) {
            $idDirs = Get-ChildItem $c -Directory -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -match '^\d+$' }
            if ($idDirs) { return $c }
        }
    }
    return $null
}

$tempExtract = $null
if ($Source -match '\.zip$') {
    if (-not (Test-Path $Source)) { throw "Archive not found: $Source" }
    $tempExtract = Join-Path $env:TEMP ("plateup_mods_" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $tempExtract | Out-Null
    Write-Host "Extracting $Source ..."
    Expand-Archive -Path $Source -DestinationPath $tempExtract -Force
    $Source = $tempExtract
}

$root = Resolve-WorkshopRoot $Source
if (-not $root) {
    throw "Could not find workshop item folders. Expected <Source>\1599600\<ID> or <Source>\<ID>."
}

if (-not (Test-Path $Destination)) {
    New-Item -ItemType Directory -Path $Destination | Out-Null
}

$items = Get-ChildItem $root -Directory | Where-Object { $_.Name -match '^\d+$' }
if (-not $items) { throw "No numeric workshop item folders found in $root" }

$ok = 0; $warn = 0; $skip = 0
foreach ($item in $items) {
    $id = $item.Name
    $name = $map[$id]
    if (-not $name) { $name = "Workshop_$id" }
    $name = $name -replace '[\\/:*?"<>|]', '_'
    $target = Join-Path $Destination $name

    if (Test-Path $target) {
        if (-not $Force) {
            Write-Host "SKIP  $name (already exists, use -Force to overwrite)" -ForegroundColor Yellow
            $skip++
            continue
        }
        Remove-Item $target -Recurse -Force
    }

    Copy-Item $item.FullName $target -Recurse -Force
    $files = Get-ChildItem $target -Recurse -File -Include *.dll, *.assets
    if ($files) {
        Write-Host "OK    $name  ($($files.Count) dll/assets)" -ForegroundColor Green
        $ok++
    }
    else {
        Write-Warning "      $name: no .dll/.assets found (check structure)"
        $warn++
    }
}

if ($tempExtract) { Remove-Item $tempExtract -Recurse -Force -ErrorAction SilentlyContinue }

Write-Host ""
Write-Host "Done. Installed: $ok, warnings: $warn, skipped: $skip"
Write-Host "Mods folder: $Destination"
Write-Host "Launch the game, then check: $env:USERPROFILE\AppData\LocalLow\It's Happening\PlateUp\Player.log"
