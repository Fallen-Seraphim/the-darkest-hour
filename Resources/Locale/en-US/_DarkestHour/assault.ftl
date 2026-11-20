## «Штурм» — базовый режим: имперский десант штурмует военную базу Республики на REP-27-7

dh-assault-title = Siege
dh-assault-description = An Imperial landing carrier jumps in from a neighbouring system to storm a Republican garrison base on REP-27-7 and carry off an experimental sample. The garrison has a few minutes to dig in.

dh-assault-carrier-name = IC-3 Sovereign
dh-celestial-rep27-7 = REP-27-7
dh-celestial-rep27-7-desc = A hot desert world of the REP-27 system with thin but breathable air. The Republican Army Corps keeps Terminus Military Base here, its anti-orbital guns dug into a mountain massif.
location-dh-experimental-sample = experimental sample
id-card-access-level-dh-republic-command = Republican Garrison Command

dh-assault-sender-republic = Republican Army Corps
dh-assault-sender-empire = Imperial Fleet Command
dh-assault-outcome-sender = Battle report

## Брифинги

dh-assault-briefing-republic = You are the garrison of the United Republic.
    Objective: do not give up the experimental sample. The Imperial carrier will tear a rift in about {$minutes} min — dig in and man the anti-air positions.
    A Republic Navy squadron will arrive in about {$relief} min — hold out until it gets here.
    More about the war and the sides is in the guidebook, "Lore" section.
dh-assault-briefing-empire = You are the Imperial Marines aboard the landing carrier IC-3 Sovereign.
    Objective: land, take the Republic base and bring the experimental sample aboard. The rift opens in about {$minutes} min.
    A Republic Navy squadron will arrive in about {$relief} min — we must be gone by then. Guard the carrier: it is your only way home.
    More about the war and the sides is in the guidebook, "Lore" section.
dh-assault-reinforced = You return to the battle with the next wave of reinforcements. Reinforcements left for your side: {$tickets}.
dh-assault-next-wave = You have fallen. The next wave of reinforcements arrives in {$minutes} min {$seconds} s ({$tickets} left for your side).
dh-assault-no-reinforcements = You have fallen. Your side has no more reinforcements.
dh-assault-wave-arrived = Reinforcements have arrived: {$count} {$count ->
    [one] soldier
   *[other] soldiers
}. Reinforcements left: {$tickets}.

## Ход боя

dh-assault-prep-over-empire = The rift generator is ready. Jump into the REP-27 system with the rift navigation computer and descend on REP-27-7 with the landing computer.
dh-assault-prep-over-republic = Sensors report a rift signature building up in the neighbouring system. The Imperial carrier may arrive at any moment. All positions, combat readiness!
dh-assault-carrier-arrived = ALERT! An Imperial landing carrier has entered the REP-27 system. Expect a landing assault on Terminus Military Base. All personnel to combat positions!
dh-assault-carrier-arrived-empire = The carrier is in the REP-27 system. Descend on REP-27-7 and take the experimental sample.
dh-assault-garrison-destroyed = The garrison of Terminus Military Base has been wiped out. Secure the experimental sample and bring it aboard, then leave the system.
dh-assault-carrier-falling = HULL BREACH! The carrier is losing altitude and going down! Brace for impact!
dh-assault-carrier-hit = The Imperial carrier has been hit and is going down near the base!
dh-assault-crashed = The carrier has crashed on REP-27-7. Survive and hold out — the Republic will hunt you down.
dh-assault-search-operation = The Imperial carrier has been shot down! Start a search operation: find and eliminate the survivors at the crash site.

## Итоги

dh-assault-outcome-imperial-major = Major Imperial victory: the experimental sample has been carried off and the garrison of Terminus Military Base is destroyed.
dh-assault-outcome-imperial-minor = Minor Imperial victory: the Empire has achieved part of its goals.
dh-assault-outcome-republic-minor = Minor Republican victory: the landing has been repelled, the Empire has retreated without the sample.
dh-assault-outcome-republic-major = Major Republican victory: the Imperial carrier has been shot down and has crashed on REP-27-7.
dh-assault-outcome-none = The battle for Terminus Military Base ended without a result.
dh-assault-end-sample-taken = The experimental sample was carried off by the Empire.
dh-assault-end-sample-kept = The experimental sample remained on REP-27-7.
dh-assault-end-tickets = Reinforcements left: Republic {$republic}, Empire {$empire}.

