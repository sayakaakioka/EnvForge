# EnvForge

A client platform for designing environments and agents, and interacting with embodied AI systems.

## Branch Notice

This repository currently contains a separate experimental branch of the original EnvForge idea described below. The current Unity work focuses on a local-first navigation experiment foundation rather than the original client/backend workflow.

To run the current MVP 0 scene, open the Unity project at `unity/EnvForge-local-first/`, create an empty GameObject in the scene, and attach the `Mvp0SceneBuilder` component to it. A good name for this GameObject is `Mvp0Bootstrap`.

Current Unity target: Unity 6.3 LTS (`6000.3.11f1`). ML-Agents is managed through `Packages/manifest.json` as `com.unity.ml-agents` `4.0.0`.

### MVP 0 Manual Navigation

The current MVP 0 prototype builds a small navigation scene at runtime. It creates the floor, walls, agent capsule, floating goal marker, direction arrow, and runtime episode helpers from code.

Setup:

1. Open the Unity project at `unity/EnvForge-local-first/`.
2. Open the sample scene.
3. Create an empty GameObject named `Mvp0Bootstrap`.
4. Attach the `Mvp0SceneBuilder` component to `Mvp0Bootstrap`.
5. Enter Play mode.

Controls:

- `W` / `S`: move forward / backward
- `A` / `D`: turn left / right
- `R`: reset the current episode

The Console logs episode resets, successes, failures, manual resets, success/failure counts, debug navigation metrics, and MVP 0.1 vector observation values such as raw and normalized distance/angle to the goal.

### MVP 1 Minimal ML-Agents Adapter

MVP 1 adds a separate runtime scene builder for the ML-Agents path. It does not add a mode switch to `Mvp0SceneBuilder`.

Setup:

1. Open the Unity project at `unity/EnvForge-local-first/`.
2. Open the sample scene.
3. Create an empty GameObject named `Mvp1Bootstrap`.
4. Attach the `Mvp1SceneBuilder` component to `Mvp1Bootstrap`.
5. Enter Play mode after Unity resolves the ML-Agents package.

`Mvp1SceneBuilder` creates an agent with `NavigationAgent`, `BehaviorParameters`, and `DecisionRequester`. The behavior name is `Mvp1Navigation`, with 4 vector observations and 2 continuous actions.

Training from WSL2 with `uv`:

1. Install `uv` in WSL2.
2. Install the Python version required by ML-Agents: `uv python install 3.10.12`.
3. From the repository root, run `uv sync --python 3.10.12`.
4. Check the trainer command with `uv run mlagents-learn --help`.
5. Run `bash scripts/train-mvp1.sh`.
6. When ML-Agents waits for Unity, enter Play mode in the Unity Editor with `Mvp1Bootstrap` active.

The Python environment is managed by `pyproject.toml` and currently pins `mlagents==1.1.0`, which matches ML-Agents Release 23 / Unity package `com.unity.ml-agents` `4.0.0`. It also pins `torch~=2.2.1` to avoid newer PyTorch ONNX export paths that require `onnxscript`, which conflicts with the `onnx==1.15.0` dependency used by ML-Agents.
The `mlagents==1.1.0` package requires Python `>=3.10.1,<=3.10.12`.

The expected successful connection log includes:

```text
Connected to Unity environment with package version 4.0.0
Connected new brain: Mvp1Navigation?team=0
```

During the first training runs, random movement, wandering, and wall collisions are expected. The initial success criterion for MVP 1 is that Python connects to Unity, the `Mvp1Navigation` behavior is recognized, and training steps advance.

If a `.venv` was already created with a different Python version, remove it and sync again:

```bash
rm -rf .venv
uv sync --python 3.10.12
```

## Overview

EnvForge is a Unity client for creating, configuring, and visualizing grid-based environments and agents used in embodied AI systems.

It is designed to provide an intuitive interface for users to define scenarios, interact with learning systems, and observe the resulting behaviors.

This repository is currently in an early prototyping phase. The initial goal is to establish a minimal end-to-end pipeline with a backend system such as EmbodiedLab.

## Goals

- Enable users to define simple environments and agents
- Send map and training configurations to a backend system
- Start training jobs from the Unity UI
- Receive training status updates through HTTP and WebSocket
- Download trained model artifacts
- Visualize learned robot behavior in Unity
- Provide a foundation for interactive embodied AI applications

## Scope (Initial Phase)

- Minimal scene setup (basic geometry)
- Grid editing for obstacles, robot start, and goal
- JSON submission for environment, robot, and training settings
- HTTP API calls for submit, train, and result retrieval
- WebSocket notifications for queued, starting, running, completed, and failed states
- Model artifact download into Unity persistent storage
- Local ONNX Runtime inference for moving the robot in the Unity scene

## Non-Goals (for now)

- Advanced 3D editing features
- Complex UI/UX design
- Full asset pipeline support
- Production-ready application
- Public endpoint abuse protection inside the Unity client

## Relationship to EmbodiedLab

EnvForge serves as a client interface for EmbodiedLab, which handles simulation, learning, and evaluation on the server side.

## Status

🚧 Work in progress — early-stage prototyping.

## Repository Structure

- `unity/EnvForge/` : Unity project
- `docs/` : API and design notes

## API Settings

API endpoints are configured in Unity through an `ApiSettings` ScriptableObject, not hard-coded in source files.

Create one in the Unity Editor:

1. Open the Unity project at `unity/EnvForge/`.
2. In the Project window, right-click inside `Assets/Settings`.
3. Select `Create > EnvForge > API Settings`.
4. Fill in:
   - `Base Url`
   - `WebSocket Url Template`
   - `Model Download Directory Name`
5. Assign the created asset to the `Api Settings` field on the scene's `TestRunner` component.

The WebSocket URL template should include `{submission_id}` where the runtime submission ID should be inserted.

Example shape:

```text
wss://example.com/ws/results/{submission_id}
```

## Public Repository Notes

The real `ApiSettings.asset` is treated as local configuration and is ignored by Git:

```text
unity/EnvForge/Assets/Settings/ApiSettings.asset
unity/EnvForge/Assets/Settings/ApiSettings.asset.meta
```

This avoids publishing live backend endpoints before the server has authentication, rate limits, quotas, or other abuse protection. Do not put API keys, bearer tokens, signed URLs, or private endpoints in committed Unity assets.

## Model Downloads

When training completes, EnvForge downloads the model artifact described by the result JSON. Files are saved below:

```text
Application.persistentDataPath/Models/
```

The configured `Model Download Directory Name` controls the `Models` folder name. Displayed paths are normalized to use `/` separators.

After an `.onnx` model is downloaded, EnvForge uses ONNX Runtime Unity to run the policy locally and move a robot from the configured start position toward the goal. The current player supports coordinate-based observations and configurable action ordering through the `RobotPolicyPlayer` component.

## License

(To be decided)
