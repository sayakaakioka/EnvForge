# EnvForge

A client platform for designing environments and agents, and interacting with embodied AI systems.

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

## License

(To be decided)