## Отделы и должности

dh-department-republic-garrison = Republican Garrison
dh-department-republic-garrison-description = The garrison of Terminus Military Base on REP-27-7: hold the base and the experimental sample, shoot down the Imperial carrier.
dh-department-imperial-carrier = Carrier Crew
dh-department-imperial-carrier-description = The crew of the Imperial landing carrier IC-3 Sovereign: fly it, support the landing with fire, keep the ship alive.
dh-department-imperial-marines = Imperial Marines
dh-department-imperial-marines-description = The landing force of the Imperial Marine Corps: storm Terminus Military Base and carry off the experimental sample.

dh-job-garrison-supervisors = the base commander
dh-job-empire-supervisors = the carrier captain and the landing commander

dh-job-garrison-commander-name = Base Commander
dh-job-garrison-commander-desc = Command Terminus Military Base: organise the defence, keep the experimental sample, decide where the anti-air fire goes.
dh-job-garrison-officer-name = Garrison Officer
dh-job-garrison-officer-desc = Lead a section of the garrison under the base commander. Has access to the sample vault.
dh-job-garrison-rifleman-name = Garrison Rifleman
dh-job-garrison-rifleman-desc = The backbone of the defence of Terminus Military Base.
dh-job-garrison-machinegunner-name = Garrison Machinegunner
dh-job-garrison-machinegunner-desc = Hold the approaches to the base with a machine gun.
dh-job-garrison-sniper-name = Garrison Sniper
dh-job-garrison-sniper-desc = Pick off the landing force at long range.
dh-job-garrison-flamer-name = Garrison Flamethrower
dh-job-garrison-flamer-desc = Burn out the attackers in close quarters.
dh-job-garrison-aa-gunner-name = Garrison Anti-Air Gunner
dh-job-garrison-aa-gunner-desc = Man the anti-air batteries of the base and bring down the Imperial carrier.
dh-job-garrison-medic-name = Garrison Medic
dh-job-garrison-medic-desc = Keep the defenders on their feet.
dh-job-garrison-sapper-name = Garrison Sapper
dh-job-garrison-sapper-desc = Fortify the base, dig in and repair what the enemy breaks.

