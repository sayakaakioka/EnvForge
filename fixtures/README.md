# Contract fixtures

These four fixtures mirror the canonical EmbodiedLab v0 examples in
`EmbodiedLab.Unity` commit `51af750807626c15da6bf873f6d010f7a78e6fa1`
(ONNX-only result contract and zero-reward replay reset).

- `scenario-bundles/navigation_default.json`
- `replay-logs/navigation_default_replay.jsonl`
- `replay-bundles/navigation_default_manifest.json`
- `result-documents/navigation_completed.json`

Update these fixtures with the package pin and verify that their bytes still
match the SDK fixtures. EnvForge does not maintain a second contract definition.
