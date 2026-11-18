# GAME_BUILDING_REFERENCE - как здания устроены в игре MiniLife (только чтение)

Источники: scripts/systems/world/settlement_roofs.gd, settlement_materials.gd, scripts/alife/settlements/settlement_layout.gd, city_street_plan.gd, data/structures.json, assets/sprites/expansion/settlements_2026_11_04/manifest.json.

## Размеры комнат (внешний прямоугольник со стенами, Rect2i)
- _build_room: 3x4, дверь в стене, внутри один предмет (bed/workbench/storage).
- _build_district_room: 5x4, дверь x=2 (середина), вход с севера или юга.
- _house в city_street_plan: ширина 4-6, высота 4-5.
Вывод: 3x2 внутри = 5x4 снаружи и 2x2 внутри = 4x4 снаружи совпадают с игровыми комнатами.

## Цвета конструкций (structures.json)
floor 8c6239, stone_floor 747d86, wall (камень) 6c757d, wood_wall 8c5a2b, door 9c6644.

## Крыша: палитры
- Каменные стены (wall): 8c513b, 725552, 74604c; цвет выбирается hash(позиции).
- Деревянные (wood_wall): 685039, 8b6b46, 6b6450.
- Стили: basilica/temple a0543a; lord_house 655653; smithy 4e4741; villa/shop b56c47; thermae 747a6d; insula от 945940 к b48157.
- Вне видимости (fog == 1) цвет darkened(0.38).

## Крыша: геометрия и детали
- Покрытие: камень = TERRACOTTA, дерево = SHINGLES; lord_house/thermae = SLATE, smithy = SOOTED_TILE, villa/shop = OCHRE_TILE (атлас city_materials.png 4x4: MEADOW, GRAVEL, COBBLE, FLAGSTONE, TERRACOTTA, SLATE, SHINGLES, SOOTED_TILE, ASHLAR, PLASTER, OAK, MOSAIC, EARTH, DRY_MEADOW, RUBBLE, OCHRE_TILE).
- Два ската. Горизонтальный конёк: линия на 43% высоты. Вертикальный (upright): по центру ширины. Вертикальный у shop, а также villa/insula, если высота больше ширины.
- Второй скат темнее: множитель цвета bdb5aa.
- Конёк: линия толщиной 7 цвета color.darkened(0.38) и блик толщиной 3 color.lightened(0.30) со сдвигом (-2,-2).
- Контур крыши: прямоугольник color.darkened(0.55), толщина 3.
- Тень: смещение (9,13), цвет (0.10, 0.08, 0.05, 0.25).
- Дымоходная труба: при каменных стенах или ширине >= 5; 20x28, цвет 736b60, оголовок a19580, отверстие 322c25, швы 4a433a.
- Кузница: труба 26x42, цвет 65534a, оголовок 998577.
- Фасад под крышей с окнами 17x14; ночью (до 6 или после 19 часов) стекло f3ad5e, днём 383c3a. Деревянный фасад 79583d, каменный 807668.
- Крыша скрывается, когда игрок в клетке на 1 шаг вокруг комнаты (grow(1)) или комната повреждена.
- Особые стили (basilica, temple, thermae, lord_house) с колоннадой, фронтоном, куполом, башнями: вне задачи PixelBuilder (жильё).

## Что взято в PixelBuilder
Палитры TIMBER_ROOFS и STONE_ROOFS (выбор по позиции якоря), тень, конёк с бликом, контур, второй скат темнее, дымоход при ширине >= 5, окна и цвета стен/пола/двери из structures.json.
