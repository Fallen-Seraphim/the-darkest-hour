## «Осада» (бывший «Штурм») — базовый режим: имперский десант штурмует военную базу Республики на REP-27-7

dh-assault-title = Осада
dh-assault-description = Имперский десантный авианосец прыгает из соседней системы, чтобы штурмовать базу республиканского гарнизона на REP-27-7 и вывезти экспериментальный образец. У гарнизона есть несколько минут, чтобы окопаться.

dh-assault-carrier-name = IC-3 «Суверен»
dh-celestial-rep27-7 = REP-27-7
dh-celestial-rep27-7-desc = Жаркий пустынный мир системы REP-27 с разреженным, но пригодным для дыхания воздухом. Здесь стоит военная база Республиканского армейского корпуса «Терминус», её орудия ПКО врыты в горный массив.
location-dh-experimental-sample = экспериментальный образец
id-card-access-level-dh-republic-command = Командование гарнизона Республики

dh-assault-sender-republic = Республиканский армейский корпус
dh-assault-sender-empire = Командование имперского флота
dh-assault-outcome-sender = Боевое донесение

## Брифинги

dh-assault-briefing-republic = Вы — гарнизон Соединённой Республики.
    Задача: не отдать экспериментальный образец. Имперский авианосец откроет разрыв примерно через {$minutes} мин. — окапывайтесь и занимайте позиции ПВО.
    Эскадра республиканского флота подойдёт примерно через {$relief} мин. — продержитесь до её подхода.
    Подробнее о войне и сторонах — в гайдбуке, раздел «Лор».
dh-assault-briefing-empire = Вы — имперская морская пехота на борту десантного авианосца IC-3 «Суверен».
    Задача: высадиться, взять базу Республики и доставить экспериментальный образец на борт. Разрыв откроется примерно через {$minutes} мин.
    Эскадра республиканского флота подойдёт примерно через {$relief} мин. — к этому времени нас здесь быть не должно. Берегите авианосец — это единственный путь домой.
    Подробнее о войне и сторонах — в гайдбуке, раздел «Лор».
dh-assault-reinforced = Вы возвращаетесь в бой со следующей волной подкреплений. Подкреплений у вашей стороны осталось: {$tickets}.
dh-assault-next-wave = Вы пали. Следующая волна подкреплений прибудет через {$minutes} мин. {$seconds} с (у вашей стороны осталось {$tickets}).
dh-assault-no-reinforcements = Вы пали. Подкреплений у вашей стороны больше нет.
dh-assault-wave-arrived = Прибыло подкрепление: {$count} { $count ->
    [one] боец
    [few] бойца
   *[other] бойцов
}. Подкреплений осталось: {$tickets}.

## Ход боя

dh-assault-prep-over-empire = Разрывной генератор готов. Прыгайте в систему REP-27 через навигационный компьютер разрыва и спускайтесь на REP-27-7 через компьютер посадки.
dh-assault-prep-over-republic = Сенсоры фиксируют нарастающую сигнатуру разрыва в соседней системе. Имперский авианосец может появиться в любой момент. Всем позициям — боевая готовность!
dh-assault-carrier-arrived = ТРЕВОГА! Имперский десантный авианосец вошёл в систему REP-27. Ожидайте высадки на военную базу «Терминус». Всему личному составу занять боевые позиции!
dh-assault-carrier-arrived-empire = Авианосец в системе REP-27. Спускайтесь на REP-27-7 и захватите экспериментальный образец.
dh-assault-garrison-destroyed = Гарнизон военной базы «Терминус» уничтожен. Захватите экспериментальный образец, доставьте его на борт и покиньте систему.
dh-assault-carrier-falling = ПРОБОИНА В КОРПУСЕ! Авианосец теряет высоту и падает! Приготовиться к удару!
dh-assault-carrier-hit = Имперский авианосец подбит и падает недалеко от базы!
dh-assault-crashed = Авианосец разбился на REP-27-7. Выживите и держитесь — Республика будет вас искать.
dh-assault-search-operation = Имперский авианосец сбит! Начинайте поисковую операцию: найдите и уничтожьте выживших на месте крушения.

## Итоги

dh-assault-outcome-imperial-major = Крупная победа Империи: экспериментальный образец вывезен, гарнизон военной базы «Терминус» уничтожен.
dh-assault-outcome-imperial-minor = Малая победа Империи: Империя добилась части своих целей.
dh-assault-outcome-republic-minor = Малая победа Республики: десант отбит, Империя отступила без образца.
dh-assault-outcome-republic-major = Крупная победа Республики: имперский авианосец сбит и разбился на REP-27-7.
dh-assault-outcome-none = Бой за военную базу «Терминус» закончился без результата.
dh-assault-end-sample-taken = Экспериментальный образец вывезен Империей.
dh-assault-end-sample-kept = Экспериментальный образец остался на REP-27-7.
dh-assault-end-sample-stolen = Экспериментальный образец похитила ЧВК LA-V — он не достался ни одной из сторон.
dh-assault-sample-stolen = Экспериментальный образец вывезен с планеты шаттлом ЧВК LA-V. Корпорация забрала то, за что воевали обе стороны.
dh-assault-end-tickets = Подкреплений осталось: Республика {$republic}, Империя {$empire}.

