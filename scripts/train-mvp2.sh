#!/usr/bin/env bash
set -euo pipefail

cat <<'SUMMARY'
EnvForge training model: MVP 2 Visual Segmentation Navigation
Behavior: Mvp2VisualNavigation
Observations: RGB segmentation image 84x84 stack=1 + 4-value goal vector
Actions: continuous [v, omega]
Unity action mapping: v is clamped to [0, 1] and scaled by 2.0 before motor movement; omega remains normalized [-1, 1]
Episode limit: 1000 Unity agent steps
Training limit: 1500000 trainer steps
Trainer extension: none, ML-Agents standard PPO/simple visual encoder
SUMMARY

uv run mlagents-learn configs/ml-agents/mvp2-visual-navigation.yaml \
  --run-id mvp2-visual-navigation \
  --time-scale 10 \
  --force
