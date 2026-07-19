import json
import re
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
UNITY_PROJECT = ROOT / "unity" / "EnvForge-local-first"
CLOUD_SOURCE = UNITY_PROJECT / "Assets" / "Scripts" / "Navigation" / "Cloud"
LOCAL_ONNX_RUNTIME = UNITY_PROJECT / "Assets" / "Plugins" / "ONNXRuntime"
SDK_REVISION = "b96a46779bef8ed24af77d9aecf49f94150d8afa"
SDK_URL = "https://github.com/sayakaakioka/EmbodiedLab.Unity.git#" + SDK_REVISION


class EmbodiedLabUnityBoundaryTests(unittest.TestCase):
    def test_package_manifest_and_lock_pin_the_canonical_sdk(self):
        manifest = self._read_json(UNITY_PROJECT / "Packages" / "manifest.json")
        package_lock = self._read_json(
            UNITY_PROJECT / "Packages" / "packages-lock.json"
        )

        self.assertEqual(
            SDK_URL,
            manifest["dependencies"]["com.embodiedlab.unity"],
        )
        locked = package_lock["dependencies"]["com.embodiedlab.unity"]
        self.assertEqual(SDK_URL, locked["version"])
        self.assertEqual(SDK_REVISION, locked["hash"])
        self.assertEqual(
            {
                "com.unity.modules.imgui": "1.0.0",
                "com.unity.modules.physics": "1.0.0",
                "com.unity.nuget.newtonsoft-json": "3.2.2",
            },
            locked["dependencies"],
        )

    def test_onnx_runtime_is_owned_only_by_the_sdk_package(self):
        self.assertFalse(LOCAL_ONNX_RUNTIME.exists())
        self.assertFalse(LOCAL_ONNX_RUNTIME.with_suffix(".meta").exists())

        duplicate_names = {
            "microsoft.ml.onnxruntime.dll",
            "onnxruntime.dll",
            "onnxruntime_providers_shared.dll",
        }
        duplicates = [
            path.relative_to(UNITY_PROJECT).as_posix()
            for path in (UNITY_PROJECT / "Assets").rglob("*")
            if path.is_file() and path.name.lower() in duplicate_names
        ]
        self.assertEqual([], duplicates)

    def test_envforge_uses_only_nested_result_artifacts(self):
        adapter = CLOUD_SOURCE / "EnvForgeResultArtifacts.cs"
        self.assertFalse(adapter.exists())
        self.assertFalse(adapter.with_suffix(".cs.meta").exists())

        sources = "\n".join(
            path.read_text(encoding="utf-8")
            for path in sorted(CLOUD_SOURCE.glob("*.cs"))
        )
        self.assertNotIn("EnvForgeResultArtifacts", sources)
        self.assertIsNone(
            re.search(r"\b(?:result|latestResult)\??\.Artifacts\b", sources)
        )

        panel = (CLOUD_SOURCE / "EnvForgeCloudRunPanel.cs").read_text(encoding="utf-8")
        history = (CLOUD_SOURCE / "EnvForgeJobHistoryStore.cs").read_text(
            encoding="utf-8"
        )
        self.assertIn("latestResult?.ResultBundle?.Artifacts", panel)
        self.assertIn("result.ResultBundle?.Artifacts", history)

    @staticmethod
    def _read_json(path):
        return json.loads(path.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
