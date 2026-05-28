# EnvForge

A client platform for designing environments and agents, and interacting with
embodied AI systems.

## Documentation Notes

Markdown files under `docs/` are primarily agent-facing project notes. They
record the overall design, implementation decisions, work history, and
phase-specific context so that coding agents and collaborators can resume work
with the right background.

## Branch Notice

This repository currently contains a separate experimental branch of the
original EnvForge idea described below. The current Unity work focuses on a
local-first navigation experiment foundation rather than the original
client/backend workflow.

The handoff-ready entry point is the final navigation demo. Earlier MVP scripts
are kept under `unity/EnvForge-local-first/Assets/Scripts/DevelopmentHistory/`
for reference, but new users should start from `NavigationBootstrap` in the
sample scene.

Current Unity target: Unity 6.3 LTS (`6000.3.11f1`). ML-Agents is managed
through `Packages/manifest.json` as `com.unity.ml-agents` `4.0.0`.

## Final Navigation Demo

The final local-first navigation demo is implemented under:

```text
unity/EnvForge-local-first/Assets/Scripts/Navigation/
```

It builds a small navigation task at runtime with:

- RGB segmentation camera observation, `84x112`, one frame stack - 2-value
  vector observation: normalized relative goal angle and distance - 2 continuous
  actions: forward speed and turn - a selectable ONNX inference model on the
  scene bootstrap - fallback to normal ML-Agents training mode when no model is
  assigned

Setup:

1. Open the Unity project at `unity/EnvForge-local-first/`. 2. Open
   `Assets/Scenes/SampleScene.unity`. 3. Keep `NavigationBootstrap` active. 4.
   Keep the historical `Mvp0Bootstrap`, `Mvp1Bootstrap`, and `Mvp2Bootstrap`
   inactive. 5. Enter Play mode.

By default, `NavigationBootstrap` has the bundled strict model assigned:

```text
unity/EnvForge-local-first/Assets/Resources/Models/NavigationFinal.onnx
```

To run a different model, assign another compatible `ModelAsset` to the
`Inference Model` field on `NavigationBootstrap`. To train instead of running
local inference, clear `Inference Model`, start the trainer, then enter Play
mode.

Windows Standalone is pinned to Direct3D 11 in Project Settings to avoid
Direct3D 12 render texture crashes observed on Snapdragon / Adreno machines.

Training from the WSL2 mirror:

```bash
cd ~/dev/EnvForge-mlagents

ENVFORGE_ROOT=/mnt/c/path/to/EnvForge

cp "$ENVFORGE_ROOT/configs/ml-agents/navigation-final.yaml" configs/ml-agents/
cp "$ENVFORGE_ROOT/scripts/train-navigation-final.sh" scripts/
cp -r "$ENVFORGE_ROOT/python/envforge_mlagents" python/

bash scripts/train-navigation-final.sh
```

The expected successful connection log includes:

```text
Connected to Unity environment with package version 4.0.0
Connected new brain: NavigationFinal?team=0
```

## Development History

The MVP folders are preserved as development history, not as the recommended
handoff entry point:

- `DevelopmentHistory/Mvp0`: manual runtime navigation prototype -
  `DevelopmentHistory/Mvp1`: minimal ML-Agents vector-observation adapter -
  `DevelopmentHistory/Mvp2`: visual segmentation observation path -
  `DevelopmentHistory/Mvp3`: custom CNN / strict trainer scaffold before final
  naming cleanup

### MVP 0 Manual Navigation

The current MVP 0 prototype builds a small navigation scene at runtime. It
creates the floor, walls, agent capsule, floating goal marker, direction arrow,
and runtime episode helpers from code.

Setup:

1. Open the Unity project at `unity/EnvForge-local-first/`. 2. Open the sample
   scene. 3. Create an empty GameObject named `Mvp0Bootstrap`. 4. Attach the
   `Mvp0SceneBuilder` component to `Mvp0Bootstrap`. 5. Enter Play mode.

