# Thin wrappers so humans and CI run the same commands.
SHELL      := /usr/bin/env bash
DOTNET     ?= dotnet
CONFIG     ?= Release
API        := src/AilabTemplate.Api/AilabTemplate.Api.csproj
EVAL       := tools/AilabTemplate.Eval/AilabTemplate.Eval.csproj
THRESHOLD  ?= 0.80
IMAGE      ?= ailab-template-dotnet:local
PORT       ?= 8080
COMMIT     := $(shell git rev-parse HEAD 2>/dev/null)

# Host runtime identifier for native AOT (osx-arm64, linux-x64, ...).
UNAME_S := $(shell uname -s)
UNAME_M := $(shell uname -m)
ifeq ($(UNAME_S),Darwin)
  RID_OS := osx
  # Homebrew's .NET links AOT binaries against Homebrew OpenSSL/Brotli; let the linker find them.
  export LIBRARY_PATH := $(if $(LIBRARY_PATH),$(LIBRARY_PATH):)/opt/homebrew/opt/openssl@3/lib:/opt/homebrew/opt/brotli/lib
else
  RID_OS := linux
endif
ifneq ($(filter $(UNAME_M),arm64 aarch64),)
  RID_ARCH := arm64
else
  RID_ARCH := x64
endif
RID ?= $(RID_OS)-$(RID_ARCH)

.PHONY: help restore build format format-check test eval publish-aot run hooks scan docker-build docker-run clean

help: ## List targets
	@grep -E '^[a-z-]+:.*## ' $(MAKEFILE_LIST) | awk -F':.*## ' '{printf "  %-14s %s\n", $$1, $$2}'

restore: ## Restore NuGet packages
	$(DOTNET) restore

build: restore ## Build everything (warnings are errors)
	$(DOTNET) build -c $(CONFIG) --no-restore

# IL2026/IL3050: dotnet format does not run the Request Delegate Generator and reports false AOT
# warnings on MapPost; build and publish-aot still enforce them as errors.
FORMAT_ARGS := --exclude-diagnostics IL2026 IL3050

format: ## Apply dotnet format
	$(DOTNET) format $(FORMAT_ARGS)

format-check: ## Verify formatting/style without changing files (what CI runs)
	$(DOTNET) format --verify-no-changes $(FORMAT_ARGS)

test: build ## Run unit + integration tests
	$(DOTNET) test -c $(CONFIG) --no-build

eval: ## Run the offline eval gate over fixtures/ (fails under THRESHOLD)
	$(DOTNET) run --project $(EVAL) -c $(CONFIG) -- --fixture fixtures/score_eval.jsonl --out eval_results.json --threshold $(THRESHOLD) --commit "$(COMMIT)"

publish-aot: ## Native AOT publish for this host into artifacts/publish/$(RID)
	$(DOTNET) publish $(API) -c Release -r $(RID) -o artifacts/publish/$(RID)
	@ls -lh artifacts/publish/$(RID)/AilabTemplate.Api

run: ## Run the API locally (set AILAB_API_TOKEN to enable /v1/score)
	PORT=$(PORT) $(DOTNET) run --project $(API) -c $(CONFIG)

hooks: ## Enable the repo's git hooks (pre-push secret wall)
	git config core.hooksPath .githooks
	@echo "hooks: core.hooksPath=.githooks (pre-push runs gitleaks + denylist scan)"

scan: ## Secret wall: gitleaks over full history + denylist over tree and history
	gitleaks git --redact --no-banner --config .gitleaks.toml
	scripts/denylist_scan.sh

docker-build: ## Build the AOT container image
	docker build -t $(IMAGE) .

docker-run: ## Run the image (needs AILAB_API_TOKEN in the environment)
	docker run --rm -p $(PORT):8080 -e AILAB_API_TOKEN --read-only --cap-drop ALL $(IMAGE)

clean: ## Remove build output
	$(DOTNET) clean -c $(CONFIG)
	rm -rf artifacts eval_results.json
