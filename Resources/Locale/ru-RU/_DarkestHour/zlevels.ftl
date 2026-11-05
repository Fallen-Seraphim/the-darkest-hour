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
