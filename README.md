# The Darkest Hour

Военный сервер [Space Station 14](https://github.com/space-wizards/space-station-14): наземные операции и десанты,
окопы и укрепления, бронетехника, артиллерия и модульная броня. Основан на оригинальном билде SS14, с собственными
системами и механиками, перенесёнными из [RMC-14](https://github.com/RMC-14/RMC-14),
[Monolith](https://github.com/Monolith-Station/Monolith) и [Goob Station](https://github.com/Goob-Station/Goob-Station).

## Что есть на сервере

- **Наземные операции.** Высадка с кораблей на поверхность планеты, точки захвата, сброс людей и грузов на
  парашютах, тактическая карта и наблюдение за бойцами своей стороны.
- **Баллистика и броня.** У патронов есть класс бронепробития, у брони — класс защиты. Модульная броня
  собирается по частям тела, модули изнашиваются и чинятся. Травмы, кровотечение и полевая медицина.
- **Укрепления.** Окопы и траншеи, мешки с песком, колючая проволока, палатки, пулемётные гнёзда.
- **Тяжёлое вооружение.** Станковые пулемёты, пехотные пушки, миномёты, артиллерия и ПВО.
- **Бронетехника.** БТР и танки с отсеком экипажа, пультами, башней, боекомплектом и ремонтом сваркой.
- **Корабли.** Корабельное оружие, межзвёздные переходы, посадка на планеты.
- **Окружение.** Огонь на клетках, природная вода, эффекты взрывов, дыма и выстрелов из RMC-14.

## Сборка и запуск

Нужны [.NET SDK 10](https://dotnet.microsoft.com/download) и Python 3.

```shell
git clone https://github.com/Fallen-Seraphim/the-darkest-hour.git
cd the-darkest-hour
python RUN_THIS.py          # подтянуть RobustToolbox (подмодуль)
dotnet build -c Release
```

Запуск: `runserver.bat` / `runserver.sh`, затем `runclient.bat` / `runclient.sh`. Подробнее о сборке SS14 —
в [документации](https://docs.spacestation14.com/en/general-development/setup.html).

## Разработка

Правила разработки, структура папок и соглашения — в [DARKESTHOUR.md](DARKESTHOUR.md). Свой код и ассеты лежат в
папках `_DarkestHour`, перенесённое из других форков — под их исходными префиксами (`_RMC14`, `_Mono`,
`_Goobstation`, `_ES` и т.д.).

## Лицензии и авторство

- Проект в целом — **GNU AGPL v3** ([LICENSE-AGPLv3.TXT](LICENSE-AGPLv3.TXT)).
- Код апстрима (Space Wizards) и RMC-14 — **MIT** ([LICENSE.TXT](LICENSE.TXT)); эти файлы сохраняют свою лицензию.
- Перенесённый код Monolith и Goob Station сохраняет лицензию и заголовки источника.
- Ассеты (спрайты, звуки) — по лицензиям в их `meta.json` и `attributions.yml`, в основном CC-BY-SA 3.0.

Спасибо командам [Space Wizards](https://github.com/space-wizards), [RMC-14](https://github.com/RMC-14),
[Monolith](https://github.com/Monolith-Station), [Goob Station](https://github.com/Goob-Station),
CM-SS13 и всем авторам ассетов.

## Оригинальный README апстрима

<div class="header" align="center">  
<img alt="Space Station 14" width="880" height="300" src="https://raw.githubusercontent.com/space-wizards/asset-dump/de329a7898bb716b9d5ba9a0cd07f38e61f1ed05/github-logo.svg">  
</div>

Space Station 14 is a remake of SS13 that runs on [Robust Toolbox](https://github.com/space-wizards/RobustToolbox), our homegrown engine written in C#.

This is the primary repo for Space Station 14. To prevent people forking RobustToolbox, a "content" pack is loaded by the client and server. This content pack contains everything needed to play the game on one specific server.

If you want to host or create content for SS14, this is the repo you need. It contains both RobustToolbox and the content pack for development of new content packs.

## Links

<div class="header" align="center">  

[Website](https://spacestation14.com/) | [Discord](https://discord.ss14.io/) | [Forum](https://forum.spacestation14.com/) | [Mastodon](https://mastodon.gamedev.place/@spacestation14) | [Patreon](https://www.patreon.com/spacestation14) | [Steam](https://store.steampowered.com/app/1255460/Space_Station_14/) | [Standalone Download](https://spacestation14.com/about/nightlies/)  

</div>

## Documentation/Wiki

Our [docs site](https://docs.spacestation14.com/) has documentation on SS14's content, engine, game design, and more.  
Additionally, see these resources for license and attribution information:  
- [Robust Generic Attribution](https://docs.spacestation14.com/en/specifications/robust-generic-attribution.html)  
- [Robust Station Image](https://docs.spacestation14.com/en/specifications/robust-station-image.html)

We also have lots of resources for new contributors to the project.

## Contributing

We are happy to accept contributions from anybody. Get in Discord if you want to help. We've got a [list of issues](https://github.com/space-wizards/space-station-14-content/issues) that need to be done and anybody can pick them up. Don't be afraid to ask for help either!  
Just make sure your changes and pull requests are in accordance with the [contribution guidelines](https://docs.spacestation14.com/en/general-development/codebase-info/pull-request-guidelines.html).

We are not currently accepting translations of the game on our main repository. If you would like to translate the game into another language, consider creating a fork or contributing to a fork.

## AI-generated contributions disclaimer
This project does not accept low-effort or wholesale AI-generated contributions. Examples include, but are not limited to:

- Any code (including yaml) generated by tools like GitHub Copilot, ChatGPT, or similar.
- AI-created artwork, sound files, or other assets.
- Auto-generated documentation, issue reports or pull request descriptions.

Exceptions to this are simple tools like Rider's single-line completion feature.

## Building

1. Clone this repo:
```shell
git clone https://github.com/space-wizards/space-station-14.git
```
2. Go to the project folder and run `RUN_THIS.py` to initialize the submodules and load the engine:
```shell
cd space-station-14
python RUN_THIS.py
```
3. Compile the solution:  

Build the server using `dotnet build`.

[More detailed instructions on building the project.](https://docs.spacestation14.com/en/general-development/setup.html)

## License

All code for the content repository is licensed under the [MIT license](https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT).  

Most assets are licensed under [CC-BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/) unless stated otherwise. Assets have their license and copyright specified in the metadata file. For example, see the [metadata for a crowbar](https://github.com/space-wizards/space-station-14/blob/master/Resources/Textures/Objects/Tools/crowbar.rsi/meta.json).  

> [!NOTE]
> Some assets are licensed under the non-commercial [CC-BY-NC-SA 3.0](https://creativecommons.org/licenses/by-nc-sa/3.0/) or similar non-commercial licenses and will need to be removed if you wish to use this project commercially.