dh-job-carrier-captain-name = Carrier Captain
dh-job-carrier-captain-desc = Command the IC-3 Sovereign: the jump, the descent and the retreat. Without the carrier nobody goes home.
dh-job-carrier-pilot-name = Carrier Pilot
dh-job-carrier-pilot-desc = Fly the carrier: the rift jump into the REP-27 system, the descent and the escape.
dh-job-carrier-artillerist-name = Carrier Artillerist
dh-job-carrier-artillerist-desc = Support the landing with the guns of the carrier.
dh-job-carrier-technician-name = Carrier Technician
dh-job-carrier-technician-desc = Keep the carrier flying: repair hull damage, the rift gear and the engines.
dh-job-marine-commander-name = Landing Commander
dh-job-marine-commander-desc = Lead the Imperial Marines in the assault on Terminus Military Base.
dh-job-marine-weapons-specialist-name = Marine Weapons Specialist
dh-job-marine-weapons-specialist-desc = Heavy weapons of the landing force; choose your kit before the jump.
dh-job-marine-field-medic-name = Marine Field Medic
dh-job-marine-field-medic-desc = Keep the marines fighting; choose your kit before the jump.
dh-job-marine-machinegunner-name = Marine Machinegunner
dh-job-marine-machinegunner-desc = Suppress the defenders; choose your kit before the jump.
dh-job-marine-rifleman-name = Marine Rifleman
dh-job-marine-rifleman-desc = The backbone of the Imperial landing force; choose your kit before the jump.
dh-job-marine-squad-leader-name = Squad Leader
dh-job-marine-squad-leader-desc = Leads one of the three landing squads: Tau, Epsilon or Omega. Marks targets, calls fire and keeps the squad together.
dh-job-garrison-artillery-officer-name = Artillery Officer
dh-job-garrison-artillery-officer-desc = Run the garrison's fire support: mortars, infantry guns and the anti-orbital battery. Spot targets and call the fire.
dh-job-garrison-mortar-commander-name = Mortar Section Leader
dh-job-garrison-mortar-commander-desc = Lead the mortar section: pick firing positions, mark targets with the laser designator and keep the crews supplied.
dh-job-garrison-mortar-operator-name = Mortar Operator
dh-job-garrison-mortar-operator-desc = Set up the mortar, lay it on the marks and keep the shells coming.
dh-job-garrison-field-artillerist-name = Gun Crewman
dh-job-garrison-field-artillerist-desc = Crew the 90 mm infantry gun: wrench it into position, lay it and load it.
dh-job-garrison-tank-commander-name = Tank Commander
dh-job-garrison-tank-commander-desc = Command the garrison tank: pick targets, work the coaxial gun and the smoke, keep the crew alive.
dh-job-garrison-tank-crew-name = Tank Crewman
dh-job-garrison-tank-crew-desc = Drive the tank or lay its main gun; patch the hull with a welder between fights.
dh-job-garrison-apc-commander-name = APC Commander
dh-job-garrison-apc-commander-desc = Command the garrison APC: carry the squads where they are needed and cover them with the autocannon.
dh-job-garrison-apc-crew-name = APC Crewman
dh-job-garrison-apc-crew-desc = Drive the APC or work its guns; patch the hull with a welder between fights.
dh-job-carrier-operation-commander-name = Operation Commander
dh-job-carrier-operation-commander-desc = You run the whole operation from the carrier: set the plan, direct the landing force, the crews and the fire support.
dh-job-carrier-support-officer-name = Fire Support Officer
dh-job-carrier-support-officer-desc = Coordinate the supporting arms: orbital scans, ship artillery, mortars, guns and vehicle drops.
dh-job-marine-mortar-commander-name = Mortar Section Leader
dh-job-marine-mortar-commander-desc = Lead the mortar section: pick firing positions, mark targets with the laser designator and keep the crews supplied.
dh-job-marine-mortar-operator-name = Mortar Operator
dh-job-marine-mortar-operator-desc = Set up the mortar, lay it on the marks and keep the shells coming.
dh-job-marine-field-artillerist-name = Gun Crewman
dh-job-marine-field-artillerist-desc = Crew the 75 mm infantry gun: get it down to the surface, wrench it into position, lay it and load it.
dh-job-marine-tank-commander-name = Tank Commander
dh-job-marine-tank-commander-desc = Command the Marine tank: get it dropped, pick targets, work the coaxial gun and the smoke, keep the crew alive.
dh-job-marine-tank-crew-name = Tank Crewman
dh-job-marine-tank-crew-desc = Drive the tank or lay its main gun; patch the hull with a welder between fights.
dh-job-marine-apc-commander-name = APC Commander
dh-job-marine-apc-commander-desc = Command the Marine APC: get it dropped, carry the squads where they are needed and cover them with the autocannon.
dh-job-marine-apc-crew-name = APC Crewman
dh-job-marine-apc-crew-desc = Drive the APC or work its guns; patch the hull with a welder between fights.

## Команды

cmd-dhcarrierdamage-desc = Damages the Imperial carrier of the Siege round (anti-air hit). At zero hull it goes down on the planet.
cmd-dhcarrierdamage-help = Usage: dhcarrierdamage <amount> (the carrier has 100 hull)
cmd-dhcarrierdamage-done = The carrier took {$amount} damage.
cmd-dhcarrierdamage-no-carrier = No active Siege round with a carrier that can still be hit.

## Укрепления: мешки с песком и палатки

dh-stack-sandbags-empty = empty sandbags
dh-stack-sandbags-full = sandbags
dh-sandbags-not-enough = You need at least {$count} sandbags for a barricade.
dh-sandbags-blocked = There is no room for a barricade in front of you.
dh-sandbags-built = You stack the sandbags into a barricade.
dh-sandbags-need-ground = Stand on bare ground to fill the bags.
dh-sandbags-filled = You fill {$count} {$count ->
    [one] sandbag
   *[other] sandbags
}.
dh-tent-no-room = There is no room for the tent in front of you.
dh-tent-deployed = You pitch the tent.
dh-tent-fold-verb = Fold the tent