## Отделы и должности

dh-department-republic-garrison = Гарнизон Республики
dh-department-republic-garrison-description = Гарнизон военной базы «Терминус» на REP-27-7: удержать базу и экспериментальный образец, сбить имперский авианосец.
dh-department-imperial-carrier = Экипаж авианосца
dh-department-imperial-carrier-description = Экипаж имперского десантного авианосца IC-3 «Суверен»: вести корабль, поддерживать десант огнём, сберечь корабль.
dh-department-imperial-marines = Имперская морская пехота
dh-department-imperial-marines-description = Десант Имперского корпуса морской пехоты: штурмовать военную базу «Терминус» и вывезти экспериментальный образец.

dh-job-garrison-supervisors = командиру базы
dh-job-empire-supervisors = капитану авианосца и командиру десанта

dh-job-garrison-commander-name = Командир базы
dh-job-garrison-commander-desc = Командуйте военной базой «Терминус»: организуйте оборону, сберегите экспериментальный образец, решайте, куда бьёт ПВО.
dh-job-garrison-officer-name = Офицер гарнизона
dh-job-garrison-officer-desc = Командуйте участком гарнизона под началом командира базы. Есть доступ в хранилище образца.
dh-job-garrison-rifleman-name = Стрелок гарнизона
dh-job-garrison-rifleman-desc = Костяк обороны военной базы «Терминус».
dh-job-garrison-machinegunner-name = Пулемётчик гарнизона
dh-job-garrison-machinegunner-desc = Держите подступы к базе пулемётным огнём.
dh-job-garrison-sniper-name = Снайпер гарнизона
dh-job-garrison-sniper-desc = Выбивайте десант на дальней дистанции.
dh-job-garrison-flamer-name = Огнемётчик гарнизона
dh-job-garrison-flamer-desc = Выжигайте штурмующих в ближнем бою.
dh-job-garrison-aa-gunner-name = Зенитчик гарнизона
dh-job-garrison-aa-gunner-desc = Обслуживайте зенитные батареи базы и сбейте имперский авианосец.
dh-job-garrison-medic-name = Медик гарнизона
dh-job-garrison-medic-desc = Держите защитников на ногах.
dh-job-garrison-sapper-name = Сапёр гарнизона
dh-job-garrison-sapper-desc = Укрепляйте базу, окапывайтесь и чините то, что ломает враг.

