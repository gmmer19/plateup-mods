[English](README.md) | [Русский](README.ru.md)

# PlateUp! Mods

A collection of clean-room recreations of popular PlateUp! Workshop mods for **PlateUp! 1.5.2 (build 24651854, Unity 2020.3.48f1, Mono, .NET Framework 4.7.2)**. The project is designed for game installations where Steam Workshop is unavailable.

Each mod is an independent implementation. Where available, the original mods' public source code, the game's decompiled code, and published descriptions were used to understand the mechanics; the implementations here were then written from scratch. This project is not affiliated with the creators of the original mods.

## No third-party mod frameworks

KitchenLib, PreferenceSystem, PlatePatch, IngredientLib, and similar frameworks are not required. Every mod in this collection uses only:

- **Harmony 2.4.2** (<code>0Harmony.dll</code>, LGPL-3.0, included in <code>src/lib/</code>) — the project's only external dependency, shared by all mods.
- **The game's built-in mod loader** (<code>KitchenMods.FolderModSource</code>, loading DLLs from <code>Mods/&lt;mod name&gt;/*.dll</code>).
- **The game's built-in preferences API** (<code>Kitchen.Preferences</code>, <code>BoolPreference</code>, <code>IntPreference</code>, and <code>FloatPreference</code>) instead of PreferenceSystem.
- **Vanilla game types** (<code>Kitchen.*</code>, <code>KitchenData.*</code>, <code>Kitchen.Modules</code>, and the <code>Unity.Entities</code> ECS) instead of KitchenLib or PlatePatch.

Only one shared Harmony folder is needed in the game's <code>Mods/</code> directory: <code>00_HarmonyX</code>. Each mod registers its settings submenu directly with the game's <code>OptionsMenu</code>; the collection's settings are grouped under <code>Options → Mods</code>.

## Included mods and feature parity

“Parity” compares this implementation with the linked Workshop mod. The “Limitations” column calls out known differences; the final column lists additions made in this project.

