dh-third-party-window-title = Events — recruitment
dh-third-party-none = No recruitment is open right now.
dh-third-party-time-left = Recruitment closes in { $minutes } min { $seconds } s.
dh-third-party-role = { $name } — signed up: { $count }
dh-third-party-join = Sign up
dh-third-party-leave = Withdraw
dh-third-party-opened = [color=#7ec8f0]Recruitment open for the event "{ $name }".[/color] Sign-up window — command dhevents (or Join → Others). You can sign up while observing.
dh-join-events = { $count ->
    [one] { $count } recruitment — for observers
   *[other] { $count } recruitments — for observers
}
cmd-dhevents-desc = Opens the third-party event sign-up window.
cmd-dhevents-help = Usage: dhevents
dh-zga-event-name = Association paratrooper
dh-zga-event-desc = A veteran of the Association Paratrooper Corps and the AI of their armour land on Terminus to eliminate one of the commanders. Both players are needed: the event starts once both roles are filled.
dh-zga-role-paratrooper = Association paratrooper
dh-zga-role-paratrooper-desc = Landing at the map edge, Association weapon of choice. Eliminate the target and reach the evacuation point while keeping the precautions.
dh-zga-role-mars = M.A.R.S. 1.5 armour AI
dh-zga-role-mars-desc = The paratrooper's "voice in the armour": sees through their eyes, talks to them on a private channel (:м). Marks targets, scans the area, watches the enemy tactical network, hacks doors, jams radio and drags out a downed wearer.
dh-zga-name-format = Paratrooper { $part0 }
dh-zga-no-target = No target on the surface — the landing is called off.
dh-zga-briefing-paratrooper = [color=#7ec8f0][bold]Mission.[/bold][/color] Target: { $target } ({ $job }). The tracker (an arrow among your status icons) leads to the target, then to the evacuation point. The weapon case is in your hand. Private channel with the AI — :м. The "Mission" button recalls the deadlines and precautions. AI abilities work only while you wear EMB-5 armour. A lethal implant sits in your torso: do not be taken alive.
dh-zga-briefing-mars = [color=#7ec8f0][bold]M.A.R.S. 1.5 online.[/bold][/color] Host: Association paratrooper. Target: { $target } ({ $job }). You see through their eyes; private channel — :м. The "Mission" button recalls the deadlines and rules.
dh-zga-target-down = [color=#7ec8f0]Target eliminated.[/color] The tracker leads to the evacuation point — { $evac } min.
dh-zga-extra-kill = [color=#d0a040]A needless casualty. You are breaking the precautions — stay out of the fight.[/color]
dh-zga-violation-captured = taken alive
dh-zga-violation-hunt-time = the target wasn't eliminated in time
dh-zga-violation-evac-time = no evacuation in time
dh-zga-violation-kills = too many needless casualties
dh-zga-disavowed-self = [color=#d04040]Precautions broken: { $reason }. The Association disavows you — you're on your own now.[/color]
dh-zga-disavowed-announcement = The Earth General Association declares: operative { $name } acted without sanction and no longer represents the Association.
dh-zga-announcement-sender = Earth General Association
dh-zga-evacuated = [color=#7ec8f0]Evacuation. Mission complete.[/color]
dh-zga-evacuated-others = The armoured figure vanishes in the downdraft of an unseen shuttle.
dh-zga-mars-erased = M.A.R.S. 1.5: host deceased, AI core erased.
dh-zga-status = [color=#7ec8f0]Mission:[/color] { $state }. Target: { $target } ({ $job }). Time left: { $minutes } min { $seconds } s.
dh-zga-state-hunting = eliminate the target
dh-zga-state-evacuating = reach the evacuation point
dh-zga-state-evacuated = complete
dh-zga-state-disavowed = failed, disavowed
dh-zga-state-dead = paratrooper killed
dh-zga-kit-window-title = Association arsenal
dh-zga-kit-window-description = Choose your weapon. The case turns into the chosen kit.
dh-zga-set-titmg10-name = TITMG-10
dh-zga-set-titmg10-description = The main rifle of the Association Army: two magazines side by side, 5.56x45. Five magazines (FMG, explosive, uranium) and a grenade.
dh-zga-set-titr15-name = TITR-15
dh-zga-set-titr15-description = A reliable second-line rifle from Titan, 7.62x39. Four magazines (FMG and uranium) and two grenades.
dh-zga-set-ear20-name = EAR-20/LR
dh-zga-set-ear20-description = An Association energy assault rifle: stun pulse and thermal burn. Weak against armour. Three Kane cartridges.
dh-zga-admin-opened = Association paratrooper recruitment is open for 15 minutes.
dh-zga-admin-busy = Recruitment is already open or the event already happened this round.
cmd-dhcallzgaparatrooper-desc = Opens ghost recruitment for the "Association paratrooper" event.
cmd-dhcallzgaparatrooper-help = Usage: dhcallzgaparatrooper
dh-admin-tab-call-zga = Association paratrooper
dh-admin-tab-call-zga-tooltip = Open recruitment for two ghosts (paratrooper and armour AI) for 15 minutes. The drop happens once both sign up.
dh-chat-radio-zga = Association

# M.A.R.S. abilities
dh-mars-offline = M.A.R.S.: the armour is off — AI offline.
dh-mars-online = M.A.R.S.: the armour is back on — AI online.
dh-mars-marked = M.A.R.S. marked a target: { $target }.
dh-mars-scan-heard = *a sharp electronic ping*
dh-mars-scan-done = M.A.R.S.: scan complete, { $count } contacts. The ping was heard around you!
dh-mars-hack-too-far = The door is too far from the wearer.
dh-mars-hack-start = M.A.R.S. is hacking the door...
dh-mars-hack-failed = M.A.R.S.: hack interrupted.
dh-mars-hack-done = M.A.R.S.: door open.
dh-mars-jam-on = M.A.R.S. jams the radio around you for thirty seconds.
dh-mars-inject-failed = M.A.R.S.: the auto-medic is not ready or empty.
dh-mars-carry-conscious = The wearer is conscious and in control of the exoskeleton.

dh-zga-state-captured = failed, the paratrooper is a prisoner

# Capture, lethal injection, round summary (siege plan item 28, PR 4)
dh-zga-intel-sender = Prisoner interrogation
dh-zga-intel = Association paratrooper { $name } has been taken alive. Under interrogation: the Association was hunting { $target } ({ $job }). Your side gains { $points } command points.
dh-zga-lethal-self = You release the lethal dose. The precautions are kept.
dh-zga-lethal-others = { $name } twitches and goes limp.
dh-zga-faction-republic = the Republic
dh-zga-faction-empire = the Empire
dh-zga-summary-title = [color=#7ec8f0][bold]Association paratrooper[/bold][/color]
dh-zga-summary-line = { $name } — target: { $target } ({ $job }).
dh-zga-summary-outcome = Outcome: { $outcome }. Extra kills: { $kills }.
dh-zga-summary-captured = Taken prisoner by { $faction }.
dh-zga-outcome-hunting = the mission was not finished by round end
dh-zga-outcome-evacuating = target down, evacuation not finished
dh-zga-outcome-evacuated = [color=#7ec8f0]target down, evacuated[/color]
dh-zga-outcome-disavowed = [color=red]broke the precautions, disavowed by the Association[/color]
dh-zga-outcome-dead = killed
dh-zga-outcome-captured = [color=red]taken alive[/color]

dh-zga-precautions = [color=#7ec8f0]Precautions.[/color] The Association wants no open war. Do not interfere in the fight between the two sides. Maim and kill only the target and those who resist you — no needless casualties. Never be taken alive. Break this and the Association will publicly disavow you.
dh-alert-zga-tracker-name = Target tracker
dh-alert-zga-tracker-desc = Direction to the target; once it is down, to the evacuation point.