Controls:

- `W`: move forward - `A` / `D`: turn left / right - `R`: reset the current
  episode

The Console logs episode resets, successes, failures, manual resets,
success/failure counts, debug navigation metrics, and MVP 0.1 vector observation
values such as raw and normalized distance/angle to the goal.

### MVP 1 Minimal ML-Agents Adapter

MVP 1 adds a separate runtime scene builder for the ML-Agents path. It does not
add a mode switch to `Mvp0SceneBuilder`.

Setup:

1. Open the Unity project at `unity/EnvForge-local-first/`. 2. Open the sample
   scene. 3. Create an empty GameObject named `Mvp1Bootstrap`. 4. Attach the
   `Mvp1SceneBuilder` component to `Mvp1Bootstrap`. 5. Enter Play mode after
   Unity resolves the ML-Agents package.

`Mvp1SceneBuilder` creates an agent with `NavigationAgent`,
`BehaviorParameters`, and `DecisionRequester`. The behavior name is
`Mvp1Navigation`, with 4 vector observations and 2 continuous actions.

Training from WSL2 with `uv`:

1. Install `uv` in WSL2. 2. Install the Python version required by ML-Agents:
   `uv python install 3.10.12`. 3. From the repository root, run `uv sync
   --python 3.10.12`. 4. Check the trainer command with `uv run mlagents-learn
   --help`. 5. Run `bash scripts/train-mvp1.sh`. 6. When ML-Agents waits for
   Unity, enter Play mode in the Unity Editor with `Mvp1Bootstrap` active.

The Python environment is managed by `pyproject.toml` and currently pins
`mlagents==1.1.0`, which matches ML-Agents Release 23 / Unity package
`com.unity.ml-agents` `4.0.0`. It also pins `torch~=2.2.1` to avoid newer
PyTorch ONNX export paths that require `onnxscript`, which conflicts with the
`onnx==1.15.0` dependency used by ML-Agents. The `mlagents==1.1.0` package
requires Python `>=3.10.1,<=3.10.12`.

The expected successful connection log includes:

```text
Connected to Unity environment with package version 4.0.0
Connected new brain: Mvp1Navigation?team=0
```

During the first training runs, random movement, wandering, and wall collisions
are expected. The initial success criterion for MVP 1 is that Python connects to
Unity, the `Mvp1Navigation` behavior is recognized, and training steps advance.

If a `.venv` was already created with a different Python version, remove it and
sync again:

```bash
rm -rf .venv
uv sync --python 3.10.12
```

For faster local development, the ML-Agents Python side can be mirrored into the
WSL2 filesystem instead of running `uv` directly under `/mnt/c`. The Unity
project remains on Windows, while the trainer runs from a lightweight WSL2
mirror that contains only the Python and trainer files:

```bash
mkdir -p ~/dev/EnvForge-mlagents
cd ~/dev/EnvForge-mlagents

ENVFORGE_ROOT=/mnt/c/path/to/EnvForge

cp "$ENVFORGE_ROOT/pyproject.toml" .
cp "$ENVFORGE_ROOT/uv.lock" .
mkdir -p configs scripts
cp -r "$ENVFORGE_ROOT/configs/ml-agents" configs/
cp "$ENVFORGE_ROOT/scripts/train-mvp1.sh" scripts/

uv python install 3.10.12
uv sync --python 3.10.12
```

When trainer configs or scripts change on the Windows-side repository, copy the
updated files into this WSL2 mirror before running training.

### MVP 2 Visual Segmentation Observation

MVP 2 adds a separate visual-observation training path. It keeps
`Mvp1SceneBuilder` intact and uses `Mvp2SceneBuilder` with the
`Mvp2VisualNavigation` behavior.

`Mvp2SceneBuilder` creates the same small navigation task as MVP 1, then adds a
front-facing segmentation camera. The camera observes an `84x84` color image
with one frame of image history:

