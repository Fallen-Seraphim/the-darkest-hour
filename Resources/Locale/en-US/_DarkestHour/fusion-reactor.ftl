## Compact fusion reactor

fusion-reactor-fuel-slot = Fuel cassette

fusion-reactor-examine-status = Core temperature: [color=orange]{$temperature} K[/color]. Containment field: [color=cyan]{$integrity}%[/color].
fusion-reactor-examine-flow = Coolant: in [color=lightblue]{$inlet} kPa[/color], out [color=orange]{$outlet} kPa[/color], flow {$flow} mol/s.
fusion-reactor-examine-breached = [color=red]The containment vessel is ruptured. This reactor is wrecked.[/color]

fusion-reactor-radio-overheat = WARNING: Fusion reactor core is overheating ({$temperature} K). The containment field is weakening. Reduce power or increase coolant flow.
fusion-reactor-radio-field-50 = DANGER: Fusion reactor containment field below 50%.
fusion-reactor-radio-field-20 = CRITICAL: Fusion reactor containment field below 20%. Breach imminent. Shut the reactor down now.
fusion-reactor-radio-normal = Fusion reactor containment field stable.
fusion-reactor-radio-breach = Fusion reactor containment BREACHED.

fusion-reactor-breach-announcement = Containment of the station fusion reactor has failed. Expect fire, superheated gas and electromagnetic interference near the reactor. Engineering, respond immediately.
fusion-reactor-announcement-sender = Reactor Control

fusion-reactor-ui-title = Compact Fusion Reactor
fusion-reactor-ui-status-off = Status: switched off
fusion-reactor-ui-status-running = Status: running
fusion-reactor-ui-status-no-power = Status: [NO POWER] containment field unpowered
fusion-reactor-ui-status-no-fuel = Status: [NO FUEL] insert a fuel cassette
fusion-reactor-ui-status-breached = Status: [BREACHED]
fusion-reactor-ui-fuel = Fuel: {$percent}%
fusion-reactor-ui-fuel-none = Fuel: no cassette
fusion-reactor-ui-power = Power: {$percent}%
fusion-reactor-ui-heat = Heat output: {$output} / {$max} kW
fusion-reactor-ui-core = Core: {$temperature} K (safe up to {$safe} K)
fusion-reactor-ui-field = Containment field: {$integrity}%
fusion-reactor-ui-gas = Coolant: in {$inlet} K, out {$outlet} K
fusion-reactor-ui-flow = Coolant pressure: in {$inlet} kPa, out {$outlet} kPa. Flow: {$flow} mol/s
fusion-reactor-ui-no-flow = NO COOLANT FLOW: the core is not being cooled. Check the loop and the turbine outlet.
fusion-reactor-ui-turn-on = Start reactor
fusion-reactor-ui-turn-off = Shut down

fusion-fuel-cassette-examine = Fuel left: [color=cyan]{$percent}%[/color].

## Gas turbine

gas-turbine-examine-status = Output: [color=yellow]{$power} kW[/color]. Inlet gas: {$temperature} K. Blades: {$integrity}%.
gas-turbine-examine-flow = Pressure: in [color=lightblue]{$inlet} kPa[/color], out [color=orange]{$outlet} kPa[/color]. Flow: {$flow} mol/s.
gas-turbine-examine-outlet-full = [color=orange]The outlet is full: gas has nowhere to go. Close the loop back to the reactor or vent the exhaust.[/color]
gas-turbine-examine-no-gas = [color=orange]No gas is flowing through the turbine.[/color]
gas-turbine-examine-broken = [color=red]The blades are wrecked. Weld them back together.[/color]
gas-turbine-broken = The blades of {THE($turbine)} tear apart from the heat!
gas-turbine-repaired = Blades repaired to {$integrity}%.

## Large machine gas ports

dh-machine-gas-ports-examine = Gas inlet on the [color=lightblue]{$inlet}[/color] side, outlet on the [color=orange]{$outlet}[/color] side.
dh-direction-north = north
dh-direction-south = south
dh-direction-east = east
dh-direction-west = west

guide-entry-fusion-reactor = Fusion Reactor
