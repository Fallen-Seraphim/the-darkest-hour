## Compact fusion reactor

fusion-reactor-fuel-slot = Топливная кассета

fusion-reactor-examine-status = Температура ядра: [color=orange]{$temperature} K[/color]. Поле удержания: [color=cyan]{$integrity}%[/color].
fusion-reactor-examine-flow = Теплоноситель: вход [color=lightblue]{$inlet} кПа[/color], выход [color=orange]{$outlet} кПа[/color], расход {$flow} моль/с.
fusion-reactor-examine-breached = [color=red]Корпус удержания разорван. Реактор уничтожен.[/color]

fusion-reactor-radio-overheat = ВНИМАНИЕ: перегрев ядра термоядерного реактора ({$temperature} K). Поле удержания слабеет. Снизьте мощность или увеличьте расход теплоносителя.
fusion-reactor-radio-field-50 = ОПАСНОСТЬ: поле удержания термоядерного реактора ниже 50%.
fusion-reactor-radio-field-20 = КРИТИЧЕСКИ: поле удержания термоядерного реактора ниже 20%. Прорыв неизбежен. Немедленно заглушите реактор.
fusion-reactor-radio-normal = Поле удержания термоядерного реактора стабильно.
fusion-reactor-radio-breach = Удержание термоядерного реактора ПРОРВАНО.

fusion-reactor-breach-announcement = Удержание станционного термоядерного реактора нарушено. Возле реактора ожидайте пожара, перегретого газа и электромагнитных помех. Инженерному отделу немедленно отреагировать.
fusion-reactor-announcement-sender = Управление реактором

fusion-reactor-ui-title = Компактный термоядерный реактор
fusion-reactor-ui-status-off = Состояние: выключен
fusion-reactor-ui-status-running = Состояние: работает
fusion-reactor-ui-status-no-power = Состояние: [НЕТ ПИТАНИЯ] поле удержания обесточено
fusion-reactor-ui-status-no-fuel = Состояние: [НЕТ ТОПЛИВА] вставьте топливную кассету
fusion-reactor-ui-status-breached = Состояние: [ПРОРЫВ]
fusion-reactor-ui-fuel = Топливо: {$percent}%
fusion-reactor-ui-fuel-none = Топливо: нет кассеты
fusion-reactor-ui-power = Мощность: {$percent}%
fusion-reactor-ui-heat = Тепловыделение: {$output} / {$max} кВт
fusion-reactor-ui-core = Ядро: {$temperature} K (безопасно до {$safe} K)
fusion-reactor-ui-field = Поле удержания: {$integrity}%
fusion-reactor-ui-gas = Теплоноситель: вход {$inlet} K, выход {$outlet} K
fusion-reactor-ui-flow = Давление теплоносителя: вход {$inlet} кПа, выход {$outlet} кПа. Расход: {$flow} моль/с
fusion-reactor-ui-no-flow = НЕТ ПОТОКА ТЕПЛОНОСИТЕЛЯ: ядро не охлаждается. Проверьте контур и выход турбины.
fusion-reactor-ui-turn-on = Запустить реактор
fusion-reactor-ui-turn-off = Заглушить

fusion-fuel-cassette-examine = Осталось топлива: [color=cyan]{$percent}%[/color].

## Gas turbine

gas-turbine-examine-status = Выработка: [color=yellow]{$power} кВт[/color]. КПД: {$efficiency}%. Лопатки: {$integrity}%.
gas-turbine-examine-hot = Горячая сторона: [color=orange]{$temperature} K[/color], вход {$inlet} кПа, выход {$outlet} кПа, {$flow} моль/с.
gas-turbine-examine-cold = Холодная сторона: [color=lightblue]{$temperature} K[/color], вход {$inlet} кПа, выход {$outlet} кПа, {$flow} моль/с.
gas-turbine-examine-hot-outlet-full = [color=orange]Горячий выход переполнен: верните горячий газ в реактор.[/color]
gas-turbine-examine-no-hot-gas = [color=orange]Горячий газ не поступает.[/color]
gas-turbine-examine-cold-outlet-full = [color=orange]Холодный выход переполнен: холодный контур должен идти через радиаторы обратно на холодный вход.[/color]
gas-turbine-examine-no-cold-gas = [color=orange]Холодный газ не поступает. Без холодного контура турбина не даёт энергии, а реактор перегревается.[/color]
gas-turbine-examine-cold-too-warm = [color=orange]Холодная сторона почти не холоднее горячей. Добавьте в холодный контур радиаторы или охладители.[/color]
gas-turbine-examine-broken = [color=red]Лопатки разрушены. Заварите их.[/color]
gas-turbine-broken = Лопатки турбины ({$turbine}) разрывает от жара!
gas-turbine-repaired = Лопатки отремонтированы до {$integrity}%.

## Large machine gas ports

dh-machine-gas-ports-examine = Вход газа с [color=lightblue]{$inlet}[/color] стороны, выход с [color=orange]{$outlet}[/color] стороны.
dh-machine-gas-ports-examine-hot = Вход горячего газа с [color=orange]{$inlet}[/color] стороны, выход с [color=orange]{$outlet}[/color] стороны.
dh-machine-gas-ports-examine-cold = Вход холодного газа с [color=lightblue]{$inlet}[/color] стороны, выход с [color=lightblue]{$outlet}[/color] стороны.
dh-machine-gas-ports-hot-inlet = Горячий вход
dh-machine-gas-ports-hot-outlet = Горячий выход
dh-machine-gas-ports-cold-inlet = Холодный вход
dh-machine-gas-ports-cold-outlet = Холодный выход
dh-direction-north = северной
dh-direction-south = южной
dh-direction-east = восточной
dh-direction-west = западной

guide-entry-fusion-reactor = Термоядерный реактор
