# Model card: keyword-baseline-v1

## Summary

`keyword-baseline-v1` is a lexicon baseline that labels a short English text as `positive`,
`negative` or `neutral`. It is **not a learned model**. It exists so the template's HTTP contract,
tests and eval gate exercise something real, and so a replacement model has a floor to beat.

## How it works

- Lower-cases the text and splits it on anything that is not a letter or an apostrophe.
- Counts hits against two fixed word lists (26 positive terms, 26 negative terms) in
  `src/AilabTemplate.Core/KeywordScorer.cs`.
- `score = (positive - negative) / (positive + negative)`, rounded to 4 places, in [-1, 1];
  `0` when there are no hits.
- `label` is `positive` if the score is above 0, `negative` if below 0, otherwise `neutral`.
- `matches` lists the lexicon terms found, in order of appearance.

Deterministic, stateless, thread-safe, no external calls, sub-millisecond per request.

## Intended use

- A placeholder scorer in a template, and a reference baseline for evaluating a real model.
- Short, informal, English product-feedback-style sentences similar to the eval fixture.

## Out-of-scope use

- Any decision about a person (moderation, hiring, credit, health, legal). Do not use it there.
- Languages other than English, long documents, or domain-specific vocabulary.
- Anything where a wrong label is costly: the error modes below are systematic, not random.

## Evaluation

| Dataset | n | Metric | Score | Threshold |
|---|---|---|---|---|
| `fixtures/score_eval.jsonl` (synthetic, see FIXTURES.md) | 40 | accuracy | 0.900 | 0.80 |

Per class: negative 13/15, neutral 11/11, positive 12/14. The fixture is small and was written
by the same author as the lexicon, so this number shows the gate works; it is **not** an estimate
of real-world accuracy.

## Known limitations

- **Negation is ignored**: "not so good" scores positive; "not bad at all" scores negative.
- **Sarcasm is ignored**: "Oh great, another outage" scores positive.
- **Out-of-lexicon sentiment is invisible**: "nobody complained" scores neutral.
- **Mixed sentences cancel out**: one positive and one negative hit gives `neutral`, score 0.
- The score is a ratio of hit counts, not a calibrated probability.

## Data

No training data. The lexicon and the eval fixture are hand-authored and synthetic; no real
user, customer or business text was used (see FIXTURES.md).

## Replacing it

Implement `IScorer`, register it in `Program.cs`, point `tools/AilabTemplate.Eval` at it, and
update this card: model id, provider, data provenance, metrics, and limitations.
