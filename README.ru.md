[English](README.md) | [Русский](README.ru.md)

# PlateUp! Mods — воссоздание популярных Workshop-модов без библиотек

Коллекция модов для **PlateUp! 1.5.2 (build 24651854, Unity 2020.3.48f1,
Mono, .NET Framework 4.7.2)**, полностью воссозданных «с нуля» для
не-Steam копии игры, с которой Steam Workshop недоступен.

Каждый мод здесь — **независимая чистая реализация** известного
Workshop-мода: механика изучалась по открытым исходникам оригиналов
(где они есть), декомпилированному коду игры и описаниям, а затем
переписывалась с нуля. Проект не связан с авторами оригинальных модов.

## Ключевая идея: ни одной внешней мод-библиотеки

KitchenLib, PreferenceSystem, PlatePatch, IngredientLib и другие фреймворки
недоступны для скачивания без Workshop — и не нужны. Каждый мод этого
набора использует только:

- **Harmony 2.4.2** (`0Harmony.dll`, LGPL-3.0, включена в `src/lib/`) —
  единственная внешняя зависимость всего проекта, одна на все моды;
- **штатный загрузчик модов игры** (`KitchenMods.FolderModSource`,
  папка `Mods/<имя>/*.dll`);
- **штатную систему настроек** (`Kitchen.Preferences`, `BoolPreference`,
  `IntPreference`, `FloatPreference`) — вместо PreferenceSystem;
- **штатные классы игры** (`Kitchen.*`, `KitchenData.*`, `Kitchen.Modules`,
  ECS `Unity.Entities`) — вместо KitchenLib/PlatePatch.

Поэтому в папке `Mods/` достаточно одной папки `00_HarmonyX` с библиотекой,
а любое подменю настроек регистрируется прямо в `OptionsMenu` игры и
собирается в едином меню `Options → Моды`.

## Состав набора и паритет с оригиналами

