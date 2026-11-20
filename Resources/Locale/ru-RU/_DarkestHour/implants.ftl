# Импланты и отторжение (план осады п. 29)
dh-implant-kind-Base = базовый
dh-implant-kind-Pulsar = «Пульсар»
dh-implant-kind-Echo = «Эхо»
dh-implant-kind-Scale = «Чешуя»
dh-implant-kind-Silence = «Тишина»
dh-implant-kind-Filter = «Фильтр»
dh-implant-kind-Hemostat = «Гемостат»
dh-implant-kind-Falcon = «Сокол»
dh-implant-kind-Vise = «Тиски»
dh-implant-kind-Support = «Опора»
dh-implant-kind-Tactic = «Тактик»
dh-implant-wrong-part = Этот имплант ставится в другую часть тела.
dh-implant-already = Такой имплант уже стоит.
dh-implant-pulsar-zap = Грудь {THE($target)} вздрагивает от разряда «Пульсара»!
dh-implant-nausea = Подкатывает тошнота, ломит всё тело.
dh-implant-weakness = Ноги подкашиваются — тело отторгает импланты.
dh-implant-agony-start = Что-то рвётся изнутри — тело больше не принимает импланты!
dh-implant-agony = {CAPITALIZE(THE($target))} бьётся в судорогах и кашляет кровью!
dh-implant-rejection-death = Организм разрушен отторжением имплантов. Реанимация бесполезна.
dh-implant-analyzer-list = [color=#80b0e0]Импланты:[/color] {$implants}
dh-implant-analyzer-rejection = [color=#d0a040]Отторжение имплантов: {$percent}%.[/color] Цитрасин и диализ (жизнеобеспечение) замедляют.
dh-implant-analyzer-terminal = [color=#d04040]Терминальное отторжение имплантов! Остановить нельзя.[/color]
dh-surgery-insert-implant = Установка импланта
dh-surgery-insert-implant-desc = Поставить имплант из руки. Пока он в теле, растёт отторжение — тем быстрее, чем чаще имплант работает.
dh-surgery-step-insert-implant = установка импланта
dh-surgery-tool-implant = имплант в руке
dh-surgery-remove-implant = Извлечение импланта
dh-surgery-remove-implant-desc = Вынуть имплант. Отторжение остановится от него, но накопленное не пройдёт.
dh-surgery-step-remove-implant = извлечение импланта
dh-entity-effect-guidebook-slow-rejection =
    { $chance ->
        [1] Замедляет
        *[other] замедлять
    } отторжение имплантов
