UV ?= uv

.PHONY: local_setup lint_markdown lint check

local_setup:
	$(UV) sync --frozen --group dev

lint_markdown: local_setup
	$(UV) run pymarkdown scan --recurse --respect-gitignore README.md AGENTS.md docs

lint: lint_markdown

check: lint
