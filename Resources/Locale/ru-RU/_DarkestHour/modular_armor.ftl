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
dh-armor-module-examine-class = Класс защиты: { $level } ({ $class }).
dh-armor-part-examine-class = Класс защиты: { $level }.
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

## Класс брони / бронепробития (значок при осмотре)
dh-armor-verb-text = Класс брони
dh-armor-verb-class = Класс брони: [color=cyan]{ $level }[/color].
dh-armor-verb-parts = Защищает: { $parts }.
dh-armor-verb-suit = Модули корпуса:
dh-armor-verb-module = { $slot }: класс [color=cyan]{ $level }[/color] ({ $class }), целостность { $percent }%.
dh-armor-verb-module-broken = { $slot }: [color=red]сломан[/color] — не защищает.
dh-armor-verb-slot-empty = { $slot }: [color=gray]пусто[/color] — не защищено.
dh-armor-slot-torso = Торс
dh-armor-slot-armleft = Левая рука
dh-armor-slot-armright = Правая рука
dh-armor-slot-legleft = Левая нога
dh-armor-slot-legright = Правая нога
dh-armor-slot-special = Спецсистема
dh-armor-part-head = голова
dh-armor-part-torso = торс
dh-armor-part-armleft = левая рука
dh-armor-part-armright = правая рука
dh-armor-part-handleft = левая кисть
dh-armor-part-handright = правая кисть
dh-armor-part-legleft = левая нога
dh-armor-part-legright = правая нога
dh-armor-part-footleft = левая стопа
dh-armor-part-footright = правая стопа
dh-penetration-verb-text = Бронепробитие
dh-penetration-verb-class = Класс бронепробития: [color=orange]{ $level }[/color].
dh-penetration-verb-hint = Пробивает броню классов 0–{ $level }; более прочная броня держит (заброневой удар).
