#!/usr/bin/env bash
set -euo pipefail

uv run mlagents-learn configs/ml-agents/mvp1-navigation.yaml \
  --run-id mvp1-navigation \
  --time-scale 20 \
  --force
