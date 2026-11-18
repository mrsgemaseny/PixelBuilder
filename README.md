# PixelBuilder + MiniLife World Studio

Инструменты авторства карт для игры MiniLife (Godot). Сборка на Windows без установки: csc из .NET Framework 4.5+.

- **PixelBuilder** (корень) - редактор карт pixel_architect_v1: глобальная 21 x 15 и локальная 1000 x 1000, здания.
  `build.cmd test` - сборка, тесты, ImportProbe. Изменения - `CHANGELOG_PixelBuilder.md`.
- **WorldStudio** (`WorldStudio/`) - единый world.json: реестр государств, легенда, проверка мира, отношения с Excel,
  экспорт обратно в PixelBuilder. Подробно - `WorldStudio/README.md`, решения - `WorldStudio/docs/DECISIONS.md`.

Оба приложения: светлая и тёмная тема по настройке Windows, общий набор интерфейса (копия в каждом, D-29).
Основные карты автора лежат в репозитории игры (`MiniLife/future/map`) и только читаются.
