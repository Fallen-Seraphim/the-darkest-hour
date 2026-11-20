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

# Кашель кровью и отделённые части тела (DHTraumaSystem.Gore, DHLimbDecaySystem)
dh-trauma-cough-blood = {CAPITALIZE($target)} кашляет кровью!
dh-trauma-cough-blood-self = Вы кашляете кровью. В груди что-то серьёзно не так.
dh-limb-examine-fresh = [color=yellow]Ещё свежая[/color]: пришить можно ещё около { $minutes } мин. Держите в холоде — в холодильнике, морозильном шкафу или морге.
dh-limb-examine-cold = [color=lightblue]В холоде[/color]: здесь не иссохнет (вне холода осталось около { $minutes } мин).
dh-limb-examine-withered = [color=gray]Иссохла[/color]: пришить уже нельзя.

dh-alert-pain-name = [color=orange]Боль[/color]
dh-alert-pain-desc = Чем сильнее боль, тем ярче значок. В болевом шоке валит с ног, сильнее — теряете сознание. Обезболивающее снимет её на время.
dh-alert-fracture-name = [color=red]Перелом[/color]
dh-alert-fracture-desc = Сломана кость: без шины — сильная боль и хромота (или роняете вещи). С шиной легче, но кость срастётся только после операции.

# Сердце, мозг, ЧМТ (план осады п. 18)
dh-trauma-brain-injury-self = Удар по голове! В глазах темнеет, мысли путаются.
dh-trauma-brain-confused = Мысли путаются, слова не складываются.
dh-trauma-brain-mute = Вы не можете выговорить ни слова!
dh-trauma-brain-seizure = {CAPITALIZE(THE($target))} бьётся в судорогах!
dh-trauma-desc-brain-injury = черепно-мозговая травма — нужна трепанация
dh-trauma-analyzer-revive-heart = [color=#d04040]Реанимация невозможна: отказ сердца. Ушейте или пересадите сердце, затем дефибриллятор.[/color]
dh-trauma-analyzer-revive-brain = [color=#d04040]Реанимация невозможна: мозг необратимо повреждён.[/color]
dh-surgery-trepanation = Трепанация
dh-surgery-trepanation-desc = Черепно-мозговая травма: просверлить фрезевое отверстие и удалить гематому. Без операции мозг за несколько минут погибнет.
dh-surgery-step-burr-hole = фрезевое отверстие в черепе
dh-surgery-step-evacuate-hematoma = удаление гематомы
dh-surgery-tool-drill = хирургическая дрель или пила

# Нервы и спинной мозг (план осады п. 18)
dh-trauma-nerve-self = {CAPITALIZE($part)} немеет — вы её почти не чувствуете!
dh-trauma-nerve-drop = Онемевшие пальцы разжимаются сами.
dh-trauma-desc-nerve = повреждён нерв
dh-trauma-spinal-legs-self = Удар в спину! Вы не чувствуете ног!
dh-trauma-spinal-full-self = Хруст в шее — тело больше не слушается!
dh-trauma-spinal-stand = Ноги не слушаются — встать не получается.
dh-trauma-spinal-permanent = Ноги так и не вернулись. Теперь — только экзоскелет.
dh-trauma-desc-spinal-legs = травма спинного мозга: паралич ног
dh-trauma-desc-spinal-full = травма спинного мозга: полный паралич
dh-trauma-desc-spinal-treatable = {" "}— нужна декомпрессия позвоночника
dh-trauma-desc-spinal-permanent = {" "}— необратимо, ходить можно только в экзоскелете
dh-surgery-repair-nerve = Шов нерва
dh-surgery-repair-nerve-desc = Найти концы повреждённого нерва и сшить их микроинструментом. Только под наркозом.
dh-surgery-spinal-decompression = Декомпрессия позвоночника
dh-surgery-spinal-decompression-desc = Убрать отломки, давящие на спинной мозг, и закрепить позвонки. Только под наркозом и в первые 10 минут после травмы.
dh-surgery-step-find-nerve = выделение концов нерва
dh-surgery-step-suture-nerve = шов нерва
dh-surgery-step-remove-bone-fragments = удаление костных отломков
dh-surgery-step-stabilize-spine = фиксация позвонков
dh-surgery-tool-microforceps = микрохирургический пинцет
dh-surgery-needs-anesthesia = Без наркоза эту работу не сделать — пациент должен быть без сознания.

# Грудь и дыхательные пути (план осады п. 18)
dh-trauma-pneumothorax-self = В груди что-то лопнуло — дышать всё тяжелее!
dh-trauma-pneumothorax-breath = Не хватает воздуха, в груди колет.
dh-trauma-airway-self = Горло залито кровью — вы не можете вдохнуть!
dh-trauma-airway-choke = Вы задыхаетесь!
dh-trauma-desc-pneumothorax = пневмоторакс (напряжение {$percent}%)
dh-trauma-desc-chest-sealed = рана закрыта окклюзионной повязкой
dh-trauma-desc-airway = дыхательные пути перекрыты — нужна трахеостомия
dh-needle-nothing = Пневмоторакса нет — колоть незачем.
dh-needle-done = {CAPITALIZE(THE($user))} вводит иглу между рёбер {THE($target)} — со свистом выходит воздух.
dh-needle-miss = {CAPITALIZE(THE($user))} промахивается иглой мимо плевры {THE($target)}!
dh-surgery-chest-drain = Дренаж грудной клетки
dh-surgery-chest-drain-desc = Пневмоторакс: ввести дренажную трубку между рёбер и выпустить воздух. Лечит окончательно.
dh-surgery-step-insert-chest-drain = установка дренажа
dh-surgery-tracheostomy = Трахеостомия
dh-surgery-tracheostomy-desc = Дыхательные пути перекрыты: вскрыть трахею ниже повреждения. Делается и на полу — дольше и с риском инфекции.
dh-surgery-step-tracheostomy = разрез трахеи

# Ожоги, лицо и глаза (план осады п. 18)
dh-trauma-deep-burn-self = {CAPITALIZE($part)} обуглена — кожа слезает лоскутами!
dh-trauma-disfigured-self = Лицо разбито — вас теперь не узнать.
dh-trauma-desc-deep-burn = ожог III степени — нужна пересадка кожи
dh-trauma-desc-disfigured = лицо обезображено
dh-surgery-burn-graft = Пересадка кожи на ожог
dh-surgery-burn-graft-desc = Ожог III степени: иссечь омертвевшую кожу и пересадить кожный трансплантат.
dh-surgery-step-excise-burn = иссечение ожога
dh-surgery-reconstruct-face = Реконструкция лица и глаз
dh-surgery-reconstruct-face-desc = Восстановить обезображенное лицо и повреждённые глаза. Без глаз — сначала пересадка глаз.
dh-surgery-step-reconstruct-face = восстановление тканей лица и глаз
