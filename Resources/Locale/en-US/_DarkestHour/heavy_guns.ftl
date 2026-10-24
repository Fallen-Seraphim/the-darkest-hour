## Тяжёлые орудия «Штурма»: корабельная артиллерия, ПКО, ПВО

dh-warhead-high-explosive = high-explosive
dh-warhead-armor-piercing = armour-piercing
dh-warhead-incendiary = incendiary
dh-warhead-cluster = cluster

dh-heavy-gun-wrong-caliber = This breech takes {$caliber} mm shells.
dh-heavy-gun-already-loaded = The breech is already loaded.
dh-heavy-gun-examine-assembled = [color=green]The gun is assembled:[/color] the barrel is in place.
dh-heavy-gun-examine-not-assembled = [color=red]The gun is not assembled:[/color] set a barrel of the same calibre on the tile in front of the breech, facing the same way.
dh-heavy-gun-examine-loaded = Loaded with a [color=yellow]{$warhead}[/color] shell.
dh-heavy-gun-examine-empty = The breech is empty.
dh-heavy-gun-examine-reloading = Reloading: {$seconds} s.

dh-heavy-gun-status-not-assembled = not assembled
dh-heavy-gun-status-reloading = reloading, {$seconds} s
dh-heavy-gun-status-empty = no shell
dh-heavy-gun-status-ready = ready, {$warhead}

dh-heavy-gun-need-seat = Sit down at the console to lay the guns.
dh-heavy-gun-bad-target = The target is out of reach.
dh-heavy-gun-fired = Salvo: {$count} {$count ->
    [one] gun
   *[other] guns
} fired.
dh-heavy-gun-nothing-fired = No selected gun is ready to fire.
dh-heavy-gun-no-targets = No ships in orbit within reach.
dh-heavy-gun-targets-found = Ships in orbit within reach: {$count}.
dh-heavy-gun-not-on-surface = The console is not on the surface of a world: anti-orbital guns fire only from the ground.
dh-heavy-gun-no-bodies = No world with known surface coordinates within {$range} below.
dh-heavy-gun-bodies-found = Choose the world and the coordinates on its surface.
dh-heavy-gun-not-in-orbit = The ship is not in orbit of a world: naval artillery fires only from orbit.

dh-heavy-gun-ui-title = Fire control
dh-heavy-gun-ui-guns = Gun
dh-heavy-gun-ui-target = Target
dh-heavy-gun-ui-defenses = Air defence of this hull
dh-heavy-gun-ui-fire = SALVO
dh-heavy-gun-ui-seated = The gunner is seated.
dh-heavy-gun-ui-not-seated = Sit down at the console to fire.
dh-heavy-gun-ui-gun = {$name} ({$caliber} mm): {$status}
dh-heavy-gun-ui-no-guns = No gun linked: every gun has its own console, set it next to the breech (within 5 m).
dh-heavy-gun-ui-target-entry = {$name} — {$altitude} m above the surface
dh-heavy-gun-ui-body-entry = {$name} ({$altitude} m below)
dh-heavy-gun-ui-zone = Surface coordinates in metres from the landing zone centre, within ±{$zone}.
dh-heavy-gun-ui-defense = {$name}: {$ammo}/{$max}
dh-heavy-gun-ui-no-defenses = None.

dh-interceptor-wrong-ammo = This ammunition does not fit.
dh-interceptor-full = It is fully loaded.
dh-interceptor-examine = Ammunition: [color=yellow]{$ammo}/{$max}[/color].

dh-assault-carrier-damaged = The carrier has been hit by anti-orbital fire! Hull integrity: {$hull}%.
