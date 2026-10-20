## DarkestHour: болезни (этап 2 медицины, шаг 3)

dh-disease-cold = Common cold
dh-disease-flu-taurus = "Taurus" flu
dh-disease-dysentery = Station dysentery
dh-disease-wound-infection = Wound infection

dh-disease-group-Virus = viral diseases
dh-disease-group-Bacteria = bacterial diseases
dh-disease-group-Fungus = fungal diseases
dh-disease-group-Parasite = parasites
dh-disease-group-Chronite = chronite disease
dh-disease-group-Radiation = radiation sickness
dh-disease-group-any = diseases

dh-disease-symptom-runny-nose = Your nose is running.
dh-disease-symptom-sore-throat = Your throat is sore.
dh-disease-symptom-fever = You feel feverish.
dh-disease-symptom-aches = Your whole body aches.
dh-disease-symptom-high-fever = You are burning up with fever!
dh-disease-symptom-weak = You feel terribly weak.
dh-disease-symptom-cramps = Your stomach cramps.
dh-disease-symptom-nausea = You feel nauseous.
dh-disease-symptom-cramps-severe = Agonising cramps twist your gut!
dh-disease-symptom-wound-throb = Your wound throbs.
dh-disease-symptom-wound-oozing = Your wound is swollen and oozing pus.
dh-disease-symptom-gangrene = Your flesh around the wound is turning black!

dh-entity-effect-guidebook-treat-disease =
    { $chance ->
        [1] Treats
        *[other] treat
    } {$target}

dh-entity-effect-guidebook-infect-disease =
    { $chance ->
        [1] Infects
        *[other] infect
    } with {$disease}

reagent-name-dh-viracide = viracide
reagent-desc-dh-viracide = An antiviral drug. Holds a viral infection back and helps the body build immunity faster. Toxic above 30u.
