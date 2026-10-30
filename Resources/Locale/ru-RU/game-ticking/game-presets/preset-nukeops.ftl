nukeops-cond-nukeactiveinstation = Ядерная бомба была оставлена взведённой на станции.

nukeops-cond-nukedisknotoncentcom = Экипаж оставил диск ядерной аутентификации на станции.

nukeops-cond-nukediskoncentcom = Экипаж улетел с диском ядерной аутентификации.

nukeops-cond-nukeexplodedonincorrectlocation = Ядерная бомба взорвалась вне станции.

nukeops-crewmajor = [color=green]Разгромная победа экипажа![/color]

nukeops-crewminor = [color=green]Малая победа экипажа![/color]

nukeops-disk-carried-by = { " " }у [color=White]{ $name }[/color], [color=orange]{ $job }[/color], { $location } { $user ->
    [unknown] { "" }
    *[other] ([color=gray]{ $user }[/color])
}

nukeops-disk-location-title = Конечное местоположение диска:

nukeops-list-name = - [color=White]{ $name }[/color]

nukeops-list-name-user = - [color=White]{ $name }[/color] ([color=gray]{ $user }[/color])

nukeops-neutral = [color=yellow]Ничейный исход![/color]

nukeops-no-one-ready = Нет готовых игроков! Нельзя запустить пресет Ядерные оперативники.

nukeops-not-enough-ready-players = Недостаточно игроков готовы к игре! { $readyPlayersCount } игроков из необходимых { $minimumPlayers } готовы. Нельзя запустить пресет Ядерные оперативники.

nukeops-role-agent = Медик

nukeops-role-commander = Командир

nukeops-role-operator = Оператор

storage-hierarchy-list = { $items-left ->
    [0] { $existing-text } { $item },
    *[other] { $existing-text } { $item }, в
}

nukeops-briefing = Когда ваш шаттл прибудет, станции дадут десять минут на капитуляцию (поднять станцию на высокую орбиту). Если откажется — доставьте заряд и уходите до взрыва. Слава Первой Империи!
nukeops-cond-allnukiesalive = Ни один имперский морпех не погиб.
nukeops-cond-allnukiesdead = Все имперские морпехи погибли.
nukeops-cond-nukeactiveatcentcom = Ядерная бомба была взведена и доставлена в Metallistick Group!
nukeops-cond-nukeexplodedoncorrectstation = Имперским морпехам удалось взорвать станцию.
nukeops-cond-nukeexplodedonnukieoutpost = Передовая база Империи уничтожена ядерным взрывом!
nukeops-cond-nukiesabandoned = Имперских морпехов бросили.
nukeops-cond-somenukiesalive = Часть имперских морпехов погибла.
nukeops-description = Рейд Имперского корпуса морской пехоты нацелен на станцию. Не дайте им взвести и взорвать ядерную бомбу — защитите диск ядерной аутентификации — или сдайтесь.
nukeops-list-start = Имперскими морпехами были:
nukeops-opsmajor = [color=crimson]Крупная победа Империи![/color]
nukeops-opsminor = [color=crimson]Малая победа Империи![/color]
nukeops-title = Имперский рейд
nukeops-welcome =
    Вы — морпех Имперского корпуса морской пехоты. Ваша цель — {$station}: заставьте её сдаться Первой Империи — или возьмите штурмом и взорвите, чтобы от неё осталась груда обломков. Империя дала вам всё необходимое для задачи.
    Операция {$name} начинается! Слава Первой Империи!
