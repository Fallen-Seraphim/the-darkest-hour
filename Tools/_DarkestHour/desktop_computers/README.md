# Настольные компьютеры

`python3 build.py <RMC-14 repo> <наш repo>` собирает `Resources/Textures/_DarkestHour/Structures/Machines/desktop_computers.rsi`
из настольных компьютеров RMC-14 (`_RMC14/Structures/Machines/computer.rsi`, взяты из CM-SS13, CC-BY-SA-3.0).

- `frame` — корпус (выключенный компьютер), общий у всех «серых» компьютеров CM.
- Экраны названы как в ванильном `Structures/Machines/computers.rsi` (`generic`, `crew`, `cameras`, `rdcomp`…), поэтому
  дочерние прототипы компьютеров не правятся: им достаточно того, что `BaseComputer` смотрит в наш RSI. Соответствие
  «ванильный экран → экран CM» — словарь `SCREENS` в build.py. Новый экран — добавить туда и пересобрать.
- Экран = пиксели, отличающиеся от корпуса; при питании рисуется без затенения (ванильный `ComputerVisualizerSystem`).
- Клавиши/клавиатура — пустые (клавиатура уже нарисована в корпусе), `broken` — разбитый экран, `panel` — открытая панель.

Физика: `BaseStructureComputerIndestructible` — слои настольной машины (`TabletopMachineMask/Layer`), поэтому каркас и
компьютер ставятся на стол. На картах компьютеры ставить на столы.
