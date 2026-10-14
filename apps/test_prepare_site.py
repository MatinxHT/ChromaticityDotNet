"""Deployment regression tests for versioned browser cache policies."""
import base64
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

from prepare_site import IMMUTABLE, REVALIDATE, build_headers, cache_policies


class SitePreparationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.repo = (Path(self.temp.name) / "repository with spaces").resolve()
        apps = self.repo / "apps"
        apps.mkdir(parents=True)
        self.script = apps / "prepare_site.py"
        shutil.copy2(Path(__file__).with_name("prepare_site.py"), self.script)
        for relative, content in {
            "apps/site/_headers": "/*\n  X-Content-Type-Options: nosniff\n",
            "CIE-DATA-NOTICE.md": "Data notice",
            "LICENSE": "Library license",
            "apps/Chromaticity.Tools/Assets/OFL.txt": "Font license",
        }.items():
            path = self.repo / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(content, encoding="utf-8")
        # Neither a stale SDK manifest nor a RID-specific directory may be used.
        for intermediate in ("obj/sdk-10.0.401/Release/net10.0-browser",
                             "obj/sdk-10.0.402/Release/net10.0-browser/browser-wasm"):
            stale = apps / "Chromaticity.Tools.Browser" / intermediate / "staticwebassets.publish.endpoints.json"
            stale.parent.mkdir(parents=True, exist_ok=True)
            stale.write_text("invalid stale manifest", encoding="utf-8")
        self.publish = self.repo / "artifacts/publish with spaces"
        self.default_publish = apps / "Chromaticity.Tools.Browser/bin/Release/net10.0-browser/publish"
        self.output = self.repo / "artifacts/site"
        self.create_publish(self.publish)

    def create_publish(self, publish):
        bundle = publish / "wwwroot"
        (bundle / "_framework").mkdir(parents=True)
        (bundle / "tools").mkdir()
        (bundle / "index.html").write_text("home", encoding="utf-8")
        (bundle / "tools/index.html").write_text("tools", encoding="utf-8")
        asset = bundle / "_framework/core.abc123.wasm"
        asset.write_bytes(b"core")
        integrity = "sha256-" + base64.b64encode(hashlib.sha256(asset.read_bytes()).digest()).decode("ascii")
        manifest = {
            "Version": 1, "ManifestType": "Publish", "Endpoints": [{
                "Route": "_framework/core.abc123.wasm", "AssetFile": "_framework/core.abc123.wasm",
                "Selectors": [],
                "ResponseHeaders": [{"Name": "Cache-Control", "Value": "max-age=31536000, immutable"}],
                "EndpointProperties": [{"Name": "fingerprint", "Value": "abc123"},
                                       {"Name": "integrity", "Value": integrity}],
            }],
        }
        (publish / "Chromaticity.Tools.Browser.staticwebassets.endpoints.json").write_text(
            json.dumps(manifest), encoding="utf-8")

    def prepare(self, *arguments):
        # Staging a completed publish must work from any cwd, without a .NET SDK.
        return subprocess.run([sys.executable, str(self.script), *map(str, arguments)],
                              cwd=self.repo.parent, env=dict(os.environ, PATH=""),
                              capture_output=True, text=True)

    def test_custom_publish_uses_its_copied_manifest_without_querying_msbuild(self):
        result = self.prepare(self.output, "--publish-dir", self.publish)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual((self.output / "_framework/core.abc123.wasm").read_bytes(), b"core")
        self.assertEqual((self.output / "tools/LICENSE.txt").read_text(), "Library license")
        headers = (self.output / "_headers").read_bytes()
        self.assertIn(f"/_framework/core.abc123.wasm\n  Cache-Control: {IMMUTABLE}".encode(), headers)
        self.assertNotIn(b"\r", headers)

    def test_default_local_publish_and_output_remain_supported(self):
        self.create_publish(self.default_publish)
        result = self.prepare()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual((self.output / "index.html").read_text(), "home")

    def test_missing_published_manifest_does_not_fall_back_to_obj(self):
        manifest = self.publish / "Chromaticity.Tools.Browser.staticwebassets.endpoints.json"
        manifest.unlink()
        result = self.prepare("--publish-dir", self.publish)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn(f"Published Browser endpoints manifest is missing: {manifest}", result.stderr)
        self.assertFalse(self.output.exists())

    def test_missing_entry_page_reports_the_exact_path(self):
        entry = self.publish / "wwwroot/index.html"
        entry.unlink()
        result = self.prepare("--publish-dir", self.publish)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn(f"Published Browser entry page is missing: {entry}", result.stderr)
        self.assertFalse(self.output.exists())

    def test_corrupt_publish_does_not_stage_a_site(self):
        (self.publish / "wwwroot/_framework/core.abc123.wasm").write_bytes(b"corrupt")
        result = self.prepare("--publish-dir", self.publish)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("integrity mismatch", result.stderr)
        self.assertFalse(self.output.exists())

    def test_existing_site_is_not_overwritten(self):
        self.output.mkdir()
        sentinel = self.output / "keep.txt"
        sentinel.write_text("keep", encoding="utf-8")
        result = self.prepare("--publish-dir", self.publish)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("Output already exists", result.stderr)
        self.assertEqual(sentinel.read_text(), "keep")
        self.assertFalse((self.output / "index.html").exists())


class CachePolicyTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.bundle = Path(self.temp.name) / "wwwroot"
        self.bundle.mkdir()
        self.manifest = Path(self.temp.name) / "endpoints.json"
        self.endpoints = []
        self.add_asset("_framework/core.abc123.wasm", b"core", "abc123")
        self.add_asset("main.def456.js", b"app", "def456")
        self.write_file("index.html", b"home")
        self.write_file("tools/index.html", b"redirect")
        self.write_file("tools/difference.html", b"tool")
        self.add_asset("_framework/avalonia.js", b"mutable", None)

    def write_file(self, route, data):
        asset = self.bundle / route
        asset.parent.mkdir(parents=True, exist_ok=True)
        asset.write_bytes(data)

    def add_asset(self, route, data, fingerprint):
        self.write_file(route, data)
        integrity = "sha256-" + base64.b64encode(hashlib.sha256(data).digest()).decode("ascii")
        self.endpoints.append({
            "Route": route, "AssetFile": route, "Selectors": [],
            "ResponseHeaders": [{"Name": "Cache-Control", "Value": "max-age=31536000, immutable" if fingerprint else "no-cache"}],
            "EndpointProperties": [
                {"Name": "fingerprint", "Value": fingerprint},
                {"Name": "integrity", "Value": integrity},
            ],
        })

    def write_manifest(self, manifest_type="Publish"):
        self.manifest.write_text(json.dumps({
            "Version": 1, "ManifestType": manifest_type, "Endpoints": self.endpoints,
        }), encoding="utf-8")

    def policies(self):
        self.write_manifest()
        return cache_policies(self.bundle, self.manifest)

    def test_only_sdk_confirmed_files_are_immutable(self):
        self.write_file("_framework/pretend.abc123.js", b"not in the SDK manifest")
        policies = self.policies()
        self.assertEqual(policies["/_framework/core.abc123.wasm"], IMMUTABLE)
        self.assertEqual(policies["/main.def456.js"], IMMUTABLE)
        self.assertEqual(policies["/_framework/avalonia.js"], REVALIDATE)
        self.assertNotIn("/_framework/pretend.abc123.js", policies)

    def test_html_revalidates_at_physical_and_cloudflare_clean_urls(self):
        policies = self.policies()
        for route in ("/", "/index.html", "/index", "/tools", "/tools/",
                      "/tools/index.html", "/tools/index", "/tools/difference.html", "/tools/difference"):
            with self.subTest(route=route):
                self.assertEqual(policies[route], REVALIDATE)

    def test_corrupt_or_stale_publish_file_is_rejected(self):
        self.write_file("_framework/core.abc123.wasm", b"changed content with an old filename")
        with self.assertRaisesRegex(ValueError, "integrity mismatch"):
            self.policies()

    def test_missing_published_file_is_rejected(self):
        (self.bundle / "main.def456.js").unlink()
        with self.assertRaisesRegex(ValueError, "asset is missing"):
            self.policies()

    def test_fingerprint_must_match_the_request_filename(self):
        self.endpoints[0]["EndpointProperties"][0]["Value"] = "wrong"
        with self.assertRaisesRegex(ValueError, "matching SDK fingerprint"):
            self.policies()

    def test_unfingerprinted_immutable_file_is_rejected(self):
        self.add_asset("_framework/storage.js", b"mutable", "abc123")
        with self.assertRaisesRegex(ValueError, "matching SDK fingerprint"):
            self.policies()

    def test_compression_and_unversioned_aliases_do_not_get_immutable_rules(self):
        self.write_file("_framework/core.abc123.wasm.br", b"compressed")
        compressed = dict(self.endpoints[0], AssetFile="_framework/core.abc123.wasm.br",
                          Selectors=[{"Name": "Content-Encoding", "Value": "br"}])
        alias = dict(self.endpoints[0], Route="_framework/core.wasm")
        self.endpoints.extend([compressed, alias])
        policies = self.policies()
        self.assertEqual(sum(value == IMMUTABLE for value in policies.values()), 2)
        self.assertNotIn("/_framework/core.wasm", policies)
        self.assertNotIn("/_framework/core.abc123.wasm.br", policies)

    def test_next_release_keeps_core_cached_and_uses_a_new_app_url(self):
        before = self.policies()
        self.endpoints = [endpoint for endpoint in self.endpoints if endpoint["Route"] != "main.def456.js"]
        self.add_asset("main.new789.js", b"updated app", "new789")
        after = self.policies()
        self.assertEqual(before["/_framework/core.abc123.wasm"], after["/_framework/core.abc123.wasm"])
        self.assertEqual(after["/main.new789.js"], IMMUTABLE)
        self.assertNotIn("/main.def456.js", after)
        self.assertEqual(after["/"], REVALIDATE)

    def test_cache_headers_never_overlap(self):
        self.write_manifest()
        result = build_headers(self.bundle, self.manifest, "/*\n  X-Content-Type-Options: nosniff\n")
        routes = [line for line in result.splitlines() if line and not line[0].isspace() and not line.startswith("#")]
        self.assertEqual(len(routes), len(set(routes)))
        self.assertNotIn("/*\n  Cache-Control", result)
        with self.assertRaisesRegex(ValueError, "exact routes"):
            build_headers(self.bundle, self.manifest, "/*\n  Cache-Control: no-cache\n")

    def test_cloudflare_rule_limit_is_checked(self):
        for index in range(100):
            self.add_asset(f"mutable-{index}.js", b"mutable", None)
        self.write_manifest()
        with self.assertRaisesRegex(ValueError, "Cloudflare Pages limits"):
            build_headers(self.bundle, self.manifest, "/*\n  X-Content-Type-Options: nosniff\n")

    def test_build_manifest_cannot_be_used_for_release_cache_rules(self):
        self.write_manifest("Build")
        with self.assertRaisesRegex(ValueError, "publish endpoints manifest"):
            cache_policies(self.bundle, self.manifest)


if __name__ == "__main__":
    unittest.main()
