## Тяжёлые орудия «Штурма»: корабельная артиллерия, ПКО, ПВО

dh-warhead-high-explosive = осколочно-фугасный
dh-warhead-armor-piercing = бронебойный
dh-warhead-incendiary = зажигательный
dh-warhead-cluster = кассетный

dh-heavy-gun-wrong-caliber = Этот казённик принимает снаряды {$caliber} мм.
dh-heavy-gun-already-loaded = Казённик уже заряжен.
dh-heavy-gun-examine-assembled = [color=green]Орудие собрано:[/color] ствол на месте.
dh-heavy-gun-examine-not-assembled = [color=red]Орудие не собрано:[/color] поставьте ствол того же калибра на тайл перед казёнником, повёрнутый так же.
dh-heavy-gun-examine-loaded = Заряжено: [color=yellow]{$warhead}[/color] снаряд.
dh-heavy-gun-examine-empty = Казённик пуст.
dh-heavy-gun-examine-reloading = Перезарядка: {$seconds} с.

dh-heavy-gun-status-not-assembled = не собрано
dh-heavy-gun-status-reloading = перезарядка, {$seconds} с
dh-heavy-gun-status-empty = нет снаряда
dh-heavy-gun-status-ready = готово, {$warhead}

dh-heavy-gun-need-seat = Сядьте за компьютер, чтобы наводить орудия.
dh-heavy-gun-bad-target = Цель вне досягаемости.
dh-heavy-gun-fired = Залп: {$count ->
    [one] выстрелило {$count} орудие
    [few] выстрелило {$count} орудия
   *[other] выстрелило {$count} орудий
}.
dh-heavy-gun-nothing-fired = Ни одно из выбранных орудий не готово к стрельбе.
dh-heavy-gun-report-intercepted = Снаряд перехвачен ПВО противника.
dh-heavy-gun-report-impact = Разрыв в точке {$x}, {$y}.
dh-heavy-gun-report-miss = Промах: снаряд прошёл мимо корабля.
dh-heavy-gun-report-hit = Попадание по кораблю!
dh-heavy-gun-no-targets = Кораблей на орбите в зоне досягаемости нет.
dh-heavy-gun-targets-found = Кораблей на орбите в зоне досягаемости: {$count}.
dh-heavy-gun-not-on-surface = Компьютер не на поверхности мира: ПКО стреляет только с земли.
dh-heavy-gun-no-bodies = Внизу в пределах {$range} нет мира с известными координатами поверхности.
dh-heavy-gun-bodies-found = Выберите мир и координаты на его поверхности.
dh-heavy-gun-not-in-orbit = Корабль не на орбите мира: корабельная артиллерия стреляет только с орбиты.

dh-heavy-gun-ui-title = Управление огнём
dh-heavy-gun-ui-guns = Орудие
dh-heavy-gun-ui-target = Цель
dh-heavy-gun-ui-defenses = ПВО этого корпуса
dh-heavy-gun-ui-fire = ЗАЛП
dh-heavy-gun-ui-seated = Наводчик на месте.
dh-heavy-gun-ui-not-seated = Сядьте за компьютер, чтобы стрелять.
dh-heavy-gun-ui-gun = {$name} ({$caliber} мм): {$status}
dh-heavy-gun-ui-no-guns = Орудие не привязано: у каждого орудия свой компьютер, поставьте его рядом с казёнником (в пределах 5 м).
dh-heavy-gun-ui-target-entry = {$name} — {$altitude} м над поверхностью
dh-heavy-gun-ui-body-entry = {$name} ({$altitude} м внизу)
dh-heavy-gun-ui-zone = Координаты поверхности в метрах от центра зоны высадки, в пределах ±{$zone}.
dh-heavy-gun-ui-defense = {$name}: {$ammo}/{$max}
dh-heavy-gun-ui-no-defenses = Нет.

dh-interceptor-wrong-ammo = Эти боеприпасы не подходят.
dh-interceptor-full = Полностью заряжено.
dh-interceptor-examine = Боезапас: [color=yellow]{$ammo}/{$max}[/color].

dh-assault-carrier-damaged = Авианосец под огнём ПКО! Целостность корпуса: {$hull}%.

