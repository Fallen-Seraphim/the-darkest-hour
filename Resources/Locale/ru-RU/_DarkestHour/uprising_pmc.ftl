## ЧВК LA-V (решение автора 2026-11-06)

dh-uprising-sender-vanguard = Lost Angels—Vanguard
dh-uprising-pmc-called = Lost Angels—Vanguard направляет в квартал эвакуационный борт с отрядом частной военной компании. Борт сядет на ЗП-1 «Аэронавтика» через { $minutes } мин. Сотрудникам LA-V — выдвигаться к площадке.
dh-uprising-pmc-called-evac = Эвакуационным бортом LA-V летит отряд частной военной компании. Посадка на ЗП-1 «Аэронавтика» через { $minutes } мин.
dh-uprising-pmc-landed = Отряд ЧВК LA-V высадился на ЗП-1 «Аэронавтика». Борт ждёт представителя корпорации { $minutes } мин. Любое вмешательство будет пресечено.
dh-uprising-pmc-call-done = Отряд ЧВК LA-V вызван: прилетит эвакуационным бортом на ЗП-1 через { $minutes } мин.
dh-uprising-pmc-call-done-now = Отряд ЧВК LA-V высажен с борта, стоящего на ЗП-1.
dh-uprising-pmc-call-pending = Отряд ЧВК LA-V уже в пути.
dh-uprising-briefing-pmc =
    Вы — боец частной военной компании Lost Angels—Vanguard.
    Корпорация отправила ваш отряд за своим представителем в квартале, охваченном беспорядками. Борт ждёт на ЗП-1 «Аэронавтика» недолго.
    Ваша задача — вывезти представителя LA-V любой ценой. Остальных сотрудников корпорации — по возможности. Слушайтесь командира отряда. Военная Полиция и Фронт вам не союзники, но и не цель: не начинайте чужую войну, если она не стоит между вами и представителем.

dh-job-lav-pmc-commander-name = командир ЧВК LA-V
dh-job-lav-pmc-commander-desc = Ведите отряд ЧВК: найдите представителя LA-V и доставьте его на борт.
dh-job-lav-pmc-medic-name = медик ЧВК LA-V
dh-job-lav-pmc-medic-desc = Держите на ногах отряд и представителя, пока борт не взлетит.
dh-job-lav-pmc-specialist-name = специалист ЧВК LA-V
dh-job-lav-pmc-specialist-desc = Прикрывайте отряд издалека из LAVR-90: стрелки, пулемётчики, засады.
dh-job-lav-pmc-rifleman-name = стрелок ЧВК LA-V
dh-job-lav-pmc-rifleman-desc = Идите первым в тяжёлой броне и прикрывайте представителя собой.
dh-job-lav-pmc-supervisors = совет директоров LA-V
dh-job-lav-pmc-supervisors-squad = командир отряда ЧВК

dh-ghost-role-lav-pmc-commander-name = Командир ЧВК LA-V
dh-ghost-role-lav-pmc-commander-desc = Ведите отряд из четырёх бойцов: вывезите представителя LA-V через ЗП-1 любой ценой, остальных — по возможности.
dh-ghost-role-lav-pmc-medic-name = Медик ЧВК LA-V
dh-ghost-role-lav-pmc-medic-desc = Боевой медик отряда ЧВК LA-V: представитель должен долететь живым.
dh-ghost-role-lav-pmc-specialist-name = Специалист ЧВК LA-V
dh-ghost-role-lav-pmc-specialist-desc = Стрелок-специалист отряда ЧВК LA-V с лёгкой снайперской винтовкой LAVR-90.
dh-ghost-role-lav-pmc-rifleman-name = Стрелок ЧВК LA-V
dh-ghost-role-lav-pmc-rifleman-desc = Штурмовик отряда ЧВК LA-V в тяжёлой броне с винтовкой LAVR-55.
dh-name-format-lav-pmc-commander = Командир {$part0}
dh-name-format-lav-pmc-medic = Медик {$part0}
dh-name-format-lav-pmc-specialist = Специалист {$part0}
dh-name-format-lav-pmc-rifleman = Стрелок {$part0}

dh-integrated-mask-occupied = Сначала снимите маску: в шлем встроен противогаз.
dh-armor-verb-class-parts = Класс [color=cyan]{ $level }[/color]: { $parts }.

cmd-dhcalllavpmc-desc = Вызывает отряд ЧВК LA-V (4 гост-роли) бортом «Аэронавтики» на ЗП-1 в режиме «Восстание».
cmd-dhcalllavpmc-help = Использование: dhcalllavpmc
dh-admin-tab-call-lav-pmc = Вызвать отряд ЧВК LA-V («Восстание»)
dh-admin-tab-call-lav-pmc-tooltip = Отряд из четырёх гост-ролей (командир, медик, специалист, стрелок) и так летит каждым эвакуационным бортом; кнопка нужна, чтобы прислать его без заказа эвакуации или ещё один на стоящий борт. Прилетает эвакуационным бортом на ЗП-1 «Аэронавтика»: если эвакуацию уже заказали — им, иначе борт с отрядом заказывается из бюджета LA-V (40 000 кр.) и прилетает через 3 мин; денег нет — не прилетит. Задача — вывезти представителя LA-V. Борт с отрядом ждёт 12 мин. Работает только в режиме «Восстание».
dh-uprising-pmc-call-no-funds = Эвакуация не заказана, а в бюджете LA-V { $balance } кр. из { $cost } — борт с отрядом не придёт.
dh-bank-note-evac-pmc = эвакуационный борт с отрядом ЧВК
