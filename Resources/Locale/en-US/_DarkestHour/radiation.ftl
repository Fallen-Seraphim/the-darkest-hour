## DarkestHour: лучевая и хронитовая кожно-покровная болезни (этап 2 медицины, шаг 4)

dh-disease-radiation-sickness = Radiation sickness
dh-disease-chronite-skin = Chronite skin disease

dh-disease-symptom-rad-metal-taste = There is a metallic taste in your mouth.
dh-disease-symptom-rad-hair = A clump of your hair falls out.
dh-disease-symptom-rad-bleeding = Your gums are bleeding and bruises bloom on your skin.
dh-disease-symptom-rad-dying = Your body is falling apart from the inside.
dh-disease-symptom-chronite-spots = Violet spots glow faintly under your skin.
dh-disease-symptom-chronite-itch = Your skin itches where it glows.
dh-disease-symptom-chronite-crystals = Your skin hardens into glittering violet crystals.
dh-disease-symptom-chronite-cracks = The crystallised skin cracks and bleeds!
dh-disease-symptom-chronite-necrosis = The flesh under the crystals is dying and turning black!
dh-disease-symptom-chronite-terminal = Chronite has eaten through you. It glows from within.

dh-dose-kind-Radiation = accumulated radiation
dh-dose-kind-Chronite = accumulated chronite

dh-dosimeter-reading = {CAPITALIZE(THE($target))}: radiation {$radiation} ({$radStage}), chronite {$chronite} ({$chrStage})
dh-dosimeter-stage = stage {$stage}
dh-dosimeter-safe = safe

dh-entity-effect-guidebook-reduce-dose =
    { $chance ->
        [1] Removes
        *[other] remove
    } {$kind} from the body

dh-entity-effect-guidebook-add-chronite =
    { $chance ->
        [1] Exposes
        *[other] expose
    } the body to chronite

dh-entity-effect-guidebook-add-pain =
    { $chance ->
        [1] Causes
        *[other] cause
    } pain

dh-surgery-step-excise-skin = excise the crystallised skin
dh-surgery-step-graft-skin = graft new skin
dh-surgery-tool-skin-graft = a skin graft in hand
dh-surgery-excise-chronite = Excise chronite skin
dh-surgery-excise-chronite-desc = Cut away the skin crystallised by chronite and cover it with a graft. Removes a large part of the chronite from the body. Impossible at the last stage.

reagent-name-dh-chronosorb = chronosorb
reagent-desc-dh-chronosorb = Binds chronite in the body and flushes it out. Toxic on its own: give it together with dylovene. Useless once chronite disease reaches its last stage.

## Экспериментальное лечение хронитовой болезни
reagent-name-dh-chronolysin = chronolysin
reagent-desc-dh-chronolysin = An experimental agent that dissolves chronite crystals in tissue, even at the last stage of chronite skin disease. Badly damages cells and causes pain and seizures.
dh-chronolysin-effect-1 = Your skin crawls as something cracks deep inside it.
dh-chronolysin-effect-2 = A violet shimmer runs under your skin. It burns.
dh-chronolysin-effect-3 = Every muscle in your body twitches at once.
research-technology-dh-experimental-chronotherapy = Experimental Chronotherapy
