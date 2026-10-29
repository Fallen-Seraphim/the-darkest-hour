## DarkestHour: модульная броня

dh-armor-module-wrong-faction = Этот модуль не подходит к этому костюму.
dh-armor-module-needs-rack = Тяжёлые модули ставятся и снимаются только на стойке обслуживания брони.
dh-armor-module-slot-taken = Это гнездо уже занято.
dh-armor-module-installed = { CAPITALIZE($module) } встаёт на место.
dh-armor-module-broken = { CAPITALIZE($module) } разлетается на куски!
dh-armor-module-repaired = Вы завариваете треснувшие пластины.
dh-armor-verb-remove = Снять: { $module }

dh-armor-examine-module = { CAPITALIZE($module) }: { $percent }%
dh-armor-examine-special = Особая система: { $module }
dh-armor-examine-empty = Модулей брони нет.
dh-armor-module-examine-class = Класс: { $class }.
dh-armor-module-examine-integrity = Целостность: { $percent }%.
dh-armor-module-examine-broken = [color=red]Сломан[/color] — заварите, чтобы починить.
dh-armor-class-light = лёгкий
dh-armor-class-heavy = тяжёлый
dh-armor-class-composite = композитный

dh-armor-automed-status = Автомедик взведён и следит за вашим состоянием.
dh-armor-automed-injected = Броня вводит вам лекарство.

dh-armor-automed-examine-volume = Лекарство: [color=#7fd4ff]{ $volume }/{ $max } ед.[/color] — осталось { $doses } { $doses ->
    [one] доза
    [few] дозы
   *[other] доз
}.
dh-armor-automed-examine-reagent = — { $reagent }: { $amount } ед.
dh-armor-automed-examine-ready = [color=#5fd35f]Готов[/color]: вводит, когда урон носителя достигает { $threshold }.
dh-armor-automed-examine-cooldown = [color=yellow]Перезарядка[/color]: { $seconds } с.
dh-armor-automed-examine-empty = [color=red]Пуст[/color] — заправьте шприцем.
dh-armor-storage-examine = Вмещает { $count } { $count ->
    [one] предмет
    [few] предмета
   *[other] предметов
}. Открывается действием особой системы.
dh-armor-weapon-examine-stowed = Встроенное оружие ({ $weapon }): убрано.
dh-armor-weapon-examine-out = Встроенное оружие ({ $weapon }): [color=yellow]выдвинуто[/color].
