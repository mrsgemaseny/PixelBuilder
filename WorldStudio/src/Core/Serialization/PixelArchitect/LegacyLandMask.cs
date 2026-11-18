using MiniLife.WorldStudio.Domain;

namespace MiniLife.WorldStudio.Serialization.PixelArchitect
{
    // Маска суши, зашитая в старом PixelBuilder (CanvasControl.Catalog.cs, GlobalLandRows; REPORT_00, раздел 3).
    // Скопирована как данные, только для экспорта: если суша мира отличается, старый PixelBuilder
    // откажется открывать файл целиком. Буквы - старое политическое деление, здесь значим только '.'.
    public static class LegacyLandMask
    {
        private static readonly string[] Rows =
        {
            ".........RRRRRRR.....", ".........RRRRRRR.....", ".......YYRRRRRRRRG...",
            "....MMYYYRRRRRRRRGGG.", "....MMYYYYYRRRRMMMGG.", "....MMMYYBBBBYMMMMMG.",
            "..MMMMMBBBBBYYYYMMGG.", "..MMMMBBBBBBYYCCGGGG.", "..MMMMBBBBBYYYCCGG...",
            "..MMMMBBBBMMYYMMM....", "..MMYYYYYMMMMMMMM....", "..MMYYYYYMMMM........",
            "....YYCCYMMMM........", "......CCMMMM.........", ".......CMM..........."
        };

        public const int Width = 21;
        public const int Height = 15;

        public static bool IsLand(CellCoord cell)
        {
            if (cell.X < 0 || cell.Y < 0 || cell.X >= Width || cell.Y >= Height) return false;
            return Rows[cell.Y][cell.X] != '.';
        }

        public static int LandCount
        {
            get
            {
                int n = 0;
                foreach (string row in Rows) foreach (char c in row) if (c != '.') n++;
                return n;
            }
        }
    }
}
