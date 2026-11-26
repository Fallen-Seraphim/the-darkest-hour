dh-control-point-sender = Оперативный штаб
dh-control-point-captured-ours = Мы заняли район: { $point }.
dh-control-point-captured-enemy = Противник занял район: { $point }.
dh-control-point-lost = Мы потеряли район: { $point }!
dh-control-point-examine-name = Флагшток зоны: [bold]{ $point }[/bold].
dh-control-point-examine-neutral = Район никем не контролируется.
dh-control-point-examine-republic = Район удерживает [color=#7fcf4f]Республика[/color].
dh-control-point-examine-empire = Район удерживает [color=#e8604a]Империя[/color].
dh-control-point-flag-raised = Противник водружает флаг: { $point }! Через { $seconds } с район перейдёт к нему — снимите или уничтожьте флаг.
dh-control-point-jammed = Над районом «{ $point }» поднят флаг ЧВК LA-V: пока он стоит, район не работает и не даёт очков.
dh-control-point-examine-jammed = В зоне стоит флаг [color=#8A6AD0]ЧВК LA-V[/color]: район заглушён — не захватить, очков и эффектов нет.
dh-control-point-examine-contested = В зоне флаги обеих сторон: район оспаривается, очков никому.
dh-control-point-examine-capturing-republic = [color=#7fcf4f]Республика[/color] водружает флаг: район перейдёт через { $seconds } с.
dh-control-point-examine-capturing-empire = [color=#e8604a]Империя[/color] водружает флаг: район перейдёт через { $seconds } с.
dh-control-point-examine-how = Захват: воткните флаг своей стороны в пределах { $radius } клеток от флагштока; если 30 с в зоне нет чужих флагов, район ваш. Чужой флаг снимается (8 с) или ломается.

dh-control-point-village = Деревня
dh-control-point-village-desc = Даёт 2 оч. командования в минуту.
dh-control-point-spaceport = Космодром
dh-control-point-spaceport-desc = Даёт 5 оч. командования в минуту.
dh-control-point-construction = Стройка
dh-control-point-construction-desc = Даёт 3 оч. командования в минуту.
dh-control-point-npp = АЭС
dh-control-point-npp-desc = У стороны, удерживающей станцию, работают прожекторы, ПВО и РЛС по всему району. Даёт 5 оч. командования в минуту.
dh-control-point-dam = Дамба
dh-control-point-dam-desc = Единственная безопасная переправа через реку. Даёт 3 оч. командования в минуту.
dh-control-point-pko = Батарея ПКО
dh-control-point-pko-desc = Орудия ПКО бьют по авианосцу, только пока батарея у Республики. Даёт 4 оч. командования в минуту.
dh-control-point-pvo = Позиция ПВО
dh-control-point-pvo-desc = ПВО сбивает удары, штурмовик и челноки, только пока позиция и АЭС у Республики. Даёт 4 оч. командования в минуту.
dh-control-point-rls = РЛС и связь
dh-control-point-rls-desc = РЛС даёт Республике сигнатуры противника, пока пост и АЭС у Республики. Потеря поста глушит общий канал гарнизона: слышно только вблизи. Даёт 4 оч. командования в минуту.

dh-overwatch-ui-tacmap = Тактическая карта
dh-tacmap-title = Тактическая карта
dh-tacmap-loading = Загрузка местности...
dh-tacmap-no-surface = Нет данных о поверхности планеты.
dh-tacmap-reset = Вся карта
dh-tacmap-legend = Колесо — масштаб, перетаскивание — сдвиг. Квадраты — зоны захвата, рамки — ЗП, крестики — метки ЛЦУ. Противник — кружки (бойцы) и квадраты (техника), замеченные своими бойцами, техникой, РЛС и детекторами; розовое — последний глобальный скан, треугольники — РЛС штурмовика.
dh-tacmap-laser-mark = Метка { $code }
dh-tacmap-counter-battery = Засечка
dh-tacmap-drop-beacon = Маяк { $number }
dh-tacmap-drop-beacon-incoming = Маяк { $number }: летит груз
dh-tacmap-drop-incoming = Сброс груза
dh-counter-battery-sender = Контрбатарейная засечка
dh-counter-battery-announce = Засечён огонь противника: орудие в районе X { $x }, Y { $y } (± { $radius } м).

cmd-dhcontrolpoint-desc = Задаёт владельца зоны захвата.
cmd-dhcontrolpoint-help = Использование: dhcontrolpoint <village|spaceport|construction|npp|dam> <Republic|Empire|none>
cmd-dhcontrolpoint-done = Владелец зоны изменён.
cmd-dhcontrolpoint-not-found = Зоны с таким id нет.

dh-tacmap-level = Уровень:
dh-tacmap-level-ground = Земля
dh-control-point-powered-backup = [color=#E0B050]Работает от резервного дизеля: осталось около {$minutes} мин.[/color]
dh-control-point-powered-off = [color=#A05050]Нет питания: сеть АЭС потеряна.[/color]
