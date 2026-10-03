"""Offline checks for update detection: never call the real CIE service."""
import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import cie_reference as cie


class UpdateDetectionTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.path = self.root / "illuminants" / "sample.json"
        self.path.parent.mkdir()
        self.csv = b"400,1\r\n401,2\r\n"
        self.metadata = b'{"version":1}'
        self.csv_url = "https://files.cie.co.at/sample.csv"
        self.metadata_url = "https://files.cie.co.at/sample.json"
        self.path.with_suffix(".csv").write_bytes(self.csv)
        self.path.write_text(json.dumps({
            "csvFile": "sample.csv", "csvSha256": hashlib.sha256(self.csv).hexdigest(),
            "metadataSha256": hashlib.sha256(self.metadata).hexdigest(),
            "csvUrl": self.csv_url, "metadataUrl": self.metadata_url,
            "sourcePage": "https://www.cie.co.at/datatable/sample",
        }))
        self.root_patch = patch.object(cie, "ROOT", self.root)
        self.root_patch.start()
        self.addCleanup(self.root_patch.stop)

    def test_csv_change_is_detected_even_if_metadata_did_not_change(self):
        with patch.object(cie, "dataset_links", return_value=(self.csv_url, self.metadata_url)), \
             patch.object(cie, "fetch", side_effect=[b"400,9\r\n401,2\r\n", self.metadata]):
            self.assertEqual("UPDATE (CSV): sample.csv", cie.check(self.path, True))
        self.assertEqual(self.csv, self.path.with_suffix(".csv").read_bytes())

    def test_unchanged_and_metadata_only_changes_are_distinguished(self):
        with patch.object(cie, "dataset_links", return_value=(self.csv_url, self.metadata_url)), \
             patch.object(cie, "fetch", side_effect=[self.csv, self.metadata, self.csv, b'{"version":2}']):
            self.assertEqual("UNCHANGED: sample.csv", cie.check(self.path, True))
            self.assertEqual("UPDATE (metadata): sample.csv", cie.check(self.path, True))

    def test_local_corruption_is_detected_without_network(self):
        self.path.with_suffix(".csv").write_bytes(b"changed")
        with patch.object(cie, "fetch") as fetch:
            self.assertTrue(cie.check(self.path, True).startswith("LOCAL CHANGE"))
            fetch.assert_not_called()

    def test_updated_links_are_detected_even_when_bytes_are_identical(self):
        with patch.object(cie, "dataset_links", return_value=(self.csv_url, self.metadata_url + "?v=2")), \
             patch.object(cie, "fetch", side_effect=[self.csv, self.metadata]):
            self.assertEqual("UPDATE (source URLs): sample.csv", cie.check(self.path, True))

    def test_dataset_page_resolves_relative_links(self):
        html = b'<a href="https://files.cie.co.at/sample.csv">CSV</a><a href="/sample.json">Metadata</a>'
        with patch.object(cie, "fetch", return_value=html):
            self.assertEqual((self.csv_url, "https://www.cie.co.at/sample.json"), cie.dataset_links("https://www.cie.co.at/datatable/sample"))


if __name__ == "__main__":
    unittest.main()