dh-job-carrier-captain-name = Капитан авианосца
dh-job-carrier-captain-desc = Командуйте IC-3 «Суверен»: прыжок, спуск и отход. Без авианосца никто не вернётся домой.
dh-job-carrier-pilot-name = Пилот авианосца
dh-job-carrier-pilot-desc = Ведите авианосец: разрывной прыжок в систему REP-27, спуск и уход.
dh-job-carrier-artillerist-name = Артиллерист авианосца
dh-job-carrier-artillerist-desc = Поддерживайте десант огнём орудий авианосца.
dh-job-carrier-technician-name = Техник авианосца
dh-job-carrier-technician-desc = Держите авианосец в воздухе: чините корпус, оборудование разрыва и двигатели.
dh-job-marine-commander-name = Командир десанта
dh-job-marine-commander-desc = Ведите имперскую морскую пехоту на штурм военной базы «Терминус».
dh-job-marine-weapons-specialist-name = Специалист по вооружению десанта
dh-job-marine-weapons-specialist-desc = Тяжёлое оружие десанта; выберите комплект перед прыжком.
dh-job-marine-field-medic-name = Полевой медик десанта
dh-job-marine-field-medic-desc = Держите морпехов в строю; выберите комплект перед прыжком.
dh-job-marine-machinegunner-name = Пулемётчик десанта
dh-job-marine-machinegunner-desc = Подавляйте обороняющихся; выберите комплект перед прыжком.
dh-job-marine-rifleman-name = Стрелок десанта
dh-job-marine-rifleman-desc = Костяк имперского десанта; выберите комплект перед прыжком.
dh-job-marine-squad-leader-name = Командир отряда
dh-job-marine-squad-leader-desc = Ведёт один из трёх отрядов десанта — Тау, Эпсилон или Омега. Отмечает цели, вызывает огонь и держит отряд вместе.
dh-job-garrison-artillery-officer-name = Артиллерийский офицер
dh-job-garrison-artillery-officer-desc = Руководите огневой поддержкой гарнизона: миномёты, пехотные пушки и батарея ПКО. Находите цели и вызывайте огонь.
dh-job-garrison-mortar-commander-name = Командир миномётного расчёта
dh-job-garrison-mortar-commander-desc = Ведите миномётный расчёт: выбирайте позиции, отмечайте цели лазерным целеуказателем и следите за боекомплектом.
dh-job-garrison-mortar-operator-name = Оператор миномёта
dh-job-garrison-mortar-operator-desc = Разворачивайте миномёт, наводите его по меткам и не давайте ему остыть.
dh-job-garrison-field-artillerist-name = Артиллерист
dh-job-garrison-field-artillerist-desc = Расчёт 90-мм пехотной пушки: закрепите её на позиции, наводите и заряжайте.
dh-job-garrison-tank-commander-name = Командир танка
dh-job-garrison-tank-commander-desc = Командуйте танком гарнизона: выбирайте цели, работайте спаренным пулемётом и дымом, берегите экипаж.
dh-job-garrison-tank-crew-name = Член экипажа танка
dh-job-garrison-tank-crew-desc = Ведите танк или наводите его пушку; между боями заваривайте пробоины.
dh-job-garrison-apc-commander-name = Командир БТР
dh-job-garrison-apc-commander-desc = Командуйте БТР гарнизона: везите отделения туда, где они нужны, и прикрывайте их автопушкой.
dh-job-garrison-apc-crew-name = Член экипажа БТР
dh-job-garrison-apc-crew-desc = Ведите БТР или работайте его орудиями; между боями заваривайте пробоины.
dh-job-carrier-operation-commander-name = Командующий операцией
dh-job-carrier-operation-commander-desc = Вы ведёте всю операцию с авианосца: ставьте задачу, направляйте десант, экипажи и огневую поддержку.
dh-job-carrier-support-officer-name = Офицер вспомогательной поддержки
dh-job-carrier-support-officer-desc = Координируйте средства поддержки: орбитальный скан, корабельную артиллерию, миномёты, пушки и сброс техники.
dh-job-marine-mortar-commander-name = Командир миномётного расчёта
dh-job-marine-mortar-commander-desc = Ведите миномётный расчёт: выбирайте позиции, отмечайте цели лазерным целеуказателем и следите за боекомплектом.
dh-job-marine-mortar-operator-name = Оператор миномёта
dh-job-marine-mortar-operator-desc = Разворачивайте миномёт, наводите его по меткам и не давайте ему остыть.
dh-job-marine-field-artillerist-name = Артиллерист
dh-job-marine-field-artillerist-desc = Расчёт 75-мм пехотной пушки: доставьте её на поверхность, закрепите на позиции, наводите и заряжайте.
dh-job-marine-tank-commander-name = Командир танка
dh-job-marine-tank-commander-desc = Командуйте танком морпехов: обеспечьте сброс, выбирайте цели, работайте спаренным пулемётом и дымом, берегите экипаж.
dh-job-marine-tank-crew-name = Член экипажа танка
dh-job-marine-tank-crew-desc = Ведите танк или наводите его пушку; между боями заваривайте пробоины.
dh-job-marine-apc-commander-name = Командир БТР
dh-job-marine-apc-commander-desc = Командуйте БТР морпехов: обеспечьте сброс, везите отделения туда, где они нужны, и прикрывайте их автопушкой.
dh-job-marine-apc-crew-name = Член экипажа БТР
dh-job-marine-apc-crew-desc = Ведите БТР или работайте его орудиями; между боями заваривайте пробоины.

## Команды

cmd-dhcarrierdamage-desc = Наносит урон имперскому авианосцу раунда «Осада» (попадание ПВО). При нулевой прочности корпуса он падает на планету.
cmd-dhcarrierdamage-help = Использование: dhcarrierdamage <урон> (прочность корпуса авианосца — 100)
cmd-dhcarrierdamage-done = Авианосец получил {$amount} урона.
cmd-dhcarrierdamage-no-carrier = Нет активного раунда «Осада» с авианосцем, который ещё можно подбить.

## Укрепления: мешки с песком и палатки

dh-stack-sandbags-empty = пустые мешки с песком
dh-stack-sandbags-full = мешки с песком
dh-sandbags-not-enough = Для баррикады нужно не меньше {$count} мешков.
dh-sandbags-blocked = Перед вами нет места для баррикады.
dh-sandbags-built = Вы складываете мешки в баррикаду.
dh-sandbags-need-ground = Встаньте на голую землю, чтобы наполнить мешки.
dh-sandbags-filled = Вы наполняете { $count ->
    [one] {$count} мешок
    [few] {$count} мешка
   *[other] {$count} мешков
}.
dh-tent-no-room = Перед вами нет места для палатки.
dh-tent-deployed = Вы ставите палатку.
dh-tent-fold-verb = Свернуть палатку