- passable area: green - blocked area: blue - goal: excluded from the
  segmentation image - vector observation: the same 4 goal distance/angle values
  used by MVP 1 - actions: 2 continuous actions, forward speed and turn

The action outputs are continuous values. Unity clamps the forward velocity
command `v` to `[0, 1]`, scales it by `2.0` before applying movement, and does
not allow reverse movement. `omega` remains a normalized angular velocity
command in `[-1, 1]`. Each Unity episode is capped at 1000 agent steps, and the
trainer configs run for 1,500,000 total steps.

The agent start position and yaw are randomized at the beginning of each
episode. The sampler avoids collider overlaps when possible and falls back to
the default start if no clear candidate is found.

The visual observation and vector observation are used together. The image is
responsible for near-field traversability, while the vector observation tells
the policy where the goal is.

The runtime map includes a few short inner walls to create mild route variation
without making the smoke-test environment overly difficult. The overhead
direction arrow is parented to the agent and uses an asymmetric arrow shape that
points along the robot's local forward direction.

Setup:

1. Open the Unity project at `unity/EnvForge-local-first/`. 2. Open the sample
   scene. 3. Create an empty GameObject named `Mvp2Bootstrap`. 4. Attach the
   `Mvp2SceneBuilder` component to `Mvp2Bootstrap`. 5. Keep the MVP 0 and MVP 1
   bootstrap objects disabled while running MVP 2. 6. Enter Play mode after the
   trainer starts.

Training from the WSL2 mirror:

```bash
cd ~/dev/EnvForge-mlagents

ENVFORGE_ROOT=/mnt/c/path/to/EnvForge

cp \
  "$ENVFORGE_ROOT/configs/ml-agents/mvp2-visual-navigation.yaml" \
  configs/ml-agents/
cp "$ENVFORGE_ROOT/scripts/train-mvp2.sh" scripts/

bash scripts/train-mvp2.sh
```

The expected successful connection log includes:

```text
Connected to Unity environment with package version 4.0.0
Connected new brain: Mvp2VisualNavigation?team=0
```

MVP 2 also includes a segmentation frame capture mode for debugging. It is
disabled by default and saves frames under
`Application.persistentDataPath/SegmentationCaptures` when enabled. The
segmentation camera is also rendered as a small lower-right Game View debug
overlay by default, so the camera image being fed into learning can be inspected
while training runs.

ML-Agents timer files under
`unity/EnvForge-local-first/Assets/ML-Agents/Timers/` are generated runtime logs
and are ignored by Git.

### MVP 3 Custom CNN Navigation Scaffold

MVP 3 adds a separate training path for the architecture documented in
`docs/implementation/mvp-3-custom-cnn-policy.md`. It keeps MVP 2 intact and uses
`Mvp3SceneBuilder` with the `Mvp3CustomCnnNavigation` behavior.

The Unity observation shape is adjusted to match the documented network shape:

- visual observation: RGB segmentation camera, `84x112`, one frame stack -
  passable area: green - blocked area: blue - goal: excluded from the
  segmentation image - vector observation: 2 values, normalized relative angle
  and normalized relative distance - actions: 2 continuous actions, forward
  speed and turn

The action outputs are continuous values. Unity clamps the forward velocity
command `v` to `[0, 1]`, scales it by `2.0` before applying movement, and does
not allow reverse movement. `omega` remains a normalized angular velocity
command in `[-1, 1]`. Each Unity episode is capped at 1000 agent steps, and the
MVP 3 trainer configs run for 1,500,000 total steps.

The agent start position and yaw are randomized at the beginning of each
episode. The sampler avoids collider overlaps when possible and falls back to
the default start if no clear candidate is found.

The runtime map includes the same mild route variation used by MVP 2, and the
overhead direction arrow points along the robot's local forward direction.

