"""Stage the published Browser bundle and landing page for Cloudflare Pages."""
from pathlib import Path
import shutil
import sys

apps = Path(__file__).resolve().parent
repo = apps.parent
bundle = apps / "Chromaticity.Tools.Browser/bin/Release/net10.0-browser/publish/wwwroot"
output = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else repo / "artifacts/site"
if not (bundle / "index.html").is_file():
    raise SystemExit(f"Publish the Browser project first: missing {bundle}")
if output.exists():
    raise SystemExit(f"Output already exists; use a fresh directory: {output}")
shutil.copytree(apps / "site", output)
shutil.copytree(bundle, output, dirs_exist_ok=True)
for source, target in [(repo / "CIE-DATA-NOTICE.md", "CIE-DATA-NOTICE.md"),
                       (repo / "LICENSE", "LICENSE.txt"),
                       (apps / "Chromaticity.Tools/Assets/OFL.txt", "FONT-LICENSE.txt")]:
    shutil.copy2(source, output / "tools" / target)
oversized = [str(p.relative_to(output)) for p in output.rglob("*") if p.is_file() and p.stat().st_size > 25 * 1024 * 1024]
if oversized:
    raise SystemExit(f"Cloudflare Pages assets exceed 25 MiB: {oversized}")
print(f"Cloudflare Pages bundle: {output}")
