# OBSERVATIONS (не исправлялось, вне задачи)

| id | Где | Что | Маркер |
|---|---|---|---|
| O-01 | PixelBuilder `MainForm.Layout.cs:77` | "Clear layer" очищает слой без диалога подтверждения (откатывается через undo, но правило 2 требует подтверждения) | [WARNING] |
| O-02 | PixelBuilder `MainForm.Files.cs` | запись файла неатомарная (`File.WriteAllText` напрямую), при сбое возможен обрезанный файл | [WARNING] |
| O-03 | PixelBuilder `CanvasControl.Catalog.cs` | `settlement_village_2/3` подписаны "Деревня, уровень 2/3", по данным это кратность | [INFO] |
| O-04 | PixelBuilder `MainForm.Layout.cs:175,180,190` | в подписях кнопок эмодзи (Save JSON, Open JSON, Copy JSON) | [INFO] |
| O-05 | PixelBuilder `CanvasControl.Serialization.cs` | `ImportJson` глотает любое исключение (`catch { return false; }`), пользователь видит только "Invalid blueprint JSON format" без причины | [WARNING] |
| O-06 | PixelBuilder `CanvasControl.Serialization.cs` | строки в экспорте не экранируются; сейчас безопасно (id из латиницы и `_`), но опасно при появлении других символов | [INFO] |
| O-07 | PixelBuilder `CanvasControl.Catalog.cs` | `GlobalLandRows` - зашитая маска суши со старым политическим делением в буквах; суша не редактируется из инструмента | [INFO] |
| O-08 | Карта ландшафтов, клетка (14,9) | единственная гора записана как `stone_wall`, остальные 16 как `color_383838` | [INFO] |
| O-09 | PixelBuilder `ExportJson` | отступ поля `blocks` 4 пробела вместо 2 (косметика) | [INFO] |
| O-10 | PixelBuilder `tests/legacy_bases` (32 файла) | все в формате до слоёв: только blocks, без map_kind и layers; created_at в двух форматах (`2026-11-09T08:08:25` и `2026-11-09T09:49:00.947004+00:00`). Старый PixelBuilder открывает их как локальные по отсутствию map_kind | [INFO] |
| O-11 | PixelBuilder `ImportJson` | при наличии поля layers требует внутри и political, и landscape; файл с layers без political не открывается, причина не показывается | [WARNING] |
| O-12 | mono 6.8 | Control.DrawToBitmap не рисует дочерние элементы формы, CopyFromScreen под Xvfb падает (BadMatch); проба UI снимает экран через ImageMagick | [INFO] |
| O-13 | Политическая карта | цвет Аскелии 51CC51 совпадает с цветом равнин; на холсте рамка владельца обведена тёмным контуром, иначе её не видно | [INFO] |