ML-Agents 1.1.0 already provides a `simple` visual encoder that matches the
documented image branch: `Conv(3->16, kernel=8, stride=4)`, `LeakyReLU`,
`Conv(16->32, kernel=4, stride=2)`, `LeakyReLU`, `Flatten`, and `FC(256)`. With
an `84x112` image, the flattened feature size is `3456`. The initial MVP 3
trainer therefore uses the standard ML-Agents network rather than a forked
trainer. The strict trainer adds the explicit sigmoid gate and strict action
mapping.

Setup:

1. Open the Unity project at `unity/EnvForge-local-first/`. 2. Open the sample
   scene. 3. Create an empty GameObject named `Mvp3Bootstrap`. 4. Attach the
   `Mvp3SceneBuilder` component to `Mvp3Bootstrap`. 5. Keep the MVP 0, MVP 1,
   and MVP 2 bootstrap objects disabled while running MVP 3. 6. Enter Play mode
   after the trainer starts.

Local ONNX inference:

`Mvp3SceneBuilder` can run an exported MVP 3 ONNX model directly in Unity.
Assign the desired model to the `Inference Model` field on `Mvp3Bootstrap`. If
`Inference Model` is empty, `Mvp3SceneBuilder` leaves the generated
`BehaviorParameters` in the default ML-Agents mode, so Python training can
connect as usual.

The historical strict MVP 3 model is stored at:

```text
unity/EnvForge-local-first/Assets/Resources/DevelopmentHistoryModels/Mvp3CustomCnnNavigation.onnx
```

When `Inference Model` is assigned, `Mvp3SceneBuilder` enables deterministic
inference and switches the generated `BehaviorParameters` to `InferenceOnly`.
Keep the MVP 0, MVP 1, and MVP 2 bootstrap objects disabled when checking the
robot with a selected model. To train from Python again, clear the `Inference
Model` field and use the normal trainer connection flow.

The imported strict model was copied from the WSL2 training output:

```text
~/src/ml-agents/results/mvp3-strict-custom-cnn-navigation/Mvp3CustomCnnNavigation.onnx
```

The model was exported at the final strict checkpoint, `1500001` steps, with
`obs_0: batch x 3 x 84 x 112`, `obs_1: batch x 2`, and `continuous_actions:
batch x 2`.

Training from the WSL2 mirror:

```bash
cd ~/dev/EnvForge-mlagents

ENVFORGE_ROOT=/mnt/c/path/to/EnvForge
SRC="$ENVFORGE_ROOT/configs/ml-agents"
cp \
  "$SRC/mvp3-custom-cnn-navigation.yaml" \
  configs/ml-agents/
cp "$ENVFORGE_ROOT/scripts/train-mvp3.sh" scripts/

bash scripts/train-mvp3.sh
```

The expected successful connection log includes:

```text
Connected to Unity environment with package version 4.0.0
Connected new brain: Mvp3CustomCnnNavigation?team=0
```

Strict navigation trainer:

The strict trainer keeps the same Unity behavior name, but starts ML-Agents
through a local Python wrapper that replaces the default ML-Agents `NetworkBody`
with the documented strict navigation body. This adds the explicit `FC -> x *
sigmoid(x)` gate layers while keeping the standard ML-Agents PPO action
distribution and ONNX export contract.

The strict wrapper also replaces the actor `ActionModel` output mapping. It
keeps the Gaussian policy distribution, then maps `v = sigmoid(raw_v)` so the
forward speed is produced in `[0, 1]` by the strict model. It maps `omega =
clip(raw_omega, -3, 3) / 3`, preserving the normalized angular velocity range
`[-1, 1]`. The same mapping is used for actions sent to Unity during training
and for ONNX forward outputs.

During strict training, the wrapper prints the RGB channel means, minimum value,
and maximum value for the actual visual observation tensor received by the
network. It prints on the first real forward pass and then every 1000 forward
passes by default. Override the interval with `ENVFORGE_IMAGE_STATS_INTERVAL`
when denser or quieter debugging is needed.

Copy the strict trainer files into the WSL2 mirror:

