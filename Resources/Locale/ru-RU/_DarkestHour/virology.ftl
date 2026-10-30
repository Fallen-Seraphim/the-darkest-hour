## DarkestHour: вирусология (этап 2 медицины, шаг 5)

dh-swab-used = Эта палочка уже использована.
dh-swab-taken = Вы берёте мазок: {$target}.
dh-swab-examine-clean = Стерильна и не использована.
dh-swab-examine-used = На ней образец: {$source}.

dh-scanner-clean = {CAPITALIZE($target)}: патогенов не обнаружено.
dh-scanner-pathogen-silent = {CAPITALIZE($target)}: патоген есть, но симптомов пока нет.
dh-scanner-pathogen = {CAPITALIZE($target)}: обнаружен патоген. Симптомы: {$symptoms}.

dh-analyzer-slot-swab = Мазок
dh-analyzer-report-name = анализ патогена
dh-analyzer-report-header = АНАЛИЗ ПАТОГЕНА — образец: {$source}
dh-analyzer-report-clean = Патогенов в образце не найдено.
dh-analyzer-report-sample = Патоген №{$index}:
dh-analyzer-report-footer = Заполните карту случая на компьютере вирусологии: группа, путь передачи, симптомы, лечение.

dh-virology-structure-Virus = Нуклеиновая кислота в белковом капсиде, своей клетки нет; размножается только в клетках хозяина. Размер 20–300 нм.
dh-virology-structure-Bacteria = Одна клетка без ядра, есть клеточная стенка; делится самостоятельно. Размер 1–5 мкм.
dh-virology-structure-Fungus = Ветвящиеся гифы или почкующиеся дрожжевые клетки, хитин в клеточной стенке, споры.
dh-virology-structure-Parasite = Многоклеточный организм: видны яйца, личинки или цисты.
dh-virology-structure-Chronite = Фиолетовые кристаллические включения в тканях.
dh-virology-structure-Radiation = Разрывы ДНК, повреждённые клетки костного мозга.

dh-virology-structure-cold = РНК, без оболочки, мелкий икосаэдрический капсид. Очень быстрое размножение в слизистой дыхательных путей.
dh-virology-structure-flu-taurus = РНК, липидная оболочка с поверхностными шипами, сегментированный геном. Передаётся воздушно-капельно и контактно.
dh-virology-structure-dysentery = Грамотрицательные подвижные палочки. Заселяют кишечник, выделяют токсины.
dh-virology-structure-wound-infection = Грамположительные кокки скоплениями из раны. Гной, поражение тканей вокруг раны.

dh-symptom-sneezing = чихание
dh-symptom-cough = кашель
dh-symptom-runny-nose = насморк
dh-symptom-fever = жар
dh-symptom-aches = ломота в теле
dh-symptom-weakness = слабость
dh-symptom-fainting = обмороки
dh-symptom-cramps = спазмы в животе
dh-symptom-nausea = тошнота
dh-symptom-vomiting = рвота
dh-symptom-wound-inflammation = воспалённая рана
dh-symptom-pus = гной
dh-symptom-gangrene = чёрные некротические ткани

dh-virology-group-Virus = Вирус
dh-virology-group-Bacteria = Бактерия
dh-virology-group-Fungus = Грибок
dh-virology-group-Parasite = Паразит

dh-virology-spread-Airborne = Воздушно-капельный
dh-virology-spread-Contact = Контактный
dh-virology-spread-Blood = Через кровь

dh-virology-treatment-Rest = Покой (противовирусное ускоряет выздоровление)
dh-virology-treatment-Vaccine = Вакцина
dh-virology-treatment-Antibiotic = Антибиотик
dh-virology-treatment-ReserveAntibiotic = Антибиотик резерва
dh-virology-treatment-Antifungal = Противогрибковое
dh-virology-treatment-Antiparasitic = Противопаразитарное
dh-virology-treatment-Surgery = Хирургическое удаление

dh-virology-ui-title = Вирусология: карта случая
dh-virology-ui-hint = Заполните карту по распечаткам сканера и анализатора патогенов.
dh-virology-ui-group = Группа патогена
dh-virology-ui-spread = Путь передачи
dh-virology-ui-symptoms = Наблюдаемые симптомы
dh-virology-ui-treatment = Основное лечение
dh-virology-ui-submit = Отправить карту
dh-virology-ui-cooldown = После ошибочной карты ввод заблокирован на {$seconds} с.
dh-virology-ui-known = Определено за смену
dh-virology-ui-known-none = Пока ничего.

dh-virology-result-wrong = Карта не совпадает ни с одним известным патогеном. Проверьте анализ и попробуйте снова.
dh-virology-result-new = Определено: {$disease}. +{$points} очков исследований. Рецепт напечатан.
dh-virology-result-known = {$disease} уже определено. Рецепт напечатан повторно.

dh-virology-recipe-name = лечение: {$disease}
dh-virology-recipe-header = ЛЕЧЕНИЕ — {$disease}
dh-virology-recipe-group = Группа: {$group}
dh-virology-recipe-treatment = Лечение: {$treatment}

