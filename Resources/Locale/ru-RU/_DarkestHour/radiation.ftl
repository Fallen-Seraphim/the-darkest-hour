## DarkestHour: лучевая и хронитовая кожно-покровная болезни (этап 2 медицины, шаг 4)

dh-disease-radiation-sickness = Лучевая болезнь
dh-disease-chronite-skin = Хронитовая кожно-покровная болезнь

dh-disease-symptom-rad-metal-taste = Во рту металлический привкус.
dh-disease-symptom-rad-hair = У вас выпадает клок волос.
dh-disease-symptom-rad-bleeding = Дёсны кровоточат, по коже расползаются синяки.
dh-disease-symptom-rad-dying = Ваше тело разваливается изнутри.
dh-disease-symptom-chronite-spots = Под кожей слабо светятся фиолетовые пятна.
dh-disease-symptom-chronite-itch = Кожа зудит там, где светится.
dh-disease-symptom-chronite-crystals = Кожа твердеет и превращается в блестящие фиолетовые кристаллы.
dh-disease-symptom-chronite-cracks = Кристаллизованная кожа трескается и кровоточит!
dh-disease-symptom-chronite-necrosis = Плоть под кристаллами отмирает и чернеет!
dh-disease-symptom-chronite-terminal = Хронит проел вас насквозь. Он светится изнутри.

dh-dose-kind-Radiation = накопленную радиацию
dh-dose-kind-Chronite = накопленный хронит

dh-dosimeter-reading = {CAPITALIZE($target)}: радиация {$radiation} ({$radStage}), хронит {$chronite} ({$chrStage})
dh-dosimeter-stage = стадия {$stage}
dh-dosimeter-safe = безопасно

dh-entity-effect-guidebook-reduce-dose =
    { $chance ->
        [1] Выводит
        *[other] выводить
    } из организма {$kind}

dh-entity-effect-guidebook-add-chronite =
    { $chance ->
        [1] Подвергает
        *[other] подвергать
    } организм воздействию хронита

dh-entity-effect-guidebook-add-pain =
    { $chance ->
        [1] Вызывает
        *[other] вызывать
    } боль

dh-surgery-step-excise-skin = иссечение кристаллизованной кожи
dh-surgery-step-graft-skin = пересадка новой кожи
dh-surgery-tool-skin-graft = кожный трансплантат в руке
dh-surgery-excise-chronite = Иссечение хронитовой кожи
dh-surgery-excise-chronite-desc = Срезать кожу, кристаллизованную хронитом, и закрыть рану трансплантатом. Выводит из организма большую часть хронита. На последней стадии невозможно.

reagent-name-dh-chronosorb = хроносорб
reagent-desc-dh-chronosorb = Связывает хронит в организме и выводит его. Сам по себе токсичен: давайте вместе с диловеном. Бесполезен, когда хронитовая болезнь дошла до последней стадии.

## Экспериментальное лечение хронитовой болезни
reagent-name-dh-chronolysin = хронолизин
reagent-desc-dh-chronolysin = Экспериментальное средство, растворяющее кристаллы хронита в тканях даже на последней стадии хронитовой болезни. Сильно повреждает клетки, вызывает боль и судороги.
dh-chronolysin-effect-1 = Кожа будто шевелится — глубоко под ней что-то трескается.
dh-chronolysin-effect-2 = Под кожей пробегает фиолетовое мерцание. Жжёт.
dh-chronolysin-effect-3 = Все мышцы тела разом дёргаются.
research-technology-dh-experimental-chronotherapy = Экспериментальная хронотерапия