| # | Mod in this collection | Original Workshop mod | Parity | Limitations | Additional features |
|---|---|---|---|---|---|
| 1 | DoorlessDining | [Doorless Dining](https://steamcommunity.com/sharedfiles/filedetails/?id=2922289918) | Full | — | — |
| 2 | VisibleUpgradeChains | [Visible Upgrade Chains](https://steamcommunity.com/sharedfiles/filedetails/?id=2925133454) | Mostly complete | Upgrade icons and prices are not shown (text only); the <code>Upgrades</code> section is not localized. | — |
| 3 | HassleFreeShop | [Hassle Free Shop](https://steamcommunity.com/sharedfiles/filedetails/?id=2927582154) | Mostly complete | Does not reproduce the weak <code>HeatWeakStartingAppliances</code> variant; no enable/disable toggle. | — |
| 4 | LargeLayoutsOnly | [Large layouts only](https://steamcommunity.com/sharedfiles/filedetails/?id=3514078362) | Mostly complete | — | If no large layouts are unlocked, selects the largest unlocked layouts instead of returning an empty selection. |
| 5 | ExtraOptions | [Extra Options](https://steamcommunity.com/sharedfiles/filedetails/?id=2913962862) | Mostly complete | No compatibility with Seed Explorer. | Checks whether tiles are occupied and avoids duplicate pedestals when re-entering the lobby. |
| 6 | MoreLoadingBayCarts | [More Loading bay carts](https://steamcommunity.com/sharedfiles/filedetails/?id=2911432872) | Full | — | Cart positions avoid blocking paths and decorations. |
| 7 | RandomCustomerColors | [Random Customer Colors](https://steamcommunity.com/sharedfiles/filedetails/?id=2961930514) | Mostly complete | Multiplayer clients fall back to per-customer colors because they do not run the ECS simulation. | Shared settings-menu toggle; existing customers are recolored immediately. |
| 8 | SoundSettingsPlus | [SoundSettings+](https://steamcommunity.com/sharedfiles/filedetails/?id=2919786563) | Mostly complete | — | Settings are available from both the main menu and pause menu, and survive <code>Preferences.CleanUp()</code>. |
| 9 | TrashToCash | [TrashToCash](https://steamcommunity.com/sharedfiles/filedetails/?id=2928449724) | Mostly complete | Uses <code>PurchaseCost</code> if an appliance has not appeared in the shop. | Internal price entities do not interfere with <code>ApplianceInfoView</code> and survive saving. |
| 10 | CountUp | [CountUp!](https://steamcommunity.com/sharedfiles/filedetails/?id=2927654156) | Full | — | The count is integrated into the built-in <code>ProcessIcons</code> text (same font and material), without a separate world-space object. |
| 11 | PatienceIndicator | [Patience Indicator Timer and Priority](https://steamcommunity.com/sharedfiles/filedetails/?id=3363858163) | Mostly complete | May conflict with other mods that redraw the customer indicator. | Client-side time estimate when the host does not have the mod; four layouts, a color palette, and font-size options. |
| 12 | PlateupPrepGhost | [PlateupPrepGhost](https://steamcommunity.com/sharedfiles/filedetails/?id=2903290561) | Full | — | Also disables the restaurant-boundary teleport (<code>EnforcePlayerBounds</code>); movement speed can be set from 1x to 10x. |
| 13 | ModSettingsMenu | Custom mod (no Workshop equivalent) | — | — | A single <code>Options → Mods</code> menu for the settings of every mod in this collection. |
| 14 | StartingMealSelector | [Starting Meal Selector](https://steamcommunity.com/sharedfiles/filedetails/?id=2898176672) | Partial | The original's seed selection is not implemented; this version selects a dish only. | The button is available in the lobby, not during a run; fixed dishes are left unchanged. |
| 15 | CleaningBonus | [Cleaning Bonus](https://steamcommunity.com/sharedfiles/filedetails/?id=3036483525) | Mostly complete | Percentages are calculated before the player-count multiplier. | Extends the day and temporarily breaks the Booking Desk; awards a bonus only if that type of mess actually appeared. |
| 16 | BetterTableAssignment | [Better Table Assignment](https://steamcommunity.com/sharedfiles/filedetails/?id=2910617016) (IcedMilo; [source code](https://github.com/UrFriendKen/PlateUpBetterTableAssignment)) | Mostly complete | PreferenceSystem-style pages are not reproduced; this version paginates four tables at a time. | Name fallbacks; excludes the Animal Bowl and Service Counter; enable/disable toggle; restores menu focus after paging. |
| 17 | Overstocked | [Overstocked](https://steamcommunity.com/sharedfiles/filedetails/?id=3001717371) (Stonepaw; [source code](https://github.com/Stonepaw/Overstocked)) | Mostly complete | Does not include the original v2.x “Order Additional Crates” menu action. | Respawned crates use the correct model (<code>CCrateAppliance</code>); saves automatically after each restock or crate destruction. |
| 18 | MoreUpgrades | [More Upgrades](https://steamcommunity.com/sharedfiles/filedetails/?id=2925086477) (Belkorin; original removed from Workshop) | Full+ | — | Two modes (chain/cross) in the settings menu; upgrade groups with candidate asset names; calibration dumps in the log. |

The collection also includes <code>00_HarmonyX</code>, a Harmony 2.4.2 build for <code>net472</code> ([Harmony](https://github.com/pardeike/Harmony), LGPL-3.0). It is the only bundled library.

## Quick start

Requirements: .NET SDK (for <code>dotnet build</code>), PowerShell, and a PlateUp! 1.5.2 installation.

Build all mods and install them into the game's <code>Mods/</code> folder:

    powershell -ExecutionPolicy Bypass -File .\src\build.ps1

The script builds every project under <code>src/</code> for .NET Framework 4.7.2. Game assembly references are read from <code>PlateUp_Data/Managed</code>. It copies <code>0Harmony.dll</code> to <code>Mods/00_HarmonyX/</code> and each mod DLL to its own <code>Mods/&lt;ModName&gt;/</code> folder.

The game loader reads all <code>.dll</code> files in folders under <code>Mods/</code>. <code>00_HarmonyX</code> must load first, so **do not rename that folder**.

If you have Workshop item archives from another source, <code>Install-Mods.ps1</code> can arrange them into named folders. Its Workshop ID-to-folder-name mapping is built into the script.

    powershell -ExecutionPolicy Bypass -File .\Install-Mods.ps1 -Source <path-to-workshop-folder-or-zip> -Destination .\Mods

Add <code>-Force</code> to overwrite existing destination folders.

## Implementation notes

### DoorlessDining

A Harmony prefix on <code>Kitchen.LayoutBuilder.BuildDoorBetween</code> prevents internal doors (<code>is_external == false</code>) from being created, leaving an open doorway. Exterior doors (the entrance and garage) are untouched so customer spawning and garage access continue to work. The patch is client-side: in multiplayer, only players with the mod installed see the missing doors.

### VisibleUpgradeChains

A prefix and finalizer on <code>ApplianceInfoView.UpdateData</code> temporarily add an <code>Upgrades</code> section to <code>Appliance.Sections</code> before the appliance information is drawn. The section lists every upgrade in the chain by recursively traversing <code>Appliance.Upgrades</code>, with cycle protection. The temporary section is removed after drawing, so shared game data is not modified. Names come from the game's current localization.

### HassleFreeShop

Three prefixes on static <code>Kitchen.PostHelpers</code> methods replace shop parcels with their contents immediately:

- <code>CreateBlueprintLetter</code> creates an already-unwrapped blueprint (<code>CCreateAppliance</code> + <code>CApplianceBlueprint</code> + <code>CForSale</code>) instead of an envelope.
- <code>CreateApplianceParcel</code> creates the appliance directly instead of a parcel.
- <code>CreateIngredientParcel</code> creates an infinite ingredient source (<code>CItemProvider.InfiniteItemProvider</code>).

This also affects starting parcels. The weak appliance variant is not reproduced.

### LargeLayoutsOnly

A prefix on the <code>Kitchen.LayoutSeed(int, int[])</code> constructor keeps only layouts with the highest <code>LayoutProfile.MaximumTables</code>. Seasonal lists are left alone. The log records each layout's ID, name, <code>MaximumTables</code>, and season. <code>MaximumTables</code> is an approximate measure of how large a layout is.

### ExtraOptions

Two postfixes add two extra layout pedestals and two extra dish pedestals: <code>CreateLayoutSlots.OnUpdate</code> uses the private <code>CreateMapSource</code>, while <code>CreateDishOptions.OnUpdate</code> uses the private <code>CreateFoodSource</code> to offer random unlocked dishes.

The new pedestals are positioned relative to the vanilla row. Layouts appear two rows above it, starting over the rightmost slot; dishes appear above slots three and four. An aisle is left between the rows. Every tile is checked against entities with <code>CPosition</code>; occupied tiles are skipped, and re-entering the lobby does not create duplicates. Demo mode and quick-load are skipped. If there are no spare dishes, no extra dish pedestals are created (the log reports <code>no spare dishes to offer</code>).

### MoreLoadingBayCarts

A postfix on <code>CreateGarage.OnUpdate</code> adds two more <code>LoadoutPedestal</code> entities with <code>CItemPedestal</code> to the left of the vanilla carts, one row closer to the player (<code>Garage + (-1,0,-3)</code> and <code>(-2,0,-3)</code>). If the garage already contains more than two <code>CItemPedestal</code> entities, nothing is added, preventing duplicates. In the greenhouse, the carts may be hidden, but their items are still included in the run.

### RandomCustomerColors

A postfix on <code>CustomerView.UpdateData</code> adds a <code>CustomerColorApplier</code> to each customer object. It locates the customer's ECS entity through <code>EntityViewManager</code> and <code>CLinkedView</code>, reads <code>CBelongsToGroup.Group</code>, and assigns one color to the entire group. The color is generated from <code>Entity.Index/Version</code> in HSV (random hue, saturation 0.6–1, value 1) and applied to <code>_Color0</code> on every renderer. An additional postfix on <code>Recolour.Start</code> reapplies the color after vanilla tinting.

The <code>Group colors</code> toggle is in <code>Options → Mods</code>. Turning it off immediately restores a random color per customer. Multiplayer clients do not run the ECS simulation, so their colors fall back to per-customer random colors after two seconds.

### SoundSettingsPlus

Separate volume sliders are provided for the dishwasher, microwave, mess, “ding”, process completion, item pickup/drop, footsteps, and doors. The mod uses the built-in <code>Preferences</code> API and <code>FloatPreference</code>. Postfixes on <code>SoundSource.Update</code> and <code>HingeSoundSource.Update</code> multiply the final volume by the configured coefficient. A postfix on <code>Preferences.SetUpPreferences</code> re-registers the settings after <code>CleanUp()</code>.

The menu is at <code>Options → Mods → SoundSettings+</code>, both in the main menu and while paused. The sound list is tied to the clip names in the current game version; unknown clips are not modified.

### TrashToCash

The <code>PriceUpdater</code> system tracks the lowest shop prices for appliances and stores them in helper entities with <code>CApplianceInfo + CImmovable</code>, but no <code>CLinkedView</code>. These entities do not interfere with <code>ApplianceInfoView</code> and survive saving.

<code>SellAtStartOfDay</code> runs before the vanilla <code>DestroyAppliancesAtDay</code> system. It awards 50% of the value of appliances in the removal area and adds an entry to the daily summary through <code>MoneyTracker.AddEvent</code>. The price is shown by a custom <code>ApplianceSellPriceView</code> as yellow outlined text. It uses <code>GameData.Main.Fonts</code> (“Reference - In-world Floating Text”), not <code>TMP_Settings.defaultFontAsset</code>, which lacks the required glyphs. In multiplayer, only the host needs the mod; the price visual is client-side.

### CountUp

The <code>CountUpViewSystem</code> (<code>IncrementalViewSystemBase</code>, <code>IModSystem</code>) tracks items with <code>CSplittableItem</code> and sends data to <code>CountUpItemView</code> through <code>MessageType.SpecificViewUpdate</code>.

The view intercepts the item's built-in <code>ProcessIcons</code> text and appends the remaining portion count next to the knife icon (<code>&lt;sprite name="split"&gt;</code>). It uses the same font, material, angle, and sorting as the game's process icons. A postfix on <code>ItemView.UpdateData</code> reapplies the count because the game overwrites <code>ProcessIcons.text</code> in full. Single-use items (<code>TotalCount == 1</code>) and effectively infinite items (<code>Count &gt;= 100</code>) are filtered out.

A separate <code>TextMeshPro</code> in <code>SplittableItemView</code> does not work: the child view is inactive in the prefab, TMP never runs <code>Awake()</code>, and accessing <code>outlineColor</code> throws.

### PatienceIndicator

On the host, <code>PatienceSecondsSystem</code> calculates exact seconds from <code>CPatience.RemainingTime * CCustomerSettings.Patience[Reason]</code>. For queues, it uses <code>SQueueMarker</code> and <code>Difficulty.QueuePatienceTime</code>. The data is sent to <code>PatienceIndicatorView</code> on <code>CustomerIndicatorView</code>. If the host does not have the mod, the client estimates the remaining time from the rate of change in <code>ViewData.Patience</code>, using exponential smoothing.

The timer is a separate TMP element over the patience bar with a high <code>sortingOrder</code>; otherwise, the Shapes mesh bar would cover the digits. Each frame, all visible indicators are compared and the bar with the least remaining patience is highlighted using the priority color.

Settings include four layouts (icon/timer combinations), timer visibility, two palette colors, priority highlighting, and four font sizes.

### PlateupPrepGhost

A prefix on <code>PlayerView.Update</code> gets the local player's (<code>IsMyPlayer</code>) <code>PlayerWalkingComponent.Rigidbody</code> and toggles <code>detectCollisions</code> (off during preparation). <code>PlayerView.Speed</code> is multiplied by the selected factor (1x–10x; default 1.5x), with the original value preserved.

A prefix on <code>EnforcePlayerBounds.OnUpdate</code> disables the “leave the restaurant and get teleported back to the entrance” behavior while ghost mode is active. It only applies in <code>SceneType.Kitchen</code> during <code>IsPreparationTime</code>. Collisions are disabled only for the local player, so every player needs the mod in multiplayer.

### ModSettingsMenu

This custom mod adds the shared <code>Options → Mods</code> menu. A postfix on <code>OptionsMenu.CreateSubmenus</code> registers <code>ModsMenu</code>; a postfix on <code>OptionsMenu.Setup</code> adds a “Mods” button after “Accessibility”.

Other mods' submenu types are discovered by their fully qualified names through <code>AppDomain.CurrentDomain.GetAssemblies()</code>. There are no hard assembly references between mods, and a missing mod is simply skipped. The CountUp and RandomCustomerColors toggles use the same preference objects as the mods themselves, so changes take effect immediately.

### StartingMealSelector

A “Starting Dish” button is added through a prefix on <code>MainMenu.Setup</code>, only when <code>GameInfo.CurrentScene == SceneType.Franchise</code> (the lobby). The submenu is registered through a prefix on <code>PlayerPauseView.SetupMenus</code> using <code>AddMenu</code>; without this, the button click is ignored.

The dish list comes from live <code>CDishUpgrade</code> entities, with <code>GameInfo.DishUpgrades</code> as a fallback. Names come from <code>GameData.Main</code>. The selection is stored in <code>PendingDish</code>; <code>DishPedestalOverrideSystem</code> applies it to the singleton <code>SDishPedestal</code> by updating <code>CDishChoice</code> or creating a <code>CCreateItem</code> paper.

Options include applying the selection to the lobby, choosing a random unlocked dish, always starting with the selected dish (saved in preferences), and resetting automatic selection. Fixed dishes (<code>FixedDishReason != None</code>, such as franchise, speedrun, or settings modes) are not changed. Dish IDs are hashes and may be negative; only zero is treated as invalid. Seed selection from the original mod is not implemented.

### CleaningBonus

<code>CleaningBonusTimeSystem</code> (<code>TimeManagementGroup</code>, before <code>AdvanceTime</code>) extends the day by a configurable number of seconds when the day timer expires, no customers remain, and mess is still present. The Booking Desk is marked as broken (a broken icon appears above it). The day ends as soon as all mess is cleaned.

At <code>SIsNightFirstUpdate</code>, <code>CleaningBonusAwardSystem</code> runs before <code>DestroyAppliancesAtNight</code> and <code>MoneyTracker</code>. It compares the balance with <code>SMoneyEarningsTracker.OldAmount</code>, awards the configured percentages through <code>SMoney</code>, and adds <code>Dish Washer</code>, <code>Floor Buffer Station</code>, and <code>Bin - Compactor</code> entries to the daily summary.

Dirty dishes are counted both in the world and inside dishwashers/sinks (<code>CItemProvider</code>), including “Dirty Soaked” items and dirty pots. Trash is counted from occupied bins (except the outdoor Wheelie Bin) and trash bags. Defaults are 8% for dishes, 8% for floors, 4% for trash, and 7 extra seconds. Percentages can be set from 0–20%; extra time can be set from 0–60 seconds (0 disables the option). A bonus is awarded only if that type of mess appeared during the day. The mod does not work in <code>SPracticeMode</code> or after Game Over.

### BetterTableAssignment

A prefix on <code>LocationComparer.Compare</code> (the free-seat comparer used by <code>ResolveCustomerAssignments</code>) sorts candidate tables by status, configured table-type priority, ascending <code>MaxCapacity</code>, and then vanilla attractiveness. Priority is stored in an <code>IntPreference</code> keyed by the first part's <code>CAppliance.ID</code> in <code>CTableSetParts</code>. <code>int.MaxValue</code> means “Ignore” and sorts last.

A prefix on <code>ResolveCustomerAssignments.OnUpdate</code> clears <code>PrioritiseExactSize</code> on all assignments for the current frame. This is equivalent to removing <code>CTablePrioritiseCorrectGroups</code> from bar tables in the original mod: vanilla's “exact party size first” preference must not override user priorities. The same prefix stores a reference to the system so the comparer can access ECS; a finalizer clears it.

Priorities are registered lazily, followed by <code>Preferences.Load()</code> to restore saved values. Host stands without <code>CTableSetParts</code> are skipped. The menu excludes appliances with <code>CApplianceTable</code> that do not accept customers: **Animal Bowl** (871235082) and **Service Counter** (638034870), listed in <code>NonCustomerTableIds</code>.

Missing localized names fall back to the Unity asset name, then the English localization, then the prefab name, and finally <code>(ID: ...)</code>. Priority controls preference order, not reservations: each group takes the first suitable table in the sorted list. When every table is set to “Ignore” (the default), each group gets the smallest suitable table. Disabling the mod restores fully vanilla seating behavior.

### Overstocked

Five systems implement the mechanics without Harmony patches, following the original mod's approach.

- <code>MarkCrateShelvesSystem</code> marks shelves with crate providers (<code>CCrateItemProvider</code>). It filters for <code>CAppliance.ID == AssetReference.GarageShelf</code> (174737401), excluding the inbox (286909233, which is restocked by <code>CCrateInbox</code>) and the workshop exit. <code>PersistentStorageType.Crate</code> excludes the greenhouse.
- <code>RespawnCratesSystem</code> creates a crate on an empty marked shelf using <code>ctx.CreateItem</code> with <code>ApplianceCrate</code> or a custom crate-item appliance, plus <code>CCrate</code>, <code>CPersistentItem</code>, <code>CCrateAppliance</code>, <code>CUpgrade</code>, and <code>CPersistThroughSceneChanges</code>. It uses <code>ItemExtensions</code>, places the item through <code>UpdateHolder</code>, and requests <code>CRequestSave{Auto}</code>, like vanilla <code>AcceptIntoInbox</code>.
- <code>UnmarkCrateShelvesSystem</code> removes shelf markers when restocking is disabled, allowing the garage to be rearranged.
- <code>DestroyCrateInteractionSystem</code> destroys the crate on the targeted shelf when the Act key is pressed (franchise only), removing the marker first so the shelf does not immediately respawn it.
- <code>InsertCrateIntoProviderSystem</code> consumes a matching crate returned to a marked shelf when the holder is already occupied by the replacement.

While restocking is enabled, a shelf is bound to its current crate type. Disable restocking before rearranging crate types. Taking a crate from the inbox pile does not restock the inbox.

### MoreUpgrades

Vanilla blueprint upgrades are handled by <code>ImproveBlueprintAfterDuration</code> (the blueprint upgrade desk): <code>comp.ApplianceID = output.Upgrades.Random().ID</code> selects a random result. Vanilla behavior is deterministic only because its lists contain a single entry.

During <code>PostInject</code> (after GameData is loaded), the mod adds missing links between members of each configured upgrade group while preserving vanilla links. Groups are identified by GDO asset names, using the first matching candidate:

- Hobs: <code>Hob - Danger/Safe</code>
- Stations: <code>Workstation/Freezer</code>, <code>Combiner/Portioner</code>
- Tables: <code>Table - Bar/Basic Cloth/Cheap Metal/Fancy Cloth</code>
- Shoes: <code>Shoe Rack - Work Boots/Trainers/Wellies</code>
- Tools: <code>Rolling Pin Provider/Sharp Knife Provider/Scrubbing Brush Provider</code>
- Mops: <code>Mop Bucket - Lasting/Fast</code>, <code>Robot Mop/Robot Buffer</code>

Two modes are available: **Chain** creates a fixed, deterministic cycle (the default). The table order already matches the vanilla cycle in PlateUp! 1.5.2, so tables are left untouched in this mode. **Cross** allows any member of a group to upgrade into any other member, chosen at random.

The log records the full vanilla upgrade graph (<code>vanilla: X -&gt; [...]</code>), calibration families (<code>family '...': name (id)</code>), and the resulting mutations by group. Changes take effect after restarting the game.

## Mods intentionally not recreated

| Mod / library | Reason |
|---|---|
| KitchenLib, PreferenceSystem, PlatePatch, IngredientLib, ApplianceLib | Frameworks; this collection is designed not to need them. |
| Kitchen Designer, MMO Kitchen, Save System, Custom Difficulty, Stuff On Demand | High complexity (networking, asset bundles, save-system internals); possible future candidates. |
| AutomationPlus | The full version requires new GDOs/prefabs. A limited version may be possible (automatic dishwasher start, bags from bins, grabber pickup priority). |
| American Breakfast, Cat Maid Outfit | Content mods that require assets (models and sprites) unavailable without Workshop. |
| Colorblind+ | Deliberately skipped; some of its value is covered by CountUp and PatienceIndicator. |
| Drag N' Drop Designer | Only works up to PlateUp! 1.1.9. |
| Modded Cosmetics Integration | Not useful without cosmetics from other mods. |

## General limitations

- **Multiplayer:** Host-side logic (money, seating, restocking, and upgrade pools) runs on the host, so those mods only need to be installed by the host. Visual effects (colors, timers, and prices) are visible only to players who have the relevant mod installed.
- **Game version:** The project targets and has been checked against 1.5.2-7473. MoreUpgrades asset names and SoundSettingsPlus clip lists are tied to this version; calibration data is written to the log.
- **Settings:** Stored through the game's built-in <code>Kitchen.Preferences</code> system and persist across restarts.
- **Upgrade pools:** <code>Appliance.Upgrades</code> is modified each time the game starts. Disabling a group removes its links on the next launch.

## Troubleshooting

Game log:

<code>%USERPROFILE%\AppData\LocalLow\It's Happening\PlateUp\Player.log</code>

Signs of normal operation include <code>Loaded N mods...</code>, <code>Injecting ...</code>, and lines beginning with a mod name such as <code>[ModName] ...</code>, without exception stack traces.

Investigate messages such as <code>Failed to activate ModPack</code>, <code>HarmonyException</code>, or <code>NullReferenceException</code> / <code>MissingMethodException</code> after the mods load.

These messages are not necessarily mod errors:

- <code>Fallback handler could not load library ... data-*.dll</code> — Mono noise while loading in-memory assemblies.
- <code>ResultException: InternalError</code> from Discord — unrelated to the mods.

## Repository layout

    .
    ├─ src/                     # Mod source code
    │  ├─ Directory.Build.props # Shared references to game assemblies
    │  ├─ lib/0Harmony.dll      # Harmony 2.4.2 (net472)
    │  ├─ build.ps1             # Build and install all mods
    │  └─ <Mod>/<Mod>.csproj + .cs
    ├─ Install-Mods.ps1         # Arrange extracted Workshop archives (optional)
    ├─ Mods.txt                 # Links to original Steam Workshop items
    ├─ README.md                # English documentation
    ├─ README.ru.md             # Russian documentation
    └─ LICENSE                  # MIT (except src/lib/0Harmony.dll, LGPL-3.0)

Compiled mod DLLs are not committed; they are build artifacts produced by <code>src/build.ps1</code>. The game's <code>Mods/</code> directory must contain <code>00_HarmonyX/</code>, which sorts first alphabetically.

## Roadmap

- **AutomationPlus (limited version):** automatic dishwasher start, bags from bins, and grabber pickup priority.
- More complex possibilities: <code>Custom Difficulty</code>, <code>Save System</code>, <code>MMO Kitchen</code>, <code>Kitchen Designer</code>, and <code>Stuff On Demand</code>.

## Sources and credits

- [It's Happening](https://store.steampowered.com/app/1599600/PlateUp/) for the game and its built-in mod loader.
- The original mod creators for their ideas and public source code:
  - IcedMilo — [Better Table Assignment](https://github.com/UrFriendKen/PlateUpBetterTableAssignment) and [AutomationPlus](https://steamcommunity.com/sharedfiles/filedetails/?id=2922412179)
  - Stonepaw — [Overstocked](https://github.com/Stonepaw/Overstocked)
  - Belkorin — More Upgrades
  - All other creators linked in <code>Mods.txt</code>.
- [Harmony](https://github.com/pardeike/Harmony) (LGPL-3.0) for the runtime patching library used throughout the collection.

## License

The code in this repository is licensed under [MIT](LICENSE). <code>src/lib/0Harmony.dll</code> is Harmony 2.4.2 and is distributed under LGPL-3.0, as-is. This project is not affiliated with the game developers or the creators of the original mods. PlateUp! is a trademark of It's Happening.