dh-virology-recipe-cold = Обычно проходит сама. Вирацид (диловен + кремний + хлор) ускоряет выздоровление. Вакцина: вирацид + физраствор + сахар, 1:1:1.
dh-virology-recipe-flu-taurus = Вакцина: вирацид + йод + сахар, 1:1:1 — даёт иммунитет и обрывает текущую болезнь. До неё болезнь сдерживает вирацид. Изолируйте больных, маски всем рядом.
dh-virology-recipe-dysentery = Спейсциллин (диловен + этанол + сера) вместе с диловеном и физраствором. Перчатки при работе с больными.
dh-virology-recipe-wound-infection = Спейсциллин (диловен + этанол + сера), пока не спадёт жар. Гангрена — ампутация. Оперировать только в стерильной маске и перчатках.

dh-entity-effect-guidebook-vaccinate =
    { $chance ->
        [1] Даёт
        *[other] давать
    } иммунитет к болезни: {$disease}

reagent-name-dh-vaccine-cold = вакцина от простуды
reagent-name-dh-vaccine-flu-taurus = вакцина от гриппа «Таурус»
reagent-desc-dh-vaccine = Вакцина. Даёт иммунитет к одной болезни и обрывает её, если она уже идёт.

dh-virology-structure-kelvin-fever = РНК, нитевидный оболочечный вирион длиной до 1 мкм. Поражает стенки сосудов и факторы свёртывания.
dh-virology-structure-pneumonia = Грамположительные кокки парами с толстой капсулой. Заполняют альвеолы лёгких.
dh-virology-structure-mrs14 = Грамположительные кокки скоплениями, изменённые белки клеточной стенки — устойчивы к обычным антибиотикам. Маркер штамма MRS-14.
dh-virology-structure-black-mold = Тёмные септированные гифы с цепочками чёрных спор. Растёт в дыхательных путях.
dh-virology-structure-grey-scab = Серый нитчатый грибок во внешних слоях кожи, споры в чешуйках.
dh-virology-structure-helminths = Яйца круглых червей с толстой оболочкой. Взрослые черви живут в кишечнике.
dh-virology-structure-planetary-worm = Членики личинки с крючьями, не из станционной фауны. Вгрызается в ткани и растёт.

dh-symptom-bleeding-gums = кровоточивость дёсен
dh-symptom-bruising = синяки
dh-symptom-bleeding = кровотечение
dh-symptom-short-breath = одышка
dh-symptom-chest-pain = боль в груди
dh-symptom-abscess = нарывы
dh-symptom-wheezing = хрипы
dh-symptom-dark-sputum = тёмная мокрота
dh-symptom-itch = зуд
dh-symptom-grey-scales = серые чешуйки на коже
dh-symptom-skin-cracks = трещины кожи
dh-symptom-constant-hunger = постоянный голод
dh-symptom-pain = боль
dh-symptom-moving-lump = подвижное уплотнение под кожей

dh-virology-recipe-kelvin-fever = Вакцина: вирацид + транексамовая кислота + сахар, 1:1:1. Пока не подействует — переливание крови и транексамовая кислота. Передаётся через кровь и контакт: перчатки, никакого ближнего боя возле больных.
dh-virology-recipe-pneumonia = Спейсциллин (диловен + этанол + сера) и дексалин для дыхания. Маски рядом с больными.
dh-virology-recipe-mrs14 = Спейсциллин не действует. Карбапенем-R: спейсциллин + фтор + литий, 1:1:1, держать ниже 20 ед. Перчатки и изоляция — расходится по медотсеку контактно.
dh-virology-recipe-black-mold = Флуконазол: фтор + фенол + кремний, 1:1:1, долгим курсом. Дексалин для дыхания. Найдите и вычистите источник спор.
dh-virology-recipe-grey-scab = Флуконазол: фтор + фенол + кремний, 1:1:1, и повязки на потрескавшуюся кожу. Перчатки — передаётся касанием.
dh-virology-recipe-helminths = Альбендазол: сера + азот + медь, 1:1:1. Кормите больного. Как следует прожаривайте мясо.
dh-virology-recipe-planetary-worm = Хирургическое удаление: вскройте торс (надрез, зажим, разведение) и извлеките личинку зажимом. Альбендазол лишь замедляет её до операции.

## Стерильность

dh-contaminated-examine = [color=#9a8a3a]Чем-то измазано — не стерильно.[/color]
dh-disinfectant-examine = Осталось салфеток: {$uses}.
dh-disinfectant-empty = Упаковка пуста.
dh-disinfectant-done = Вы протираете {$target} до стерильности.
dh-autoclave-no-power = У автоклава нет питания.
dh-autoclave-start = Вы загружаете в автоклав: {$item}.
dh-autoclave-done = {CAPITALIZE($item)} выходит из автоклава горячим и стерильным.
dh-analyzer-aerosol = Из анализатора вырывается облачко аэрозоля — вытяжного шкафа нет!
