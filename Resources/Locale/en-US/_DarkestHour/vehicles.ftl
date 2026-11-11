dh-vehicle-entering = You climb into {THE($vehicle)}...
dh-vehicle-runover = {CAPITALIZE(THE($vehicle))} runs you over!
dh-vehicle-station-sit = Sit down in the seat at this station first.
dh-vehicle-station-occupied = Someone is already at this station.
dh-vehicle-station-taken = You take over the { $station }. Use it again or get up from the seat to let go.
dh-vehicle-ammo-no-gun = This vehicle has no gun that takes this ammunition.
dh-vehicle-ammo-full = { CAPITALIZE($gun) } is fully loaded.
dh-vehicle-ammo-loaded = Loaded { $count } into the { $gun }.
dh-vehicle-ammo-examine = { CAPITALIZE($gun) }: [color=yellow]{ $count }[/color] / { $capacity }.

dh-vehicle-examine-integrity = Hull integrity: [color={ $color }]{ $percent }%[/color].

dh-tow-hitch-verb = Hitch to the truck
dh-tow-unhitch-verb = Unhitch from the truck
dh-tow-hitched = {CAPITALIZE($gun)} is hitched to {$vehicle}.
dh-tow-unhitched = {CAPITALIZE($gun)} is unhitched.

# Hardpoints (DHVehicleServerSystem.Hardpoints)
dh-hardpoint-wrong-vehicle = This module does not fit this vehicle.
dh-hardpoint-slot-taken = That mount is already taken — remove the old module with a wrench first.
dh-hardpoint-installed = You install {THE($module)}.
dh-hardpoint-removed = You remove {THE($module)}.
dh-hardpoint-intact = The module is intact.
dh-hardpoint-broken = {CAPITALIZE(THE($module))} is knocked out!
dh-hardpoint-verb-remove = Remove: { $module }
dh-hardpoint-verb-repair = Repair: { $module } ({ $percent }%)
dh-hardpoint-examine-header = [bold]Modules:[/bold]
dh-hardpoint-examine-empty = { $slot }: [color=gray]empty[/color]
dh-hardpoint-examine-module = { $slot }: { $module } — [color={ $color }]{ $percent }%[/color]
dh-hardpoint-examine-integrity = Integrity: [color={ $color }]{ $percent }%[/color]. Weld it to repair.
dh-hardpoint-slot-primary = main gun
dh-hardpoint-slot-secondary = secondary gun
dh-hardpoint-slot-support = support
dh-hardpoint-slot-armor = armour
dh-hardpoint-slot-wheels = wheels / tracks
dh-hardpoint-slot-port-left = left port
dh-hardpoint-slot-port-right = right port

# Crew (DHVehicleServerSystem.Crew)
dh-vehicle-locked = The hatches are locked from inside.
dh-vehicle-verb-lock = Lock the hatches
dh-vehicle-verb-unlock = Unlock the hatches
dh-vehicle-locked-now = You lock the hatches. Nobody gets in from outside.
dh-vehicle-unlocked-now = You unlock the hatches.
dh-vehicle-periscope-look = You look through the periscope. Step away to stop.