```bash
cd ~/dev/EnvForge-mlagents

mkdir -p python configs/ml-agents scripts
ENVFORGE_ROOT=/mnt/c/path/to/EnvForge
cp -r "$ENVFORGE_ROOT/python/envforge_mlagents" python/
SRC="$ENVFORGE_ROOT/configs/ml-agents"
cp \
  "$SRC/mvp3-strict-custom-cnn-navigation.yaml" \
  configs/ml-agents/
cp "$ENVFORGE_ROOT/scripts/train-mvp3-strict.sh" scripts/
```

Run:

```bash
bash scripts/train-mvp3-strict.sh
```

## Overview

EnvForge is a Unity client for creating, configuring, and visualizing grid-based
environments and agents used in embodied AI systems.

It is designed to provide an intuitive interface for users to define scenarios,
interact with learning systems, and observe the resulting behaviors.

This repository is currently in an early prototyping phase. The initial goal is
to establish a minimal end-to-end pipeline with a backend system such as
EmbodiedLab.

## Goals

- Enable users to define simple environments and agents - Send map and training
  configurations to a backend system - Start training jobs from the Unity UI -
  Receive training status updates through HTTP and WebSocket - Download trained
  model artifacts - Visualize learned robot behavior in Unity - Provide a
  foundation for interactive embodied AI applications

## Scope (Initial Phase)

- Minimal scene setup (basic geometry) - Grid editing for obstacles, robot
  start, and goal - JSON submission for environment, robot, and training
  settings - HTTP API calls for submit, train, and result retrieval - WebSocket
  notifications for queued, starting, running, completed, and failed states -
  Model artifact download into Unity persistent storage - Local ONNX Runtime
  inference for moving the robot in the Unity scene

## Non-Goals (for now)

- Advanced 3D editing features - Complex UI/UX design - Full asset pipeline
  support - Production-ready application - Public endpoint abuse protection
  inside the Unity client

## Relationship to EmbodiedLab

EnvForge serves as a client interface for EmbodiedLab, which handles simulation,
learning, and evaluation on the server side.

## Status

🚧 Work in progress — early-stage prototyping.

## Repository Structure

- `unity/EnvForge/` : Unity project - `docs/` : API and design notes

## API Settings

API endpoints are configured in Unity through an `ApiSettings` ScriptableObject,
not hard-coded in source files.

Create one in the Unity Editor:

1. Open the Unity project at `unity/EnvForge/`. 2. In the Project window,
   right-click inside `Assets/Settings`. 3. Select `Create > EnvForge > API
   Settings`. 4. Fill in: - `Base Url` - `WebSocket Url Template` - `Model
   Download Directory Name` 5. Assign the created asset to the `Api Settings`
   field on the scene's `TestRunner` component.

The WebSocket URL template should include `{submission_id}` where the runtime
submission ID should be inserted.

Example shape:

```text
wss://example.com/ws/results/{submission_id}
```

## Public Repository Notes

The real `ApiSettings.asset` is treated as local configuration and is ignored by
Git:

```text
unity/EnvForge/Assets/Settings/ApiSettings.asset
unity/EnvForge/Assets/Settings/ApiSettings.asset.meta
```

This avoids publishing live backend endpoints before the server has
authentication, rate limits, quotas, or other abuse protection. Do not put API
keys, bearer tokens, signed URLs, or private endpoints in committed Unity
assets.

## Model Downloads

When training completes, EnvForge downloads the model artifact described by the
result JSON. Files are saved below:

```text
Application.persistentDataPath/Models/
```

The configured `Model Download Directory Name` controls the `Models` folder
name. Displayed paths are normalized to use `/` separators.

After an `.onnx` model is downloaded, EnvForge uses ONNX Runtime Unity to run
the policy locally and move a robot from the configured start position toward
the goal. The current player supports coordinate-based observations and
configurable action ordering through the `RobotPolicyPlayer` component.

## License

(To be decided)