## Bunker and levels
dh-bunker-name = Бункер «Терминуса»
dh-ladder-blocked = Проход перекрыт.
dh-ladder-start-down = {CAPITALIZE($user)} начинает спускаться.
dh-ladder-start-up = {CAPITALIZE($user)} начинает подниматься.
dh-ladder-arrived-down = {CAPITALIZE($user)} спускается по лестнице.
dh-ladder-arrived-up = {CAPITALIZE($user)} выбирается из люка.
dh-ladder-somewhere = куда-то
dh-ladder-examine-down = Ведёт вниз: [color=yellow]{$where}[/color].
dh-ladder-examine-up = Ведёт наверх: [color=yellow]{$where}[/color].
dh-lift-verb-call = Вызвать лифт
dh-lift-broken = Лифт не отвечает.
dh-lift-busy = Лифт уже едет.
dh-lift-departing = Лифт отправляется через {$seconds} с! Освободите платформу или поезжайте.
dh-lift-arriving = Лифт прибывает через {$seconds} с! Освободите платформу.

## Dropships and landing zones
dh-dropship-name-alpha = Челнок «Альфа»
dh-dropship-name-bravo = Челнок «Браво»
dh-lz-name = ЗП-{$number}
dh-lz-temporary-name = Полевая ЗП-{$number}
dh-lz-beacon-not-surface = Установите его на поверхности планеты.
dh-lz-beacon-deployed = {$zone} установлена: здесь могут садиться челноки.
dh-landing-no-surface-landing = Авианосец не может сесть: на поверхность спускаются челноки.
dh-dropship-busy = Челнок уже в пути.
dh-dropship-zone-unavailable = Зона посадки недоступна.
dh-dropship-no-hangar = Нет ангара, куда вернуться.
dh-dropship-hangar = Ангар: {$carrier}
dh-dropship-hangar-lost = Авианосец потерян.
dh-dropship-in-air = в воздухе
dh-dropship-course = Новый курс: {$destination}.
dh-dropship-departing = Вылет на {$destination} через {$seconds} с! Занять места.
dh-dropship-aborted = Вылет отменён.
dh-dropship-launched = Взлёт. Курс: {$destination}, прибытие через {$seconds} с.
dh-dropship-docked = Пристыкован к {$carrier}.
dh-dropship-hovering = Зона посадки занята или потеряна: зависаем, выберите другое направление.
dh-dropship-landed = Посадка: {$zone}. Шасси выпущены, рампу — с пульта.
dh-dropship-note-lost = потеряна
dh-dropship-note-other-system = в другой системе
dh-dropship-note-here = мы здесь
dh-dropship-note-blocked = занята
dh-dropship-ui-title = Челнок
dh-dropship-ui-abort = Отменить вылет
dh-dropship-ui-destinations = Направления
dh-dropship-ui-no-destinations = В этой системе нет зон посадки.
dh-dropship-ui-state-docked = Пристыкован в ангаре.
dh-dropship-ui-state-departing = Готовится к вылету.
dh-dropship-ui-state-flight = В полёте.
dh-dropship-ui-state-landed = На земле.
dh-dropship-ui-location = Местоположение: {$location}
dh-dropship-ui-flight = Курс: {$destination}, {$seconds} с.

## Comms
dh-chat-radio-republic-command = Командование гарнизона
dh-chat-radio-imperial-command = Имперское командование
dh-relay-broken = Ретранслятор умолкает!
dh-relay-repaired = Ретранслятор снова гудит.
dh-relay-examine-active = Он [color=green]ретранслирует[/color] радиосети.
dh-relay-examine-broken = Он [color=red]молчит[/color]: заварите его, чтобы вернуть в строй.
dh-relay-portable-deployed = {CAPITALIZE($user)} разворачивает переносной ретранслятор.
dh-relay-portable-fold-verb = Свернуть ретранслятор
dh-relay-portable-no-ground = Здесь не на чем поставить ретранслятор.
dh-phone-busy-self = Линия уже занята.
dh-phone-busy = {$phone}: занято.
dh-phone-ui-title = Полевой телефон
dh-phone-ui-answer = Ответить
dh-phone-ui-hang-up = Положить трубку
dh-phone-ui-book = Справочник
dh-phone-ui-empty = Других телефонов на линии нет.
dh-phone-ui-entry-busy = {$name} (занято)
dh-phone-ui-idle = Линия свободна.
dh-phone-ui-calling = Вызов: {$other}...
dh-phone-ui-ringing = Входящий звонок: {$other}!
dh-phone-ui-in-call = На линии: {$other}. Говорите рядом с телефоном.

