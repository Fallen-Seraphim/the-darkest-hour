# "LA-V PMC" event in Siege (siege plan item 11): a PMC squad on its own shuttle.
dh-lav-event-name = LA-V PMC
dh-lav-event-desc = A squad of the LA-V private military company arrives in its own shuttle on a corporate contract. Only the squad knows the objective. Deploys once at least six are interested; roles are handed out at random, command roles first.

dh-lav-role-commander = Squad commander
dh-lav-role-commander-desc = Leads the operation and knows the objective. LAVR-55, light armour.
dh-lav-role-deputy = Deputy commander
dh-lav-role-deputy-desc = The commander's right hand; takes over if the commander falls. LAVR-55, heavy armour.
dh-lav-role-technician = Combat technician
dh-lav-role-technician-desc = LAVMG-14 light machine gun, welder, tools, sandbags and barbed wire — fortify the objective and hold it.
dh-lav-role-sniper = Scout sniper
dh-lav-role-sniper-desc = LAVR-90 and a scout sight: sees further than anyone and removes machine gunners.
dh-lav-role-medic = Medic
dh-lav-role-medic-desc = Keeps the squad on its feet. LAVSMG-23, field surgery.
dh-lav-role-operative = Operative
dh-lav-role-operative-desc = The backbone of the squad: LAVR-55 and heavy armour.

dh-lav-name-format-commander = Commander { $part0 }
dh-lav-name-format-deputy = Deputy { $part0 }
dh-lav-name-format-technician = Technician { $part0 }
dh-lav-name-format-sniper = Sniper { $part0 }
dh-lav-name-format-medic = Medic { $part0 }
dh-lav-name-format-operative = Operative { $part0 }

dh-lav-shuttle-name = LA-V "Vanguard Runner"
dh-lav-target-tanker = the tanker
dh-lav-target-sample = the experimental sample
dh-lav-zone-edge = the edge of the operation area

dh-lav-no-target = LA-V operation cancelled: there is no suitable objective on the planet.
dh-lav-no-zone = LA-V operation cancelled: the shuttle has nowhere to land.

