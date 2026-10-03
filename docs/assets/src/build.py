"""Build the README graphics for ailab-template-dotnet.

All four graphics are STRUCTURAL (flow / anatomy / catalog of what exists in the source). None show
a captured run: this template is a .NET 9 service and no .NET SDK was available on the build machine,
so every graphic is labelled "HOW IT WORKS" in the footer and shows contracts and wiring read from
the source, never a mock run or a hand-typed metric. Each label names real code so a reader can check
it against the repository.
"""

from __future__ import annotations

import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import readme_kit as k  # noqa: E402

FOOT = "AILAB-TEMPLATE-DOTNET · HOW IT WORKS"


def hero() -> str:
    wheel = k.wheel(
        ["HTTP", "AUTH", "EVAL", "CI", "DOCKER", "HEALTH"],
        center_top="AROUND THE",
        center_main="scorer",
    )
    return k.hero(
        kicker="AILAB TEMPLATE · .NET 9",
        title="An AI lab sidecar",
        accent="shaped to ship",
        lede_html=(
            "A narrow, stateless HTTP service a host app calls to score text. "
            "The scorer is a trivial keyword baseline on purpose. Everything around it is the point."
        ),
        rules=[
            ("One IScorer interface", "Swap the baseline for a real model, keep the contract and the rest."),
            ("Native AOT, non-root", "A chiseled container image that starts fast and drops every capability."),
            ("Eval gate and secret wall", "An offline accuracy threshold and a two-tool secret scan, local and in CI."),
        ],
        pill="NATIVE AOT · CHISELED · NON-ROOT",
        right_html=wheel,
        footer_left=FOOT,
    )


def flow() -> str:
    y = 262
    h = 118
    boxes = (
        k.box(56, y, 188, h, "CALLER", ["host app", "Bearer token"])
        + k.box(312, y, 214, h, "BEARER FILTER", ["/v1 endpoint filter", "constant-time match"], accent=True)
        + k.box(596, y, 214, h, "/v1/score", ["validate text", "length <= max"])
        + k.box(880, y, 206, h, "KeywordScorer", ["IScorer.Score", "lexicon baseline"])
        + k.box(1156, y, 188, h, "200 OK", ["label, score", "matches, model"], accent=True)
        # rejection lane
        + k.box(312, 486, 214, 112, "401 / 503", ["unauthorized", "unavailable (no token)"])
        + k.box(596, 486, 214, 112, "400", ["invalid_request", "blank or too long"])
    )
    specs = [
        (244, y + 59, 312, y + 59),
        (526, y + 59, 596, y + 59, "token ok", False, "above"),
        (810, y + 59, 880, y + 59),
        (1086, y + 59, 1156, y + 59),
        (419, y + h, 419, 486, "reject", True, "right"),
        (703, y + h, 703, 486, "reject", True, "right"),
    ]
    return k.flow(
        kicker="REQUEST PATH",
        title_html=f"One {k.em('POST /v1/score')}, four ways out",
        subline="src/AilabTemplate.Api · Program.cs maps the routes; ScoreEndpoints and BearerTokenFilter own the lane",
        boxes_html=boxes,
        arrow_specs=specs,
        footer_left=FOOT,
    )


def targets() -> str:
    cards = [
        ("BUILD + TEST", "Warnings are errors, then 32 xUnit cases", "make test", "CI job: build-test"),
        ("EVAL GATE", "Scores the fixture, fails under 0.80", "make eval", "CI job: eval"),
        ("RUN LOCAL", "Serves http://localhost:8080", "make run", "needs AILAB_API_TOKEN"),
        ("NATIVE AOT", "Native binary, no shared runtime", "make publish-aot", "CI job: aot-publish"),
        ("CONTAINER", "Chiseled, read-only, non-root image", "make docker-build", "CI job: docker"),
        ("SECRET WALL", "gitleaks plus denylist, tree and history", "make scan", "CI job: secret-scan"),
    ]
    return k.catalog(
        kicker="MAKE TARGETS",
        title_html=f"Humans and CI run the {k.em('same commands')}",
        sub_html="Every make target maps to a job in .github/workflows/ci.yml, so a green badge means these ran.",
        cards=cards,
        footer_left=FOOT,
        cols=3,
        card_height=168,
    )


def contract() -> str:
    doc_lines = [
        ("h1", "The HTTP contract"),
        ("q", "One uniform error envelope for every non-2xx this service writes itself."),
        ("h2", "Endpoints"),
        ("code", "GET   /healthz    none     liveness, 200 Healthy"),
        ("code", "GET   /readyz     none     readiness, 200 | 503"),
        ("code", "POST  /v1/score   Bearer   200 | 400 | 401 | 503"),
        ("h2", "Success body (POST /v1/score)"),
        ("code", '{"label":"positive","score":1,'),
        ("code", ' "matches":["easy","great"],'),
        ("code", ' "model":"keyword-baseline-v1"}'),
        ("h2", "Error envelope (every non-2xx)"),
        ("code", '{"error":"&lt;code&gt;","detail":"&lt;message&gt;"}'),
        ("li", "invalid_request  400  blank or over AILAB_MAX_TEXT_LENGTH"),
        ("li", "unauthorized     401  bad or missing bearer token"),
        ("li", "unavailable      503  AILAB_API_TOKEN not configured"),
    ]
    notes = [
        (104, "Liveness runs no check: 200 while the process can serve HTTP at all"),
        (300, "score is rounded (positive - negative) / total hits"),
        (470, "Fail closed: an unset token answers 503, never open access"),
    ]
    return k.anatomy(
        kicker="ANATOMY · src/AilabTemplate.Api",
        doc_lines=doc_lines,
        notes=notes,
        footer_left=FOOT,
        doc_width=760,
    )


if __name__ == "__main__":
    k.write_pages(HERE, {
        "hero": hero(),
        "architecture": flow(),
        "catalog": targets(),
        "anatomy": contract(),
    })
