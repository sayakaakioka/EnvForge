#!/usr/bin/env bash
set -euo pipefail

cat <<'SUMMARY'
EnvForge training model: MVP 1 Navigation
Behavior: Mvp1Navigation
Observations: 4-value goal vector
Actions: continuous [v, omega]
Unity action mapping: v is clamped to [0, 1] and scaled by 2.0 before motor movement; omega remains normalized [-1, 1]
Episode limit: 1000 Unity agent steps
Training limit: 1500000 trainer steps
Trainer extension: none, ML-Agents standard PPO
SUMMARY

uv run mlagents-learn configs/ml-agents/mvp1-navigation.yaml \
  --run-id mvp1-navigation \
  --time-scale 20 \
  --force
