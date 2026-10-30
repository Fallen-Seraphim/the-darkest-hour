## DarkestHour: болезни (этап 2 медицины, шаг 3)

dh-disease-cold = Простуда
dh-disease-flu-taurus = Грипп «Таурус»
dh-disease-dysentery = Станционная дизентерия
dh-disease-wound-infection = Раневая инфекция

dh-disease-group-Virus = вирусные болезни
dh-disease-group-Bacteria = бактериальные болезни
dh-disease-group-Fungus = грибковые болезни
dh-disease-group-Parasite = паразитов
dh-disease-group-Chronite = хронитовую болезнь
dh-disease-group-Radiation = лучевую болезнь
dh-disease-group-any = болезни

dh-disease-symptom-runny-nose = У вас течёт из носа.
dh-disease-symptom-sore-throat = У вас болит горло.
dh-disease-symptom-fever = Вас лихорадит.
dh-disease-symptom-aches = Ломит всё тело.
dh-disease-symptom-high-fever = Вы горите в лихорадке!
dh-disease-symptom-weak = Вы чувствуете страшную слабость.
dh-disease-symptom-cramps = Живот сводит.
dh-disease-symptom-nausea = Вас тошнит.
dh-disease-symptom-cramps-severe = Мучительные спазмы скручивают кишки!
dh-disease-symptom-wound-throb = Рана пульсирует болью.
dh-disease-symptom-wound-oozing = Рана опухла и сочится гноем.
dh-disease-symptom-gangrene = Плоть вокруг раны чернеет!

dh-entity-effect-guidebook-treat-disease =
    { $chance ->
        [1] Лечит
        *[other] лечить
    } {$target}

dh-entity-effect-guidebook-infect-disease =
    { $chance ->
        [1] Заражает
        *[other] заражать
    } болезнью: {$disease}

reagent-name-dh-viracide = вирацид
reagent-desc-dh-viracide = Противовирусный препарат. Сдерживает вирусную инфекцию и помогает организму быстрее выработать иммунитет. Токсичен свыше 30 ед.

## Болезни второй волны (лихорадка Келвина, пневмония, MRS-14, грибки, паразиты)

dh-disease-kelvin-fever = Лихорадка Келвина
dh-disease-pneumonia = Бактериальная пневмония
dh-disease-mrs14 = Резистентный штамм MRS-14
dh-disease-black-mold = Чёрная плесень
dh-disease-grey-scab = Серая парша
dh-disease-helminths = Кишечные гельминты
dh-disease-planetary-worm = Планетарный червь

dh-disease-symptom-bleeding-gums = У вас кровоточат дёсны.
dh-disease-symptom-bruising = Под кожей расползаются тёмные синяки.
dh-disease-symptom-kelvin-terminal = Кровь сочится из глаз и из каждой царапины!
dh-disease-symptom-short-breath = Вам не хватает воздуха.
dh-disease-symptom-chest-pain = При вдохе грудь пронзает острая боль.
dh-disease-symptom-gasping = Вы хватаете ртом воздух!
dh-disease-symptom-abscess = Под кожей пульсирует горячий опухший нарыв.
dh-disease-symptom-sepsis = Вас колотит озноб, всё плывёт перед глазами!
dh-disease-symptom-wheezing = Дыхание с хрипом.
dh-disease-symptom-dark-sputum = Вы откашливаете тёмную комковатую мокроту.
dh-disease-symptom-itch = Кожа нестерпимо зудит.
dh-disease-symptom-grey-scales = Кожу покрывают серые шелушащиеся пятна.
dh-disease-symptom-skin-cracks = Чешуйчатая кожа трескается и кровоточит.
dh-disease-symptom-hungry = Вы снова голодны, хотя только что ели.
dh-disease-symptom-starving = Вас не отпускает грызущий голод.
dh-disease-symptom-moving-lump = Под кожей живота что-то шевелится.
dh-disease-symptom-belly-pain = Глубоко в животе пульсирует тупая боль.
dh-disease-symptom-worm-gnawing = Что-то грызёт вас изнутри!
dh-disease-symptom-worm-terminal = То, что внутри вас, извивается и рвёт органы!

reagent-name-dh-vaccine-kelvin-fever = вакцина от лихорадки Келвина
reagent-name-dh-carbapenem-r = карбапенем-R
reagent-desc-dh-carbapenem-r = Антибиотик резерва. Действует на резистентные штаммы, которым нипочём спейсциллин. Токсичен свыше 20 ед.
reagent-name-dh-fluconazole = флуконазол
reagent-desc-dh-fluconazole = Противогрибковый препарат. Действует медленно, курс долгий. Токсичен свыше 30 ед.
reagent-name-dh-albendazole = альбендазол
reagent-desc-dh-albendazole = Противопаразитарный препарат. Изгоняет кишечных червей; тканевых паразитов лишь сдерживает. Токсичен свыше 30 ед.