## Recon and camouflage
dh-motion-detector-on = Детектор движения щёлкает и включается.
dh-motion-detector-off = Детектор движения затихает.
dh-camo-net-deployed = {CAPITALIZE($user)} растягивает маскировочную сеть.
dh-camo-net-fold-verb = Свернуть сеть
dh-scan-sender = Орбитальное сканирование
dh-scan-status-no-surface = Внизу нет поверхности для сканирования.
dh-scan-status-cooldown = Перезарядка: {$minutes} мин {$seconds} с.
dh-scan-status-ready = Готов к сканированию.
dh-scan-ui-title = Глобальное орбитальное сканирование
dh-scan-ui-surface = Поверхность внизу: {$surface}. Скан охватывает всю поверхность.
dh-scan-ui-no-surface = Внизу нет поверхности.
dh-scan-ui-scan = СКАНИРОВАТЬ
dh-scan-ui-report = Последний отчёт
dh-scan-ui-no-report = Сканирований ещё не было.
dh-scan-report-header = ОТЧЁТ СКАНИРОВАНИЯ — {$surface}
dh-scan-report-empty = Позиции противника не обнаружены (возможна маскировка).
dh-scan-line = {$name}: ({$x}, {$y})
dh-scan-kind-anti-orbital = Бункер ПКО
dh-scan-kind-base = База Республики
dh-scan-kind-anti-air = Зенитка
dh-scan-kind-infantry = Крупное скопление пехоты (~{$count})

## Hearing
dh-tinnitus-start = У вас звенит в ушах!

## Barbed wire
dh-stack-barbed-wire = колючая проволока
dh-sandbag-wire-cut = Колючая проволока рвёт вас!
dh-wireable-already-wired = На нём уже есть колючая проволока.
dh-wireable-wired = {CAPITALIZE($user)} натягивает колючую проволоку по забору.
dh-construction-barbed-wire-obstacle = проволочное заграждение
dh-construction-barbed-wire-obstacle-desc = Два мотка колючей проволоки, закреплённые на кольях: пробираться медленно и больно.
dh-construction-barbed-wire = колючая проволока
dh-construction-barbed-wire-desc = Моток колючей проволоки, скрученный из листа стали.
dh-construction-sandbag = пустой мешок для песка
dh-construction-sandbag-desc = Мешок для песка, сшитый из ткани. Наполните его лопатой на голой земле.

