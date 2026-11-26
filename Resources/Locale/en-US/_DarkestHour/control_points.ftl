dh-control-point-sender = Operations
dh-control-point-captured-ours = We have taken { $point }.
dh-control-point-captured-enemy = The enemy has taken { $point }.
dh-control-point-lost = We have lost { $point }!
dh-control-point-examine-name = Capture area flagpole: [bold]{ $point }[/bold].
dh-control-point-examine-neutral = Nobody controls the area.
dh-control-point-examine-republic = The area is held by the [color=#7fcf4f]Republic[/color].
dh-control-point-examine-empire = The area is held by the [color=#e8604a]Empire[/color].
dh-control-point-flag-raised = The enemy is raising a flag at { $point }! The area falls to them in { $seconds } s — take the flag down or destroy it.
dh-control-point-jammed = The LA-V PMC flag has been raised over { $point }: while it stands, the area is out of action and gives no points.
dh-control-point-examine-jammed = The [color=#8A6AD0]LA-V PMC[/color] flag stands in the area: it is jammed — it cannot be taken and gives no points or effects.
dh-control-point-examine-contested = Flags of both sides stand in the area: it is contested, no points to anyone.
dh-control-point-examine-capturing-republic = The [color=#7fcf4f]Republic[/color] is raising its flag: the area falls in { $seconds } s.
dh-control-point-examine-capturing-empire = The [color=#e8604a]Empire[/color] is raising its flag: the area falls in { $seconds } s.
dh-control-point-examine-how = Capture: plant your side's flag within { $radius } tiles of the flagpole; if no enemy flag stands in the area for 30 s, it is yours. An enemy flag can be taken down (8 s) or destroyed.

dh-control-point-village = Village
dh-control-point-village-desc = Gives 2 command points per minute.
dh-control-point-spaceport = Spaceport
dh-control-point-spaceport-desc = Gives 5 command points per minute.
dh-control-point-construction = Construction site
dh-control-point-construction-desc = Gives 3 command points per minute.
dh-control-point-npp = Power plant
dh-control-point-npp-desc = The side holding the plant has power for its floodlights and air defences across the area. Gives 5 command points per minute.
dh-control-point-dam = Dam
dh-control-point-dam-desc = The only safe crossing over the river. Gives 3 command points per minute.
dh-control-point-pko = Anti-orbital battery
dh-control-point-pko-desc = The anti-orbital guns can fire at the carrier only while the Republic holds the battery. Gives 4 command points per minute.
dh-control-point-pvo = Air defence site
dh-control-point-pvo-desc = Air defence intercepts strikes, the attack aircraft and dropships only while the Republic holds the site and the power plant. Gives 4 command points per minute.
dh-control-point-rls = Radar and comms post
dh-control-point-rls-desc = The radar shows the Republic enemy signatures while it holds the post and the power plant. Losing the post jams the garrison's common channel: only nearby radios hear it. Gives 4 command points per minute.

dh-overwatch-ui-tacmap = Tactical map
dh-tacmap-title = Tactical map
dh-tacmap-loading = Loading terrain...
dh-tacmap-no-surface = No planet surface data.
dh-tacmap-reset = Fit
dh-tacmap-legend = Wheel — zoom, drag — pan. Squares — capture zones, frames — landing zones, crosses — laser marks. Enemies — dots (troops) and squares (vehicles) spotted by your troops, vehicles, radars and detectors; pink — the last global scan, triangles — attack aircraft radar.
dh-tacmap-laser-mark = Mark { $code }
dh-tacmap-counter-battery = Counter-battery
dh-tacmap-drop-beacon = Beacon { $number }
dh-tacmap-drop-beacon-incoming = Beacon { $number }: cargo inbound
dh-tacmap-drop-incoming = Supply drop
dh-counter-battery-sender = Counter-battery radar
dh-counter-battery-announce = Enemy fire detected: gun near X { $x }, Y { $y } (± { $radius } m).

cmd-dhcontrolpoint-desc = Sets the owner of a capture zone.
cmd-dhcontrolpoint-help = Usage: dhcontrolpoint <village|spaceport|construction|npp|dam> <Republic|Empire|none>
cmd-dhcontrolpoint-done = Zone owner changed.
cmd-dhcontrolpoint-not-found = No capture zone with that id.

dh-tacmap-level = Level:
dh-tacmap-level-ground = Ground
dh-control-point-powered-backup = [color=#E0B050]Running on the backup diesel: about {$minutes} min left.[/color]
dh-control-point-powered-off = [color=#A05050]No power: the plant grid is lost.[/color]
