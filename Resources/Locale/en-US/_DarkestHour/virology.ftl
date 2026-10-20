## DarkestHour: вирусология (этап 2 медицины, шаг 5)

dh-swab-used = This swab has already been used.
dh-swab-taken = You take a swab from {THE($target)}.
dh-swab-examine-clean = It is sterile and unused.
dh-swab-examine-used = It holds a sample from {$source}.

dh-scanner-clean = {CAPITALIZE(THE($target))}: no pathogens detected.
dh-scanner-pathogen-silent = {CAPITALIZE(THE($target))}: a pathogen is present, but there are no symptoms yet.
dh-scanner-pathogen = {CAPITALIZE(THE($target))}: pathogen detected. Symptoms: {$symptoms}.

dh-analyzer-slot-swab = Swab
dh-analyzer-report-name = pathogen analysis
dh-analyzer-report-header = PATHOGEN ANALYSIS — sample from {$source}
dh-analyzer-report-clean = No pathogens found in the sample.
dh-analyzer-report-sample = Pathogen #{$index}:
dh-analyzer-report-footer = Fill in a case record at the virology computer: group, transmission, symptoms, treatment.

dh-virology-structure-Virus = Nucleic acid in a protein capsid, no cell of its own; replicates only inside host cells. Size 20–300 nm.
dh-virology-structure-Bacteria = Single cell without a nucleus, cell wall present; divides on its own. Size 1–5 μm.
dh-virology-structure-Fungus = Branching hyphae or budding yeast cells, chitin in the cell wall, spores.
dh-virology-structure-Parasite = Multicellular organism: eggs, larvae or cysts visible.
dh-virology-structure-Chronite = Violet crystalline inclusions in the tissue.
dh-virology-structure-Radiation = DNA breaks, damaged bone marrow cells.

dh-virology-structure-cold = RNA, no envelope, small icosahedral capsid. Very fast replication in the airway lining.
dh-virology-structure-flu-taurus = RNA, lipid envelope with surface spikes, segmented genome. Spreads through air droplets and contact.
dh-virology-structure-dysentery = Gram-negative rods, motile. Colonises the gut, produces toxins.
dh-virology-structure-wound-infection = Gram-positive cocci in clusters from a wound. Pus, tissue damage around the wound.

dh-symptom-sneezing = sneezing
dh-symptom-cough = cough
dh-symptom-runny-nose = runny nose
dh-symptom-fever = fever
dh-symptom-aches = body aches
dh-symptom-weakness = weakness
dh-symptom-fainting = fainting
dh-symptom-cramps = stomach cramps
dh-symptom-nausea = nausea
dh-symptom-vomiting = vomiting
dh-symptom-wound-inflammation = inflamed wound
dh-symptom-pus = pus
dh-symptom-gangrene = black necrotic tissue

dh-virology-group-Virus = Virus
dh-virology-group-Bacteria = Bacterium
dh-virology-group-Fungus = Fungus
dh-virology-group-Parasite = Parasite

dh-virology-spread-Airborne = Airborne
dh-virology-spread-Contact = Contact
dh-virology-spread-Blood = Blood

dh-virology-treatment-Rest = Rest (antiviral speeds recovery)
dh-virology-treatment-Vaccine = Vaccine
dh-virology-treatment-Antibiotic = Antibiotic
dh-virology-treatment-ReserveAntibiotic = Reserve antibiotic
dh-virology-treatment-Antifungal = Antifungal
dh-virology-treatment-Antiparasitic = Antiparasitic
dh-virology-treatment-Surgery = Surgical removal

dh-virology-ui-title = Virology: case record
dh-virology-ui-hint = Use the pathogen scanner and analyzer printouts to fill in the record.
dh-virology-ui-group = Pathogen group
dh-virology-ui-spread = Transmission
dh-virology-ui-symptoms = Observed symptoms
dh-virology-ui-treatment = Main treatment
dh-virology-ui-submit = Submit record
dh-virology-ui-cooldown = Records are locked for {$seconds} s after a wrong one.
dh-virology-ui-known = Identified this shift
dh-virology-ui-known-none = Nothing yet.

dh-virology-result-wrong = The record matches no known pathogen. Check the analysis and try again.
dh-virology-result-new = Identified: {$disease}. +{$points} research points. The recipe has been printed.
dh-virology-result-known = {$disease} is already identified. The recipe has been printed again.

dh-virology-recipe-name = treatment: {$disease}
dh-virology-recipe-header = TREATMENT — {$disease}
dh-virology-recipe-group = Group: {$group}
dh-virology-recipe-treatment = Treatment: {$treatment}

dh-virology-recipe-cold = Usually passes on its own. Viracide (dylovene + silicon + chlorine) speeds recovery. Vaccine: viracide + saline + sugar, 1:1:1.
dh-virology-recipe-flu-taurus = Vaccine: viracide + iodine + sugar, 1:1:1 — gives immunity and breaks the running disease. Viracide holds it back until then. Isolate patients, masks for everyone nearby.
dh-virology-recipe-dysentery = Spacecillin (dylovene + ethanol + sulfur) with dylovene and saline. Gloves when handling patients.
dh-virology-recipe-wound-infection = Spacecillin (dylovene + ethanol + sulfur) until the fever stops. Gangrene — amputation. Operate only in a sterile mask and gloves.

dh-entity-effect-guidebook-vaccinate =
    { $chance ->
        [1] Immunises
        *[other] immunise
    } against {$disease}

reagent-name-dh-vaccine-cold = cold vaccine
reagent-name-dh-vaccine-flu-taurus = "Taurus" flu vaccine
reagent-desc-dh-vaccine = A vaccine. Gives immunity to one disease and breaks it if it is already running.
