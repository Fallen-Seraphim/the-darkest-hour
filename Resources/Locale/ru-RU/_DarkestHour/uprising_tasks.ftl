# «Восстание», задания (решение автора 2026-11-12)

dh-task-ui-title-police = Задания Военной Полиции
dh-task-ui-title-front = Задания ОФСК
dh-task-ui-title-vanguard = Задания LA-V
dh-task-ui-balance = Счёт организации: { $balance } кр.
dh-task-ui-active = АКТИВНОЕ ЗАДАНИЕ
dh-task-ui-offers = ПРЕДЛОЖЕНИЯ (одно на выбор)
dh-task-ui-reward = Награда: { $reward } кр.
dh-task-ui-reward-time = Награда: { $reward } кр. Осталось: { $time }
dh-task-ui-accept = Взять
dh-task-ui-abandon = Отказаться
dh-task-ui-cooldown = Новые задания появятся через { $time }.
dh-task-ui-log = ЖУРНАЛ
dh-task-cant-start = Задание сейчас не начать (нет места или база недоступна).

dh-task-log-accepted = Взято: { $name }.
dh-task-log-done = Выполнено: { $name }.
dh-task-log-abandoned = Отказ: { $name }.
dh-task-log-expired = Время вышло: { $name }.
dh-task-log-exploded = Бомба взорвалась: { $name }.
dh-task-log-reward = +{ $amount } кр. — { $reason }
dh-task-log-arrest = арест: { $name }
dh-task-log-kill = ликвидация: { $name }
dh-task-log-forged = поддельный паспорт: { $name }

dh-task-hint-level = уровень { $level }
dh-task-hint = { $level }, метрах в { $meters } { $direction } от ориентира «{ $beacon }»
dh-task-dir-here = рядом
dh-task-dir-n = к северу
dh-task-dir-ne = к северо-востоку
dh-task-dir-e = к востоку
dh-task-dir-se = к юго-востоку
dh-task-dir-s = к югу
dh-task-dir-sw = к юго-западу
dh-task-dir-w = к западу
dh-task-dir-nw = к северо-западу

# военпол
dh-task-police-stash-name = Найти закладку
dh-task-police-stash-desc = Осведомитель сообщил о закладке. Найти и изъять.
dh-task-stash-text = Осведомитель: закладка — { $hint }. Найдите свёрток и изымите его (щелчок рукой).
dh-task-stash-found = Закладка изъята.
dh-task-stash-not-police = Какой-то свёрток. Лучше не трогать.

dh-task-police-defuse-name = Разминировать объект
dh-task-police-defuse-desc = Сообщение о самодельном взрывном устройстве. Код деактивации получен от сапёров.
dh-task-defuse-text = Взрывное устройство — { $hint }. Таймер — около { $minutes } мин. Код деактивации: [bold]{ $code }[/bold]. Неверный код сокращает таймер.

dh-task-police-database-name = Проверить базу данных
dh-task-police-database-desc = В базу паспортов просочились фальшивые записи. Найти и удалить.
dh-task-database-text = В базе данных — { $count } фальшивые записи с явными несостыковками. Найдите их на терминале базы и удалите. Удаление настоящей записи — штраф.
dh-task-database-left = Осталось найти: { $count }.

# бомба
dh-task-bomb-ui-title = Взрывное устройство
dh-task-bomb-ui-timer = ДО ВЗРЫВА: { $time }
dh-task-bomb-ui-defused = ОБЕЗВРЕЖЕНО
dh-task-bomb-ui-code = Код деактивации
dh-task-bomb-ui-enter = Ввести
dh-task-bomb-wrong = Неверный код! Таймер сокращён на { $seconds } с.
dh-task-bomb-defused = Устройство обезврежено.

# база данных военпола
dh-police-db-ui-title = База данных Военной Полиции — паспорта района
dh-police-db-ui-filter = Поиск по имени или номеру
dh-police-db-ui-columns = Имя | Номер | Пол, вид | Рождение, место | Выдан
dh-police-db-ui-delete = Удалить
dh-police-db-ui-confirm = Точно?
dh-police-db-ui-rules = [bold]Правила проверки[/bold]
    • Паспорт выдаётся не раньше 14 лет.
    • Серия номера (LA [bold]NN[/bold] ...) — две последние цифры года выдачи.
    • Номер паспорта не повторяется.
    • Дата выдачи — не позже сегодняшней (05.04.2318).
    • В месте рождения в год рождения уже жили (справка ниже).
    Фальшивку — удалить. Удаление настоящей записи — штраф 150 кр.
dh-police-db-ui-worlds = Справка: миры и годы заселения
dh-police-db-world = { $name } — с { $from }
dh-police-db-world-until = { $name } — { $from }–{ $until }
dh-police-db-offline = Нет связи с сервером базы данных.
dh-police-db-removed-fake = Фальшивая запись «{ $name }» удалена.
dh-police-db-removed-forged = Поддельный паспорт ОФСК «{ $name }» выявлен и аннулирован. Награда: { $reward } кр.
dh-police-db-removed-genuine = Удалена настоящая запись «{ $name }»! Штраф: { $fine } кр.
dh-police-db-fine-note = штраф: удалена настоящая запись ({ $name })
dh-loadout-group-lav-eyes = Очки

