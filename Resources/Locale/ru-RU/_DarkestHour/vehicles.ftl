dh-vehicle-entering = Вы забираетесь в { $vehicle }...
dh-vehicle-runover = Вас сбивает { $vehicle }!
dh-vehicle-station-sit = Сначала сядьте в кресло у этого пульта.
dh-vehicle-station-occupied = За этим пультом уже кто-то работает.
dh-vehicle-station-taken = Вы берёте управление: { $station }. Чтобы отпустить — нажмите на пульт ещё раз или встаньте с кресла.
dh-vehicle-ammo-no-gun = У этой машины нет орудия под такие боеприпасы.
dh-vehicle-ammo-full = { CAPITALIZE($gun) }: боекомплект полный.
dh-vehicle-ammo-loaded = Заложено { $count } — { $gun }.
dh-vehicle-ammo-examine = { CAPITALIZE($gun) }: [color=yellow]{ $count }[/color] / { $capacity }.

dh-vehicle-examine-integrity = Целостность корпуса: [color={ $color }]{ $percent }%[/color].

dh-tow-hitch-verb = Прицепить к грузовику
dh-tow-unhitch-verb = Отцепить от грузовика
dh-tow-hitched = {CAPITALIZE($gun)} прицеплена к машине «{$vehicle}».
dh-tow-unhitched = {CAPITALIZE($gun)} отцеплена.

# Модули техники (DHVehicleServerSystem.Hardpoints)
dh-hardpoint-wrong-vehicle = Этот модуль не подходит к этой машине.
dh-hardpoint-slot-taken = Место уже занято — сначала снимите старый модуль гаечным ключом.
dh-hardpoint-installed = Вы ставите { $module }.
dh-hardpoint-removed = Вы снимаете { $module }.
dh-hardpoint-intact = Модуль цел.
dh-hardpoint-broken = { CAPITALIZE($module) } выведен из строя!
dh-hardpoint-verb-remove = Снять: { $module }
dh-hardpoint-verb-repair = Чинить: { $module } ({ $percent }%)
dh-hardpoint-examine-header = [bold]Модули:[/bold]
dh-hardpoint-examine-empty = { $slot }: [color=gray]пусто[/color]
dh-hardpoint-examine-module = { $slot }: { $module } — [color={ $color }]{ $percent }%[/color]
dh-hardpoint-examine-integrity = Целостность: [color={ $color }]{ $percent }%[/color]. Чинится сваркой.
dh-hardpoint-slot-primary = основное орудие
dh-hardpoint-slot-secondary = второе орудие
dh-hardpoint-slot-support = поддержка
dh-hardpoint-slot-armor = броня
dh-hardpoint-slot-wheels = колёса / гусеницы
dh-hardpoint-slot-port-left = левый порт
dh-hardpoint-slot-port-right = правый порт

# Экипаж (DHVehicleServerSystem.Crew)
dh-vehicle-locked = Люки заперты изнутри.
dh-vehicle-verb-lock = Запереть люки
dh-vehicle-verb-unlock = Отпереть люки
dh-vehicle-locked-now = Вы запираете люки. Снаружи никто не войдёт.
dh-vehicle-unlocked-now = Вы отпираете люки.
dh-vehicle-periscope-look = Вы смотрите в перископ. Отойдите, чтобы перестать.
dh-vehicle-key-required = Без ключа машину не завести.

## Отсеки RMC/CMU, двигатель, места, доставка (план осады п. 5)
dh-vehicle-engine-hint = Двигатель заглушён — заведите его кнопкой «Двигатель».
dh-vehicle-engine-on = Вы заводите двигатель.
dh-vehicle-engine-off = Вы глушите двигатель. Машину не видно на РЛС.
dh-vehicle-full = В { $vehicle } нет свободных мест.
dh-vehicle-delivery-sender = Автопарк
dh-vehicle-delivery-arrived = { CAPITALIZE($vehicle) } пригнали в гараж базы.
dh-vehicle-delivery-lift = { CAPITALIZE($vehicle) } — в трюме лёгкого лифта техники.

## Лодки (план осады п. 10)
dh-boat-no-shore = Вокруг глубокая вода — сначала причальте к берегу или мелководью.
dh-boat-washed-ashore = Вас выбросило на берег, вы наглотались воды!
dh-boat-drowned = Лодка уходит под воду, до берега не доплыть…
dh-boat-verb-fold = Сложить лодку
dh-boat-fold-occupied = В лодке кто-то сидит.
dh-boat-no-water = Рядом нет воды, где поместится лодка.
