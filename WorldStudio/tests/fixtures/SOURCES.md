# Источники фикстур

Копии файлов автора, переданные 2026-11-17. Не редактировать: это эталоны приёмки.

| Файл | Источник | sha256 (начало) |
|---|---|---|
| legacy_nature_184.json | nature.json (карта ландшафтов) | fc7128ac |
| legacy_wildlands_226.json | "Д=деревни(пока что просто метки), г-имперские армии.json" | 8cdd288e |
| legacy_political_286.json | политическая карта; совпадает с PixelBuilder\tests\fixtures\global_layers_286.json | 3aab3a38 |

| relations_author_like.xlsx | создан агентом через openpyxl как имитация файла из Excel: заголовок, таблица со смещением, sharedStrings, верхний регистр, другой порядок, дробные числа; значения - Приложение B | - |

Проверено 2026-11-18: слои political, landscape, objects этих фикстур совпадают с основными картами из `MiniLife\\future\\map` (data/sources.json); отличается только форматирование файла.

Старые локальные базы не копируются: тесты читают их из `..\tests\legacy_bases` (PixelBuilder), только чтение.