# ОФСК
dh-task-side-civilian = житель
dh-task-side-police = Военная Полиция
dh-task-side-vanguard = LA-V
dh-task-side-front = ОФСК
dh-task-front-extortion-name = Вымогательство
dh-task-front-extortion-desc = Выбить деньги из жителя района. Деньги приходят на счёт ОФСК, сверх того — премия. С военпола и LA-V — больше.
dh-task-extortion-text = Цель: [bold]{ $name }[/bold] ({ $side }). Требование: [bold]{ $amount } кр.[/bold] на счёт ОФСК { $account } до { $time }. Цель уже получила сообщение со скрытого номера. Убедите её заплатить — переводом со своего счёта. Если цель погибнет — задание провалено.
dh-task-extortion-sms = { $name }, мы знаем, где вы живёте. Переведите { $amount } кр. на счёт { $account } до { $time }. Иначе пожалеете. Полиции — ни слова.
dh-task-log-extortion-paid = { $name } заплатил(а) { $amount } кр.
dh-task-log-extortion-dead = Цель погибла: { $name }. Вымогательство сорвано.
dh-task-front-bombing-name = Подорвать объект
dh-task-front-bombing-desc = Командование требует громкой акции: заложить взрывчатку с таймером и подорвать указанный объект.
dh-task-bombing-text = Объект: [bold]{ $target }[/bold] — { $hint }. Заложите взрывчатку с таймером (C4) не дальше { $radius } м от сердца объекта и дождитесь взрыва.
dh-task-log-capture = пленный: { $name }

dh-bomb-target-transformer = трансформаторная подстанция района
dh-bomb-target-metro = станция метро
dh-bomb-target-clinic = поликлиника «Ворот»
dh-bomb-target-lav-office = офис LA-V
dh-bomb-target-lav-warehouse = склад LA-V
dh-bomb-target-police-lobby = дежурная часть Военной Полиции

# LA-V: продажи
dh-task-vanguard-sales-goods-name = План продаж: сувениры
dh-task-vanguard-sales-goods-desc = Региональный офис ждёт продаж корпоративной мелочи.
dh-task-vanguard-sales-devices-name = План продаж: техника и аксессуары
dh-task-vanguard-sales-devices-desc = Продать жителям технику и аксессуары LA-V.
dh-task-vanguard-sales-security-name = План продаж: средства защиты
dh-task-vanguard-sales-security-desc = Неспокойно в районе — продать жителям средства защиты.
dh-task-sales-text = Продайте [bold]{ $goods }[/bold] — { $count } шт. Засчитываются чеки чековой машинки: товар в лотке, продавец — LA-V (счёт корпорации или ваш), покупатель — не из LA-V. Товар можно заказать на складе LA-V.
dh-task-sales-left = Осталось продать: { $count }.
dh-task-extortion-paid = Уже переведено: { $paid } из { $amount } кр.

# рабочий компьютер
dh-work-ui-title = Рабочая станция
dh-work-ui-task = ЗАДАНИЕ — оплата { $reward } кр., попыток: { $left }
dh-work-ui-wait = Новое задание через { $time }.
dh-work-ui-idle = Заданий от руководства пока нет. Выпейте кофе.
dh-work-ui-answer = Ответ (число)
dh-work-ui-send = Отправить
dh-work-ui-earned = Заработано за смену: { $earned } кр.
dh-work-note = оплата работы в офисе
dh-work-correct = Верно! { $reward } кр. зачислено на ваш счёт.
dh-work-wrong = Неверно. Попыток осталось: { $left }.
dh-work-failed = Задание не принято (верный ответ: { $answer }). Следующее — позже.

dh-work-statement-head = [bold]Сверка выписки.[/bold] Остаток на начало дня: { $start } кр.
dh-work-statement-in = { $n }. Поступление: +{ $amount } кр. ({ $what })
dh-work-statement-out = { $n }. Списание: −{ $amount } кр. ({ $what })
dh-work-statement-ask = Какой остаток на конец дня?
dh-work-op-0 = оплата поставщику
dh-work-op-1 = аренда склада
dh-work-op-2 = поступление от клиента
dh-work-op-3 = возврат товара
dh-work-op-4 = комиссия банка
dh-work-op-5 = премия сотрудникам
dh-work-op-6 = оплата счёта
dh-work-op-7 = налоговый платёж
dh-work-interest = [bold]Расчёт процентов.[/bold] Клиент взял кредит { $principal } кр. под { $rate }% годовых (простые проценты) на { $months } мес. Сколько процентов он заплатит?
dh-work-vat = [bold]Налог.[/bold] Сумма счёта с налогом 20%: { $gross } кр. Сколько в ней налога?
dh-work-month-jan = янв
dh-work-month-feb = фев
dh-work-month-mar = мар
dh-work-month-apr = апр
dh-work-month-may = май
dh-work-month-jun = июн
dh-work-invoice-head = [bold]Накладная.[/bold] Посчитайте итог:
dh-work-invoice-line = { $n }. { $item }: { $qty } шт. × { $price } кр.
dh-work-invoice-ask = Итого по накладной?
dh-work-item-0 = бумага для принтера
dh-work-item-1 = картриджи
dh-work-item-2 = ручки LA-V
dh-work-item-3 = кофе в зёрнах
dh-work-item-4 = папки-регистраторы
dh-work-item-5 = батарейки
dh-work-item-6 = кабели связи
dh-work-item-7 = ежедневники
dh-work-payroll = [bold]Зарплатная ведомость.[/bold] Ставка { $rate } кр./час, отработано { $hours } ч и ещё { $overtime } ч сверхурочно (оплачиваются ×1,5). Сколько начислить?
dh-work-chart-head = [bold]График выручки отдела[/bold] по месяцам (тыс. кр.):
dh-work-chart-ask-max = Номер месяца с [bold]наибольшей[/bold] выручкой?
dh-work-chart-ask-min = Номер месяца с [bold]наименьшей[/bold] выручкой?
