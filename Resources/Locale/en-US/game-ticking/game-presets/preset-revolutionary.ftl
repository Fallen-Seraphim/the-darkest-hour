## Rev Head

roles-antag-rev-head-name = Front Commissar
roles-antag-rev-head-objective = Overthrow the old elite: recruit the crew to the United Front of Free Colonies and remove every member of Command.

head-rev-role-greeting =
    You are a commissar of the United Front of Free Colonies, an infiltrator planted among the crew. Once you sat in a prison of the United Independent Colonies on a firing list; you took the deal instead — a weapon, a one-way ticket and freedom on whatever ground you can tear away from the old elites.
    Your task is to remove all of Command from power through death, restraint, or recruitment.
    Your flash recruits the crew to the Front. It won't work on those with eye protection or a Metallistick loyalty implant — Command and Security receive one when they are hired.
    For liberty and justice for all!

head-rev-briefing =
    Use your flash to recruit the crew to the Front.
    Kill, restrain, or recruit all members of Command to seize the station.
    If the Front wins, the Republic will condemn the station — be ready for its sweep team.

head-rev-break-mindshield = The loyalty implant was destroyed!

## Rev

roles-antag-rev-name = Front Partisan
roles-antag-rev-objective = Protect the commissars of the Front and follow their orders to seize the station.

rev-break-control = {$name} has broken with the Front!

rev-role-greeting =
    You are a partisan of the United Front of Free Colonies. Protect the commissars and help them seize the station from the old elites.
    The Front must work together to kill, restrain, or recruit all members of Command.
    For liberty and justice for all!

rev-briefing = Help the commissars of the Front kill, restrain, or recruit all members of Command to seize the station.

## General

rev-title = Revolution
rev-description = Commissars of the United Front of Free Colonies hidden among the crew are recruiting partisans to overthrow Command.

rev-not-enough-ready-players = Not enough players readied up for the game. There were {$readyPlayersCount} players readied up out of {$minimumPlayers} needed. Can't start the Revolution.
rev-no-one-ready = No players readied up! Can't start the Revolution.
rev-no-heads = There were no Front commissars to be selected. Can't start the Revolution.

rev-won = The commissars of the Front survived and seized the station. The Republic condemned it as a separatist nest.

rev-lost = All commissars of the Front have died, and Command survived.

rev-stalemate = Both Command and the commissars of the Front have all died. It's a draw.

rev-reverse-stalemate = Both Command and the commissars of the Front survived.

rev-headrev-count = {$initialCount ->
    [one] There was one Front commissar:
    *[other] There were {$initialCount} Front commissars:
}

rev-headrev-name-user = [color=#c8322b]{$name}[/color] ([color=gray]{$username}[/color]) converted {$count} {$count ->
    [one] person
    *[other] people
}

rev-headrev-name = [color=#c8322b]{$name}[/color] converted {$count} {$count ->
    [one] person
    *[other] people
}

## Deconverted window

rev-deconverted-title = Free of the Front!
rev-deconverted-text =
    The last commissar of the Front is dead, and the uprising is over.

    You are no longer a partisan of the Front, so be nice.
rev-deconverted-confirm = Confirm
