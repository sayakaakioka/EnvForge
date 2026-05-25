#!/usr/bin/env bash
set -euo pipefail

cat <<'SUMMARY'
EnvForge training model: MVP 3 Custom CNN Navigation
Behavior: Mvp3CustomCnnNavigation
Observations: RGB segmentation image 84x112 stack=1 + 2-value goal vector
Actions: continuous [v, omega]
Unity action mapping: v is clamped to [0, 1] and scaled by 2.0 before motor movement; omega remains normalized [-1, 1]
Episode limit: 1000 Unity agent steps
Training limit: 1500000 trainer steps
Trainer extension: none, ML-Agents standard PPO/simple visual encoder
SUMMARY

uv run mlagents-learn configs/ml-agents/mvp3-custom-cnn-navigation.yaml \
  --run-id mvp3-custom-cnn-navigation \
  --time-scale 10 \
  --force
