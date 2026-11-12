# Uprising tasks

dh-task-ui-title-police = Military Police tasks
dh-task-ui-title-front = LFFC tasks
dh-task-ui-title-vanguard = LA-V tasks
dh-task-ui-balance = Organisation account: { $balance } cr.
dh-task-ui-active = ACTIVE TASK
dh-task-ui-offers = OFFERS (pick one)
dh-task-ui-reward = Reward: { $reward } cr.
dh-task-ui-reward-time = Reward: { $reward } cr. Time left: { $time }
dh-task-ui-accept = Accept
dh-task-ui-abandon = Abandon
dh-task-ui-cooldown = New tasks in { $time }.
dh-task-ui-log = LOG
dh-task-cant-start = The task cannot start now (no spot or the database is down).

dh-task-log-accepted = Accepted: { $name }.
dh-task-log-done = Done: { $name }.
dh-task-log-abandoned = Abandoned: { $name }.
dh-task-log-expired = Time is up: { $name }.
dh-task-log-exploded = The bomb went off: { $name }.
dh-task-log-reward = +{ $amount } cr. — { $reason }
dh-task-log-arrest = arrest: { $name }
dh-task-log-kill = eliminated: { $name }
dh-task-log-forged = forged passport: { $name }

dh-task-hint-level = level { $level }
dh-task-hint = { $level }, about { $meters } m { $direction } of the "{ $beacon }" landmark
dh-task-dir-here = next to
dh-task-dir-n = north
dh-task-dir-ne = north-east
dh-task-dir-e = east
dh-task-dir-se = south-east
dh-task-dir-s = south
dh-task-dir-sw = south-west
dh-task-dir-w = west
dh-task-dir-nw = north-west

dh-task-police-stash-name = Find a stash
dh-task-police-stash-desc = An informant reported a stash. Find and seize it.
dh-task-stash-text = Informant: the stash is at { $hint }. Find the package and seize it (click it by hand).
dh-task-stash-found = Stash seized.
dh-task-stash-not-police = Some package. Better not touch it.

dh-task-police-defuse-name = Defuse a device
dh-task-police-defuse-desc = A report of an improvised explosive device. The sappers have the deactivation code.
dh-task-defuse-text = Explosive device at { $hint }. Timer: about { $minutes } min. Deactivation code: [bold]{ $code }[/bold]. A wrong code shortens the timer.

dh-task-police-database-name = Check the database
dh-task-police-database-desc = Fake records slipped into the passport database. Find and delete them.
dh-task-database-text = The database has { $count } fake records with obvious inconsistencies. Find them on a database terminal and delete them. Deleting a genuine record is fined.
dh-task-database-left = Left to find: { $count }.

dh-task-bomb-ui-title = Explosive device
dh-task-bomb-ui-timer = DETONATION IN: { $time }
dh-task-bomb-ui-defused = DEFUSED
dh-task-bomb-ui-code = Deactivation code
dh-task-bomb-ui-enter = Enter
dh-task-bomb-wrong = Wrong code! Timer cut by { $seconds } s.
dh-task-bomb-defused = Device defused.

dh-police-db-ui-title = Military Police database — district passports
dh-police-db-ui-filter = Search by name or number
dh-police-db-ui-columns = Name | Number | Sex, species | Born, place | Issued
dh-police-db-ui-delete = Delete
dh-police-db-ui-confirm = Sure?
dh-police-db-ui-rules = [bold]Checks[/bold]
    • A passport is issued no earlier than age 14.
    • The number's series (LA [bold]NN[/bold] ...) is the last two digits of the issue year.
    • Passport numbers never repeat.
    • The issue date is not later than today (05.04.2318).
    • The birthplace was already inhabited in the birth year (see below).
    Delete fakes. Deleting a genuine record is fined 150 cr.
dh-police-db-ui-worlds = Reference: worlds and settlement years
dh-police-db-world = { $name } — since { $from }
dh-police-db-world-until = { $name } — { $from }–{ $until }
dh-police-db-offline = No connection to the database server.
dh-police-db-removed-fake = Fake record "{ $name }" deleted.
dh-police-db-removed-forged = LFFC forged passport "{ $name }" exposed and cancelled. Reward: { $reward } cr.
dh-police-db-removed-genuine = A genuine record "{ $name }" was deleted! Fine: { $fine } cr.
dh-police-db-fine-note = fine: genuine record deleted ({ $name })
