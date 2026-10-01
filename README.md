# ailab-template-dotnet

[![ci](https://github.com/apyle0710/ailab-template-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/apyle0710/ailab-template-dotnet/actions/workflows/ci.yml)

A small, production-shaped template for **AI lab sidecars in C#**: a narrow, stateless HTTP
service that a host application calls to classify or score something. It ships as a
**Native AOT** binary in a **chiseled, non-root** container (a 24 MB image that uses roughly
10 to 30 MB of memory), with an offline eval gate and a secret-scanning wall that runs both locally and in CI.

The scoring logic here is a deliberately trivial keyword baseline. The point of the template is
everything around it: the HTTP contract, health probes, auth, config, logging, tests, the eval
regression gate, and the build and supply-chain hygiene. A real lab swaps in a model behind the
same `IScorer` interface and keeps the rest.

## Use as a template

1. On GitHub, choose **Use this template** (or `gh repo create my-lab --template apyle0710/ailab-template-dotnet`).
2. Rename the solution and projects (`AilabTemplate.*`) to your lab's name.
3. Replace `KeywordScorer` with your implementation of `IScorer`, and replace
   `fixtures/score_eval.jsonl` with a public or synthetic dataset (see [FIXTURES.md](FIXTURES.md)).
4. Run `make hooks`, create your private denylist (see [Secret scanning](#secret-scanning)), and
   add the `AILAB_DENYLIST` repository secret before your first push.
5. Record your first eval run in the [Eval results](#eval-results) table.

## Quickstart

Requires the .NET 9 SDK (pinned by `global.json`), `make`, and optionally Docker and gitleaks.

```bash
make test                                   # build (warnings are errors) + 32 unit/integration tests
make eval                                   # offline eval gate, writes eval_results.json
export AILAB_API_TOKEN="$(openssl rand -hex 32)"
make run                                    # http://localhost:8080
```

```bash
curl -s localhost:8080/healthz
curl -s -X POST localhost:8080/v1/score \
  -H "Authorization: Bearer $AILAB_API_TOKEN" -H "Content-Type: application/json" \
  -d '{"text":"Setup was easy and support was great."}'
# {"label":"positive","score":1,"matches":["easy","great"],"model":"keyword-baseline-v1"}
```

Native AOT and container:

```bash
make publish-aot                            # native binary in artifacts/publish/<rid>/
make docker-build                           # multi-stage AOT build, chiseled runtime image
docker compose up --build                   # reads AILAB_API_TOKEN from your shell
```

On macOS with Homebrew's .NET, AOT links against Homebrew OpenSSL and Brotli; the Makefile adds
their library paths. Xcode Command Line Tools are required for the native linker.

## Endpoints

| Method | Path | Auth | Purpose | Responses |
|---|---|---|---|---|
| GET | `/healthz` | none | Liveness: the process can serve HTTP | `200 Healthy` |
| GET | `/readyz` | none | Readiness: started and not shutting down | `200 Healthy`, `503 Unhealthy` |
| POST | `/v1/score` | Bearer | Score `{"text": "..."}` | `200`, `400` invalid input, `401` bad/missing token, `503` token not configured |

Errors use one envelope: `{"error": "<code>", "detail": "<message>"}`.

## Configuration

Environment variables only; there is no `appsettings.json`.

| Variable | Default | Meaning |
|---|---|---|
| `AILAB_API_TOKEN` | unset | Shared bearer token. If unset, `/v1/*` fails closed with 503 and a warning is logged at startup. |
| `AILAB_MAX_TEXT_LENGTH` | `4096` | Maximum characters accepted by `/v1/score` (1 to 100000). |
| `AILAB_SHUTDOWN_TIMEOUT_SECONDS` | `20` | Graceful-shutdown budget for in-flight requests (1 to 300). |
| `PORT` | `8080` | Listen port on `0.0.0.0`, used when `ASPNETCORE_URLS` is not set. |
| `ASPNETCORE_URLS` | unset | Full listen URL(s); overrides `PORT`. |
| `Logging__LogLevel__Default` | `Information` | Standard .NET logging levels (JSON lines on stdout). |

Invalid values stop the process at startup (options are validated on start, not on first use).

## Eval results

`make eval` (and the CI `eval` job) runs the scorer over `fixtures/score_eval.jsonl`, writes
`eval_results.json` (schema below), prints the row to paste here, and exits non-zero if the
primary metric falls below the threshold (0.80). CI uploads the file as the `eval-results` artifact.

| Date | Commit | Model/Provider | Dataset | Metric | Score | Notes |
|---|---|---|---|---|---|---|
| 2026-10-01 | 8a19893 | keyword-baseline-v1 / baseline | score_eval.jsonl (n=40) | accuracy | 0.900 | threshold 0.80; misses: syn-036, syn-037, syn-038, syn-039 (negation, sarcasm, no lexicon hit) |

`eval_results.json` contract (`schema_version` 1; extra keys allowed, required keys never renamed):

```json
{
  "schema_version": 1,
  "lab": "ailab-template-dotnet",
  "dataset": "score_eval.jsonl",
  "provider": "baseline",
  "model": "keyword-baseline-v1",
  "primary_metric": "accuracy",
  "metrics": { "accuracy": 0.9, "n": 40 },
  "threshold": 0.8,
  "passed": true,
  "commit": "<git sha or null>",
  "generated_at": "2026-10-01T00:00:00Z"
}
```

## Secret scanning

Two independent checks run in the `pre-push` hook (`make hooks`) **and** in the `secret-scan` CI
job, so nothing is published that either one rejects:

1. **gitleaks** with `.gitleaks.toml`: the upstream default ruleset plus a rule for Doppler-style
   tokens. The hook scans the outgoing commits (all commits for a new branch); CI scans full history.
   CI downloads a pinned gitleaks release and verifies its SHA-256 checksum before running it.
2. **Denylist scan** (`scripts/denylist_scan.sh`) over every tracked file **and** full history
   (`git log -p --all`, including commit messages):
   - *Generic* patterns, committed in `.denylist-generic.txt`: shapes of private data such as
     private-network VPN hostnames and addresses, and secret-manager token prefixes.
   - *Private* patterns, never committed: instance-specific strings (real hostnames, company or
     client names). Read from `AILAB_DENYLIST` (newline-separated regexes; a repository secret in
     CI) or from `~/.config/ailab/denylist.txt` locally. Matching is case-insensitive, and private
     pattern text is never printed: findings report only `file:line` and the pattern number.

The denylist scan **fails closed**: with no private patterns it exits non-zero instead of
reporting clean. The single exception is CI for pull requests from forks (which cannot read
repository secrets), where `AILAB_DENYLIST_OPTIONAL=1` lets the generic checks run alone.

```bash
make hooks          # git config core.hooksPath .githooks
make scan           # gitleaks (full history) + denylist (tree + history)
```

If either check fires, removing the string in a new commit is not enough: the value is still in
history. Rotate the secret, then rewrite history before pushing.

## Repository layout

```
src/AilabTemplate.Api/        Minimal API host: endpoints, auth filter, health, options
src/AilabTemplate.Core/       IScorer + KeywordScorer (no ASP.NET dependency, AOT-compatible)
tests/AilabTemplate.Api.Tests xUnit: WebApplicationFactory integration tests + unit tests
tools/AilabTemplate.Eval/     Offline eval gate over fixtures/*.jsonl
fixtures/                     Synthetic, hand-authored eval data (see FIXTURES.md)
scripts/, .githooks/          Secret wall
docs/DESIGN.md                Decisions and rejected alternatives
docs/LEARNING.md              Concepts used here, plus interview questions
MODEL_CARD.md                 What the baseline is, and is not, good for
```

## License

[Apache-2.0](LICENSE). Copyright 2026 Andrew Pyle.
