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
for local inference. Reusable cloud transport and contract handling come from
the `com.embodiedlab.unity` package. EnvForge is responsible for:

- editing the world size, boundary walls, user walls, start pose, and goal
- building a Scenario Bundle from the Unity scene
- presenting cloud job controls and maintaining local job history
- replaying train/eval trajectories in Unity
- running local ONNX Runtime inference against the current scene

`EmbodiedLab.Unity` submits and starts jobs, monitors progress over WebSocket,
performs explicit HTTP resynchronization, cancels jobs, serializes the shared
contracts, downloads model and replay artifacts, and distributes the tested
ONNX Runtime managed and Windows x64 native binaries. EnvForge owns the
navigation-specific inference behavior and UI without keeping a second local
plugin copy.

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
- `Cloud`: endpoint settings, EnvForge job history, and cloud UI
- `Contracts`: EnvForge scenario defaults, layout, and scene-to-contract builder
- `Inference`: local ONNX Runtime inference
- `Replay`: Replay Bundle playback
- `Sensors`: segmentation preview and camera observation support

The shared Scenario, Result, and Replay contract types and the HTTP/WebSocket
clients live in the separate
[`EmbodiedLab.Unity`](https://github.com/sayakaakioka/EmbodiedLab.Unity)
repository. This project pins the package to a tested commit in
`Packages/manifest.json`.

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

Do not commit bearer tokens, cancellation capability tokens, signed URLs,
private endpoint assets, or local environment files. Public deployment
endpoints may be serialized in the scene or supplied through Unity settings and
command-line configuration.

## License

To be decided.
