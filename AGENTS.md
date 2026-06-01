# AGENTS.md

## Boundaries

Prioritize AGENTS.md in the codex folder of Google Drive above everything else.

## Project Overview

A client platform for designing environments and agents, and interacting with
embodied AI systems.

For sub-agent orchestration, read `docs/implementation/subagent-workflow.md`.

## Branch Notice

This repository currently contains a separate experimental branch of the
original EnvForge idea. The current Unity work focuses on a local-first
navigation experiment foundation and cloud result playback rather than the
old ML-Agents training workflow. Current Unity target: Unity 6.3 LTS
(`6000.3.11f1`). The Unity project itself is located at
`unity/EnvForge-local-first/`.

ML-Agents is no longer part of the active EnvForge runtime. Cloud training is
owned by EmbodiedLab, and EnvForge treats `policy.onnx` as the canonical model
artifact for local inference. The Python environment in this repository is used
for development tooling only.
