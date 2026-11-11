# Перенос «Мидуэя» из CMU-14

Скрипты, которыми челнок «Мидуэй» (класс «Мохок», CMU-14 `Content.CMU/Resources/Maps/CMU14/Dropships/Mohawk/midway.yml`,
коммит RuCM 0e24ae881d) перенесён в DH с одной палубой.

- `merge.py` — свод прототипов CMU/RMC с наследованием (нужен `defs.pkl` из прототипов RuCM).
- `convert.py` — классифицирует прототипы карты и пишет `Prototypes/_DarkestHour/Shuttles/Midway/parts.yml`.
- `convmap.py` — переписывает карту в `Maps/_DarkestHour/Shuttles/midway.yml` (новые id, без механик CMU).
- `preview.py` — рисует превью карты в PNG.

Пути в скриптах указывают на временную папку сессии — при повторном запуске поправьте их.
