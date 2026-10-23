# DarkestHour: тексты ядерных оперативников переписаны под Имперский Рейд.
nukeops-title = Imperial Raid
nukeops-description = An Imperial Marine Corps raid has targeted the station. Keep them from arming and detonating the nuke by protecting the nuke disk — or surrender.

nukeops-welcome =
    You are a marine of the Imperial Marine Corps. Your goal is {$station}: make it surrender to the First Empire — or take it by storm and blow it up, so that nothing is left but a pile of rubble. The Empire has given you everything you need for the task.
    Operation {$name} is a go! Glory to the First Empire!
nukeops-briefing = When your shuttle arrives, the station is given ten minutes to surrender (raise the station to a high orbit). If it refuses — deliver the payload and get out before it detonates. Glory to the First Empire!

nukeops-opsmajor = [color=crimson]Imperial major victory![/color]
nukeops-opsminor = [color=crimson]Imperial minor victory![/color]
nukeops-neutral = [color=yellow]Neutral outcome![/color]
nukeops-crewminor = [color=green]Crew minor victory![/color]
nukeops-crewmajor = [color=green]Crew major victory![/color]

nukeops-cond-nukeexplodedoncorrectstation = The Imperial marines managed to blow up the station.
nukeops-cond-nukeexplodedonnukieoutpost = The Imperial forward base was destroyed by a nuclear blast!
nukeops-cond-nukeexplodedonincorrectlocation = The nuclear bomb detonated off-station.
nukeops-cond-nukeactiveinstation = The nuclear bomb was left armed on-station.
nukeops-cond-nukeactiveatcentcom = The nuclear bomb was armed and delivered to Metallistick Group!
nukeops-cond-nukediskoncentcom = The crew escaped with the nuclear authentication disk.
nukeops-cond-nukedisknotoncentcom = The crew left the nuclear authentication disk behind.
nukeops-cond-nukiesabandoned = The Imperial marines were abandoned.
nukeops-cond-allnukiesdead = All Imperial marines have died.
nukeops-cond-somenukiesalive = Some Imperial marines died.
nukeops-cond-allnukiesalive = No Imperial marines died.

nukeops-disk-location-title = Final location of Disk:
nukeops-disk-carried-by = {" "}carried by [color=White]{$name}[/color], [color=orange]{$job}[/color], {$location} { $user ->
    [unknown] { "" }
    *[other] ([color=gray]{$user}[/color])
}

storage-hierarchy-list = { $items-left ->
  [0] { $existing-text } { $item },
  *[other] { $existing-text } { $item }, in
}

nukeops-list-start = The Imperial marines were:
nukeops-list-name = - [color=White]{$name}[/color]
nukeops-list-name-user = - [color=White]{$name}[/color] ([color=gray]{$user}[/color])
nukeops-not-enough-ready-players = Not enough players readied up for the game! There were {$readyPlayersCount} players readied up out of {$minimumPlayers} needed. Can't start Nukeops.
nukeops-no-one-ready = No players readied up! Can't start Nukeops.

nukeops-role-commander = Commander
nukeops-role-agent = Corpsman
nukeops-role-operator = Operator
