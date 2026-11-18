# REPORT_03 - Валидатор (Этап 3)

## 1. Что сделано
14 проверок раздела 8, по классу на проверку (`src/Core/Validation/Checks/`), раннер с сортировкой по уровню. Сбой одной проверки становится находкой и не прячет остальные. Валидатор только читает.

## 2. Новые файлы
`Validation/WorldValidator.cs` (ValidationInput, IWorldCheck, раннер, связность); `Checks/UnknownValueCheck.cs`, `SharedValueCheck.cs`, `LandWithoutOwnerCheck.cs`, `TotalBlocksCheck.cs`, `ArmyMarksCheck.cs`, `VillageMarksCheck.cs`, `SettlementTableCheck.cs`, `RelationsCheck.cs`, `ConnectivityCheck.cs`, `ArmyOutsideWildlandsCheck.cs`, `ArmyRouteCheck.cs`, `DuplicateIdsCheck.cs`, `OutOfGridCheck.cs`, `SeaCheck.cs`.

## 3. Как проверить
F5 в редакторе; клик по находке подсвечивает клетки.

## 4. Тесты
S3.ExpectedFindings (ровно 9 находок на текущих картах, 0 критических), S3.EachCheckFires (каждая проверка срабатывает на своём нарушении с правильным маркером), S3.ValidatorReadOnly (мир и исходные файлы байт-в-байт до и после) - пройдено.

## 5. Решения
D-16, D-24, D-25.

## 6. Риски
- [INFO] Сверка поселений считает государство по владельцу клетки, на которой стоит поселение, а не по полю owner объекта: так перекраска клетки сразу видна в сверке.
- [INFO] Проверка 14 смотрит id и названия типов, не заметки ("Мореход" не находка).

## 7. Приёмка
| Пункт | Статус |
|---|---|
| Расхождение Ультерии | пройдено: [WARNING] "городов на карте 3, в таблице автора 4" |
| Пометки заготовок | пройдено: [INFO] "38 меток Д" |
| Пятая (уничтоженная) армия | пройдено: [INFO] army.girdenburg |
| Валидатор ничего не меняет | пройдено |
| Тест на каждую проверку | пройдено (14 из 14) |
