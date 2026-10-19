# Генератор оружия The Darkest Hour

- `guns.py` — описания стволов (детали в пиксельных координатах, сетка 32px, ствол вправо).
- `gunart.py` — растеризация, затенение, обводка.
- `templates.py` — снимает положения «в руке / с двух рук / на спине» с ванильных AK и Viper → `tpl_*.json`.
- `build.py` — собирает RSI: иконка, base, bolt-open, mag-0, состояния обвеса, руки/спина, магазины, иконки обвеса.
  `python3 build.py <out> <repo>` → скопировать `<out>/Objects/Weapons/Guns` в `Resources/Textures/_DarkestHour/Objects/Weapons/`.
- `gen_yaml.py` — калибры, пули, патроны, коробки, магазины, оружие, обвес. `python3 gen_yaml.py <repo>`.
  Урон, бронепробитие, ёмкости и темп стрельбы правятся здесь, затем перегенерировать.
