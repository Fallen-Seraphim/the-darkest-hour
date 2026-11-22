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

## Полевая медицина: жгуты, турникеты и полевые повязки

dh-tourniquet-no-limb = Every limb already has a tourniquet on it.
dh-tourniquet-applied = {CAPITALIZE(THE($user))} applies a tourniquet to {POSS-ADJ($target)} {$part}.
dh-tourniquet-removed = {CAPITALIZE(THE($user))} takes the tourniquet off {POSS-ADJ($target)} {$part}.
dh-tourniquet-remove-verb = Remove the tourniquet ({$part})
dh-tourniquet-necrosis = Your {$part} is numb and cold — the tourniquet has been on too long!
dh-tourniquet-desc = tourniquet on ({$minutes} min)
dh-tourniquet-desc-necrosis = [color=red]tourniquet on too long ({$minutes} min), the limb is dying[/color]
dh-tourniquet-examine = [color=orange]{CAPITALIZE(SUBJECT($target))} has a tourniquet on the {$part} ({$minutes} min).[/color]
dh-field-dressing-applied = {CAPITALIZE(THE($user))} applies {THE($item)} to {POSS-ADJ($target)} {$part}.
reagent-name-dh-tramadex = tramadex
reagent-desc-dh-tramadex = An advanced painkiller: stronger than morphine and does not slow you down. Above 30u it poisons the liver.
reagent-name-dh-neuroblocker = neuroblocker
reagent-desc-dh-neuroblocker = A combat pain blocker: shuts pain off completely, keeping a wounded soldier on their feet. Above 15u it stops the heart.

# Coughing blood and severed parts (DHTraumaSystem.Gore, DHLimbDecaySystem)
dh-trauma-cough-blood = {CAPITALIZE(THE($target))} coughs up blood!
dh-trauma-cough-blood-self = You cough up blood. Something is badly wrong inside your chest.
dh-limb-examine-fresh = [color=yellow]Still fresh[/color]: it can be reattached for about { $minutes } more min. Keep it cold — a fridge, a freezer or the morgue.
dh-limb-examine-cold = [color=lightblue]Kept cold[/color]: it will not wither here (about { $minutes } min left outside).
dh-limb-examine-withered = [color=gray]Withered[/color]: it can no longer be reattached.

dh-alert-pain-name = [color=orange]Pain[/color]
dh-alert-pain-desc = The stronger the pain, the brighter the icon. Pain shock knocks you down, worse than that and you pass out. Painkillers dull it for a while.
dh-alert-fracture-name = [color=red]Fracture[/color]
dh-alert-fracture-desc = A broken bone: without a splint it hurts badly and you limp (or drop things). A splint helps, but the bone only heals after surgery.

# Heart, brain, TBI
dh-trauma-brain-injury-self = A blow to the head! Your vision darkens and your thoughts scatter.
dh-trauma-brain-confused = Your thoughts tangle, the words won't come together.
dh-trauma-brain-mute = You can't get a single word out!
dh-trauma-brain-seizure = {CAPITALIZE(THE($target))} is convulsing!
dh-trauma-desc-brain-injury = traumatic brain injury — needs trepanation
dh-trauma-analyzer-revive-heart = [color=#d04040]Revival impossible: heart failure. Repair or transplant the heart, then defibrillate.[/color]
dh-trauma-analyzer-revive-brain = [color=#d04040]Revival impossible: the brain is irreversibly damaged.[/color]
dh-surgery-trepanation = Trepanation
dh-surgery-trepanation-desc = Traumatic brain injury: drill a burr hole and evacuate the hematoma. Without it the brain dies within minutes.
dh-surgery-step-burr-hole = burr hole in the skull
dh-surgery-step-evacuate-hematoma = evacuating the hematoma
dh-surgery-tool-drill = surgical drill or saw

# Nerves and spinal cord
dh-trauma-nerve-self = Your {$part} goes numb — you can barely feel it!
dh-trauma-nerve-drop = Your numb fingers let go on their own.
dh-trauma-desc-nerve = nerve damage
dh-trauma-spinal-legs-self = A blow to the back! You can't feel your legs!
dh-trauma-spinal-full-self = A crunch in your neck — your body won't obey!
dh-trauma-spinal-stand = Your legs won't respond — you can't get up.
dh-trauma-spinal-permanent = Your legs never came back. Only an exoskeleton now.
dh-trauma-desc-spinal-legs = spinal cord injury: legs paralyzed
dh-trauma-desc-spinal-full = spinal cord injury: full paralysis
dh-trauma-desc-spinal-treatable = {" "}— needs spinal decompression
dh-trauma-desc-spinal-permanent = {" "}— irreversible, can only walk in an exoskeleton
dh-surgery-repair-nerve = Nerve repair
dh-surgery-repair-nerve-desc = Find the ends of the damaged nerve and suture them with a micro instrument. Anesthesia only.
dh-surgery-spinal-decompression = Spinal decompression
dh-surgery-spinal-decompression-desc = Remove bone fragments pressing on the spinal cord and fix the vertebrae. Anesthesia only, within 10 minutes of the injury.
dh-surgery-step-find-nerve = exposing the nerve ends
dh-surgery-step-suture-nerve = nerve suture
dh-surgery-step-remove-bone-fragments = removing bone fragments
dh-surgery-step-stabilize-spine = fixing the vertebrae
dh-surgery-tool-microforceps = microsurgical forceps
dh-surgery-needs-anesthesia = This can't be done without anesthesia — the patient must be unconscious.