## Bunker and levels
dh-bunker-name = Terminus Bunker
dh-ladder-blocked = The passage is blocked.
dh-ladder-start-down = {CAPITALIZE(THE($user))} starts climbing down.
dh-ladder-start-up = {CAPITALIZE(THE($user))} starts climbing up.
dh-ladder-arrived-down = {CAPITALIZE(THE($user))} climbs down the ladder.
dh-ladder-arrived-up = {CAPITALIZE(THE($user))} climbs up out of the hatch.
dh-ladder-somewhere = somewhere
dh-ladder-examine-down = It leads down to [color=yellow]{$where}[/color].
dh-ladder-examine-up = It leads up to [color=yellow]{$where}[/color].
dh-lift-verb-call = Call the lift
dh-lift-broken = The lift does not respond.
dh-lift-busy = The lift is already moving.
dh-lift-departing = The lift departs in {$seconds} s! Clear the platform or ride along.
dh-lift-arriving = The lift is coming in {$seconds} s! Clear the platform.

## Dropships and landing zones
dh-dropship-name-alpha = Dropship Alpha
dh-dropship-name-bravo = Dropship Bravo
dh-lz-name = LZ-{$number}
dh-lz-temporary-name = Field LZ-{$number}
dh-lz-beacon-not-surface = Set it up on a planet's surface.
dh-lz-beacon-deployed = {$zone} is set up: dropships can land here.
dh-landing-no-surface-landing = The carrier can't land: the dropships go down to the surface.
dh-dropship-busy = The dropship is already on its way.
dh-dropship-zone-unavailable = The landing zone is not available.
dh-dropship-no-hangar = There is no hangar to return to.
dh-dropship-hangar = Hangar of {$carrier}
dh-dropship-hangar-lost = The carrier is lost.
dh-dropship-in-air = in the air
dh-dropship-course = New course: {$destination}.
dh-dropship-departing = Departure for {$destination} in {$seconds} s! Take your seats.
dh-dropship-aborted = Departure aborted.
dh-dropship-launched = Launched. Course: {$destination}, arrival in {$seconds} s.
dh-dropship-docked = Docked to {$carrier}.
dh-dropship-hovering = The landing zone is blocked or lost: hovering, pick another destination.
dh-dropship-landed = Touchdown: {$zone}. Ramp down!
dh-dropship-note-lost = lost
dh-dropship-note-other-system = in another system
dh-dropship-note-here = we are here
dh-dropship-note-blocked = blocked
dh-dropship-ui-title = Dropship
dh-dropship-ui-abort = Abort departure
dh-dropship-ui-destinations = Destinations
dh-dropship-ui-no-destinations = No landing zones in this system.
dh-dropship-ui-state-docked = Docked in the hangar.
dh-dropship-ui-state-departing = Preparing for departure.
dh-dropship-ui-state-flight = In flight.
dh-dropship-ui-state-landed = On the ground.
dh-dropship-ui-location = Location: {$location}
dh-dropship-ui-flight = Course: {$destination}, {$seconds} s.

## Comms
dh-chat-radio-republic-command = Garrison Command
dh-chat-radio-imperial-command = Imperial Command
dh-relay-broken = The relay falls silent!
dh-relay-repaired = The relay hums back to life.
dh-relay-examine-active = It is [color=green]relaying[/color] the radio nets.
dh-relay-examine-broken = It is [color=red]silent[/color]: weld it back into working order.
dh-relay-portable-deployed = {CAPITALIZE($user)} sets up the portable relay.
dh-relay-portable-fold-verb = Fold the relay
dh-relay-portable-no-ground = There is nothing to set the relay on here.
dh-phone-busy-self = The line is already in use.
dh-phone-busy = {$phone} is busy.
dh-phone-ui-title = Field telephone
dh-phone-ui-answer = Answer
dh-phone-ui-hang-up = Hang up
dh-phone-ui-book = Directory
dh-phone-ui-empty = No other telephones on the line.
dh-phone-ui-entry-busy = {$name} (busy)
dh-phone-ui-idle = The line is free.
dh-phone-ui-calling = Calling {$other}...
dh-phone-ui-ringing = Incoming call: {$other}!
dh-phone-ui-in-call = On the line with {$other}. Speak near the telephone.

