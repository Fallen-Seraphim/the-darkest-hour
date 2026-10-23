## Соединённая Республика: отряд зачистки Республиканского Армейского Корпуса

id-card-access-level-dh-republic = Republican Army Corps

dh-job-republic-supervisors = the Republican Army Corps
dh-job-republic-commander-name = Republic sweep team commander
dh-job-republic-commander-desc = Lead the Republic sweep team: take the station under martial law for the United Republic.
dh-job-republic-sniper-name = Republic sweep team marksman
dh-job-republic-sniper-desc = Cover the squad from range with the RMAR-AS-M-15-18.
dh-job-republic-machinegunner-name = Republic sweep team machinegunner
dh-job-republic-machinegunner-desc = Pin down resistance with the RAMG-M-14-1. Heavy plates, slow feet.
dh-job-republic-rifleman-name = Republic sweep team rifleman
dh-job-republic-rifleman-desc = The backbone of the sweep team. Follow the commander's orders.
dh-job-republic-medic-name = Republic sweep team medic
dh-job-republic-medic-desc = Keep the squad on its feet.
dh-job-republic-flamer-name = Republic sweep team flamethrower
dh-job-republic-flamer-desc = Burn out whatever refuses to come out. Heavy plates and a rack of spare fuel tanks.

dh-ghost-role-republic-rules = You are a soldier of a Republic sweep team. The station is under martial law (threat code Indomitus): its contracts with Metallistick Group are void and the crew must obey you. Follow your commander. Detain those who resist and use lethal force only against armed resistance; do not harm crew who comply.
dh-ghost-role-republic-commander-name = Republic sweep team commander
dh-ghost-role-republic-commander-desc = Lead an eight-strong Republican sweep team sent to take a Metallistick Group station under martial law.
dh-ghost-role-republic-sniper-name = Republic sweep team marksman
dh-ghost-role-republic-sniper-desc = Marksman of a Republican sweep team, with an RMAR-AS-M-15-18 and light RMB-1 plates.
dh-ghost-role-republic-machinegunner-name = Republic sweep team machinegunner
dh-ghost-role-republic-machinegunner-desc = Machinegunner of a Republican sweep team, with an RAMG-M-14-1 and heavy RMB-1 plates.
dh-ghost-role-republic-rifleman-name = Republic sweep team rifleman
dh-ghost-role-republic-rifleman-desc = Rifleman of a Republican sweep team, with an RAR-M-11-1.
dh-ghost-role-republic-medic-name = Republic sweep team medic
dh-ghost-role-republic-medic-desc = Field medic of a Republican sweep team, with an RAP-TOR-M-12-16.
dh-ghost-role-republic-flamer-name = Republic sweep team flamethrower
dh-ghost-role-republic-flamer-desc = Flamethrower of a Republican sweep team, with an RFL-M-13 incinerator and heavy RMB-1 plates.

dh-name-format-republic-commander = Commander {$part0}
dh-name-format-republic-sniper = Marksman {$part0}
dh-name-format-republic-machinegunner = Machinegunner {$part0}
dh-name-format-republic-rifleman = Rifleman {$part0}
dh-name-format-republic-medic = Medic {$part0}
dh-name-format-republic-flamer = Flamethrower {$part0}

dh-republic-staging-map = Republican staging area
dh-republic-call-no-station = No station to send the sweep team to.
dh-republic-call-load-failed = Could not load the sweep team shuttle ({$path}).
dh-republic-call-done = Republic sweep team dispatched to {$station}; threat code Indomitus is in effect.

cmd-dhcallrepublic-desc = Sends the Republic sweep team shuttle to a station and sets threat code Indomitus there.
cmd-dhcallrepublic-help = Usage: dhcallrepublic [station EntityUid]

dh-admin-tab-call-republic = Call the Republic sweep team (Indomitus)
dh-admin-tab-call-republic-tooltip = Loads the RAC Cordon shuttle with eight ghost roles (commander, marksman, machinegunner, two riflemen, two medics, flamethrower), jumps it to the station and sets threat code Indomitus.

## Усиленный отряд зачистки (код «Деаденд»)

