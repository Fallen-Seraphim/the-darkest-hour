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
dh-armor-module-examine-class = Protection class: { $level } ({ $class }).
dh-armor-part-examine-class = Protection class: { $level }.
dh-armor-module-examine-integrity = Integrity: { $percent }%.
dh-armor-module-examine-broken = [color=red]Broken[/color] — weld it to repair.
dh-armor-class-light = light
dh-armor-class-heavy = heavy
dh-armor-class-composite = composite

dh-armor-automed-status = The auto-medic is armed and watches your vitals.
dh-armor-automed-injected = Your armour injects you with omnireazine.

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

## Armour / penetration class (examine icon)
dh-armor-verb-text = Armour class
dh-armor-verb-class = Armour class: [color=cyan]{ $level }[/color].
dh-armor-verb-parts = Covers: { $parts }.
dh-armor-verb-suit = Suit modules:
dh-armor-verb-module = { $slot }: class [color=cyan]{ $level }[/color] ({ $class }), integrity { $percent }%.
dh-armor-verb-module-broken = { $slot }: [color=red]broken[/color] — no protection.
dh-armor-verb-slot-empty = { $slot }: [color=gray]empty[/color] — unprotected.
dh-armor-slot-torso = Torso
dh-armor-slot-armleft = Left arm
dh-armor-slot-armright = Right arm
dh-armor-slot-legleft = Left leg
dh-armor-slot-legright = Right leg
dh-armor-slot-special = Special system
dh-armor-part-head = head
dh-armor-part-torso = torso
dh-armor-part-armleft = left arm
dh-armor-part-armright = right arm
dh-armor-part-handleft = left hand
dh-armor-part-handright = right hand
dh-armor-part-legleft = left leg
dh-armor-part-legright = right leg
dh-armor-part-footleft = left foot
dh-armor-part-footright = right foot
dh-penetration-verb-text = Armour penetration
dh-penetration-verb-class = Penetration class: [color=orange]{ $level }[/color].
dh-penetration-verb-hint = Defeats armour of class 0–{ $level }; tougher armour stops it (blunt trauma only).

# Exoskeletons (DHExoskeletonComponent)
dh-exo-speed-same = unchanged
dh-exo-class-light = light
dh-exo-class-medium = medium
dh-exo-class-heavy = heavy
dh-exo-no-special = A light exoskeleton has no mount for a special system.
dh-exo-wrong-plate = A { $exo } exoskeleton does not take { $plate } plates.
dh-exo-examine = [color=lightblue]{ CAPITALIZE($exo) } exoskeleton[/color]: takes { $plates } plates; speed { $speed }, carries { $load }% of your gear, melee strikes +{ $melee }%.
