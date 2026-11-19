namespace MiniLife.WorldStudio.UI
{
    // Все строки интерфейса в одном месте (раздел 9: локализация - русский, в одном месте).
    // Стиль: коротко, с глагола, без "пожалуйста" и восклицаний.
    internal static class Strings
    {
        public const string AppTitle = "MiniLife World Studio";
        public const string Untitled = "Новый мир";
        public const string StateSaved = "Сохранено";
        public const string StateDirty = "Есть несохранённые изменения";
        public const string StateNew = "Ещё не сохранён";

        // Приветствие
        public const string WelcomeTitle = "MiniLife World Studio";
        public const string WelcomeSubtitle = "Весь мир игры в одном файле world.json";
        public const string WelcomeNew = "Собрать из основных карт";
        public const string WelcomeNewHint = "Три карты из data\\sources.json, сразу";
        public const string WelcomeOpen = "Открыть world.json";
        public const string WelcomeOpenHint = "Продолжить работу с миром";
        public const string WelcomeRecent = "Недавние";

        // Слои и инструменты
        public const string LayerLandscape = "Ландшафт";
        public const string LayerPolitical = "Политика";
        public const string LayerWildlands = "Дикие земли";
        public const string LayerObjects = "Объекты";
        public const string LayersTitle = "Видимость слоёв";
        public const string ToolBrush = "Кисть";
        public const string ToolRect = "Прямоугольник";
        public const string ToolFill = "Заливка связной области";
        public const string ToolPipette = "Пипетка: взять значение с карты";
        public const string ToolEraser = "Ластик: стирает только текущий слой";
        public const string ToolRoute = "Маршрут армии";
        public const string RouteHint = "Кликайте по клеткам по порядку. Backspace убирает последнюю точку, Enter сохраняет.";
        public const string RouteDone = "Сохранить маршрут";
        public const string RouteClear = "Очистить";

        // Инспектор и проверка
        public const string InspectorEmpty = "Выберите клетку на карте";
        public const string InspectorEmptyHint = "Здесь появятся ландшафт, владелец, объект и заметка";
        public const string NotLand = "Не суша";
        public const string NotesPlaceholder = "Заметка к клетке";
        public const string FindingsTitle = "Проверка мира";
        public const string FindingsRerun = "Проверить снова";
        public const string FindingsClean = "Всё в порядке";
        public const string FindingsCleanHint = "Проверки не нашли ни одной проблемы";
        public const string CheckButton = "Проверка";

        // Сообщения
        public const string LayerLockedMsg = "Слой \"{0}\" заблокирован. Снимите замок слева";
        public const string LayerHiddenMsg = "Слой \"{0}\" скрыт. Включите его, чтобы видеть правки";
        public const string PickValueFirst = "Выберите значение в палитре слева";
        public const string AskSaveTitle = "Сохранить изменения?";
        public const string AskSaveText = "Если не сохранить, изменения в мире пропадут.";
        public const string Saved = "Сохранено";
        public const string Autosaved = "Автосохранение";
        public const string SaveFailed = "Не удалось сохранить файл. Подробности в журнале ошибок.";
        public const string ExportWarningsTitle = "Экспорт с предупреждениями";
        public const string ExportDone = "Файл для PixelBuilder записан";
        public const string ConfirmRemoveLandTitle = "Убрать сушу?";
        public const string ConfirmRemoveLand = "Клетка {0} перестанет быть сушей: ландшафт, владелец и заметка будут убраны. Это можно отменить.";
        public const string UnexpectedError = "Данные не изменены. Подробности записаны в журнал ошибок:\n{0}";
        public const string Busy = "Сохраняю...";
        public const string ValueOutOfRange = "Значение отношения - целое число от -100 до 100";

        // Файловые фильтры
        public const string FilterWorld = "Мир MiniLife (world.json)|*.json";
        public const string FilterLegacy = "Карта PixelBuilder (*.json)|*.json";
        public const string FilterXlsx = "Книга Excel (*.xlsx)|*.xlsx";

        // Импорт
        public const string ImportTitle = "Собрать мир из карт PixelBuilder";
        public const string ImportExplain = "Основные карты из data\\sources.json уже подставлены. Любую можно заменить перетаскиванием или кнопкой. Смысл цвета задаёт карточка: CC751E - пески на карте ландшафтов и Ультерия на политической.";
        public const string ImportNeedOne = "Выберите хотя бы один файл";
        public const string ImportBuild = "Собрать мир";

        // Реестр, отношения, история
        public const string RegistryTitle = "Реестр";
        public const string RegistrySubtitle = "Государства, группы диких земель и армии. Цвет - только отображение, клетки ссылаются на id.";
        public const string Add = "Добавить";
        public const string Remove = "Удалить";
        public const string RelationsTitle = "Отношения государств";
        public const string RelationsSubtitle = "Строка - как государство относится к остальным, столбец - как к нему. Маленькое число - отношение в обратную сторону.";
        public const string ImportXlsx = "Импорт из Excel";
        public const string ExportXlsx = "Экспорт в Excel";
        public const string HistoryTitle = "История версий";
        public const string HistorySubtitle = "Копия world.json перед каждым сохранением. Откат сначала сохраняет текущую версию.";
        public const string HistoryRestore = "Откатить";
        public const string HistoryNeedSaved = "История появится после первого сохранения мира.";
        public const string Done = "Готово";
        public const string Cancel = "Отмена";
    }
}