| # | Мод (наш) | Оригинал | Паритет | Чего нет у нас | Что сверх оригинала |
|---|---|---|---|---|---|
| 1 | DoorlessDining | [Doorless Dining](https://steamcommunity.com/sharedfiles/filedetails/?id=2922289918) | Полный | — | — |
| 2 | VisibleUpgradeChains | [Visible Upgrade Chains](https://steamcommunity.com/sharedfiles/filedetails/?id=2925133454) | Почти полный | иконки и цены не показываются (только текст); секция `Upgrades` не локализована | — |
| 3 | HassleFreeShop | [Hassle Free Shop](https://steamcommunity.com/sharedfiles/filedetails/?id=2927582154) | Почти полный | weak-вариант `HeatWeakStartingAppliances`; нет переключателя вкл/выкл | — |
| 4 | LargeLayoutsOnly | [Large layouts only](https://steamcommunity.com/sharedfiles/filedetails/?id=3514078362) | Почти полный | — | если большие макеты не открыты, выбираются самые большие из открытых (не пустой выбор) |
| 5 | ExtraOptions | [Extra Options](https://steamcommunity.com/sharedfiles/filedetails/?id=2913962862) | Почти полный | совместимость с Seed Explorer | проверка занятости тайлов, нет дублей при повторном входе |
| 6 | MoreLoadingBayCarts | [More Loading bay carts](https://steamcommunity.com/sharedfiles/filedetails/?id=2911432872) | Полный | — | позиции подобраны так, что не занимают проход и декор |
| 7 | RandomCustomerColors | [Random Customer Colors](https://steamcommunity.com/sharedfiles/filedetails/?id=2961930514) | Почти полный | на клиенте мультиплеера откат к цвету «на каждого отдельно» (нет ECS-симуляции) | переключатель в общем меню; уже созданные посетители перекрашиваются сразу |
| 8 | SoundSettingsPlus | [SoundSettings+](https://steamcommunity.com/sharedfiles/filedetails/?id=2919786563) | Почти полный | — | настройки доступны и из главного меню, и из паузы; переживают `Preferences.CleanUp()` |
| 9 | TrashToCash | [TrashToCash](https://steamcommunity.com/sharedfiles/filedetails/?id=2928449724) | Почти полный | если прибор не видели в магазине, берётся `PurchaseCost` | служебные сущности цен не мешают `ApplianceInfoView` и переживают сохранение |
| 10 | CountUp | [CountUp!](https://steamcommunity.com/sharedfiles/filedetails/?id=2927654156) | Полный | — | цифра интегрируется в штатный `ProcessIcons` (тот же шрифт/материал), без отдельного world-space объекта |
| 11 | PatienceIndicator | [Patience Indicator Timer and Priority](https://steamcommunity.com/sharedfiles/filedetails/?id=3363858163) | Почти полный | конфликты с другими модами, перерисовывающими индикатор | резервная оценка времени на клиенте без мода у хоста; 4 макета, палитра, размеры шрифта |
| 12 | PlateupPrepGhost | [PlateupPrepGhost](https://steamcommunity.com/sharedfiles/filedetails/?id=2903290561) | Полный | — | заодно отключает возврат за границы ресторана (`EnforcePlayerBounds`), скорость 1x–10x |
| 13 | ModSettingsMenu | *(собственный мод, аналога в Workshop нет)* | — | — | единое меню `Options → Моды` для настроек всех модов набора |
| 14 | StartingMealSelector | [Starting Meal Selector](https://steamcommunity.com/sharedfiles/filedetails/?id=2898176672) | Частичный | выбор сида из оригинала не реализован — только блюдо | кнопка только в лобби (не в паузе забега); фиксированные блюда не трогаются |
| 15 | CleaningBonus | [Cleaning Bonus](https://steamcommunity.com/sharedfiles/filedetails/?id=3036483525) | Почти полный | проценты считаются до множителя за число игроков | продление дня с поломкой Booking Desk; бонусы только если грязь реально появлялась |
| 16 | BetterTableAssignment | [Better Table Assignment](https://steamcommunity.com/sharedfiles/filedetails/?id=2910617016) (IcedMilo, [исходники](https://github.com/UrFriendKen/PlateUpBetterTableAssignment)) | Почти полный | страницы PreferenceSystem (у нас пагинация по 4) | фолбэки имён; исключение миски питомца и стойки ожидания; переключатель вкл/выкл; возврат фокуса при листании |
| 17 | Overstocked | [Overstocked](https://steamcommunity.com/sharedfiles/filedetails/?id=3001717371) (Stonepaw, [исходники](https://github.com/Stonepaw/Overstocked)) | Почти полный | «Order Additional Crates» из меню оригинала (v2.x) | возрождённые ящики с правильной моделью (`CCrateAppliance`), автосейв после каждого пополнения/ломания |
| 18 | MoreUpgrades | [More Upgrades](https://steamcommunity.com/sharedfiles/filedetails/?id=2925086477) (Belkorin; оригинал удалён из Workshop) | Полный+ | — | два режима (цепочка/кросс) в меню; группы с кандидатами имён; калибровочные дампы в лог |

Плюс `00_HarmonyX` — [Harmony](https://github.com/pardeike/Harmony)
(HarmonyX-сборка 2.4.2 для net472), единственная библиотека набора.

## Быстрый старт

Требования: .NET SDK (для `dotnet build`), PowerShell, копия игры
PlateUp! 1.5.2.

```powershell
# сборка всех модов + установка в <игра>/Mods/
powershell -ExecutionPolicy Bypass -File .\src\build.ps1
```

Скрипт собирает все проекты из `src/` под .NET Framework 4.7.2 (ссылки на
сборки игры берутся из `PlateUp_Data/Managed`), копирует `0Harmony.dll`
в `Mods/00_HarmonyX/` и DLL каждого мода в `Mods/<Имя>/`. Загрузчик игры
читает все `.dll` из папок внутри `Mods/`; `00_HarmonyX` должен
загружаться первым — не переименовывайте его.

Если Workshop-архивы всё же удалось достать где-то ещё —
`Install-Mods.ps1` раскладывает их по именованным папкам (карта
Workshop ID → имя вшита в скрипт).

## Как это работает (по модулям)

### DoorlessDining
Harmony-префикс на `Kitchen.LayoutBuilder.BuildDoorBetween`: внутренние
двери (`is_external == false`) не создаются вообще — остаётся проём.
Внешние двери (вход, гараж) не трогаются, чтобы не ломать появление
посетителей и доступ к гаражу. Патч клиентский: в мультиплеере двери
исчезнут только у игроков с модом.

### VisibleUpgradeChains
Префикс + finalizer на `ApplianceInfoView.UpdateData`: перед отрисовкой
в `Appliance.Sections` временно добавляется секция `Upgrades` со списком
всех улучшений по цепочке (рекурсия по `Appliance.Upgrades` с защитой от
циклов), после отрисовки секция удаляется — общие данные игры не
меняются. Названия берутся из текущей локализации.

### HassleFreeShop
Три префикса на статические методы `Kitchen.PostHelpers`:
`CreateBlueprintLetter` создаёт сразу развёрнутый чертёж
(`CCreateAppliance` + `CApplianceBlueprint` + `CForSale`) вместо конверта;
`CreateApplianceParcel` — сразу appliance вместо посылки;
`CreateIngredientParcel` — сразу бесконечный источник ингредиента
(`CItemProvider.InfiniteItemProvider`). Затрагивает и стартовые посылки;
weak-вариант приборов не воспроизводится.

### LargeLayoutsOnly
Префикс на конструктор `Kitchen.LayoutSeed(int, int[])`: из списка
остаются только макеты с максимальным `LayoutProfile.MaximumTables`.
Сезонные списки не трогаются. В лог пишутся ID, имя, `MaximumTables` и
сезон каждого макета. `MaximumTables` — приблизительная метрика
«большого» макета.

### ExtraOptions
Два постфикса: на `CreateLayoutSlots.OnUpdate` (+2 `LayoutPedestal` через
приватный `CreateMapSource`) и на `CreateDishOptions.OnUpdate` (+2
`DishPedestal` через приватный `CreateFoodSource`, случайные доступные
блюда). Позиции считаются от ванильного ряда: макеты — двумя рядами выше
начиная над крайним правым слотом, блюда — над 3-м и 4-м слотами; между
рядами проход. Каждый тайл проверяется по сущностям с `CPosition` —
занятые пропускаются, дублей при повторном входе в лобби нет. Демо и
quick-load пропускаются; если свободных блюд нет — доп. пьедесталы не
создаются (в лог пишется `no spare dishes to offer`).

### MoreLoadingBayCarts
Постфикс на `CreateGarage.OnUpdate`: ещё две сущности `LoadoutPedestal`
с `CItemPedestal` слева от ванильных тележек, на ряд ближе к игроку
(`Garage + (-1,0,-3)` и `(-2,0,-3)`). Если `CItemPedestal` в гараже уже
больше двух — ничего не добавляется (защита от дублей). В теплице тележки
могут скрываться, но предметы с них всё равно попадают в забег.

### RandomCustomerColors
Постфикс на `CustomerView.UpdateData` добавляет на объект посетителя
`CustomerColorApplier`: через `EntityViewManager` и `CLinkedView` находится
ECS-сущность посетителя, читается `CBelongsToGroup.Group`, и вся группа
красится одним цветом (HSV из `Entity.Index/Version`: случайный тон,
насыщенность 0.6–1, яркость 1) в `_Color0` всех рендереров. Доп. постфикс
на `Recolour.Start` перекрашивает после ванильного тонирования.
Переключатель `Цвет по группам` в `Options → Моды`; выключение мгновенно
возвращает случайный цвет на каждого. На клиенте мультиплеера ECS-симуляции
нет — через 2 секунды откат к цвету на каждого.

### SoundSettingsPlus
Отдельные ползунки громкости для посудомойки, микроволновки, грязи, «ding»,
завершения процесса, взятия/падения предметов, шагов и дверей. Штатные
`Preferences` + `FloatPreference`; постфиксы на `SoundSource.Update` и
`HingeSoundSource.Update` умножают итоговую громкость на коэффициент;
постфикс на `Preferences.SetUpPreferences` перерегистрирует настройки
после `CleanUp()`. Меню — `Options → Моды → SoundSettings+` (и в главном
меню, и в паузе). Список звуков привязан к именам клипов текущей версии
игры; неизвестные клипы не меняются.

### TrashToCash
Система `PriceUpdater` отслеживает минимальные цены приборов из магазина
и складывает их в служебные сущности `CApplianceInfo + CImmovable` без
`CLinkedView` (не мешают `ApplianceInfoView`, переживают сохранение).
`SellAtStartOfDay` выполняется до ванильного `DestroyAppliancesAtDay`:
начисляет 50% стоимости за приборы в зоне удаления, добавляет запись в
итоговую сводку дня (`MoneyTracker.AddEvent`), цену показывает кастомный
`ApplianceSellPriceView` (жёлтые цифры с обводкой; шрифт — `GameData.Main.Fonts`
«Reference - In-world Floating Text», не `TMP_Settings.defaultFontAsset`,
который без глифов). В мультиплеере мод нужен только хосту; визуал цены —
клиентский.

### CountUp
Система `CountUpViewSystem` (`IncrementalViewSystemBase`, `IModSystem`)
следит за предметами с `CSplittableItem` и через
`MessageType.SpecificViewUpdate` отправляет данные в `CountUpItemView`.
Тот перехватывает штатный текстовый элемент `ProcessIcons` вью предмета и
дописывает к значку ножа `<sprite name="split">` число оставшихся порций —
тот же шрифт, материал, наклон и сортировка, что у штатных иконок
процессов. Постфикс на `ItemView.UpdateData` повторно применяет цифру,
потому что игра перезаписывает `ProcessIcons.text` целиком. Одноразовые
(`TotalCount == 1`) и бесконечные (`Count >= 100`) предметы фильтруются.
Отдельный `TextMeshPro` в `SplittableItemView` не работает: под-вьюшка
неактивна в префабе, TMP не проходит `Awake()` и падает на `outlineColor`.

### PatienceIndicator
Система `PatienceSecondsSystem` на хосте считает точные секунды:
`CPatience.RemainingTime * CCustomerSettings.Patience[Reason]`, для
очереди — `SQueueMarker` и `Difficulty.QueuePatienceTime`. Данные уходят
в `PatienceIndicatorView` на `CustomerIndicatorView`; если у хоста мода
нет, клиент оценивает скорость убывания по `ViewData.Patience` с
экспоненциальным сглаживанием. Таймер — отдельный TMP поверх полоски с
высоким `sortingOrder` (mesh-полоска Shapes иначе перекрывает цифры).
Раз в кадр сравниваются остатки всех видимых индикаторов: полоса лидера
красится в цвет приоритета. Настройки: макет (иконка/таймер, 4 варианта),
показ таймера, два цвета из палитры, подсветка приоритета, 4 размера
шрифта.

### PlateupPrepGhost
Префикс на `PlayerView.Update`: у локального игрока (`IsMyPlayer`)
берётся `PlayerWalkingComponent.Rigidbody` и переключается
`detectCollisions` (false в подготовке). `PlayerView.Speed` умножается на
выбранный коэффициент (1x–10x, по умолчанию 1.5x), исходное значение
запоминается. Префикс на `EnforcePlayerBounds.OnUpdate` отключает
телепорт «вышел за границы — ко входу», пока призрак активен. Условие:
`SceneType.Kitchen` и `IsPreparationTime`. Коллизии отключаются только у
локального игрока — в мультиплеере мод нужен каждому.

### ModSettingsMenu *(собственный)*
Единое меню `Options → Моды`: постфикс на `OptionsMenu.CreateSubmenus`
регистрирует `ModsMenu`, постфикс на `OptionsMenu.Setup` добавляет кнопку
«Моды» после «Доступность». Типы подменю других модов ищутся по полному
имени через `AppDomain.CurrentDomain.GetAssemblies()` — жёстких ссылок
между сборками нет, отсутствующий мод просто пропускается. Переключатели
CountUp и RandomCustomerColors используют те же `Pref`, что и сами моды,
поэтому изменения применяются мгновенно.

### StartingMealSelector
Кнопка «Стартовое блюдо» добавляется префиксом на `MainMenu.Setup`
только при `GameInfo.CurrentScene == SceneType.Franchise` (лобби); меню
регистрируется префиксом на `PlayerPauseView.SetupMenus` через `AddMenu`
(иначе клик игнорируется). Список блюд — из живых `CDishUpgrade`
(fallback `GameInfo.DishUpgrades`), имена из `GameData.Main`. Выбор
класться в `PendingDish`; `DishPedestalOverrideSystem` применяет его к
singleton `SDishPedestal` (правка `CDishChoice` или создание бумажки
`CCreateItem`). Опции: применить к лобби, случайное из открытых,
«всегда начинать с этого блюда» (сохраняется в `Pref`), сброс автоподбора.
Фиксированные блюда (`FixedDishReason != None` — франшиза, спидран,
настройка) не трогаются; ID блюд — хэши, бывают отрицательными,
валидность отсекает только 0. Выбор сида из оригинала не реализован.

### CleaningBonus
`CleaningBonusTimeSystem` (`TimeManagementGroup`, до `AdvanceTime`): когда
время дня подошло к концу, клиентов больше нет, а грязь осталась — день
продлевается на N секунд, `Booking Desk` помечается сломанным (иконка
поломки над столом брони). Как только всё убрано — день завершается.
`CleaningBonusAwardSystem` в момент `SIsNightFirstUpdate` (до
`DestroyAppliancesAtNight`/`MoneyTracker`) сравнивает баланс с
`SMoneyEarningsTracker.OldAmount`, начисляет проценты через `SMoney` и
добавляет записи `Dish Washer` / `Floor Buffer Station` / `Bin - Compactor`
в сводку. Грязная посуда считается и в мире, и внутри посудомоек/раковин
(`CItemProvider`), включая «Dirty Soaked» и грязные кастрюли; мусор — по
занятым бакам (кроме уличного Wheelie Bin) и мешкам. Дефолты: 8% посуда,
8% пол, 4% мусор, +7 секунд; настраивается 0–20% и 0–60 с (0 = выкл). Бонус
только если грязь этого типа реально появлялась за день. Не работает в
`SPracticeMode` и после Game Over.

### BetterTableAssignment
Префикс на `LocationComparer.Compare` (компаратор свободных мест в
`ResolveCustomerAssignments`): состояние → приоритет типа стола →
`MaxCapacity` по возрастанию → ванильная привлекательность. Приоритет
хранится в `IntPreference` с ключом = `CAppliance.ID` первой части
`CTableSetParts` (`int.MaxValue` = «Игнор» — сортируется последним).
Префикс на `ResolveCustomerAssignments.OnUpdate` обнуляет
`PrioritiseExactSize` у всех заданий кадра — аналог удаления
`CTablePrioritiseCorrectGroups` с барных столов в оригинале: ванильный
«первый выбор точного размера» не перебивает пользовательские приоритеты.
Тот же префикс запоминает ссылку на систему для доступа к ECS из
компаратора (финализатор снимает). Приоритеты регистрируются лениво,
после регистрации вызывается `Preferences.Load()` для восстановления
сохранённых значений. Хост-стенды (без `CTableSetParts`) сравнение
приоритетов пропускают. В меню исключены appliance с `CApplianceTable`,
но не принимающие посетителей: **Animal Bowl** (871235082, миска питомца)
и **Service Counter** (638034870, стойка ожидания; русский перевод
«Раздаточная» вводит в заблуждение) — список `NonCustomerTableIds`.
Пустые имена (нет локализации) заменяются именем Unity-ассета →
английской локализацией → именем префаба → `(ID: ...)`. Приоритет —
порядок предпочтения, а не бронирование: группа садится за первый подходящий
стол по порядку. Все «Игнор» (дефолт) = «каждой группе наименьший
подходящий стол». Выключено = полностью ванильная рассадка.

### Overstocked
Пять систем, без Harmony-патчей механики (как у оригинала):
`MarkCrateShelvesSystem` помечает полки с ящиками `CCrateItemProvider`
(фильтры: `CAppliance.ID == AssetReference.GarageShelf` (174737401) —
отсекает inbox (286909233, сам пополняется из `CCrateInbox`) и выход
мастерской; `PersistentStorageType.Crate` — отсекает теплицу).
`RespawnCratesSystem` для пустой помеченной полки создаёт ящик
(`ctx.CreateItem` с `ApplianceCrate` или кастомным `CrateItem` appliance,
+ `CCrate` + `CPersistentItem` + `CCrateAppliance` + `CUpgrade` +
`CPersistThroughSceneChanges`) через extension-методы `ItemExtensions`,
кладёт `UpdateHolder`-ом и запрашивает `CRequestSave{Auto}` — как ваниль
в `AcceptIntoInbox`. `UnmarkCrateShelvesSystem` снимает метки при
выключенном пополнении (гараж можно переставлять). `DestroyCrateInteractionSystem`
(Act, только франшиза) уничтожает ящик, сняв метку — иначе полка мгновенно
возродила бы его. `InsertCrateIntoProviderSystem` поглощает такой же
ящик, возвращаемый на помеченную полку (держатель уже занят заменой).
Пока пополнение включено, полка привязана к типу своего ящика — для
перестановки выключите пополнение. Взятие из кучи inbox не пополняет её.

### MoreUpgrades
Превращение чертежей в ваниле делает `ImproveBlueprintAfterDuration`
(«стол улучшения чертежей»): `comp.ApplianceID = output.Upgrades.Random().ID`
— выбор случайный; детерминированность ванили — это просто списки из
одного элемента. Мод в `PostInject` (GameData загружена) добавляет членам
групп связи друг на друга (add-if-missing, ванильные связи сохраняются).
Группы по именам ассетов GDO с кандидатами (первый найденный выигрывает):
плиты `Hob - Danger/Safe`; станции `Workstation/Freezer`,
`Combiner/Portioner`; столы `Table - Bar/Basic Cloth/Cheap Metal/Fancy
Cloth`; обувь `Shoe Rack - Work Boots/Trainers/Wellies`; инструменты
`Rolling Pin Provider/Sharp Knife Provider/Scrubbing Brush Provider`;
швабры `Mop Bucket - Lasting/Fast`, `Robot Mop/Robot Buffer`. Два режима:
**цепочка** (фиксированный круг, детерминированно — по умолчанию; для
столов порядок совпадает с ванильным кругом, поэтому в этом режиме столы
не трогаются вовсе — ваниль 1.5.2 уже циклит их сама) и **кросс** (любой
в любого, случайно). В лог пишется полный ванильный граф
(`vanilla: X -> [...]`), калибровочные семейства (`family '...': имя (id)`)
и результат мутации по группам. Изменения применяются после перезапуска
игры.

## Что сознательно не воссоздаётся

| Мод / библиотека | Причина |
|---|---|
| KitchenLib, PreferenceSystem, PlatePatch, IngredientLib, ApplianceLib | Фреймворки; весь набор построен так, что они не нужны |
| Kitchen Designer, MMO Kitchen, Save System, Custom Difficulty, Stuff On Demand | Высокая сложность (сеть, ассет-бандлы, ядро сейвов); возможные будущие кандидаты |
| AutomationPlus | Полная версия требует новых GDO/префабов; возможна «лайт»-версия (автостарт посудомойки, мешки из баков, приоритет взятия граббером) |
| American Breakfast, Cat Maid Outfit | Контентные моды: нужны ассеты (модели, спрайты), которых нет без Workshop |
| Colorblind+ | Пропущен по решению пользователя; часть пользы закрыта CountUp и PatienceIndicator |
| Drag N' Drop Designer | Работает только до PlateUp! 1.1.9 |
| Modded Cosmetics Integration | Бесполезен без косметики других модов |

## Общие ограничения

- **Мультиплеер:** серверная логика (деньги, рассадка, пополнение,
  апгрейд-пулы) выполняется на хосте — моды достаточно ставить хосту;
  визуальные эффекты (цвета, таймеры, цены) видят только игроки с модом.
- **Версия игры:** всё написано и проверено против 1.5.2-7473;
  имена ассетов в MoreUpgrades и списки звуков в SoundSettingsPlus
  привязаны к этой версии (калибровка — по дампам в логе).
- **Настройки** хранятся в штатных `Kitchen.Preferences` игры
  (переживают перезапуск, синхронизируются платформой).
- Мутация `Appliance.Upgrades` выполняется при каждой загрузке игры —
  выключение группы убирает связи со следующего запуска.

## Диагностика

Лог игры: `%USERPROFILE%\AppData\LocalLow\It's Happening\PlateUp\Player.log`.

Признаки нормальной работы: `Loaded N mods...`, `Injecting ...`, строки
`[ИмяМода] ...` без стека исключений. Тревожные строки: `Failed to
activate ModPack`, `HarmonyException`, `NullReferenceException` /
`MissingMethodException` после загрузки модов.

Не ошибки: `Fallback handler could not load library ... data-*.dll`
(шум Mono при загрузке сборок из памяти) и `ResultException:
InternalError` от Discord (несвязанно с модами).

## Структура репозитория

```
├─ src/                    ← исходники всех модов
│  ├─ Directory.Build.props ← общие ссылки на сборки игры
│  ├─ lib/0Harmony.dll     ← Harmony 2.4.2 (net472)
│  ├─ build.ps1            ← сборка + установка в Mods/
│  └─ <Мод>/<Мод>.csproj + .cs
├─ Install-Mods.ps1        ← установка модов из Workshop-архивов (если есть)
├─ Mods.txt                ← ссылки на оригиналы в Steam Workshop
├─ README.md
└─ LICENSE                 ← MIT (кроме src/lib/0Harmony.dll — LGPL-3.0)
```

Собранные моды в репозиторий не входят — они артефакты сборки и
восстанавливаются `src/build.ps1`. Папка `Mods/` игры должна содержать
`00_HarmonyX/` первой по алфавиту.

## Дорожная карта

- **AutomationPlus (лайт)** — автостарт посудомойки, мешки из баков,
  приоритет взятия граббером.
- Сложные направления: `Custom Difficulty`, `Save System`, `MMO Kitchen`,
  `Kitchen Designer`, `Stuff On Demand`.

## Источники и благодарности

- [It's Happening](https://store.steampowered.com/app/1599600/PlateUp/) —
  за игру и встроенный загрузчик модов.
- Авторам оригинальных модов — за идеи и открытые исходники:
  IcedMilo ([Better Table Assignment](https://github.com/UrFriendKen/PlateUpBetterTableAssignment),
  [AutomationPlus](https://steamcommunity.com/sharedfiles/filedetails/?id=2922412179)),
  Stonepaw ([Overstocked](https://github.com/Stonepaw/Overstocked)),
  Belkorin (More Upgrades) и всем остальным авторам из `Mods.txt`.
- [Harmony](https://github.com/pardeike/Harmony) (LGPL-3.0) — за
  runtime-патчинг, на котором стоит весь набор.

## Лицензия

Код этого репозитория — [MIT](LICENSE). `src/lib/0Harmony.dll` —
Harmony 2.4.2, поставляется как есть под LGPL-3.0. Проект не связан с
разработчиками игры и авторами оригинальных модов; PlateUp! — товарный
знак It's Happening.
