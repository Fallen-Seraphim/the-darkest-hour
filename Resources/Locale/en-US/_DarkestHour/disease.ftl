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

## Болезни второй волны (лихорадка Келвина, пневмония, MRS-14, грибки, паразиты)

dh-disease-kelvin-fever = Kelvin fever
dh-disease-pneumonia = Bacterial pneumonia
dh-disease-mrs14 = MRS-14 resistant strain
dh-disease-black-mold = Black mold
dh-disease-grey-scab = Grey scab
dh-disease-helminths = Intestinal helminths
dh-disease-planetary-worm = Planetary worm

dh-disease-symptom-bleeding-gums = Your gums are bleeding.
dh-disease-symptom-bruising = Dark bruises spread under your skin.
dh-disease-symptom-kelvin-terminal = Blood seeps from your eyes and every scratch!
dh-disease-symptom-short-breath = You can't catch your breath.
dh-disease-symptom-chest-pain = A sharp pain stabs your chest when you breathe.
dh-disease-symptom-gasping = You gasp for air!
dh-disease-symptom-abscess = A hot, swollen abscess throbs under your skin.
dh-disease-symptom-sepsis = You are shaking with chills, everything swims before your eyes!
dh-disease-symptom-wheezing = Your breath wheezes.
dh-disease-symptom-dark-sputum = You cough up dark, grainy phlegm.
dh-disease-symptom-itch = Your skin itches maddeningly.
dh-disease-symptom-grey-scales = Grey flaky patches cover your skin.
dh-disease-symptom-skin-cracks = Your scaly skin cracks and bleeds.
dh-disease-symptom-hungry = You are hungry again, though you just ate.
dh-disease-symptom-starving = A gnawing hunger never leaves you.
dh-disease-symptom-moving-lump = Something shifts under the skin of your belly.
dh-disease-symptom-belly-pain = A dull pain pulses deep in your belly.
dh-disease-symptom-worm-gnawing = Something is gnawing at you from the inside!
dh-disease-symptom-worm-terminal = The thing inside you writhes, tearing at your organs!

reagent-name-dh-vaccine-kelvin-fever = Kelvin fever vaccine
reagent-name-dh-carbapenem-r = carbapenem-R
reagent-desc-dh-carbapenem-r = A reserve antibiotic. Works on resistant strains that shrug off spacecillin. Toxic above 20u.
reagent-name-dh-fluconazole = fluconazole
reagent-desc-dh-fluconazole = An antifungal drug. Works slowly, the course is long. Toxic above 30u.
reagent-name-dh-albendazole = albendazole
reagent-desc-dh-albendazole = An antiparasitic drug. Clears intestinal worms; tissue parasites are only held back. Toxic above 30u.
