anomaly-status-charges = [color=orange]{ $charges } зарядов[/color]

anomaly-status-infinite = [color=gold]Бесконечные заряды[/color]

battery-status-charge = Заряд: [color=#5E7C16]{ $percent }[/color] %

battery-status-state = Статус: { $state }

battery-status-switchable-state = { $state ->
    [on] [color=green]Вкл[/color]
    [off] [color=red]Выкл[/color]
    *[other] Неизвестно
}

charge-status-count = Заряды: [color=fuchsia]{ $current }/{ $max }[/color]

charge-status-recharge = Перезарядка: [color=yellow]{ $seconds }с[/color]

guardian-status-ready = [color=green]Готов[/color]

guardian-status-used = [color=red]Использован[/color]

magazine-status-rounds = Патроны: [color=yellow]{ $current }/{ $max }[/color]

tank-pressure-status = Давл.: [color=orange]{ $pressure } кПа[/color]

tank-status-state = Клапан: { $state }

tank-status-switchable-state = { $state ->
    [open] [color=red]Открыт[/color]
    [closed] [color=green]Закрыт[/color]
    *[other] Неизвестно
}

timer-trigger-status-delay = Задержка: [color=white]{ $delay } сек[/color]

timer-trigger-status-delay-unknown = Установить задержку: [color=gray]???[/color]
