## DarkestHour: травмы (этап 2 медицины, шаг 2)

dh-targeting-title = Прицел

dh-trauma-fracture-self = Тошнотворный хруст! Перелом: {$part}!
dh-trauma-organ-failing-self = Внутри что-то сдаёт. Вам совсем плохо.
dh-trauma-severed = {CAPITALIZE($target)}: {$part} оторвана начисто!
dh-trauma-skull-dizzy = Голова кружится, в глазах плывёт.
dh-trauma-hand-drop = Сломанная рука подводит, и вы роняете то, что держали!
dh-trauma-pain-faint = Боль невыносима. Всё темнеет...
dh-trauma-pain-shock = Ослепляющая боль! Ноги подкашиваются!
dh-trauma-pain-moderate = От боли трудно думать.
dh-trauma-pain-mild = Раны ноют.

dh-trauma-desc-damage = Раны: {$brute} механических, {$burn} ожоговых
dh-trauma-desc-fracture = Перелом кости
dh-trauma-desc-fracture-splinted = Перелом кости (шина)
dh-trauma-desc-internal-bleeding = Внутреннее кровотечение
dh-trauma-desc-shrapnel = Осколки и пули: {$count}
dh-trauma-desc-organ-damaged = {$organ}: повреждение ({$percent}%)
dh-trauma-desc-organ-failing = {$organ}: отказ!

dh-trauma-organ-Brain = Мозг
dh-trauma-organ-Eyes = Глаза
dh-trauma-organ-Heart = Сердце
dh-trauma-organ-Lungs = Лёгкие
dh-trauma-organ-Liver = Печень
dh-trauma-organ-Kidneys = Почки
dh-trauma-organ-Stomach = Желудок

dh-trauma-splint-nothing = Нет сломанной конечности, на которую нужна шина.
dh-trauma-splint-done = {CAPITALIZE($user)} накладывает шину: {$target}, {$part}.

dh-trauma-examine-fracture = [color=orange]{CAPITALIZE($part)} неестественно вывернута.[/color]
dh-trauma-examine-splinted = {CAPITALIZE($part)} в шине.
dh-trauma-examine-shrapnel = [color=orange]{CAPITALIZE($part)}: рваные раны, внутри поблёскивает металл.[/color]
dh-trauma-examine-pain = [color=red]{CAPITALIZE(SUBJECT($target))} корчится от боли.[/color]

dh-entity-effect-guidebook-suppress-pain =
    { $chance ->
        [1] Подавляет
        *[other] подавлять
    } боль на {$amount}

dh-entity-effect-guidebook-slow-internal-bleeding =
    { $chance ->
        [1] Замедляет
        *[other] замедлять
    } внутреннее кровотечение

dh-surgery-step-set-fracture = вправление сломанной кости
dh-surgery-step-gel-fracture = склеивание перелома
dh-surgery-step-tie-vessels = перевязка кровоточащих сосудов
dh-surgery-step-extract-shrapnel = извлечение осколков и пуль
dh-surgery-step-repair-organs = ушивание повреждённых органов

dh-surgery-fix-fracture = Вправление перелома
dh-surgery-fix-fracture-desc = Вправить сломанную кость костоправом и склеить костным гелем.
dh-surgery-stop-internal-bleeding = Остановка внутреннего кровотечения
dh-surgery-stop-internal-bleeding-desc = Найти и перевязать разорванные сосуды. Транексамовая кислота лишь замедляет кровотечение.
dh-surgery-remove-shrapnel = Удаление осколков
dh-surgery-remove-shrapnel-desc = Извлечь все осколки и пули, застрявшие в этой части тела.
dh-surgery-repair-organs = Восстановление органов
dh-surgery-repair-organs-desc = Зашить повреждённые органы внутри. Отказывающий орган можно и пересадить.

reagent-name-dh-morphine = морфин
reagent-desc-dh-morphine = Сильное обезболивающее. Предотвращает болевой шок и обмороки от боли, немного замедляет. Свыше 20 ед. угнетает дыхание.

## Анализатор здоровья
dh-trauma-analyzer-part = [color=#d0a040]{CAPITALIZE($part)}:[/color] {$findings}
dh-trauma-analyzer-pain-mild = [color=#d0c040]Боль: слабая.[/color]
dh-trauma-analyzer-pain-moderate = [color=#d08040]Боль: умеренная — замедление.[/color]
dh-trauma-analyzer-pain-shock = [color=#d04040]Боль: болевой шок! Дайте обезболивающее.[/color]
dh-trauma-analyzer-pain-unconscious = [color=#d04040]Боль: без сознания от боли! Дайте обезболивающее.[/color]

## Полевая медицина: жгуты, турникеты и полевые повязки

dh-tourniquet-no-limb = Жгут уже наложен на каждую конечность.
dh-tourniquet-applied = {CAPITALIZE($user)} накладывает жгут: {$target}, {$part}.
dh-tourniquet-removed = {CAPITALIZE($user)} снимает жгут: {$target}, {$part}.
dh-tourniquet-remove-verb = Снять жгут ({$part})
dh-tourniquet-necrosis = {CAPITALIZE($part)} онемела и похолодела — жгут наложен слишком долго!
dh-tourniquet-desc = наложен жгут ({$minutes} мин)
dh-tourniquet-desc-necrosis = [color=red]жгут наложен слишком долго ({$minutes} мин), конечность отмирает[/color]
dh-tourniquet-examine = [color=orange]{CAPITALIZE($target)}: жгут на конечности ({$part}, {$minutes} мин).[/color]
dh-field-dressing-applied = {CAPITALIZE($user)} накладывает {$item}: {$target}, {$part}.
reagent-name-dh-tramadex = трамадекс
reagent-desc-dh-tramadex = Продвинутое обезболивающее: сильнее морфина и не замедляет. Свыше 30 ед. отравляет печень.
reagent-name-dh-neuroblocker = нейроблокатор
reagent-desc-dh-neuroblocker = Боевой блокатор боли: полностью отключает боль, раненый остаётся на ногах. Свыше 15 ед. останавливает сердце.
