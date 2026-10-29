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