## Mortar and laser target designator
dh-warhead-flare = осветительный
dh-heavy-gun-ui-marks = Лазерные метки
dh-heavy-gun-ui-no-marks = На поверхностях в зоне досягаемости лазерных меток нет.
dh-heavy-gun-ui-mark = Метка {$code}: {$body} ({$x}, {$y})
dh-laser-underground = Здесь, внизу, нет неба, чтобы подсвечивать цель.
dh-laser-no-line = Нет прямой видимости или дальше {$range} м.
dh-laser-lasing = Вы держите лазер на цели...
dh-laser-marked = Метка {$code} поставлена в точке ({$x}, {$y}).
dh-laser-sender = Лазерный целеуказатель ({$name})
dh-laser-announce = Цель отмечена: метка {$code} в точке ({$x}, {$y}).
dh-mortar-no-ground = Здесь нет грунта, чтобы установить миномёт.
dh-mortar-deployed = {CAPITALIZE($user)} устанавливает миномёт.
dh-mortar-fold-verb = Сложить миномёт
dh-mortar-out-of-range = Вне дальности: от {$min} до {$max} м.
dh-mortar-aimed = Миномёт наведён на ({$x}, {$y}).
dh-mortar-underground = Миномёт не стреляет под землёй.
dh-mortar-status-not-aimed = Не наведён на цель.
dh-mortar-status-reloading = Ствол ещё горячий.
dh-mortar-status-ready = Готов: опустите мину в ствол.
dh-mortar-examine-aimed = Наведён на [color=yellow]({$x}, {$y})[/color].
dh-mortar-examine-not-aimed = Не наведён на цель.
dh-mortar-ui-title = Миномёт M402
dh-mortar-ui-position = Позиция миномёта: ({$x}, {$y})
dh-mortar-ui-aimed = Наведён на ({$x}, {$y}), {$distance} м.
dh-mortar-ui-not-aimed = Не наведён на цель.
dh-mortar-ui-range = Дальность: {$min}–{$max} м.
dh-mortar-ui-coordinates = Координаты цели
dh-mortar-ui-aim = Навести миномёт
dh-mortar-ui-marks = Лазерные метки в зоне досягаемости
dh-mortar-ui-mark = Метка {$code}: ({$x}, {$y}), {$distance} м
dh-mortar-ui-no-marks = Лазерных меток поблизости нет.

## Infantry guns
dh-field-gun-wrong-caliber = Эта пушка принимает снаряды {$caliber} мм.
dh-field-gun-already-loaded = Пушка уже заряжена.
dh-field-gun-recoiling = Затвор ещё открыт после выстрела.
dh-field-gun-examine-loaded = Заряжено: [color=yellow]{$shell}[/color].
dh-field-gun-examine-empty = Не заряжена.
dh-field-gun-examine-aim = Наведена: горизонт {$azimuth}°, возвышение {$elevation}°.
dh-field-gun-status-not-deployed = Не закреплена ключом: на ходу пушка не стреляет.
dh-field-gun-status-underground = Пушка не стреляет под землёй.
dh-field-gun-status-empty = Не заряжена.
dh-field-gun-status-too-high = Этим снарядом нельзя стрелять выше {$max}°.
dh-field-gun-status-ready = Готова к выстрелу.
dh-field-gun-ui-title = Пехотная пушка — место наводчика
dh-field-gun-ui-empty = Казённик: пуст.
dh-field-gun-ui-loaded = Казённик: {$shell}.
dh-field-gun-ui-aim = Наводка
dh-field-gun-ui-azimuth = Горизонт, °
dh-field-gun-ui-elevation = Возвышение, °
dh-field-gun-ui-limits = Сейчас {$current}° / {$elevation}°. Горизонт ±{$traverse}° от лафета, возвышение {$min}–{$max}°; до {$direct}° — прямая наводка.
dh-field-gun-ui-set = Навести
dh-field-gun-ui-mode-direct = Прямая наводка: снаряд летит прямо и бьёт в первое препятствие.
dh-field-gun-ui-mode-indirect = Навесной огонь: снаряд перелетает препятствия и падает на расчётной дальности.
dh-field-gun-ui-range = Расчётная дальность заряженного снаряда: {$range} м.
dh-field-gun-ui-range-unknown = Зарядите снаряд, чтобы увидеть дальность.
dh-field-gun-ui-fire = ОГОНЬ
dh-field-gun-ui-table = Таблица стрельбы, м
dh-field-gun-ui-table-shell = Снаряд
dh-field-gun-ui-marks = Лазерные метки
dh-field-gun-ui-mark = Метка {$code}: {$distance} м, горизонт {$azimuth}°
dh-field-gun-ui-mark-low = Настильно {$elevation}°
dh-field-gun-ui-mark-high = Навесом {$elevation}°
dh-field-gun-ui-no-marks = Лазерных меток в зоне досягаемости нет.

## Radars
dh-radar-broken = Экран РЛС гаснет: ПВО ослепла!
dh-radar-repaired = РЛС оживает.
dh-radar-examine-active = Антенна ровно вращается: РЛС [color=green]работает[/color].
dh-radar-examine-broken = РЛС [color=red]молчит[/color]. Сварка могла бы её вернуть.
dh-radar-sender = Пост РЛС
dh-radar-incoming-dropship = Вражеский челнок ({$ship}) заходит на {$zone}!
