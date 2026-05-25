#!/usr/bin/env bash
set -euo pipefail

cat <<'SUMMARY'
EnvForge training model: Navigation Final
Behavior: NavigationFinal
Observations: RGB segmentation image 84x112 stack=1 + 2-value goal vector
Actions: continuous [v, omega]
Strict model action mapping: v = sigmoid(raw_v) in [0, 1]; omega = clip(raw_omega, -3, 3) / 3 in [-1, 1]
Unity action mapping: v is scaled by 2.0 before motor movement; omega remains normalized [-1, 1]
Episode limit: 1000 Unity agent steps
Training limit: 1500000 trainer steps
Trainer extension: envforge_mlagents.navigation_strict replaces ML-Agents NetworkBody and ActionModel
SUMMARY

PYTHONPATH="python${PYTHONPATH:+:$PYTHONPATH}" \
  uv run python -m envforge_mlagents.navigation_strict.train \
  configs/ml-agents/navigation-final.yaml \
  --run-id navigation-final \
  --time-scale 10 \
  --force
