# Генератор оружия The Darkest Hour

- `pixart.py` — попиксельная отрисовка: палитры (градиенты металла/фурнитуры/акцентов по фракциям),
  примитивы (панели с объёмом, многоугольники, прорези, планки), рендер иконки и спрайтов в руках/на спине
  (суперсэмплинг повёрнутого спрайта).
- `guns.py` — сами стволы: `base` (затвор закрыт, без магазина), `mag` (магазин в оружии), `bolt_open`
  (что меняется при открытом затворе), точки крепления: дуло, планка, подствольная, приклад.
  Республика — в духе морпехов CM-SS13/RMC-14, Империя — в духе PDV из Monolith (чёрный/белый/жёлтый).
- `templates.py` — снимает положения «в руке / с двух рук / на спине» с ванильных AK и Viper → `tpl_*.json`.
- `build.py` — собирает RSI: иконка, base, bolt-open, mag-0, состояния обвеса, руки/спина, иконки обвеса.
  `python3 build.py <out> <repo>` → скопировать `<out>/Objects/Weapons/Guns` в `Resources/Textures/_DarkestHour/Objects/Weapons/`.
- `gen_yaml.py` — калибры, пули, патроны, коробки, магазины, оружие, обвес. `python3 gen_yaml.py <repo>`.
  Урон задаётся по шкале Monolith и умножается на `DAMAGE_MULT` (сейчас 1.5). Спрайты патронов и магазинов —
  из Monolith (CC-BY-SA-3.0), лежат в `Resources/Textures/_Mono/Objects/Weapons/Guns/Ammunition`.
- `emplacements.py` — станковые пулемёты (RSPMG-10-54, IHMG-17): сложенный вид, станок на треноге в 4 направлениях,
  лента и раскалённый ствол отдельными слоями, в руках и на спине. `python3 emplacements.py <out> <repo>` →
  скопировать `<out>/*.rsi` в `Resources/Textures/_DarkestHour/Objects/Weapons/Guns/Emplacements/`.
- Гильза 14.5x114 (`Resources/Textures/_DarkestHour/Objects/Weapons/Guns/Ammunition/Casings/14.5x114mm_casing.rsi`) —
  стальная, сделана из `big_ammo_casing` Monolith (CC-BY-SA-3.0); путь задаётся в `CASING_PATH` в `gen_yaml.py`.
