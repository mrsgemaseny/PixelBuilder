using System.Runtime.CompilerServices;

// Проба интерфейса (tests/UiProbe.cs) проверяет окна без диалогов: ей нужен доступ к internal-членам UI.
[assembly: InternalsVisibleTo("WorldStudio.UiProbe")]
