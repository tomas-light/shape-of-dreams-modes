# Сборка и установка модов / Build and install mods

Установите .NET 10 SDK и Steam-версию Shape of Dreams. Из корня репозитория запустите команду для нужного мода.

Install the .NET 10 SDK and the Steam edition of Shape of Dreams. Run the command for the mod you want to build from the repository root.

## Chaos Slots

```bat
.\mods\chaos-slots\scripts\Pack-Mod.cmd
```

Результат / Output in `artifacts/` — `ChaosSlots`, `ChaosSlotsTestRoom`.

## Constellation Viewer

```bat
.\mods\constellation-viewer\scripts\Pack-Mod.cmd
```

Результат / Output in `artifacts/` — `ConstellationViewer`.

## Путь к игре и установка / Game path and installation

Скрипты ищут игру в стандартной библиотеке Steam. Для другой библиотеки добавьте к команде `-GameDir "D:\SteamLibrary\steamapps\common\Shape of Dreams"` или задайте переменную окружения `SOD_GAME_DIR`. В `artifacts/` появятся папки модов и ZIP-архивы с версиями в названиях. Скопируйте нужную папку мода в каталог `Mods` внутри папки игры и включите мод в главном меню.

The scripts look for the game in the default Steam library. To use another library, append `-GameDir "D:\SteamLibrary\steamapps\common\Shape of Dreams"` to the command or set `SOD_GAME_DIR`. The scripts put the mod folders and versioned ZIP archives in `artifacts/`. Copy the mod folder into the game's `Mods` directory and enable it in the main menu.

## Проверка и публикация Chaos Slots

### Локальная проверка

[Соберите Chaos Slots](#chaos-slots). Скопируйте папки `artifacts/ChaosSlots` и `artifacts/ChaosSlotsTestRoom` в каталог `Mods` внутри папки игры. Включите оба мода в главном меню и начните новый забег. В первом зале появятся алтари Искажённого Хаоса и эссенции.

### Публикация в Steam Workshop

1. Укажите версию релиза в [манифесте мода](chaos-slots/mod/about/metadata.json). Workshop использует [описание](chaos-slots/mod/about/description.txt) и [обложку](chaos-slots/mod/about/preview.png) из папки `about`.
2. [Соберите Chaos Slots](#chaos-slots) и установите папку `ChaosSlots` в каталог `Mods` внутри папки игры.
3. Запустите игру через Steam. В настройках геймплея включите **Режим разработчика**. В главном меню откройте **Моды**, выберите локальный **Chaos Slots** и нажмите **Upload**. [Руководство разработчиков игры](https://github.com/LizardSmoothie/ShapeOfDreamsModDocs#uploading-a-mod-to-workshop)
4. Первая загрузка создаст скрытую страницу мода. Проверьте описание и обложку. Если Steam попросит принять соглашение Workshop, примите его. Когда страница будет готова, выставьте видимость **Для всех** (**Public**). [Соглашение Steam Workshop](https://partner.steamgames.com/doc/features/workshop/implementation#LegalAgreement)

Чтобы обновить публикацию, увеличьте версию, пересоберите мод и скопируйте новые файлы поверх установленных. Сохраните файл `Mods/ChaosSlots/about/publishedfileid.txt`, который игра создаёт после первой загрузки. Выберите тот же локальный мод, нажмите **Update** в меню модов и проверьте видимость страницы в Steam.
