"""Create the bundled UI font: python subset_font.py /path/to/NotoSansSC[wght].ttf.

Requires fonttools==4.60.2. Source: google/fonts, ofl/notosanssc (SIL OFL 1.1).
Includes GB2312, Latin, Greek, math/punctuation, and current UI source characters.
"""
from pathlib import Path
import sys
from fontTools import subset
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

root = Path(__file__).resolve().parent
chars = set(range(32, 0x250)) | set(range(0x370, 0x400)) | set(range(0x2000, 0x2300))
for lead in range(0xA1, 0xF8):
    for trail in range(0xA1, 0xFF):
        try:
            chars.update(map(ord, bytes([lead, trail]).decode("gb2312")))
        except UnicodeDecodeError:
            pass
for path in (root / "Chromaticity.Tools").rglob("*.cs"):
    if not {"obj", "bin"} & set(path.parts):
        chars.update(map(ord, path.read_text()))
font = instantiateVariableFont(TTFont(sys.argv[1]), {"wght": 400}, inplace=True)
options = subset.Options()
options.name_IDs = [0, 1, 2, 3, 4, 5, 6, 13, 14, 16, 17]
subsetter = subset.Subsetter(options=options)
subsetter.populate(unicodes=chars)
subsetter.subset(font)
for record in font["name"].names:
    replacement = {1: "Chromaticity UI", 2: "Regular", 3: "ChromaticityUI-Regular",
                   4: "Chromaticity UI Regular", 6: "ChromaticityUI-Regular",
                   16: "Chromaticity UI", 17: "Regular"}.get(record.nameID)
    if replacement:
        record.string = replacement.encode(record.getEncoding())
destination = root / "Chromaticity.Tools/Assets/ChromaticityUI.ttf"
destination.parent.mkdir(parents=True, exist_ok=True)
font.save(destination)
print(f"Saved {destination}: {destination.stat().st_size:,} bytes")
