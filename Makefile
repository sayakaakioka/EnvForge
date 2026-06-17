UV ?= uv
POWERSHELL ?= powershell.exe
UNITY_HUB ?= Unity Hub.exe
UNITY_EDITOR ?= Unity.exe
UNITY_PROJECT_PATH ?= unity\EnvForge-local-first
UNITY_LOG_FILE ?= unity-batchmode.log
UNITY_CHECK_SCRIPT ?= scripts\check-unity.ps1
UNITY_BUILD_SCRIPT ?= scripts\build-unity-windows.ps1
UNITY_BUILD_OUTPUT ?= artifacts\builds\windows\EnvForge.exe
UNITY_BUILD_LOG_FILE ?= unity-build-windows.log
UNITY_CHECK_TIMEOUT ?= 240
UNITY_BUILD_TIMEOUT ?= 900
UNITY_INITIALIZE_HUB ?= 0
UNITY_RESOLVE_ONLY ?= 0
UNITY_RUN_BATCHMODE ?= 0

.PHONY: local_setup lint_markdown lint_python lint check check_unity build_unity_windows

local_setup:
	$(UV) sync --frozen --group dev

lint_markdown: local_setup
	$(UV) run pymarkdown scan --recurse --respect-gitignore README.md AGENTS.md docs

lint_python: local_setup
	$(UV) run ruff check python

lint: lint_markdown lint_python

check: lint

check_unity:
	$(POWERSHELL) -NoProfile -ExecutionPolicy Bypass -File "$(UNITY_CHECK_SCRIPT)" -UnityHub "$(UNITY_HUB)" -UnityEditor "$(UNITY_EDITOR)" -UnityProjectPath "$(UNITY_PROJECT_PATH)" -UnityLogFile "$(UNITY_LOG_FILE)" -TimeoutSeconds "$(UNITY_CHECK_TIMEOUT)" $(if $(filter 1 true TRUE,$(UNITY_INITIALIZE_HUB)),-InitializeUnityHub,) $(if $(filter 1 true TRUE,$(UNITY_RESOLVE_ONLY)),-ResolveOnly,) $(if $(filter 1 true TRUE,$(UNITY_RUN_BATCHMODE)),-RunBatchmode,)

build_unity_windows:
	$(POWERSHELL) -NoProfile -ExecutionPolicy Bypass -File "$(UNITY_BUILD_SCRIPT)" -UnityHub "$(UNITY_HUB)" -UnityEditor "$(UNITY_EDITOR)" -UnityProjectPath "$(UNITY_PROJECT_PATH)" -BuildOutputPath "$(UNITY_BUILD_OUTPUT)" -UnityLogFile "$(UNITY_BUILD_LOG_FILE)" -TimeoutSeconds "$(UNITY_BUILD_TIMEOUT)" $(if $(filter 1 true TRUE,$(UNITY_INITIALIZE_HUB)),-InitializeUnityHub,)
