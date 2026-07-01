# EnvForge

EnvForge is a Unity client for designing navigation environments, submitting
scenario bundles to EmbodiedLab, and inspecting the returned model and replay
artifacts locally.

## Current Runtime

The active Unity project is:

```text
unity/EnvForge-local-first/
```

Current Unity target: Unity 6.3 LTS (`6000.3.11f1`).

The active runtime no longer uses Unity ML-Agents. Training is owned by
EmbodiedLab, and EnvForge treats `policy.onnx` as the canonical model artifact
for local inference. EnvForge is responsible for:

- editing the world size, boundary walls, user walls, start pose, and goal
- building a Scenario Bundle from the Unity scene
- submitting cloud training jobs to EmbodiedLab
- receiving job progress and result metadata
- downloading `policy.onnx` and Replay Bundle artifacts
- replaying train/eval trajectories in Unity
- running local ONNX Runtime inference against the current scene

The ONNX Runtime Unity files under `Assets/Plugins/ONNXRuntime/` are part of
the active path and should stay in the Unity project.

## Unity Workflow

1. Open `unity/EnvForge-local-first/` in Unity.
2. Open `Assets/Scenes/EnvForgeNavigationLab.unity`.
3. Enter Play mode.
4. Use the in-scene world editor and cloud panel to configure, submit, replay,
   and run local inference.

The scene is built around runtime scripts under:

```text
unity/EnvForge-local-first/Assets/Scripts/Navigation/
```

The current main groups are:

- `Automation`: unattended model evaluation, cloud job submission, screenshots,
  and world variants
- `Cloud`: EmbodiedLab API, artifact download, job history, and cloud UI
- `Contracts`: Scenario Bundle, Result Bundle, and Replay Bundle DTOs
- `Inference`: local ONNX Runtime inference
- `Replay`: Replay Bundle playback
- `Sensors`: segmentation preview and camera observation support

## Artifacts

EnvForge downloads cloud artifacts into Unity's
`Application.persistentDataPath`, not into the repository.

Result artifacts currently include:

- `policy.onnx`
- `replay/manifest.json`
- `replay/train/*.jsonl.gz`
- `replay/eval/*.jsonl.gz`

Replay is structured data, not video. EnvForge downloads the Replay Bundle
manifest, fetches the listed chunks, and loads the resulting timeline into the
Unity replay player.

## Development Commands

Unity checks and Windows builds are wrapped by PowerShell scripts:

```powershell
.\scripts\check-unity.ps1 -ResolveOnly
.\scripts\build-unity-windows.ps1
```

If `make` is available, the same helpers can be run through:

```powershell
make check_unity UNITY_RESOLVE_ONLY=1
make build_unity_windows
```

This repository no longer contains a Python or `uv` environment. Python
training, linting, and tests for the cloud runtime live in the separate
EmbodiedLab repository.

## Documentation

Project notes live under `docs/`.

- `docs/vision/` describes the product direction.
- `docs/implementation/` records current implementation phases and operational
  notes.

Old ML-Agents training notes and local Python trainer scaffolding have been
removed from the active repository. Use Git history if that context is needed.

## Public Repository Notes

Do not commit private endpoints, bearer tokens, signed URLs, or local
environment files. API endpoints should be configured through Unity settings or
runtime configuration, not hard-coded into source files.

## License

To be decided.