dh-ghost-role-republic-deadend-rules = You are a soldier of a reinforced Republic sweep team. The station capitulated to the First Empire and is condemned for treason (threat code Deadend): the crew's citizenship is annulled and every one of them is wanted by the Republic. Your mission is to destroy the station with the tactical nuclear warhead. Follow your commander, suppress all resistance and protect the warhead until it detonates. You may let unarmed crew who surrender flee the station.
dh-ghost-role-republic-deadend-commander-name = Reinforced sweep team commander
dh-ghost-role-republic-deadend-commander-desc = Lead a ten-strong reinforced Republican sweep team and destroy a condemned station with a tactical nuclear warhead. You carry the warhead keys and the authentication code.
dh-ghost-role-republic-deadend-sniper-name = Reinforced sweep team marksman
dh-ghost-role-republic-deadend-sniper-desc = Marksman of a reinforced Republican sweep team sent to destroy a condemned station.
dh-ghost-role-republic-deadend-machinegunner-name = Reinforced sweep team machinegunner
dh-ghost-role-republic-deadend-machinegunner-desc = Machinegunner of a reinforced Republican sweep team sent to destroy a condemned station.
dh-ghost-role-republic-deadend-rifleman-name = Reinforced sweep team rifleman
dh-ghost-role-republic-deadend-rifleman-desc = Rifleman in full composite RMB-1 of a reinforced Republican sweep team sent to destroy a condemned station.
dh-ghost-role-republic-deadend-medic-name = Reinforced sweep team medic
dh-ghost-role-republic-deadend-medic-desc = Medic of a reinforced Republican sweep team sent to destroy a condemned station.
dh-ghost-role-republic-deadend-flamer-name = Reinforced sweep team flamethrower
dh-ghost-role-republic-deadend-flamer-desc = Flamethrower of a reinforced Republican sweep team sent to destroy a condemned station.

dh-republic-call-deadend-done = Reinforced Republic sweep team with a tactical nuclear warhead dispatched to {$station}; threat code Deadend is in effect.

cmd-dhcallrepublicdeadend-desc = Sends the reinforced Republic sweep team (tactical nuclear warhead) to a station and sets threat code Deadend there.
cmd-dhcallrepublicdeadend-help = Usage: dhcallrepublicdeadend [station EntityUid]

dh-admin-tab-call-republic-deadend = Call the reinforced sweep team (Deadend)
dh-admin-tab-call-republic-deadend-tooltip = Loads the RAC Verdict shuttle with ten ghost roles and a tactical nuclear warhead (keys and code go to the commander), jumps it to the station and sets threat code Deadend. Happens automatically when a station surrenders to the Imperial Raid.

## Тактическая ядерная боеголовка

location-dh-tactical-warhead = tactical nuclear warhead

dh-warhead-code-paper-content = [head=2]UNITED REPUBLIC — REPUBLICAN ARMY CORPS[/head]
    Order: [bold]Deadend[/bold]. Eyes of the sweep team commander only.

    Nuclear authentication code of the RTW-9 tactical warhead:
    {"[head=1]"}{$code}{"[/head]"}

    1. Open the input panel with the panel key.
    2. Swipe the authorisation key on the warhead.
    3. Enter the code, set the timer, arm.

    Glory to the United Republic!

dh-warhead-panel-closed = The input panel is locked. Open it with the panel key.
dh-warhead-need-auth = Swipe the authorisation key.
dh-warhead-panel-opened = The input panel slides open.
dh-warhead-panel-closed-now = The input panel locks shut.
dh-warhead-authorized = Authorisation accepted.
dh-warhead-need-deck = The warhead must stand on a deck to be armed.
dh-warhead-disarming = You start disarming the warhead...
dh-warhead-examine-armed = [color=red]It is armed: {$time} s left.[/color]
dh-warhead-examine-panel-open = The input panel is open.
dh-warhead-examine-panel-closed = The input panel is locked.

dh-warhead-announcement-sender = Republican Army Corps
dh-warhead-announcement-armed = A tactical nuclear warhead has been armed {$location}. Detonation in {$time} seconds. The sentence of the United Republic is being carried out. Glory to the United Republic!
dh-warhead-announcement-disarmed = The tactical nuclear warhead has been disarmed.

dh-warhead-ui-title = RTW-9 tactical nuclear warhead
dh-warhead-ui-status-await-code = ENTER NUCLEAR AUTHENTICATION CODE
dh-warhead-ui-status-ready = CODE ACCEPTED — READY TO ARM
dh-warhead-ui-status-armed = ARMED
dh-warhead-ui-countdown = T-minus {$time}
dh-warhead-ui-code-label = Authentication code
dh-warhead-ui-code-accepted = ACCEPTED
dh-warhead-ui-timer = Timer {$time}
dh-warhead-ui-arm = ARM
dh-warhead-ui-disarm = DISARM (30 s)
