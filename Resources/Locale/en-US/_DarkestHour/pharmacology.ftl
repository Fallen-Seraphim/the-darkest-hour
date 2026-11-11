dh-drug-class-opioid = opioids
dh-drug-class-sedative = sedatives
dh-drug-class-alcohol = alcohol
dh-drug-class-stimulant = stimulants
dh-drug-class-nsaid = NSAIDs

dh-entity-effect-guidebook-drug =
    { $chance ->
        [1] Belongs
        *[other] belong
    } to the "{$class}" class (tolerance, dependence, interactions)
dh-entity-effect-guidebook-drug-antagonist =
    { $chance ->
        [1] Reverses
        *[other] reverse
    } the effect of {$class}; triggers withdrawal in dependent patients
dh-entity-condition-guidebook-drug-load = {$class} are active in the blood

dh-drug-respiratory-depression = Breathing gets harder. Every breath is an effort.
dh-drug-withdrawal-relieved = The withdrawal eases off.
dh-drug-withdrawal-over = The craving has let go. Looks like it's over.

dh-drug-withdrawal-opioid-1 = Your whole body aches. You need a dose.
dh-drug-withdrawal-opioid-2 = Cold sweat, goosebumps, your joints feel twisted.
dh-drug-withdrawal-opioid-3 = Every old wound reminds you it's there. Some morphine right now…
dh-drug-withdrawal-sedative-1 = Your hands shake, your heart pounds, you can't sleep.
dh-drug-withdrawal-sedative-2 = Anxiety rolls in for no reason.
dh-drug-withdrawal-stimulant-1 = No strength left. None at all.
dh-drug-withdrawal-stimulant-2 = Your head is heavy, your thoughts drag.
