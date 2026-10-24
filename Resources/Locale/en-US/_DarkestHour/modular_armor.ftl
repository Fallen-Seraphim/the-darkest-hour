## DarkestHour: модульная броня

dh-armor-module-wrong-faction = This module does not fit this suit.
dh-armor-module-needs-rack = Heavy modules can only be fitted or removed at an armour maintenance rack.
dh-armor-module-slot-taken = That slot is already taken.
dh-armor-module-installed = { CAPITALIZE($module) } locks into place.
dh-armor-module-broken = { CAPITALIZE($module) } shatters!
dh-armor-module-repaired = You weld the cracked plates back together.
dh-armor-verb-remove = Remove { $module }

dh-armor-examine-module = { CAPITALIZE($module) }: { $percent }%
dh-armor-examine-special = Special system: { $module }
dh-armor-examine-empty = No armour modules fitted.
dh-armor-module-examine-class = Class: { $class }.
dh-armor-module-examine-integrity = Integrity: { $percent }%.
dh-armor-module-examine-broken = [color=red]Broken[/color] — weld it to repair.
dh-armor-class-light = light
dh-armor-class-heavy = heavy
dh-armor-class-composite = composite

dh-armor-automed-status = The auto-medic is armed and watches your vitals.
dh-armor-automed-injected = Your armour injects you with medicine.

dh-armor-automed-examine-volume = Medicine: [color=#7fd4ff]{ $volume }/{ $max } u[/color] — { $doses } { $doses ->
    [one] dose
   *[other] doses
} left.
dh-armor-automed-examine-reagent = — { $reagent }: { $amount } u
dh-armor-automed-examine-ready = [color=#5fd35f]Ready[/color]: injects when the wearer's damage reaches { $threshold }.
dh-armor-automed-examine-cooldown = [color=yellow]Recharging[/color]: { $seconds } s.
dh-armor-automed-examine-empty = [color=red]Empty[/color] — refill it with a syringe.
dh-armor-storage-examine = Holds { $count } { $count ->
    [one] item
   *[other] items
}. Open it with the special system action.
dh-armor-weapon-examine-stowed = Built-in { $weapon }: stowed.
dh-armor-weapon-examine-out = Built-in { $weapon }: [color=yellow]extended[/color].
