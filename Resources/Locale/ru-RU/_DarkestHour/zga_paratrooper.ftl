# Наборы событий третьей стороны (план осады пп. 11 и 28)
dh-third-party-window-title = События — набор участников
dh-third-party-none = Сейчас открытых наборов нет.
dh-third-party-time-left = Набор закроется через { $minutes } мин { $seconds } с.
dh-third-party-role = { $name } — записались: { $count }
dh-third-party-join = Записаться
dh-third-party-leave = Отказаться
dh-third-party-opened = [color=#7ec8f0]Открыт набор на событие «{ $name }».[/color] Окно записи — команда dhevents (или «Присоединиться» → «Другие»). Записываться можно, пока вы наблюдатель.
dh-join-events = { $count ->
    [one] { $count } набор — для наблюдателей
    [few] { $count } набора — для наблюдателей
   *[other] { $count } наборов — для наблюдателей
}
cmd-dhevents-desc = Открывает окно записи на события третьей стороны.
cmd-dhevents-help = Использование: dhevents

# Ассоциативный десантник (план осады п. 28)
dh-zga-event-name = Ассоциативный десантник
dh-zga-event-desc = Опытнейший боец Ассоциативного Десантного Корпуса ЗГА и ИИ его брони высаживаются на Терминус, чтобы устранить одного из командиров. Нужны оба игрока: событие начнётся, когда запишутся на обе роли.
dh-zga-role-paratrooper = Ассоциативный десантник
dh-zga-role-paratrooper-desc = Высадка у края карты, оружие ЗГА на выбор. Устранить цель и уйти к точке эвакуации, соблюдая меры предосторожности.
dh-zga-role-mars = ИИ брони M.A.R.S. 1.5
dh-zga-role-mars-desc = «Голос в броне» десантника: видит его глазами, говорит с ним по личному каналу (:м). Размечает цели, сканирует округу, смотрит тактическую сеть противника, взламывает двери, глушит радио и вытаскивает раненого носителя.
dh-zga-name-format = Десантник { $part0 }
dh-zga-no-target = Цели на поверхности нет — высадка отменена.
dh-zga-briefing-paratrooper = [color=#7ec8f0][bold]Задание.[/bold][/color] Цель: { $target } ({ $job }). Трекер (стрелка среди значков состояния) ведёт к цели, после неё — к точке эвакуации. Кейс с оружием — в руке. Личный канал с ИИ — :м. Кнопка «Задание» напомнит сроки и меры предосторожности. Способности ИИ работают, только пока на вас броня ЭМБ-5. В торсе — летальный имплант: не даться живым.
dh-zga-briefing-mars = [color=#7ec8f0][bold]M.A.R.S. 1.5 активирован.[/bold][/color] Носитель — ассоциативный десантник. Цель: { $target } ({ $job }). Вы видите его глазами; личный канал с ним — :м. Кнопка «Задание» напомнит сроки и правила.
dh-zga-target-down = [color=#7ec8f0]Цель устранена.[/color] Трекер ведёт к точке эвакуации — { $evac } мин.
dh-zga-extra-kill = [color=#d0a040]Лишняя жертва. Меры предосторожности нарушаются — не вмешивайтесь в бой.[/color]
dh-zga-violation-captured = попали в плен живым
dh-zga-violation-hunt-time = не устранили цель вовремя
dh-zga-violation-evac-time = не эвакуировались вовремя
dh-zga-violation-kills = слишком много лишних жертв
dh-zga-disavowed-self = [color=#d04040]Меры предосторожности нарушены: { $reason }. ЗГА отрекается от вас — дальше вы сами по себе.[/color]
dh-zga-disavowed-announcement = Земная Генеральная Ассоциация заявляет: оперативник { $name } действовал без санкции и больше не представляет Ассоциацию.
dh-zga-announcement-sender = Земная Генеральная Ассоциация
dh-zga-evacuated = [color=#7ec8f0]Эвакуация. Задание выполнено.[/color]
dh-zga-evacuated-others = Фигура в броне исчезает в вихре от невидимого челнока.
dh-zga-mars-erased = M.A.R.S. 1.5: носитель погиб, ядро ИИ стёрто.
dh-zga-status = [color=#7ec8f0]Задание:[/color] { $state }. Цель: { $target } ({ $job }). Осталось: { $minutes } мин { $seconds } с.
dh-zga-state-hunting = устранить цель
dh-zga-state-evacuating = уйти к точке эвакуации
dh-zga-state-evacuated = выполнено
dh-zga-state-disavowed = провалено, ЗГА отреклась
dh-zga-state-dead = десантник погиб
dh-zga-kit-window-title = Арсенал ЗГА
dh-zga-kit-window-description = Выберите оружие. Кейс превратится в выбранный комплект.
dh-zga-set-titmg10-name = TITMG-10
dh-zga-set-titmg10-description = Основная винтовка армии Ассоциации: два магазина рядом, 5,56×45. Пять магазинов (FMG, разрывной, урановый) и граната.
dh-zga-set-titr15-name = TITR-15
dh-zga-set-titr15-description = Надёжный автомат второй линии с Титана, 7,62×39. Четыре магазина (FMG и урановый) и две гранаты.
dh-zga-set-ear20-name = EAR-20/LR
dh-zga-set-ear20-description = Энергетическая штурмовая винтовка ЗГА: оглушающий импульс и термический ожог. Слаба против брони. Три картриджа Кейна.
dh-zga-admin-opened = Набор на ассоциативного десантника открыт на 15 минут.
dh-zga-admin-busy = Набор уже открыт или событие уже было в этом раунде.
cmd-dhcallzgaparatrooper-desc = Открывает набор призраков на событие «Ассоциативный десантник».
cmd-dhcallzgaparatrooper-help = Использование: dhcallzgaparatrooper
dh-admin-tab-call-zga = Ассоциативный десантник
dh-admin-tab-call-zga-tooltip = Открыть набор двух призраков (десантник и ИИ брони) на 15 минут. Высадка — когда запишутся оба.
dh-chat-radio-zga = Канал ЗГА

# Способности M.A.R.S.
dh-mars-offline = M.A.R.S.: броня снята — ИИ отключён.
dh-mars-online = M.A.R.S.: броня надета — ИИ снова в сети.
dh-mars-marked = M.A.R.S. отметил цель: { $target }.
dh-mars-scan-heard = *резкий электронный писк*
dh-mars-scan-done = M.A.R.S.: скан завершён, отметок — { $count }. Писк слышали вокруг!
dh-mars-hack-too-far = Дверь слишком далеко от носителя.
dh-mars-hack-start = M.A.R.S. взламывает дверь...
dh-mars-hack-failed = M.A.R.S.: взлом сорван.
dh-mars-hack-done = M.A.R.S.: дверь вскрыта.
dh-mars-jam-on = M.A.R.S. глушит радио вокруг на полминуты.
dh-mars-inject-failed = M.A.R.S.: автоаптечка не готова или пуста.
dh-mars-carry-conscious = Носитель в сознании — экзоскелетом управляет он.

dh-zga-state-captured = провалено, десантник в плену

# Плен, летальная инъекция, сводка (п. 28, PR 4)
dh-zga-intel-sender = Допрос пленного
dh-zga-intel = Взят живым ассоциативный десантник { $name }. На допросе: ЗГА охотилась за { $target } ({ $job }). Сторона получает { $points } очков командования.
dh-zga-lethal-self = Вы выпускаете летальную дозу. Меры предосторожности соблюдены.
dh-zga-lethal-others = { $name } дёргается и обмякает.
dh-zga-faction-republic = Республика
dh-zga-faction-empire = Империя
dh-zga-summary-title = [color=#7ec8f0][bold]Ассоциативный десантник[/bold][/color]
dh-zga-summary-line = { $name } — цель: { $target } ({ $job }).
dh-zga-summary-outcome = Итог: { $outcome }. Лишних убийств: { $kills }.
dh-zga-summary-captured = Взят в плен — сторона: { $faction }.
dh-zga-outcome-hunting = задание не завершено к концу раунда
dh-zga-outcome-evacuating = цель устранена, эвакуация не завершена
dh-zga-outcome-evacuated = [color=#7ec8f0]цель устранена, эвакуирован[/color]
dh-zga-outcome-disavowed = [color=red]нарушил меры предосторожности, ЗГА отреклась[/color]
dh-zga-outcome-dead = погиб
dh-zga-outcome-captured = [color=red]взят в плен живым[/color]

dh-zga-precautions = [color=#7ec8f0]Меры предосторожности.[/color] ЗГА не нужна открытая война. Не вмешивайтесь в бой двух сторон. Калечить и убивать можно только цель и тех, кто оказывает вам сопротивление, — лишние жертвы не нужны. Живым не сдаваться. Нарушите — ЗГА публично отречётся от вас.
dh-alert-zga-tracker-name = Трекер цели
dh-alert-zga-tracker-desc = Направление на цель, после её устранения — на точку эвакуации.
