## DarkestHour: огонь и огнемёт

dh-flamer-not-ignited = Light the pilot flame first! (Alt-click)
dh-flamer-empty = The tank is empty!
dh-flamer-bad-fuel = The mixture in the tank won't burn.
dh-flamer-verb-ignite = Light pilot flame
dh-flamer-verb-extinguish = Put out pilot flame
dh-flamer-examine-ignited = The pilot flame is [color=orange]lit[/color].
dh-flamer-examine-not-ignited = The pilot flame is [color=gray]out[/color].
dh-flamer-examine-toggle = Alt-click to light or put out the pilot flame.
dh-flamer-examine-no-tank = There is no fuel tank inside.
dh-flamer-examine-fuel = Fuel: [color=yellow]{ $volume }/{ $max }[/color] u.
dh-flamer-examine-not-flammable = The contents won't burn.
dh-flamer-examine-fuel-type = Burns as [color=orange]{ $name }[/color] ({ $percent }% flammable): heat [color=lightblue]{ $intensity }[/color]/s, burns [color=lightblue]{ $duration }[/color] s, reach [color=lightblue]{ $range }[/color] tiles{ $spread ->
        [true] , [bold]spreads wide[/bold]
       *[false] {""}
    }{ $structural ->
        [true] , [bold]burns through structures[/bold]
       *[false] {""}
    }.
