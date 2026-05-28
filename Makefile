UV ?= uv
POWERSHELL ?= powershell.exe
UNITY_HUB ?= Unity Hub.exe
UNITY_EDITOR ?= Unity.exe
UNITY_PROJECT_PATH ?= unity\EnvForge-local-first
UNITY_LOG_FILE ?= unity-batchmode.log
UNITY_CHECK_SCRIPT ?= scripts\check-unity.ps1

.PHONY: local_setup lint_markdown lint_python lint check check_unity

local_setup:
	$(UV) sync --frozen --group dev

lint_markdown: local_setup
	$(UV) run pymarkdown scan --recurse --respect-gitignore README.md AGENTS.md docs

lint_python: local_setup
	$(UV) run ruff check python

lint: lint_markdown lint_python

check: lint

check_unity:
	$(POWERSHELL) -NoProfile -ExecutionPolicy Bypass -File "$(UNITY_CHECK_SCRIPT)" -UnityHub "$(UNITY_HUB)" -UnityEditor "$(UNITY_EDITOR)" -UnityProjectPath "$(UNITY_PROJECT_PATH)" -UnityLogFile "$(UNITY_LOG_FILE)"
