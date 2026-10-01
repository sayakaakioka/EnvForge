import json
import re
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
UNITY_PROJECT = ROOT / "unity" / "EnvForge-local-first"
CLOUD_SOURCE = UNITY_PROJECT / "Assets" / "Scripts" / "Navigation" / "Cloud"
INFERENCE_SOURCE = (
    UNITY_PROJECT
    / "Assets"
    / "Scripts"
    / "Navigation"
    / "Inference"
    / "NavigationModelInferenceController.cs"
)
SCENE_BUILDER_SOURCE = (
    UNITY_PROJECT / "Assets" / "Scripts" / "Navigation" / "NavigationSceneBuilder.cs"
)
CONTRACT_DEFAULTS_SOURCE = (
    UNITY_PROJECT
    / "Assets"
    / "Scripts"
    / "Navigation"
    / "Contracts"
    / "NavigationScenarioBundleDefaults.cs"
)
LOCAL_ONNX_RUNTIME = UNITY_PROJECT / "Assets" / "Plugins" / "ONNXRuntime"
SDK_REVISION = "51af750807626c15da6bf873f6d010f7a78e6fa1"
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
        self.assertIn("latestResult?.ToDocument().ResultBundle?.Artifacts", panel)
        self.assertIn("result.ResultBundle?.Artifacts", history)

    def test_replay_manifest_reads_include_job_identity(self):
        panel = (CLOUD_SOURCE / "EnvForgeCloudRunPanel.cs").read_text(encoding="utf-8")
        self.assertIsNone(
            re.search(r"EmbodiedLabReplay\.ReadManifest\(\s*manifestPath\s*\)", panel)
        )

    def test_local_inference_does_not_reapply_policy_output_mapping(self):
        inference = INFERENCE_SOURCE.read_text(encoding="utf-8")
        self.assertIn("float forward = rawForward;", inference)
        self.assertIn("float turn = rawTurn;", inference)
        self.assertNotIn("Mathf.Exp(-rawForward)", inference)
        self.assertIn("rawForward < 0f || rawForward > 1f", inference)
        self.assertIn("rawTurn < -1f || rawTurn > 1f", inference)

    def test_cached_artifacts_are_verified_before_reuse(self):
        panel = (CLOUD_SOURCE / "EnvForgeCloudRunPanel.cs").read_text(encoding="utf-8")
        self.assertGreaterEqual(panel.count("IsCachedArtifactValid("), 5)
        self.assertIn("expectedSizeBytes", panel)
        self.assertIn("expectedSha256", panel)
        self.assertIn("sha256.ComputeHash(stream)", panel)

    def test_camera_allocation_uses_schema_bounds(self):
        inference = INFERENCE_SOURCE.read_text(encoding="utf-8")
        scene_builder = SCENE_BUILDER_SOURCE.read_text(encoding="utf-8")
        defaults = CONTRACT_DEFAULTS_SOURCE.read_text(encoding="utf-8")
        self.assertIn("MinimumCameraDimensionPixels = 20", defaults)
        self.assertIn("MaximumCameraDimensionPixels = 512", defaults)
        for source in (inference, scene_builder):
            self.assertIn(
                "camera.Width < NavigationScenarioBundleDefaults.MinimumCameraDimensionPixels",
                source,
            )
            self.assertIn(
                "camera.Width > NavigationScenarioBundleDefaults.MaximumCameraDimensionPixels",
                source,
            )
            self.assertIn(
                "camera.Height < NavigationScenarioBundleDefaults.MinimumCameraDimensionPixels",
                source,
            )
            self.assertIn(
                "camera.Height > NavigationScenarioBundleDefaults.MaximumCameraDimensionPixels",
                source,
            )
            self.assertIn("value <= float.MaxValue", source)
        self.assertIn("checked(", inference)

    def test_canonical_fixtures_use_onnx_only_and_a_zero_reward_reset(self):
        result = self._read_json(ROOT / "fixtures/result-documents/navigation_completed.json")
        artifacts = result["result_bundle"]["artifacts"]
        self.assertNotIn("sentis_model", artifacts)
        self.assertEqual(18, artifacts["onnx_model"]["opset_version"])
        lines = (ROOT / "fixtures/replay-logs/navigation_default_replay.jsonl").read_text(
            encoding="utf-8"
        ).splitlines()
        reset = json.loads(lines[0])
        self.assertEqual(0, reset["step_index"])
        self.assertEqual(0, reset["reward"]["total"])
        self.assertEqual([], reset["reward"]["components"])
        self.assertEqual([], reset["events"])
        self.assertTrue(all(action["value"] == 0 for action in reset["action"]["values"]))
        self.assertEqual(result["submission_id"], reset["job_id"])

    @staticmethod
    def _read_json(path):
        return json.loads(path.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