dh-lav-briefing-hold = [color=#8A6AD0][bold]LA-V PMC.[/bold][/color] The shuttle is in the atmosphere above the operation area — get ready. Land from the autopilot console in the cockpit (right-click → "Land" → LZ); if you do not choose within { $minutes } min, the autopilot lands on its own. Corporate contract — seize and hold until the end of the operation: [bold]{ $target }[/bold] — plant the LA-V flag there and keep other flags out. Neither side is your ally. Only the squad knows the objective. The tracker in the corner of the screen points to the objective.
dh-lav-briefing-sample = [color=#8A6AD0][bold]LA-V PMC.[/bold][/color] The shuttle is in the atmosphere above the operation area — get ready. Land from the autopilot console in the cockpit (right-click → "Land" → LZ); if you do not choose within { $minutes } min, the autopilot lands on its own. Corporate contract — take [bold]{ $target }[/bold] and fly it out on our shuttle (same console). Neither side is your ally. Only the squad knows the objective. The tracker in the corner of the screen points to the target.
dh-lav-briefing-commander-hold = [color=#8A6AD0][bold]LA-V PMC.[/bold][/color] The shuttle is in the atmosphere above the operation area — get ready. Land from the autopilot console in the cockpit (right-click → "Land" → LZ); if you do not choose within { $minutes } min, the autopilot lands on its own. Corporate contract — seize and hold until the end of the operation: [bold]{ $target }[/bold] — plant the LA-V flag there and keep other flags out. Neither side is your ally. Only the squad knows the objective. The tracker in the corner of the screen points to the objective. You are the commander: lead the squad. The objective counts if, when the operation ends, an LA-V flag stands within { $radius } tiles of it (you, the deputy and the technician carry them) and no side flags do. An LA-V flag on any capture area jams it — it cannot be taken and gives no points or effects. Leaving on the shuttle early fails the contract.
dh-lav-briefing-commander-sample = [color=#8A6AD0][bold]LA-V PMC.[/bold][/color] The shuttle is in the atmosphere above the operation area — get ready. Land from the autopilot console in the cockpit (right-click → "Land" → LZ); if you do not choose within { $minutes } min, the autopilot lands on its own. Corporate contract — take [bold]{ $target }[/bold] and fly it out on our shuttle (same console). Neither side is your ally. Only the squad knows the objective. The tracker in the corner of the screen points to the target. You are the commander: lead the squad. The sample counts once the shuttle leaves with it aboard.

dh-lav-task-hold = Objective: hold until the end of the operation — { $target }: an LA-V flag within { $radius } tiles of the objective, no side flags nearby.
dh-lav-task-sample = Objective: fly out on the shuttle — { $target }.
dh-lav-status = { $task } Shuttle: { $zone }. Status: { $state }.

dh-lav-outcome-active = operation in progress
dh-lav-outcome-held = objective held
dh-lav-outcome-stolen = sample flown out
dh-lav-outcome-withdrew = the squad left without completing the contract
dh-lav-outcome-failed = contract failed
dh-lav-outcome-wiped = squad wiped out

dh-lav-zone-atmosphere = in the atmosphere, waiting to land
dh-lav-verb-land = Land
dh-lav-console-choose-zone = Autopilot standing by: choose an LZ — right-click the console → "Land".
dh-lav-console-descending = Descent in progress.
dh-lav-descent = Autopilot: descending to { $zone }, touchdown in { $seconds } s. Strap in!
dh-lav-descent-blocked = Autopilot: no free LZ, holding and retrying.
dh-lav-autoland-warning = Autopilot: landing on a free LZ in one minute unless you choose one.
dh-lav-landed = The shuttle has landed: { $zone }.
dh-lav-flag-standing = Now: the LA-V flag stands, no enemy flags.
dh-lav-flag-missing = Now: no LA-V flag in the area, or an enemy flag stands nearby.
dh-lav-announcement-sender = Signals intelligence
dh-lav-landed-announcement = Radar has picked up an unidentified shuttle landing: { $zone }. Markings — LA-V private military company.
dh-lav-departed-announcement = The LA-V PMC shuttle has lifted off and left the operation area.
dh-lav-wiped-announcement = Intercept: the LA-V PMC squad is no longer answering its shuttle.

dh-lav-console-denied = The console does not respond: LA-V clearance required.
dh-lav-console-offline = The autopilot is offline.
dh-lav-console-start = Autopilot: preparing for lift-off!

dh-lav-summary-title = [color=#8A6AD0][bold]LA-V PMC[/bold][/color]
dh-lav-summary-outcome = Outcome: { $outcome }. Operatives evacuated: { $evacuated }.
dh-lav-summary-member = — { $name } ({ $role })

dh-lav-admin-opened = LA-V squad recruitment opened for 15 minutes (minimum — { $min }).
dh-lav-admin-busy = Recruitment is already open or a third-party event already happened this round.
dh-lav-admin-usage = Usage: dhcalllavsquad [minimum operatives]
cmd-dhcalllavsquad-desc = Opens ghost recruitment for the "LA-V PMC" Siege event.
cmd-dhcalllavsquad-help = Usage: dhcalllavsquad [minimum operatives, default 6]
dh-admin-tab-call-lav-squad = LA-V PMC (Siege)
dh-admin-tab-call-lav-squad-tooltip = Open recruitment for the LA-V squad (10 slots) for 15 minutes. Deploys once six sign up.

dh-alert-lav-tracker-name = Contract objective
dh-alert-lav-tracker-desc = Direction to the LA-V contract objective.

dh-job-lav-pmc-deputy-name = LA-V PMC deputy commander
dh-job-lav-pmc-deputy-desc = Support the commander and take over the squad if they fall.
dh-job-lav-pmc-technician-name = LA-V PMC combat technician
dh-job-lav-pmc-technician-desc = Hold the objective with the machine gun and fortify it.

tiles-dh-lav-floor = LA-V shuttle deck
