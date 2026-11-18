namespace MiniLife.WorldStudio.Domain
{
    // Законы мира, раздел 4 промпта: 21 x 15 клеток, клетка = 1000 x 1000 тайлов, чанк 16 x 16.
    public sealed class GridSpec
    {
        public const int DefaultWidth = 21;
        public const int DefaultHeight = 15;
        public const int DefaultCellTiles = 1000;
        public const int DefaultChunkTiles = 16;

        private readonly int _width;
        private readonly int _height;
        private readonly int _cellTiles;
        private readonly int _chunkTiles;

        public GridSpec(int width, int height, int cellTiles, int chunkTiles)
        {
            if (width <= 0 || height <= 0 || cellTiles <= 0 || chunkTiles <= 0)
                throw new DomainException("Размеры сетки должны быть положительными.");
            _width = width;
            _height = height;
            _cellTiles = cellTiles;
            _chunkTiles = chunkTiles;
        }

        public static GridSpec Default()
        {
            return new GridSpec(DefaultWidth, DefaultHeight, DefaultCellTiles, DefaultChunkTiles);
        }

        public int Width { get { return _width; } }
        public int Height { get { return _height; } }
        public int CellTiles { get { return _cellTiles; } }
        public int ChunkTiles { get { return _chunkTiles; } }

        // Чанков по оси, последний может быть неполным: 1000 / 16 = 62.5, значит 63.
        public int ChunksPerAxis { get { return (_cellTiles + _chunkTiles - 1) / _chunkTiles; } }

        // Тайлов в последнем чанке оси: 1000 - 62 * 16 = 8. Если делится нацело, чанк полный.
        public int LastChunkTiles
        {
            get
            {
                int rest = _cellTiles % _chunkTiles;
                return rest == 0 ? _chunkTiles : rest;
            }
        }

        public int WorldTilesX { get { return _width * _cellTiles; } }
        public int WorldTilesY { get { return _height * _cellTiles; } }

        public bool Contains(CellCoord cell)
        {
            return cell.X >= 0 && cell.Y >= 0 && cell.X < _width && cell.Y < _height;
        }
    }
}
