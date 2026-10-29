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
dh-heavy-gun-report-intercepted = Shell intercepted by enemy air defence.
dh-heavy-gun-report-impact = Shell landed at {$x}, {$y}.
dh-heavy-gun-report-miss = Miss: the shell went past the ship.
dh-heavy-gun-report-hit = Hit on the ship!
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

## Mortar and laser target designator
dh-warhead-flare = illumination
dh-heavy-gun-ui-marks = Laser marks
dh-heavy-gun-ui-no-marks = No laser marks on the surfaces in range.
dh-heavy-gun-ui-mark = Mark {$code}: {$body} ({$x}, {$y})
dh-laser-underground = There is no sky to lase from down here.
dh-laser-no-line = No line of sight, or farther than {$range} m.
dh-laser-lasing = You hold the laser on the target...
dh-laser-marked = Mark {$code} set at ({$x}, {$y}).
dh-laser-sender = Laser designator ({$name})
dh-laser-announce = Target marked: mark {$code} at ({$x}, {$y}).
dh-mortar-no-ground = There is no ground here to set up a mortar.
dh-mortar-deployed = {CAPITALIZE(THE($user))} sets up the mortar.
dh-mortar-fold-verb = Fold the mortar
dh-mortar-out-of-range = Out of range: from {$min} to {$max} m.
dh-mortar-aimed = The mortar is laid on ({$x}, {$y}).
dh-mortar-underground = The mortar can't fire underground.
dh-mortar-status-not-aimed = Not laid on a target.
dh-mortar-status-reloading = The tube is still hot.
dh-mortar-status-ready = Ready: drop a shell down the tube.
dh-mortar-examine-aimed = It is laid on [color=yellow]({$x}, {$y})[/color].
dh-mortar-examine-not-aimed = It is not laid on a target.
dh-mortar-ui-title = M402 mortar
dh-mortar-ui-position = Mortar position: ({$x}, {$y})
dh-mortar-ui-aimed = Laid on ({$x}, {$y}), {$distance} m.
dh-mortar-ui-not-aimed = Not laid on a target.
dh-mortar-ui-range = Range: {$min}-{$max} m.
dh-mortar-ui-coordinates = Target coordinates
dh-mortar-ui-aim = Lay the mortar
dh-mortar-ui-marks = Laser marks in range
dh-mortar-ui-mark = Mark {$code}: ({$x}, {$y}), {$distance} m
dh-mortar-ui-no-marks = No laser marks nearby.

## Infantry guns
dh-field-gun-wrong-caliber = This gun takes {$caliber} mm shells.
dh-field-gun-already-loaded = The gun is already loaded.
dh-field-gun-recoiling = The breech is still open after the last shot.
dh-field-gun-examine-loaded = Loaded: [color=yellow]{$shell}[/color].
dh-field-gun-examine-empty = It is not loaded.
dh-field-gun-examine-aim = Laid at {$azimuth}° traverse, {$elevation}° elevation.
dh-field-gun-status-not-deployed = Not wrenched down: the gun can't fire on the move.
dh-field-gun-status-underground = The gun can't fire underground.
dh-field-gun-status-empty = Not loaded.
dh-field-gun-status-too-high = This shell can't be fired above {$max}°.
dh-field-gun-status-ready = Ready to fire.
dh-field-gun-ui-title = Infantry gun — gunner's seat
dh-field-gun-ui-empty = Breech: empty.
dh-field-gun-ui-loaded = Breech: {$shell}.
dh-field-gun-ui-aim = Laying
dh-field-gun-ui-azimuth = Traverse, °
dh-field-gun-ui-elevation = Elevation, °
dh-field-gun-ui-limits = Now {$current}° / {$elevation}°. Traverse ±{$traverse}° of the carriage, elevation {$min}–{$max}°; up to {$direct}° — point-blank.
dh-field-gun-ui-set = Lay the gun
dh-field-gun-ui-mode-direct = Point-blank fire: the shell flies straight and hits the first thing in its way.
dh-field-gun-ui-mode-indirect = Indirect fire: the shell arcs over obstacles and lands at the computed range.
dh-field-gun-ui-range = Computed range for the loaded shell: {$range} m.
dh-field-gun-ui-range-unknown = Load a shell to see its range.
dh-field-gun-ui-fire = FIRE
dh-field-gun-ui-table = Range table, m
dh-field-gun-ui-table-shell = Shell
dh-field-gun-ui-marks = Laser marks
dh-field-gun-ui-mark = Mark {$code}: {$distance} m, traverse {$azimuth}°
dh-field-gun-ui-mark-low = Flat {$elevation}°
dh-field-gun-ui-mark-high = Lob {$elevation}°
dh-field-gun-ui-no-marks = No laser marks in range.

## Radars
dh-radar-broken = The radar screen goes dark: the anti-air is blind!
dh-radar-repaired = The radar comes back to life.
dh-radar-examine-active = The antenna turns steadily: the radar is [color=green]working[/color].
dh-radar-examine-broken = The radar is [color=red]silent[/color]. A welder could bring it back.
dh-radar-sender = Radar post
dh-radar-incoming-dropship = Enemy dropship ({$ship}) inbound to {$zone}!
