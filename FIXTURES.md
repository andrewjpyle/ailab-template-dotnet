# Fixtures and provenance

Every file under `fixtures/` must have an entry here. This repository is public: **only public
or synthetic data may ever be added**, never real user, customer, employee or business data,
and nothing derived from private systems (logs, tickets, emails, database rows, screenshots),
even if anonymised.

## Rules for adding a fixture

1. It is either **synthetic** (written by hand or generated, and checked by a person) or
   **public** under a license that allows redistribution in an Apache-2.0 repository.
2. It contains no personal data, real names of private individuals, real contact details,
   hostnames, IP addresses, credentials or internal identifiers.
3. It gets an entry below: file, rows, how it was made, license, and known biases.
4. It passes the secret wall (`make scan`) like any other file.

If you are unsure whether data qualifies, it does not.

## Inventory

### `fixtures/score_eval.jsonl`

| Field | Value |
|---|---|
| Rows | 40 (JSON Lines: `id`, `text`, `expected`) |
| Labels | 14 `positive`, 15 `negative`, 11 `neutral` |
| Provenance | Synthetic. Hand-written for this template by the repository author in 2026. No source text was copied or paraphrased from any real review, ticket or message. |
| Personal data | None. No names, contact details, products or companies. |
| License | Apache-2.0, same as the repository. |
| Purpose | Regression gate for `IScorer` implementations (`make eval`). |
| Intentionally hard rows | `syn-036` (negation), `syn-037` (sarcasm), `syn-038` (negated negative), `syn-039` (sentiment without lexicon words), `syn-040` (mixed). They make the baseline's ceiling visible. |
| Known biases | Short, informal, English, product-feedback register. Written by the same author as the baseline lexicon, so baseline accuracy on it is optimistic. |