## Recon and camouflage
dh-motion-detector-on = The motion detector clicks on.
dh-motion-detector-off = The motion detector goes quiet.
dh-camo-net-deployed = {CAPITALIZE(THE($user))} spreads a camouflage net.
dh-camo-net-fold-verb = Fold the net
dh-scan-sender = Orbital scan
dh-scan-status-no-surface = No surface below to scan.
dh-scan-status-cooldown = Recharging: {$minutes} min {$seconds} s.
dh-scan-status-ready = Ready to scan.
dh-scan-ui-title = Global orbital scan
dh-scan-ui-surface = Surface below: {$surface}. The scan covers the whole surface.
dh-scan-ui-no-surface = No surface below.
dh-scan-ui-scan = SCAN
dh-scan-ui-report = Last report
dh-scan-ui-no-report = No scans yet.
dh-scan-report-header = SCAN REPORT — {$surface}
dh-scan-report-empty = No enemy positions found (camouflage is possible).
dh-scan-line = {$name}: ({$x}, {$y})
dh-scan-kind-anti-orbital = Anti-orbital battery bunker
dh-scan-kind-base = Republic base
dh-scan-kind-anti-air = Anti-aircraft gun
dh-scan-kind-infantry = Large infantry concentration (~{$count})

## Hearing
dh-tinnitus-start = Your ears ring!

## Barbed wire
dh-stack-barbed-wire = barbed wire
dh-sandbag-wire-cut = The barbed wire tears at you!
dh-wireable-already-wired = There is already barbed wire on it.
dh-wireable-wired = {CAPITALIZE(THE($user))} strings barbed wire along the fence.
dh-construction-barbed-wire-obstacle = barbed wire entanglement
dh-construction-barbed-wire-obstacle-desc = Two coils of barbed wire staked to the ground: slow and painful to wade through.
dh-construction-barbed-wire = barbed wire
dh-construction-barbed-wire-desc = A coil of barbed wire twisted from a steel sheet.
dh-construction-sandbag = empty sandbag
dh-construction-sandbag-desc = A sandbag sewn from cloth. Fill it with a shovel on bare ground.

