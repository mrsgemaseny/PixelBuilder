#!/usr/bin/env bash
# Сборка и все проверки на Linux (mono, mcs -langversion:5). Так код проверяется в среде агента.
# Использование: bash build_mono.sh [путь к PixelBuilder, по умолчанию ..]
# Нужны: mono-mcs, mono-runtime, libmono-system-windows-forms4.0-cil, libmono-system-web-extensions4.0-cil,
#        libmono-system-io-compression4.0-cil, xvfb; для пробы UI - imagemagick; для проверки xlsx - python3-openpyxl.
set -euo pipefail
cd "$(dirname "$0")"
PB="${1:-..}"
mkdir -p bin/data bin/export
cp data/*.json bin/data/

mcs -langversion:5 -warnaserror+ -target:library -out:bin/WorldStudio.Core.dll -r:System.dll -r:System.Core.dll \
    -r:System.Xml.dll -r:System.IO.Compression.dll -recurse:'src/Core/*.cs'
mcs -langversion:5 -warnaserror+ -target:winexe -out:bin/WorldStudio.exe -r:bin/WorldStudio.Core.dll -r:System.dll -r:System.Core.dll \
    -r:System.Windows.Forms.dll -r:System.Drawing.dll -recurse:'src/UI/*.cs'
mcs -langversion:5 -warnaserror+ -target:exe -out:bin/WorldStudio.Tests.exe -r:bin/WorldStudio.Core.dll tests/Tests.cs tests/Tests.World.cs
mono bin/WorldStudio.Tests.exe tests/fixtures "$PB/tests/legacy_bases" bin/export

# Excel-совместимость экспорта: openpyxl (если есть) читает записанный xlsx.
if python3 -c "import openpyxl" 2>/dev/null; then
  python3 -I - bin/export/relations.xlsx <<'PY'
import sys, openpyxl
ws = openpyxl.load_workbook(sys.argv[1]).active
rows = list(ws.iter_rows(values_only=True))
assert len(rows) == 9 and rows[0][1] == "Рейнгард" and rows[1][2] == -100 and rows[2][1] == -80 and rows[1][1] is None, rows[:3]
print("XLSX_OPENPYXL_PASS")
PY
fi

# Старый PixelBuilder собирается из его исходников во временную папку, его файлы не меняются.
TMP="$(mktemp -d)"
XVFB_PID=""
cleanup() { [ -n "$XVFB_PID" ] && kill "$XVFB_PID" 2>/dev/null; rm -rf "$TMP"; }
trap cleanup EXIT
mcs -langversion:5 -target:winexe -out:"$TMP/PixelBuilder.exe" -r:System.Windows.Forms.dll -r:System.Drawing.dll \
    -r:System.Web.Extensions.dll -recurse:"$PB/src/*.cs" > /dev/null
cp bin/WorldStudio.Core.dll "$TMP/"
mcs -langversion:5 -target:exe -out:"$TMP/WorldStudio.CompatProbe.exe" -r:bin/WorldStudio.Core.dll -r:"$TMP/PixelBuilder.exe" \
    -r:System.Windows.Forms.dll -r:System.Drawing.dll tests/CompatProbe.cs
if [ -z "${DISPLAY:-}" ]; then
  Xvfb :98 -screen 0 1600x1000x24 > /dev/null 2>&1 &
  XVFB_PID=$!
  export DISPLAY=:98
  sleep 1
fi
mono "$TMP/WorldStudio.CompatProbe.exe" tests/fixtures "$PB/tests/legacy_bases" bin/export 2>&1 | grep -v "^Gtk not found"

# Проба интерфейса: окна открываются, кисть рисует, скриншоты в bin/ui.
if command -v import > /dev/null; then
  mcs -langversion:5 -target:exe -out:bin/WorldStudio.UiProbe.exe -r:bin/WorldStudio.Core.dll -r:bin/WorldStudio.exe \
      -r:System.Windows.Forms.dll -r:System.Drawing.dll tests/UiProbe.cs
  mono bin/WorldStudio.UiProbe.exe tests/fixtures data bin/ui 2>&1 | grep -v "^Gtk not found"
fi
