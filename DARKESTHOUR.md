# The Darkest Hour — правила разработки

Сервер Space Station 14 на базе [space-wizards/space-station-14](https://github.com/space-wizards/space-station-14)
с механиками из RMC-14, Monolith, Goob Station и собственными.

## Где что лежит

| Что | Куда |
| --- | --- |
| Свой код | `Content.Server/_DarkestHour`, `Content.Shared/_DarkestHour`, `Content.Client/_DarkestHour` |
| Свои прототипы | `Resources/Prototypes/_DarkestHour` |
| Свои тексты (локализация) | `Resources/Locale/<язык>/_DarkestHour` |
| Свои спрайты и звуки | `Resources/Textures/_DarkestHour`, `Resources/Audio/_DarkestHour` |
| Свои карты | `Resources/Maps/_DarkestHour` |
| Перенесённое из других форков | под их исходным префиксом: `_RMC14`, `_Mono`, `_NF`, `_ES`, `_FarHorizons`, `_Shitmed`, `_Goobstation` |

## Правила

1. **Ванильные файлы правим минимально.** Каждая правка помечается комментарием `# DarkestHour: <причина>`
   (в C# — `// DarkestHour: <причина>`), чтобы при обновлении с апстрима было видно, что и зачем меняли.
2. **Отключаем, а не удаляем.** Ненужное ванильное (расы, ксеноархеология, блюспейс, плазма) выключается в
   прототипах и убирается из источников; сами прототипы и код визденов остаются, чтобы не ломать обновления.
3. **Одна задача — одна ветка `dh/...` и один PR в `master`.** В `master` попадает только то, что собирается
   (`dotnet build`), проходит тесты (`dotnet test Content.IntegrationTests`) и проверено в игре.
4. **Авторство и лицензии сохраняем.** Перенесённые файлы оставляют заголовки и лицензии источника; у спрайтов —
   `meta.json` с лицензией и автором. Ассеты с лицензией CC-BY-NC-SA помечаем отдельно.
5. **Ничего из этого репозитория не отправляется в space-wizards.** Апстрим не принимает вклад, сделанный с помощью ИИ
   (см. `.noai`).

## Обновление с апстрима

```
git remote add upstream https://github.com/space-wizards/space-station-14.git   # один раз
git fetch upstream
git merge upstream/master
```

Конфликты ищутся по меткам `DarkestHour`.

## Лицензия

- Проект в целом распространяется под **GNU AGPL v3** ([LICENSE-AGPLv3.TXT](LICENSE-AGPLv3.TXT)): исходный код
  сервера, на котором идёт игра, должен быть доступен игрокам.
- Код апстрима (space-wizards) и RMC-14 — под MIT ([LICENSE.TXT](LICENSE.TXT)); эти файлы сохраняют свою лицензию.
- Перенесённый код Monolith и Goob Station сохраняет лицензию и заголовки источника (AGPLv3, MPL-2.0, MIT).
- Ассеты — по лицензиям в их `meta.json` (в основном CC-BY-SA 3.0).
- **Некоммерческие части.** Ядерный реактор, турбина и центрифуга (`_FarHorizons/.../FissionGenerator`) портированы
  из goonstation и помечены в заголовках как **CC-BY-NC-SA-3.0**; часть их спрайтов и звуков — тоже NC.
  Пока они в сборке, сервер нельзя монетизировать (платные привилегии, продажа доступа и т.п.).