dh-trench-need-ground = You can only dig a trench in bare ground.
dh-trench-blocked = Something is in the way.
dh-weapon-mount-examine-nest = [color=#9fd06f]Machine-gun nest[/color]: dug in or behind sandbags, the gun spreads less.

dh-republic-kit-window-title = Republic requisition
dh-republic-kit-window-description = Choose your loadout. The case turns into the chosen gear: weapon, ammunition and an RMB-1 modular armour suit with its special system.
dh-republic-set-rifleman-rifle-name = Rifle: RAR-M-11-1
dh-republic-set-rifleman-rifle-description = The standard RAR-M-11-1 rifle with four FMG magazines, one armour-piercing BB and a grenade. RMB-1 with a composite torso; special system: auto-medic.
dh-republic-set-rifleman-marksman-name = Marksman: RAR-M-11-2
dh-republic-set-rifleman-marksman-description = The RAR-M-11-2 marksman rifle with four 7.62 mm FMG magazines. RMB-1 with a composite torso; special system: extra storage.
dh-republic-set-rifleman-smg-name = SMG: RAP-TOR-M-12-16
dh-republic-set-rifleman-smg-description = The RAP-TOR-M-12-16 SMG with three expanding and two armour-piercing SVP magazines. RMB-1 with a composite torso; special system: auto-medic.
dh-republic-set-machinegunner-light-name = Light machine gun: RAMG-M-14-1
dh-republic-set-machinegunner-light-description = The RAMG-M-14-1 squad machine gun with three 100-round boxes. Light RMB-1 plates; special system: auto-medic.
dh-republic-set-machinegunner-heavy-name = Mounted machine gun: RSPMG-10-54
dh-republic-set-machinegunner-heavy-description = The RSPMG-10-54 tripod machine gun with three belts. Heavy RMB-1 plates and heavy armoured magboots (change into them); special system: auto-medic.
dh-republic-set-sniper-antimateriel-name = Anti-materiel rifle: RMAR-AS-M-15-18
dh-republic-set-sniper-antimateriel-description = The 12.7 mm RMAR-AS-M-15-18 with three SVP and one A magazine, binoculars. Light RMB-1 plates; special system: auto-medic.
dh-republic-set-sniper-marksman-name = Marksman rifle: RMAD-AS-M-15-17
dh-republic-set-sniper-marksman-description = The 10x51 mm RMAD-AS-M-15-17 with three FMG and two SVP magazines, binoculars. Light RMB-1 plates; special system: auto-medic.
dh-republic-set-medic-smg-name = SMG: RAP-TOR-M-12-16
dh-republic-set-medic-smg-description = The RAP-TOR-M-12-16 SMG with two expanding and two SVP magazines. Light RMB-1 plates; special system: auto-medic.
dh-republic-set-medic-rifle-name = Rifle: RAR-M-11-1
dh-republic-set-medic-rifle-description = The RAR-M-11-1 rifle with four FMG magazines. Light RMB-1 plates; special system: auto-medic.
dh-republic-set-officer-rifle-name = Rifle: RAR-M-11-2
dh-republic-set-officer-rifle-description = The RAR-M-11-2 rifle with three FMG and one expanding magazine. Composite RMB-1 plates on a medium exoskeleton; special system: comms module with orders.
dh-republic-set-officer-smg-name = SMG: RAP-TOR-M-12-16
dh-republic-set-officer-smg-description = The RAP-TOR-M-12-16 SMG with two expanding and two SVP magazines. Composite RMB-1 plates on a medium exoskeleton; special system: comms module with orders.
dh-sandbag-intact = The sandbags are intact — nothing to patch.
dh-sandbag-repaired = You patch the barricade with fresh sandbags.
dh-dropship-ui-flares = FIRE DECOY FLARES ({$count}/{$max})
dh-dropship-ui-flares-active = Decoy flares are covering the craft ({$count}/{$max})
dh-dropship-flares-fired = Decoy flares away! Enemy air defence will lose the craft for the rest of this run. Salvos left: {$left}.

# Barricades (DHBarricade)
dh-construction-barricade-metal = metal barricade
dh-construction-barricade-metal-desc = A barricade of four steel sheets. Stops most bullets from the front.
dh-construction-barricade-plasteel = plasteel barricade
dh-construction-barricade-plasteel-desc = A barricade of four plasteel sheets. Stops nearly every bullet from the front.

# Ghost warp points
dh-warp-terminus = Terminus base (HQ)
dh-warp-carrier = Carrier Sovereign (bridge)

# Командование сторон, командный доступ Империи, сети «СКАТ» и снабжения (2026-11-13)
dh-department-republic-command = Republic Command
dh-department-republic-command-description = The command of Terminus Military Base: the base commander and his officers. They start in the commandant's office.
dh-department-imperial-command = Imperial Command
dh-department-imperial-command-description = The command of the Imperial landing operation: the operation commander, the carrier captain, the landing force commander and the support officer. They start in their cabins.
id-card-access-level-dh-imperial-command = Imperial Operation Command
dh-chat-radio-imperial-skat = SKAT
dh-chat-radio-imperial-supply = Imperial Supply

## Job tags on the radio
dh-job-abbrev-garrison-commander = BC
dh-job-abbrev-garrison-officer = OFF
dh-job-abbrev-garrison-artillery-officer = AO
dh-job-abbrev-garrison-rifleman = RFL
dh-job-abbrev-garrison-machinegunner = MG
dh-job-abbrev-garrison-sniper = SNP
dh-job-abbrev-garrison-flamer = FLM
dh-job-abbrev-garrison-aa-gunner = AA
dh-job-abbrev-garrison-medic = MED
dh-job-abbrev-garrison-sapper = SAP
dh-job-abbrev-garrison-mortar-commander = MSL
dh-job-abbrev-garrison-mortar-operator = MOR
dh-job-abbrev-garrison-field-artillerist = ART
dh-job-abbrev-garrison-tank-commander = TC
dh-job-abbrev-garrison-tank-crew = TK
dh-job-abbrev-garrison-apc-commander = APCC
dh-job-abbrev-garrison-apc-crew = APC
dh-job-abbrev-carrier-operation-commander = OC
dh-job-abbrev-carrier-captain = CPT
dh-job-abbrev-carrier-support-officer = FSO
dh-job-abbrev-marine-commander = LFC
dh-job-abbrev-marine-squad-leader = SL
dh-job-abbrev-marine-weapons-specialist = WS
dh-job-abbrev-marine-field-medic = MED
dh-job-abbrev-marine-machinegunner = MG
dh-job-abbrev-marine-rifleman = RFL
dh-job-abbrev-carrier-pilot = PLT
dh-job-abbrev-carrier-attack-pilot = AP
dh-job-abbrev-carrier-artillerist = ART
dh-job-abbrev-carrier-technician = TEC
dh-job-abbrev-marine-mortar-commander = MSL
dh-job-abbrev-marine-mortar-operator = MOR
dh-job-abbrev-marine-field-artillerist = ART
dh-job-abbrev-marine-tank-commander = TC
dh-job-abbrev-marine-tank-crew = TK
dh-job-abbrev-marine-apc-commander = APCC
dh-job-abbrev-marine-apc-crew = APC

# Dropship autopilot flight over the battlefield
dh-dropship-descending = Over {$zone}. Descending!
dh-dropship-hovering-lost = The landing zone is lost: hovering, pick another destination.
dh-dropship-manual-unavailable = You can take the controls only while flying over the battlefield.
dh-dropship-manual-on = The pilot has taken the controls.
dh-dropship-ui-manual = TAKE THE CONTROLS (WASD)
dh-dropship-ui-autopilot = ENGAGE AUTOPILOT
dh-dropship-ui-phase-transit = Orbit-to-atmosphere transit.
dh-dropship-ui-phase-cruise = Flying over the battlefield.
dh-dropship-ui-phase-descend = Descending.
dh-dropship-ui-phase-climb = Climbing.
dh-dropship-ui-phase-hover = Hovering.
dh-dropship-ui-phase-manual = Manual control.
dh-dropship-atmosphere-unavailable = You can climb into the atmosphere only while flying over the battlefield.
dh-dropship-atmosphere-entered = The dropship is in the atmosphere layer: drop run, manual control.
dh-dropship-atmosphere-left = The dropship is back at cruise height over the battlefield.
dh-dropship-ui-atmosphere-enter = CLIMB INTO THE ATMOSPHERE (DROP RUN)
dh-dropship-ui-atmosphere-leave = RETURN OVER THE BATTLEFIELD
dh-dropship-ui-release = RELEASE THE CONTROLS
dh-dropship-ui-phase-atmosphere = Atmosphere layer, drop run. Below us: {$x}, {$y}.

# Reinforcement trucks
dh-reinforcement-entry-east = east entry
dh-reinforcement-entry-west = west entry
dh-reinforcement-entry-unknown = road entry
dh-reinforcement-truck-sender = Convoy
dh-reinforcement-truck-announce = A reinforcement truck has arrived ({ $entry }). Expect it at the base.
dh-reinforcement-truck-boarded = You are in the back of a reinforcement truck ({ $entry }). Take the wheel (in the cab) or wait for a driver, then drive to the base along the highway.

# Terminus map greeting (siege plan item 23)
dh-assault-briefing-terminus-republic = You are the garrison of Terminus Military Base on REP-27-7.
    Objective: do not give up the experimental sample in the bunker vault. The Imperial carrier will tear a rift in about {$minutes} min — dig in and man the anti-air positions.
    A Republic Navy squadron will arrive in about {$relief} min — hold out until it gets here.
    Reinforcements come by truck along the highway. More about the war and the sides is in the guidebook, "Lore" section.
dh-assault-briefing-terminus-empire = You are the Imperial Marines aboard the landing carrier IC-3 Sovereign.
    Objective: land on REP-27-7, take Terminus Military Base and bring the experimental sample from its bunker aboard.
    The rift opens in about {$minutes} min. A Republic Navy squadron will arrive in about {$relief} min — we must be gone by then.
    Guard the carrier: it is your only way home. More about the war and the sides is in the guidebook, "Lore" section.

# Round summary
dh-assault-stats-republic = Republic
dh-assault-stats-empire = Empire
dh-assault-stats-none = — none —
dh-assault-stats-nobody = —
dh-assault-stats-captures-header = [bold]Capture timeline[/bold]
dh-assault-stats-capture = { $time } — [color={ $color }]{ $side }[/color]: "{ $point }"
dh-assault-stats-capture-from = { $time } — [color={ $color }]{ $side }[/color]: "{ $point }" (taken from { $from })
dh-assault-stats-losses = [bold]Losses:[/bold] Republic — { $republic }, Empire — { $empire }.
dh-assault-stats-downed = [bold]Shot down:[/bold] Imperial attack aircraft — { $aircraft }; carrier — { $carrier }.
dh-assault-stats-carrier-down = shot down
dh-assault-stats-carrier-intact = intact
dh-assault-stats-best-header = [bold]Best soldiers[/bold]
dh-assault-stats-best-side = [color={ $color }]{ $side }[/color]: most kills — { $killer }; most saves — { $medic }; most captures — { $captor }.
dh-assault-stats-best-entry = { $name } ({ $count })
dh-assault-stats-medals-header = [bold]Awards[/bold]
dh-assault-stats-medal = [color={ $color }]"{ $medal }"[/color] — { $recipient }. Awarded by { $giver }: "{ $citation }"

# Award sheet
dh-medal-ui-title = Award sheet
dh-medal-ui-remaining = Awards left: { $count }
dh-medal-ui-recipient = Recipient
dh-medal-ui-candidate = { $name } — { $job }
dh-medal-ui-candidate-dead = { $name } — { $job } (posthumous)
dh-medal-ui-medal = Decoration
dh-medal-ui-citation = Citation
dh-medal-ui-award = Award
dh-medal-none-left = The award sheet is used up.
dh-medal-wrong-side = This award sheet belongs to the other side.
dh-medal-no-citation = Write the citation.
dh-medal-sender = Command
dh-medal-announce = { $giver } awards { $recipient } the "{ $medal }": "{ $citation }"

dh-medal-imperial-cross = Imperial Cross
dh-medal-imperial-cross-desc = The First Empire's highest combat decoration — for a deed that decided the battle.
dh-medal-imperial-assault = For the Assault
dh-medal-imperial-assault-desc = For being first into enemy positions and taking fortifications.
dh-medal-imperial-loyalty = For Loyalty to the Emperor
dh-medal-imperial-loyalty-desc = For steadfastness and doing one's duty to the end.
dh-medal-imperial-wound = Wound Badge
dh-medal-imperial-wound-desc = For those who shed blood for the Empire.
dh-medal-republic-star = Star of the Republic
dh-medal-republic-star-desc = The United Republic's highest combat decoration — for exceptional bravery.
dh-medal-republic-defense = For Defense
dh-medal-republic-defense-desc = For holding positions under enemy fire.
dh-medal-republic-valor = For Valor
dh-medal-republic-valor-desc = For personal bravery in battle, saving comrades and bold action.
dh-medal-republic-wound = Wound Badge
dh-medal-republic-wound-desc = For those who shed blood for the Republic.

## Sniper ghillie cloak
dh-ghillie-preparing = You freeze and settle into your ghillie cloak…
dh-ghillie-hidden = You are hidden. Do not move.
dh-ghillie-already-hidden = You are already hidden.
dh-ghillie-revealed-self = You come out of hiding.
dh-ghillie-revealed-move = You moved — your camouflage is broken!
dh-ghillie-revealed-shot = Your shot gave away your position!
dh-ghillie-revealed-hit = You were hit — your camouflage is broken!
