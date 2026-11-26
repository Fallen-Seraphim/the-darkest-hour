# Событие «ЧВК LA-V» в «Осаде» (план осады п. 11): отряд ЧВК на своём шаттле.
dh-lav-event-name = ЧВК LA-V
dh-lav-event-desc = Отряд частной военной компании LA-V прибывает на своём шаттле с заданием корпорации. Задание знает только отряд. Выход — когда заинтересуются не меньше шести; роли раздаются случайно, сначала командные.

dh-lav-role-commander = Командир отряда
dh-lav-role-commander-desc = Ведёт операцию и знает задание. LAVR-55, лёгкая броня.
dh-lav-role-deputy = Заместитель командира
dh-lav-role-deputy-desc = Правая рука командира, принимает отряд, если тот выбыл. LAVR-55, тяжёлая броня.
dh-lav-role-technician = Боевой техник
dh-lav-role-technician-desc = Ручной пулемёт LAVMG-14, сварка, инструменты, мешки с песком и колючая проволока — укрепить объект и держать его.
dh-lav-role-sniper = Снайпер-разведчик
dh-lav-role-sniper-desc = LAVR-90 и разведывательный прицел: видит дальше всех и снимает пулемётчиков.
dh-lav-role-medic = Медик
dh-lav-role-medic-desc = Держит отряд на ногах. LAVSMG-23, полевая хирургия.
dh-lav-role-operative = Боец
dh-lav-role-operative-desc = Основа отряда: LAVR-55 и тяжёлая броня.

dh-lav-name-format-commander = Командир { $part0 }
dh-lav-name-format-deputy = Заместитель { $part0 }
dh-lav-name-format-technician = Техник { $part0 }
dh-lav-name-format-sniper = Снайпер { $part0 }
dh-lav-name-format-medic = Медик { $part0 }
dh-lav-name-format-operative = Боец { $part0 }

dh-lav-shuttle-name = LA-V «Vanguard Runner»
dh-lav-target-tanker = танкер
dh-lav-target-sample = экспериментальный образец
dh-lav-zone-edge = окраина района операции

dh-lav-no-target = Операция LA-V отменена: на планете нет подходящей цели.
dh-lav-no-zone = Операция LA-V отменена: шаттлу некуда сесть.