dh-trench-need-ground = Окоп можно выкопать только в голой земле.
dh-trench-blocked = Здесь что-то мешает.
dh-weapon-mount-examine-nest = [color=#9fd06f]Пулемётное гнездо[/color]: в окопе или за мешками пулемёт бьёт кучнее.

dh-republic-kit-window-title = Республиканское снабжение
dh-republic-kit-window-description = Выберите снаряжение. Кейс превратится в выбранный комплект: оружие, боеприпасы и модульная броня РМБ-1 с особой системой.
dh-republic-set-rifleman-rifle-name = Винтовка: RAR-M-11-1
dh-republic-set-rifleman-rifle-description = Базовая винтовка RAR-M-11-1: четыре магазина FMG, один бронебойный BB и граната. РМБ-1 с композитным торсом; особая система: автомедик.
dh-republic-set-rifleman-marksman-name = Марксман: RAR-M-11-2
dh-republic-set-rifleman-marksman-description = Марксманская винтовка RAR-M-11-2 с четырьмя магазинами 7,62 мм FMG. РМБ-1 с композитным торсом; особая система: дополнительное хранилище.
dh-republic-set-rifleman-smg-name = ПП: RAP-TOR-M-12-16
dh-republic-set-rifleman-smg-description = Пистолет-пулемёт RAP-TOR-M-12-16: три магазина экспансивных и два бронебойных SVP. РМБ-1 с композитным торсом; особая система: автомедик.
dh-republic-set-machinegunner-light-name = Ручной пулемёт: RAMG-M-14-1
dh-republic-set-machinegunner-light-description = Пулемёт поддержки RAMG-M-14-1 с тремя коробами по 100 патронов. Лёгкие пластины РМБ-1; особая система: автомедик.
dh-republic-set-machinegunner-heavy-name = Станковый пулемёт: RSPMG-10-54
dh-republic-set-machinegunner-heavy-description = Станковый пулемёт RSPMG-10-54 с тремя лентами. Тяжёлые пластины РМБ-1 и тяжёлые бронированные магнитные сапоги (переобуйтесь); особая система: автомедик.
dh-republic-set-sniper-antimateriel-name = Противотанковое ружьё: RMAR-AS-M-15-18
dh-republic-set-sniper-antimateriel-description = Ружьё 12,7 мм RMAR-AS-M-15-18: три магазина SVP и один A, бинокль. Лёгкие пластины РМБ-1; особая система: автомедик.
dh-republic-set-sniper-marksman-name = Марксманская винтовка: RMAD-AS-M-15-17
dh-republic-set-sniper-marksman-description = Винтовка 10×51 мм RMAD-AS-M-15-17: три магазина FMG и два SVP, бинокль. Лёгкие пластины РМБ-1; особая система: автомедик.
dh-republic-set-medic-smg-name = ПП: RAP-TOR-M-12-16
dh-republic-set-medic-smg-description = Пистолет-пулемёт RAP-TOR-M-12-16: два магазина экспансивных и два SVP. Лёгкие пластины РМБ-1; особая система: автомедик.
dh-republic-set-medic-rifle-name = Винтовка: RAR-M-11-1
dh-republic-set-medic-rifle-description = Винтовка RAR-M-11-1 с четырьмя магазинами FMG. Лёгкие пластины РМБ-1; особая система: автомедик.
dh-republic-set-officer-rifle-name = Винтовка: RAR-M-11-2
dh-republic-set-officer-rifle-description = Винтовка RAR-M-11-2: три магазина FMG и один экспансивный. Композитные пластины РМБ-1 на среднем экзоскелете; особая система: ком-модуль с приказами.
dh-republic-set-officer-smg-name = ПП: RAP-TOR-M-12-16
dh-republic-set-officer-smg-description = Пистолет-пулемёт RAP-TOR-M-12-16: два магазина экспансивных и два SVP. Композитные пластины РМБ-1 на среднем экзоскелете; особая система: ком-модуль с приказами.
dh-sandbag-intact = Мешки целы — досыпать нечего.
dh-sandbag-repaired = Вы досыпаете мешки в баррикаду.
dh-dropship-ui-flares = ОТСТРЕЛИТЬ ЛТЦ ({$count}/{$max})
dh-dropship-ui-flares-active = ЛТЦ прикрывают челнок ({$count}/{$max})
dh-dropship-flares-fired = Отстрел ЛТЦ! ПВО противника потеряет челнок до конца захода. Осталось залпов: {$left}.

# Баррикады (DHBarricade)
dh-construction-barricade-metal = металлическая баррикада
dh-construction-barricade-metal-desc = Баррикада из четырёх листов стали. Держит большинство пуль спереди.
dh-construction-barricade-plasteel = пласталевая баррикада
dh-construction-barricade-plasteel-desc = Баррикада из четырёх листов пластали. Держит почти любую пулю спереди.

# Метки телепорта призраков
dh-warp-terminus = База «Терминус» (штаб)
dh-warp-carrier = Авианосец «Суверен» (мостик)

# Командование сторон, командный доступ Империи, сети «СКАТ» и снабжения (2026-11-13)
dh-department-republic-command = Командование Республики
dh-department-republic-command-description = Командование военной базы «Терминус»: командир базы и его офицеры. Появляются в комендатуре.
dh-department-imperial-command = Имперское командование
dh-department-imperial-command-description = Командование имперской десантной операции: командующий операцией, капитан авианосца, командир десанта и офицер поддержки. Появляются в своих каютах.
id-card-access-level-dh-imperial-command = Командование имперской операции
dh-chat-radio-imperial-skat = СКАТ
dh-chat-radio-imperial-supply = Имперские поставки

## Сокращения должностей в рации (решение автора 2026-11-19)
dh-job-abbrev-garrison-commander = КБ
dh-job-abbrev-garrison-officer = ОФ
dh-job-abbrev-garrison-artillery-officer = АО
dh-job-abbrev-garrison-rifleman = СТР
dh-job-abbrev-garrison-machinegunner = ПУЛ
dh-job-abbrev-garrison-sniper = СНП
dh-job-abbrev-garrison-flamer = ОГН
dh-job-abbrev-garrison-aa-gunner = ЗЕН
dh-job-abbrev-garrison-medic = МЕД
dh-job-abbrev-garrison-sapper = САП
dh-job-abbrev-garrison-mortar-commander = КМР
dh-job-abbrev-garrison-mortar-operator = ОМ
dh-job-abbrev-garrison-field-artillerist = АРТ
dh-job-abbrev-garrison-tank-commander = КТ
dh-job-abbrev-garrison-tank-crew = ЭТ
dh-job-abbrev-garrison-apc-commander = КБТР
dh-job-abbrev-garrison-apc-crew = ЭБТР
dh-job-abbrev-carrier-operation-commander = КО
dh-job-abbrev-carrier-captain = КА
dh-job-abbrev-carrier-support-officer = ОВП
dh-job-abbrev-marine-commander = КД
dh-job-abbrev-marine-squad-leader = КОТ
dh-job-abbrev-marine-weapons-specialist = СВ
dh-job-abbrev-marine-field-medic = МЕД
dh-job-abbrev-marine-machinegunner = ПУЛ
dh-job-abbrev-marine-rifleman = СТР
dh-job-abbrev-carrier-pilot = ПИЛ
dh-job-abbrev-carrier-attack-pilot = ПШ
dh-job-abbrev-carrier-artillerist = АРТ
dh-job-abbrev-carrier-technician = ТЕХ
dh-job-abbrev-marine-mortar-commander = КМР
dh-job-abbrev-marine-mortar-operator = ОМ
dh-job-abbrev-marine-field-artillerist = АРТ
dh-job-abbrev-marine-tank-commander = КТ
dh-job-abbrev-marine-tank-crew = ЭТ
dh-job-abbrev-marine-apc-commander = КБТР
dh-job-abbrev-marine-apc-crew = ЭБТР

# Полёт челнока на автопилоте над полем боя (решение автора 2026-11-19)
dh-dropship-descending = Над {$zone}. Снижение!
dh-dropship-hovering-lost = Зона посадки потеряна: зависаем, выберите другое направление.
dh-dropship-manual-unavailable = Взять управление можно только в полёте над полем боя.
dh-dropship-manual-on = Пилот взял управление.
dh-dropship-ui-manual = ВЗЯТЬ УПРАВЛЕНИЕ (WASD)
dh-dropship-ui-autopilot = ВКЛЮЧИТЬ АВТОПИЛОТ
dh-dropship-ui-phase-transit = Переход орбита — атмосфера.
dh-dropship-ui-phase-cruise = Полёт над полем боя.
dh-dropship-ui-phase-descend = Снижение.
dh-dropship-ui-phase-climb = Набор высоты.
dh-dropship-ui-phase-hover = Висим в воздухе.
dh-dropship-ui-phase-manual = Ручное управление.
dh-dropship-atmosphere-unavailable = Подняться в атмосферу можно только из полёта над полем боя.
dh-dropship-atmosphere-entered = Челнок в слое атмосферы: заход на выброску, пилот ведёт вручную.
dh-dropship-atmosphere-left = Челнок вернулся на высоту полёта над полем боя.
dh-dropship-ui-atmosphere-enter = ПОДНЯТЬСЯ В АТМОСФЕРУ (ВЫБРОСКА)
dh-dropship-ui-atmosphere-leave = ВЕРНУТЬСЯ НАД ПОЛЕМ БОЯ
dh-dropship-ui-release = ОТПУСТИТЬ УПРАВЛЕНИЕ
dh-dropship-ui-phase-atmosphere = Слой атмосферы, заход на выброску. Под нами: {$x}, {$y}.

# Подкрепления на грузовиках (план осады п. 2)
dh-reinforcement-entry-east = восточный въезд
dh-reinforcement-entry-west = западный въезд
dh-reinforcement-entry-unknown = въезд
dh-reinforcement-truck-sender = Автоколонна
dh-reinforcement-truck-announce = Грузовик с подкреплением прибыл ({ $entry }). Ждите на базе.
dh-reinforcement-truck-boarded = Вы в кузове грузовика с подкреплением ({ $entry }). Сядьте за руль (кабина) или дождитесь водителя и езжайте на базу по шоссе.

# Приветствие карты «Терминус» (план осады п. 23, решение автора 2026-11-20): своё у каждой карты, задаётся на станции
# карты (DHAssaultMapBriefing). Коротко: кто ты, где ты, задача и сроки; лор — в гайдбуке.
dh-assault-briefing-terminus-republic = Вы — гарнизон военной базы «Терминус» на REP-27-7.
    Задача: не отдать экспериментальный образец — он в хранилище на нижнем ярусе бункера под базой. Имперский авианосец откроет разрыв примерно через {$minutes} мин.
    Держите точки — дамбу, нефтяной терминал, деревню, завод, АЭС и космодром — и горные ПКО, ПВО и РЛС: без них авианосец и челноки работают свободно.
    Эскадра республиканского флота подойдёт примерно через {$relief} мин. — продержитесь до её подхода. Подкрепления приходят грузовиками по шоссе Н-14 с запада и востока.
    Подробнее о войне и сторонах — в гайдбуке, раздел «Лор».
dh-assault-briefing-terminus-empire = Вы — имперская морская пехота на борту десантного авианосца IC-3 «Суверен».
    Задача: высадиться на REP-27-7, взять военную базу «Терминус» на северо-востоке и доставить на борт экспериментальный образец из нижнего яруса её бункера.
    Зоны высадки: ЗП-1 в полях на юго-западе, ЗП-2 на палубе танкера у нефтяного терминала, ЗП-3 на космодроме; челнок может сесть и в поле, но взлетать оттуда дольше. Горные ПКО, ПВО и РЛС прикрывают базу — подавите их.
    Разрыв откроется примерно через {$minutes} мин. Эскадра республиканского флота подойдёт примерно через {$relief} мин. — к этому времени нас здесь быть не должно.
    Берегите авианосец — это единственный путь домой. Подробнее о войне и сторонах — в гайдбуке, раздел «Лор».

# Итоги раунда (план осады п. 6)
dh-assault-stats-republic = Республика
dh-assault-stats-empire = Империя
dh-assault-stats-none = — нет —
dh-assault-stats-nobody = —
dh-assault-stats-captures-header = [bold]Хронология захвата точек[/bold]
dh-assault-stats-capture = { $time } — [color={ $color }]{ $side }[/color]: «{ $point }»
dh-assault-stats-capture-from = { $time } — [color={ $color }]{ $side }[/color]: «{ $point }» (отбита у стороны «{ $from }»)
dh-assault-stats-losses = [bold]Потери:[/bold] Республика — { $republic }, Империя — { $empire }.
dh-assault-stats-downed = [bold]Сбито:[/bold] штурмовиков Империи — { $aircraft }; авианосец — { $carrier }.
dh-assault-stats-carrier-down = сбит
dh-assault-stats-carrier-intact = уцелел
dh-assault-stats-best-header = [bold]Лучшие бойцы[/bold]
dh-assault-stats-best-side = [color={ $color }]{ $side }[/color]: больше всех убил — { $killer }; больше всех спас — { $medic }; больше всех захватов — { $captor }.
dh-assault-stats-best-entry = { $name } ({ $count })
dh-assault-stats-medals-header = [bold]Награды[/bold]
dh-assault-stats-medal = [color={ $color }]«{ $medal }»[/color] — { $recipient }. Вручил { $giver }: «{ $citation }»

# Наградной лист
dh-medal-ui-title = Наградной лист
dh-medal-ui-remaining = Можно вручить наград: { $count }
dh-medal-ui-recipient = Кого наградить
dh-medal-ui-candidate = { $name } — { $job }
dh-medal-ui-candidate-dead = { $name } — { $job } (посмертно)
dh-medal-ui-medal = Награда
dh-medal-ui-citation = Формулировка
dh-medal-ui-award = Наградить
dh-medal-none-left = Наградной лист исчерпан.
dh-medal-wrong-side = Это наградной лист другой стороны.
dh-medal-no-citation = Напишите формулировку награды.
dh-medal-sender = Командование
dh-medal-announce = { $giver } награждает { $recipient } — «{ $medal }»: «{ $citation }»

dh-medal-imperial-cross = Имперский крест
dh-medal-imperial-cross-desc = Высшая боевая награда Первой Империи — за подвиг, решивший исход боя.
dh-medal-imperial-assault = За штурм
dh-medal-imperial-assault-desc = Тем, кто первым врывается на позиции противника и берёт укрепления.
dh-medal-imperial-loyalty = За верность Императору
dh-medal-imperial-loyalty-desc = За стойкость и исполнение долга до конца.
dh-medal-imperial-wound = Знак ранения
dh-medal-imperial-wound-desc = Тем, кто пролил кровь за Империю.
dh-medal-republic-star = Звезда Республики
dh-medal-republic-star-desc = Высшая боевая награда Соединённой Республики — за исключительную храбрость.
dh-medal-republic-defense = За оборону
dh-medal-republic-defense-desc = За удержание позиций под огнём противника.
dh-medal-republic-valor = За отвагу
dh-medal-republic-valor-desc = За личную храбрость в бою, спасение товарищей и смелые действия.
dh-medal-republic-wound = Знак ранения
dh-medal-republic-wound-desc = Тем, кто пролил кровь за Республику.

## Маскхалат снайпера
dh-ghillie-preparing = Вы замираете и прячетесь в маскхалате…
dh-ghillie-hidden = Вы скрыты. Не двигайтесь.
dh-ghillie-already-hidden = Вы уже скрыты.
dh-ghillie-revealed-self = Вы выходите из маскировки.
dh-ghillie-revealed-move = Вы двинулись — маскировка сбита!
dh-ghillie-revealed-shot = Выстрел выдал вашу позицию!
dh-ghillie-revealed-hit = Вас ранили — маскировка сбита!

# Палубы, рампа и ручной полёт челнока (2026-11-27)
dh-dropship-departing-field = Взлёт с неподготовленной площадки: прогрев двигателей, вылет на {$destination} через {$seconds} с! Занять места.
dh-dropship-landed-field = Посадка вне площадки. Шасси выпущены, рампу — с пульта.
dh-dropship-ramp-not-landed = Рампа опускается только на земле.
dh-dropship-ramp-lowered = Рампа опущена.
dh-dropship-ramp-lowering = Рампа опускается.
dh-dropship-ramp-raising = Рампа поднимается.
dh-dropship-ramp-crush = Вас придавило опускающейся рампой!
dh-dropship-ramp-raised = Рампа поднята.
dh-dropship-gear-down = Шасси выпущены.
dh-dropship-altitude-unavailable = Высоту руками можно менять только в ручном полёте над полем боя.
dh-dropship-altitude-max = Выше высоты полёта не подняться.
dh-dropship-altitude-blocked = Мешает строение — туда не опуститься.
dh-dropship-land-here-blocked = Здесь не сесть: мешают строения, стены или другой корабль.
dh-dropship-ui-up = ▲ ВЫШЕ (R)
dh-dropship-ui-down = ▼ НИЖЕ (F)
dh-dropship-ui-land-here = ▼ СЕСТЬ ЗДЕСЬ (F)
dh-dropship-ui-manual-hint = Уровень {$level}. WASD — полёт, Q/E — поворот корпуса, R/F — выше/ниже.
dh-dropship-ui-ramp-lower = ОПУСТИТЬ РАМПУ
dh-dropship-ui-ramp-raise = ПОДНЯТЬ РАМПУ
