#!/usr/bin/env python3
"""Archive CIE reference tables or check upstream changes; never modifies C# data."""
import argparse
import concurrent.futures
import hashlib
import json
from datetime import datetime, timezone
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import urljoin, urlparse
from urllib.request import Request, urlopen

ROOT = Path(__file__).resolve().parents[1] / "reference"
INDEX = "https://www.cie.co.at/data-tables"


class Links(HTMLParser):
    def __init__(self, html):
        super().__init__()
        self.urls = []
        self.feed(html)

    def handle_starttag(self, tag, attrs):
        if tag == "a":
            self.urls.extend(value for key, value in attrs if key == "href" and value)


def fetch(url):
    # Only fetch public CIE data, never execute content obtained from a page.
    if urlparse(url).hostname not in {"cie.co.at", "www.cie.co.at", "files.cie.co.at"}:
        raise ValueError("Unexpected source: " + url)
    with urlopen(Request(url, headers={"User-Agent": "ChromaticityDotNet reference archive"}), timeout=45) as response:
        return response.read()


def dataset_links(page):
    links = [urljoin(page, x) for x in Links(fetch(page).decode("utf-8")).urls]
    csv = next(x for x in links if urlparse(x).path.endswith(".csv"))
    metadata = next(x for x in links if urlparse(x).path.endswith(".json"))
    return csv, metadata


def digest(data):
    return hashlib.sha256(data).hexdigest()


def category(filename):
    name = filename.lower()
    if "dxx_comp" in name:
        return "daylight"
    if "xyz" in name or "cfb_stv" in name:
        return "observers"
    if "chrom" in name or "mb" in name or name.startswith("cie_cc_"):
        return "spectral-loci"
    if "illum" in name or "l41" in name:
        return "illuminants"
    if any(x in name for x in ("tcs", "ces", "cqs", "rygb", "skin", "srf")):
        return "test-samples"
    if "deviat" in name or "meta_ind" in name:
        return "metamerism"
    return "visual-response"


def archive(page):
    csv_url, metadata_url = dataset_links(page)
    csv, metadata_bytes = fetch(csv_url), fetch(metadata_url)
    metadata = json.loads(metadata_bytes)
    filename = Path(urlparse(csv_url).path).name
    folder = ROOT / category(filename)
    folder.mkdir(parents=True, exist_ok=True)
    target = folder / filename
    manifest = target.with_suffix(".json")
    if target.exists() or manifest.exists():
        raise ValueError("Refusing to overwrite existing reference: " + filename)
    declared = metadata.get("checksums", [])
    checks = [{"method": c["hashMethod"], "declared": c["checksum"],
               "matches": hashlib.new(c["hashMethod"], csv).hexdigest() == c["checksum"]}
              for c in declared]
    record = {
        "sourcePage": page, "csvUrl": csv_url, "metadataUrl": metadata_url,
        "retrievedAtUtc": datetime.now(timezone.utc).isoformat(),
        "csvFile": filename, "csvSha256": digest(csv),
        "metadataSha256": digest(metadata_bytes),
        "officialChecksumValidation": checks, "officialMetadata": metadata,
    }
    target.write_bytes(csv)
    manifest.write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return filename + (" [official checksum mismatch; review required]" if any(not c["matches"] for c in checks) else "")


def check(path, online):
    record = json.loads(path.read_text(encoding="utf-8"))
    local = path.with_name(record["csvFile"])
    if digest(local.read_bytes()) != record["csvSha256"]:
        return "LOCAL CHANGE: " + str(local.relative_to(ROOT))
    if not online:
        return "OK: " + str(local.relative_to(ROOT))
    csv_url, metadata_url = dataset_links(record["sourcePage"])
    changes = []
    if digest(fetch(csv_url)) != record["csvSha256"]:
        changes.append("CSV")
    if digest(fetch(metadata_url)) != record["metadataSha256"]:
        changes.append("metadata")
    if (csv_url, metadata_url) != (record["csvUrl"], record["metadataUrl"]):
        changes.append("source URLs")
    return ("UPDATE (" + ", ".join(changes) + ")" if changes else "UNCHANGED") + ": " + record["csvFile"]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=["archive", "check", "verify"])
    parser.add_argument("--filter", default="", help="Filename substring for check/verify")
    args = parser.parse_args()
    if args.action == "archive":
        pages = sorted({urljoin(INDEX, x) for x in Links(fetch(INDEX).decode("utf-8")).urls if "/datatable/" in x})
        operation = archive
        items = pages
    else:
        items = sorted(p for p in ROOT.glob("*/*.json") if args.filter in p.name)
        operation = lambda p: check(p, args.action == "check")
    if not items:
        print("ERROR: no matching datasets.")
        return 1
    failures = 0
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        futures = {pool.submit(operation, item): item for item in items}
        for future in concurrent.futures.as_completed(futures):
            try:
                result = future.result()
                print(result, flush=True)
                if result.startswith(("UPDATE", "LOCAL CHANGE")):
                    failures += 1
            except Exception as error:
                failures += 1
                print(f"ERROR {futures[future]}: {error}", flush=True)
    print(f"Checked {len(items)} datasets; {failures} change(s) or error(s).")
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
