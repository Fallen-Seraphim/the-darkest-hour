## Z-уровни

dh-zlevel-name-ground = уровень земли
dh-zlevel-name-up = уровень +{ $level }
dh-zlevel-name-down = уровень -{ $level }
dh-zlevel-map-up = Уровень +{ $level }
dh-zlevel-map-down = Подземный уровень -{ $level }
dh-zlevel-stairs-blocked = Проход перекрыт.
dh-zlevel-fell = Вы падаете { $levels ->
    [one] на этаж вниз!
    [few] на { $levels } этажа вниз!
   *[other] на { $levels } этажей вниз!
}
dh-zlevel-look-nothing = Здесь нет других уровней.
dh-zlevel-look-nothing-up = Выше ничего нет.
dh-zlevel-look-nothing-down = Ниже ничего нет.
dh-zlevel-look-too-far = Так далеко не видно — нужен прицел x8 или x16 в двух руках.
dh-zlevel-look-covered = Над вами потолок — найдите лестницу или шахту лифта.
dh-zlevel-look-no-edge = Не через что смотреть вниз — подойдите к краю, лестнице, люку или колодцу.
dh-zlevel-look-start = Вы смотрите на { $level }.
dh-zlevel-look-stop = Вы перестаёте смотреть.
dh-lift-choose-floor = Выберите уровень в альтернативных действиях.
dh-lift-verb-send-to = Отправить: { $level }
dh-lift-verb-call-from = Вызвать: { $level }
dh-mortar-covered = Над миномётом крыша — стрелять нельзя.
dh-field-gun-status-covered = Над орудием крыша — навесом не выстрелить.

cmd-dhzlevel-desc = Z-уровни для маппинга: список уровней вашей стопки или создание пустого уровня в ней.
cmd-dhzlevel-help = Использование: dhzlevel [уровень] (−3..8, не 0)
cmd-dhzlevel-no-map = Вы не на карте.
cmd-dhzlevel-no-stack = У этой карты пока нет Z-уровней.
cmd-dhzlevel-entry = Уровень { $level }: карта { $map }
cmd-dhzlevel-exists = Такой уровень уже есть.
cmd-dhzlevel-range = Уровень — от -3 до 8, кроме 0.
cmd-dhzlevel-created = Создан уровень { $level } — карта { $map }. Постройте этаж там же по координатам и сохраните его через savegrid.
dh-zlevel-moved = Вы переходите на { $level }.
cmd-dhztp-desc = Z-уровни для маппинга: перейти в ту же точку на другом уровне.
cmd-dhztp-help = Использование: dhztp <уровень>
cmd-dhztp-no-level = Здесь нет такого уровня.
cmd-dhzload-desc = Z-уровни для маппинга: загрузить грид из файла в уровень (уровень создаётся, если его нет).
cmd-dhzload-help = Использование: dhzload <уровень> <путь к .yml грида>
cmd-dhzload-failed = Не удалось загрузить грид.
cmd-dhzload-done = Грид загружен в уровень { $level } (карта { $map }).
cmd-dhzsave-desc = Z-уровни для маппинга: сохранить все уровни, кроме земли (её — через savemap), как папка/level_N.yml.
cmd-dhzsave-help = Использование: dhzsave <папка> (стоя на любом уровне стопки)
cmd-dhzsave-empty = Уровень { $level }: грида нет, пропущен.
cmd-dhzsave-many = Уровень { $level }: гридов { $count } — сохраняется только первый, объедините их.
cmd-dhzsave-saved = Уровень { $level } сохранён в { $path }.
cmd-dhzsave-failed = Уровень { $level }: не удалось сохранить.
cmd-dhzroofs-desc = Z-уровни для маппинга: проставить крыши под полами верхних уровней (там нет солнца и погоды).
cmd-dhzroofs-done = Проставлено флагов крыши: { $count }.
dh-lift-ui-title = Лифт
dh-lift-ui-busy = Лифт в пути...
dh-lift-ui-ready = Выберите этаж.
dh-lift-ui-here = { $level } (вы здесь)
dh-lift-ui-send = Отправить
dh-lift-ui-call = Вызвать
