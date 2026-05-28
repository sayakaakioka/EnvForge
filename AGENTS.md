# AGENTS.md

## Boundaries

Prioritize AGENTS.md in the codex folder of Google Drive above everything else.

## Project Overview

A client platform for designing environments and agents, and interacting with embodied AI systems.

For sub-agent orchestration, read `docs/implementation/subagent-workflow.md`.

## Branch Notice

This repository currently contains a separate experimental branch of the original EnvForge idea. The current Unity work focuses on a local-first navigation experiment foundation rather than the original client/backend workflow.
Current Unity target: Unity 6.3 LTS (`6000.3.11f1`). ML-Agents is managed through `Packages/manifest.json` as `com.unity.ml-agents` `4.0.0`. The unity project itself is located at `unity/EnvForge-local-first/`.

The Python environment is managed by `pyproject.toml` and currently pins `mlagents==1.1.0`, which matches ML-Agents Release 23 / Unity package `com.unity.ml-agents` `4.0.0`. It also pins `torch~=2.2.1` to avoid newer PyTorch ONNX export paths that require `onnxscript`, which conflicts with the `onnx==1.15.0` dependency used by ML-Agents.
The `mlagents==1.1.0` package requires Python `>=3.10.1,<=3.10.12`.