dh-lav-briefing-hold = [color=#8A6AD0][bold]ЧВК LA-V.[/bold][/color] Шаттл в атмосфере над районом операции — готовьтесь. Посадка — с пульта автопилота в кабине (ПКМ → «Посадка» → ЗП); не выберете за { $minutes } мин — автопилот сядет сам. Задание корпорации — занять и удержать до конца операции: [bold]{ $target }[/bold] — водрузить там флаг LA-V и не дать поставить чужие. Обе стороны вам не союзники. Задание знает только отряд. Трекер в углу экрана показывает направление на объект.
dh-lav-briefing-sample = [color=#8A6AD0][bold]ЧВК LA-V.[/bold][/color] Шаттл в атмосфере над районом операции — готовьтесь. Посадка — с пульта автопилота в кабине (ПКМ → «Посадка» → ЗП); не выберете за { $minutes } мин — автопилот сядет сам. Задание корпорации — забрать [bold]{ $target }[/bold] и вывезти его на нашем шаттле (тот же пульт). Обе стороны вам не союзники. Задание знает только отряд. Трекер в углу экрана показывает направление на цель.
dh-lav-briefing-commander-hold = [color=#8A6AD0][bold]ЧВК LA-V.[/bold][/color] Шаттл в атмосфере над районом операции — готовьтесь. Посадка — с пульта автопилота в кабине (ПКМ → «Посадка» → ЗП); не выберете за { $minutes } мин — автопилот сядет сам. Задание корпорации — занять и удержать до конца операции: [bold]{ $target }[/bold] — водрузить там флаг LA-V и не дать поставить чужие. Обе стороны вам не союзники. Задание знает только отряд. Трекер в углу экрана показывает направление на объект. Вы командир: ведите отряд. Объект засчитан, если к концу операции в пределах { $radius } клеток от него стоит флаг LA-V (они у вас, заместителя и техника) и нет флагов сторон. Флаг LA-V на любой точке сторон глушит её — не захватить, нет очков и эффектов. Уйти на шаттле раньше — провал задания.
dh-lav-briefing-commander-sample = [color=#8A6AD0][bold]ЧВК LA-V.[/bold][/color] Шаттл в атмосфере над районом операции — готовьтесь. Посадка — с пульта автопилота в кабине (ПКМ → «Посадка» → ЗП); не выберете за { $minutes } мин — автопилот сядет сам. Задание корпорации — забрать [bold]{ $target }[/bold] и вывезти его на нашем шаттле (тот же пульт). Обе стороны вам не союзники. Задание знает только отряд. Трекер в углу экрана показывает направление на цель. Вы командир: ведите отряд. Образец засчитан, когда шаттл уйдёт с ним на борту.

dh-lav-task-hold = Задание: удержать до конца операции — { $target }: флаг LA-V в пределах { $radius } клеток от объекта, флагов сторон рядом нет.
dh-lav-task-sample = Задание: вывезти на шаттле — { $target }.
dh-lav-status = { $task } Шаттл: { $zone }. Состояние: { $state }.

dh-lav-outcome-active = операция идёт
dh-lav-outcome-held = объект удержан
dh-lav-outcome-stolen = образец вывезен
dh-lav-outcome-withdrew = отряд ушёл, задание не выполнено
dh-lav-outcome-failed = задание провалено
dh-lav-outcome-wiped = отряд уничтожен

dh-lav-zone-atmosphere = в атмосфере, ждёт посадки
dh-lav-verb-land = Посадка
dh-lav-console-choose-zone = Автопилот ждёт: выберите ЗП — ПКМ по пульту → «Посадка».
dh-lav-console-descending = Идёт снижение.
dh-lav-descent = Автопилот: снижение на { $zone }, посадка через { $seconds } с. Пристегнитесь!
dh-lav-descent-blocked = Автопилот: свободных ЗП нет, ждём и пробуем снова.
dh-lav-autoland-warning = Автопилот: через минуту посадка на свободную ЗП, если не выберете сами.
dh-lav-landed = Шаттл сел: { $zone }.
dh-lav-flag-standing = Сейчас: флаг LA-V стоит, чужих нет.
dh-lav-flag-missing = Сейчас: флага LA-V в зоне нет или рядом чужой флаг.
dh-lav-announcement-sender = Радиоразведка
dh-lav-landed-announcement = Радары засекли посадку неопознанного шаттла: { $zone }. Опознавательные знаки — частная военная компания LA-V.
dh-lav-departed-announcement = Шаттл ЧВК LA-V взлетел и покинул район операции.
dh-lav-wiped-announcement = Перехват: отряд ЧВК LA-V не выходит на связь со своим шаттлом.

dh-lav-console-denied = Пульт не отвечает: нужен допуск LA-V.
dh-lav-console-offline = Автопилот отключён.
dh-lav-console-start = Автопилот: подготовка к взлёту!

dh-lav-summary-title = [color=#8A6AD0][bold]ЧВК LA-V[/bold][/color]
dh-lav-summary-outcome = Итог: { $outcome }. Эвакуировано бойцов: { $evacuated }.
dh-lav-summary-member = — { $name } ({ $role })

dh-lav-admin-opened = Набор отряда LA-V открыт на 15 минут (минимум — { $min }).
dh-lav-admin-busy = Набор уже открыт или событие третьей стороны уже было в этом раунде.
dh-lav-admin-usage = Использование: dhcalllavsquad [минимум бойцов]
cmd-dhcalllavsquad-desc = Открывает набор призраков на событие «ЧВК LA-V» в «Осаде».
cmd-dhcalllavsquad-help = Использование: dhcalllavsquad [минимум бойцов, по умолчанию 6]
dh-admin-tab-call-lav-squad = ЧВК LA-V (Осада)
dh-admin-tab-call-lav-squad-tooltip = Открыть набор отряда LA-V (10 мест) на 15 минут. Высадка — когда запишутся шестеро.

dh-alert-lav-tracker-name = Объект задания
dh-alert-lav-tracker-desc = Направление на объект задания LA-V.

dh-job-lav-pmc-deputy-name = заместитель командира ЧВК LA-V
dh-job-lav-pmc-deputy-desc = Помогайте командиру и примите отряд, если он выбыл.
dh-job-lav-pmc-technician-name = боевой техник ЧВК LA-V
dh-job-lav-pmc-technician-desc = Держите объект пулемётом и укрепляйте его.

tiles-dh-lav-floor = палуба шаттла LA-V
