## Z-уровни

dh-zlevel-name-ground = ground level
dh-zlevel-name-up = level +{ $level }
dh-zlevel-name-down = level -{ $level }
dh-zlevel-map-up = Level +{ $level }
dh-zlevel-map-down = Underground level -{ $level }
dh-zlevel-stairs-blocked = The way is blocked.
dh-zlevel-fell = You fall { $levels ->
    [one] a floor down!
   *[other] { $levels } floors down!
}
dh-zlevel-look-nothing = There are no other levels here.
dh-zlevel-look-nothing-up = There is nothing above.
dh-zlevel-look-nothing-down = There is nothing below.
dh-zlevel-look-too-far = You can't see that far — a wielded x8 or x16 scope would help.
dh-zlevel-look-covered = There's a ceiling above you — find stairs, a ladder or a lift shaft.
dh-zlevel-look-no-edge = Nothing to look down through — get to an edge, stairs, a hatch or a manhole.
dh-zlevel-look-start = You look at { $level }.
dh-zlevel-look-stop = You stop looking.
dh-lift-choose-floor = Choose a level in the alternative actions.
dh-lift-verb-send-to = Send to { $level }
dh-lift-verb-call-from = Call from { $level }
dh-mortar-covered = There's a roof overhead — the mortar can't fire.
dh-field-gun-status-covered = There's a roof overhead — no indirect fire.

cmd-dhzlevel-desc = Z-levels for mapping: lists the levels of your stack, or creates an empty level in it.
cmd-dhzlevel-help = Usage: dhzlevel [level] (−3..8, not 0)
cmd-dhzlevel-no-map = You are not on a map.
cmd-dhzlevel-no-stack = This map has no Z-levels yet.
cmd-dhzlevel-entry = Level { $level }: map { $map }
cmd-dhzlevel-exists = That level already exists.
cmd-dhzlevel-range = Level must be from -3 to 8 and not 0.
cmd-dhzlevel-created = Created level { $level } as map { $map }. Build the floor there at the same coordinates and save it with savegrid.
