## DarkestHour: травмы (этап 2 медицины, шаг 2)

dh-targeting-title = Aim

dh-trauma-fracture-self = A sickening crack! Your {$part} is broken!
dh-trauma-organ-failing-self = Something inside you gives way. You feel terribly wrong.
dh-trauma-severed = {CAPITALIZE(POSS-ADJ($target))} {$part} is torn clean off!
dh-trauma-skull-dizzy = Your head spins and your vision swims.
dh-trauma-hand-drop = Your broken arm gives out and you drop what you were holding!
dh-trauma-pain-faint = The pain is too much. Everything goes dark...
dh-trauma-pain-shock = Blinding pain! Your legs buckle!
dh-trauma-pain-moderate = The pain makes it hard to think.
dh-trauma-pain-mild = Your injuries ache.

dh-trauma-desc-damage = Wounds: {$brute} brute, {$burn} burn
dh-trauma-desc-fracture = Fractured bone
dh-trauma-desc-fracture-splinted = Fractured bone (splinted)
dh-trauma-desc-internal-bleeding = Internal bleeding
dh-trauma-desc-shrapnel = Shrapnel and bullets: {$count}
dh-trauma-desc-organ-damaged = {$organ} damaged ({$percent}%)
dh-trauma-desc-organ-failing = {$organ} failing!

dh-trauma-organ-Brain = Brain
dh-trauma-organ-Eyes = Eyes
dh-trauma-organ-Heart = Heart
dh-trauma-organ-Lungs = Lungs
dh-trauma-organ-Liver = Liver
dh-trauma-organ-Kidneys = Kidneys
dh-trauma-organ-Stomach = Stomach

dh-trauma-splint-nothing = There is no broken limb to splint.
dh-trauma-splint-done = {CAPITALIZE(THE($user))} splints {POSS-ADJ($target)} {$part}.

dh-trauma-examine-fracture = [color=orange]{CAPITALIZE(POSS-ADJ($target))} {$part} is bent at an unnatural angle.[/color]
dh-trauma-examine-splinted = {CAPITALIZE(POSS-ADJ($target))} {$part} is held in a splint.
dh-trauma-examine-shrapnel = [color=orange]{CAPITALIZE(POSS-ADJ($target))} {$part} has ragged wounds with metal glinting inside.[/color]
dh-trauma-examine-pain = [color=red]{CAPITALIZE(SUBJECT($target))} {CONJUGATE-BE($target)} writhing in pain.[/color]

dh-entity-effect-guidebook-suppress-pain =
    { $chance ->
        [1] Suppresses
        *[other] suppress
    } pain by {$amount}

dh-entity-effect-guidebook-slow-internal-bleeding =
    { $chance ->
        [1] Slows
        *[other] slow
    } internal bleeding

dh-surgery-step-set-fracture = set the broken bone
dh-surgery-step-gel-fracture = glue the fracture
dh-surgery-step-tie-vessels = tie off the bleeding vessels
dh-surgery-step-extract-shrapnel = extract shrapnel and bullets
dh-surgery-step-repair-organs = suture the damaged organs

dh-surgery-fix-fracture = Set fracture
dh-surgery-fix-fracture-desc = Set the broken bone with a bone setter and glue it with bone gel.
dh-surgery-stop-internal-bleeding = Stop internal bleeding
dh-surgery-stop-internal-bleeding-desc = Find and tie off the torn vessels. Tranexamic acid only slows the bleeding.
dh-surgery-remove-shrapnel = Remove shrapnel
dh-surgery-remove-shrapnel-desc = Pull out all fragments and bullets lodged in this part.
dh-surgery-repair-organs = Repair organs
dh-surgery-repair-organs-desc = Suture the damaged organs inside. A failing organ can also be replaced by a transplant.

reagent-name-dh-morphine = morphine
reagent-desc-dh-morphine = A strong painkiller. Prevents pain shock and fainting from pain, slows you slightly. Above 20u it depresses breathing.

## Анализатор здоровья
dh-trauma-analyzer-part = [color=#d0a040]{CAPITALIZE($part)}:[/color] {$findings}
dh-trauma-analyzer-pain-mild = [color=#d0c040]Pain: mild.[/color]
dh-trauma-analyzer-pain-moderate = [color=#d08040]Pain: moderate — slowed.[/color]
dh-trauma-analyzer-pain-shock = [color=#d04040]Pain: pain shock! Give painkillers.[/color]
dh-trauma-analyzer-pain-unconscious = [color=#d04040]Pain: unconscious from pain! Give painkillers.[/color]
