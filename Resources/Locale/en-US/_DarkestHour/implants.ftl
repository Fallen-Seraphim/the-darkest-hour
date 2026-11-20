# Implants and rejection
dh-implant-kind-Base = basic
dh-implant-kind-Pulsar = "Pulsar"
dh-implant-kind-Echo = "Echo"
dh-implant-kind-Scale = "Scale"
dh-implant-kind-Silence = "Silence"
dh-implant-kind-Filter = "Filter"
dh-implant-kind-Hemostat = "Hemostat"
dh-implant-kind-Falcon = "Falcon"
dh-implant-kind-Vise = "Vise"
dh-implant-kind-Support = "Support"
dh-implant-kind-Tactic = "Tactic"
dh-implant-wrong-part = This implant goes into a different body part.
dh-implant-already = This implant is already installed.
dh-implant-pulsar-zap = {CAPITALIZE(THE($target))}'s chest jolts from the "Pulsar" discharge!
dh-implant-nausea = A wave of nausea, your whole body aches.
dh-implant-weakness = Your legs give way — your body is rejecting the implants.
dh-implant-agony-start = Something tears inside — your body no longer accepts the implants!
dh-implant-agony = {CAPITALIZE(THE($target))} convulses and coughs up blood!
dh-implant-rejection-death = The body was destroyed by implant rejection. Revival is pointless.
dh-implant-analyzer-list = [color=#80b0e0]Implants:[/color] {$implants}
dh-implant-analyzer-rejection = [color=#d0a040]Implant rejection: {$percent}%.[/color] Citrasin and dialysis (life support) slow it.
dh-implant-analyzer-terminal = [color=#d04040]Terminal implant rejection! It cannot be stopped.[/color]
dh-surgery-insert-implant = Implant installation
dh-surgery-insert-implant-desc = Install the implant in your hand. While it's in the body, rejection grows — faster the more the implant works.
dh-surgery-step-insert-implant = installing the implant
dh-surgery-tool-implant = an implant in hand
dh-surgery-remove-implant = Implant removal
dh-surgery-remove-implant-desc = Take the implant out. Rejection stops growing from it, but what has built up stays.
dh-surgery-step-remove-implant = removing the implant
dh-entity-effect-guidebook-slow-rejection =
    { $chance ->
        [1] Slows
        *[other] slow
    } implant rejection
