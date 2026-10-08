"""Stage the published Browser bundle and landing page for Cloudflare Pages."""
import base64
import hashlib
import json
from pathlib import Path, PurePosixPath
import shutil
import sys

REVALIDATE = "no-cache"
IMMUTABLE = "public, max-age=31536000, immutable"


def cache_policies(bundle: Path, manifest_path: Path) -> dict[str, str]:
    """Trust SDK fingerprints and integrity, never just a hash-looking filename."""
    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    if manifest.get("ManifestType") != "Publish" or manifest.get("Version") != 1:
        raise ValueError("Expected a .NET publish endpoints manifest (version 1).")

    policies = {}
    for asset in bundle.rglob("*"):
        if not asset.is_file() or asset.suffix != ".html":
            continue
        route = "/" + asset.relative_to(bundle).as_posix()
        policies[route] = REVALIDATE
        if asset.suffix == ".html":
            # Pages redirects .html to clean URLs and index.html to its directory.
            clean_route = route.removesuffix(".html")
            policies[clean_route] = REVALIDATE
            if asset.name == "index.html":
                directory = route.removesuffix("index.html")
                policies[directory] = REVALIDATE
                if directory != "/":
                    policies[directory.rstrip("/")] = REVALIDATE

    immutable_count = 0
    for endpoint in manifest["Endpoints"]:
        route = endpoint["Route"]
        # Only canonical files; compressed variants use the same request URL.
        if endpoint["Selectors"] or endpoint["AssetFile"] != route:
            continue
        path = PurePosixPath(route)
        if path.suffix not in {".js", ".wasm"}:
            continue
        if path.is_absolute() or ".." in path.parts or any(c in route for c in "\\*:?\n\r #"):
            raise ValueError(f"Invalid static asset route: {route!r}")
        asset = bundle / route
        if not asset.is_file():
            raise ValueError(f"Published asset is missing: {route}")
        # Only current manifest resources get rules; stale local build files use
        # Pages' default revalidation and cannot exhaust its 100-rule limit.
        policies["/" + route] = REVALIDATE
        headers = {h["Name"].lower(): h["Value"] for h in endpoint["ResponseHeaders"]}
        if "immutable" not in {value.strip() for value in headers.get("cache-control", "").split(",")}:
            continue
        properties = {p["Name"]: p["Value"] for p in endpoint["EndpointProperties"]}
        fingerprint = properties.get("fingerprint")
        if not fingerprint or not path.name.endswith(f".{fingerprint}{path.suffix}"):
            raise ValueError(f"Immutable asset has no matching SDK fingerprint: {route}")
        integrity = "sha256-" + base64.b64encode(hashlib.sha256(asset.read_bytes()).digest()).decode("ascii")
        if properties.get("integrity") != integrity:
            raise ValueError(f"Published asset integrity mismatch: {route}; publish again.")
        policies["/" + route] = IMMUTABLE
        immutable_count += 1
    if not immutable_count:
        raise ValueError("No fingerprinted WASM/JS assets found; publish with fingerprinting enabled.")
    return policies


def build_headers(bundle: Path, manifest_path: Path, template: str) -> str:
    # Comments may mention the header, but wildcard cache rules must not overlap.
    if any("cache-control" in line.lower() for line in template.splitlines() if not line.lstrip().startswith("#")):
        raise ValueError("Cache-Control must be generated as exact routes, not template rules.")
    policies = cache_policies(bundle, manifest_path)
    blocks = [template.rstrip(), "# Generated cache rules; do not edit the staged file."]
    for route, policy in sorted(policies.items()):
        blocks.append(f"{route}\n  Cache-Control: {policy}")
    result = "\n\n".join(blocks) + "\n"
    rule_count = sum(1 for line in result.splitlines() if line and not line[0].isspace() and not line.startswith("#"))
    if rule_count > 100 or any(len(line) > 2000 for line in result.splitlines()):
        raise ValueError("Generated _headers exceeds Cloudflare Pages limits (100 rules, 2000 characters per line).")
    return result


def main() -> None:
    apps = Path(__file__).resolve().parent
    repo = apps.parent
    bundle = apps / "Chromaticity.Tools.Browser/bin/Release/net10.0-browser/publish/wwwroot"
    manifest = apps / "Chromaticity.Tools.Browser/obj/Release/net10.0-browser/staticwebassets.publish.endpoints.json"
    output = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else repo / "artifacts/site"
    if not (bundle / "index.html").is_file() or not manifest.is_file():
        raise SystemExit(f"Publish the Browser project first: missing bundle or endpoints manifest in {bundle}")
    if output.exists():
        raise SystemExit(f"Output already exists; use a fresh directory: {output}")
    # Validate before staging so a bad manifest cannot produce a deployable site.
    try:
        headers = build_headers(bundle, manifest, (apps / "site/_headers").read_text(encoding="utf-8"))
    except (ValueError, KeyError) as error:
        raise SystemExit(f"Cannot prepare cache rules: {error}") from error
    shutil.copytree(apps / "site", output)
    shutil.copytree(bundle, output, dirs_exist_ok=True)
    (output / "_headers").write_text(headers, encoding="utf-8", newline="\n")
    for source, target in [(repo / "CIE-DATA-NOTICE.md", "CIE-DATA-NOTICE.md"),
                           (repo / "LICENSE", "LICENSE.txt"),
                           (apps / "Chromaticity.Tools/Assets/OFL.txt", "FONT-LICENSE.txt")]:
        shutil.copy2(source, output / "tools" / target)
    oversized = [str(p.relative_to(output)) for p in output.rglob("*") if p.is_file() and p.stat().st_size > 25 * 1024 * 1024]
    if oversized:
        raise SystemExit(f"Cloudflare Pages assets exceed 25 MiB: {oversized}")
    print(f"Cloudflare Pages bundle: {output}")


if __name__ == "__main__":
    main()
