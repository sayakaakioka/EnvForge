import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
IMPLEMENTATION_DOCS = ROOT / "docs" / "implementation"
LEDGER_PATH = IMPLEMENTATION_DOCS / "cloud-result-retention.json"
DOCUMENT_PATH = IMPLEMENTATION_DOCS / "cloud-result-retention.md"


class CloudResultRetentionTests(unittest.TestCase):
    def test_machine_readable_ledger_separates_active_and_released_records(self):
        ledger = json.loads(LEDGER_PATH.read_text(encoding="utf-8"))

        self.assertEqual(1, ledger["schema_version"])
        active = ledger["active_retention"]
        released = ledger["released_retention"]
        self.assertIsInstance(active, list)
        self.assertIsInstance(released, list)

        required = {
            "retention_id",
            "submission_id",
            "gcs_prefix",
            "firestore_documents",
            "cloud_run_execution",
        }
        all_records = active + released
        for record in all_records:
            self.assertFalse(required - record.keys())

        retention_ids = [record["retention_id"] for record in all_records]
        submission_ids = [record["submission_id"] for record in all_records]
        self.assertEqual(len(retention_ids), len(set(retention_ids)))
        self.assertEqual(len(submission_ids), len(set(submission_ids)))

        active_submissions = {record["submission_id"] for record in active}
        released_submissions = {record["submission_id"] for record in released}
        self.assertFalse(active_submissions & released_submissions)

        for record in released:
            self.assertRegex(record["released_on"], r"^\d{4}-\d{2}-\d{2}$")
            self.assertRegex(record["last_verified_on"], r"^\d{4}-\d{2}-\d{2}$")
            verification = record["verification"]
            required_verification = {
                "firestore_submissions",
                "firestore_results",
                "firestore_job_debug",
                "gcs_live_prefix",
                "gcs_soft_deleted_prefix",
                "cloud_run_execution",
                "cloud_logging",
            }
            self.assertFalse(required_verification - verification.keys())
            self.assertTrue(
                set(verification.values()) <= {"present", "not_found", "not_checked"}
            )

    def test_human_ledger_places_records_under_the_matching_status_heading(self):
        ledger = json.loads(LEDGER_PATH.read_text(encoding="utf-8"))
        document = DOCUMENT_PATH.read_text(encoding="utf-8")

        active_marker = "## 現在の保持対象"
        released_marker = "## 保持解除履歴"
        self.assertIn(active_marker, document)
        self.assertIn(released_marker, document)

        active_section = document.split(active_marker, 1)[1].split(released_marker, 1)[
            0
        ]
        released_section = document.split(released_marker, 1)[1]

        for record in ledger["active_retention"]:
            self.assertIn(record["retention_id"], active_section)
            self.assertNotIn(record["retention_id"], released_section)

        for record in ledger["released_retention"]:
            self.assertNotIn(record["retention_id"], active_section)
            self.assertIn(record["retention_id"], released_section)


if __name__ == "__main__":
    unittest.main()
