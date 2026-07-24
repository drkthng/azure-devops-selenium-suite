# Model-tier evals — how cheap can we go?

Purpose: replace gut feeling with a repeatable check that tells us, per
pipeline step, the **cheapest model tier that still meets the quality bar**.
Lives in `eval/`. Runs are occasional (new model available, price change,
pipeline step changed), not continuous.

## Principle

Every pipeline step gets a small frozen set of tasks with known-good
("golden") outputs. We run the same tasks through candidate model tiers and
score against the golden output. The cheapest tier that clears the bar
becomes the default for that step; the result is recorded in
`PROGRESS.md` (Decision Log, answers Q7).

Honest scope note: with 5–10 items per step we detect **big** quality gaps,
not subtle ones. That is fine — big gaps are what decide tiers. Do not
over-interpret single-item differences.

## Where the golden data comes from (near-free)

- **Matching step:** take test cases whose steps a human has already mapped
  to existing Selenium steps during review. Input: written step + inventory.
  Golden: the human-confirmed match (or confirmed "no match → TODO").
- **Codegen step:** every human-reviewed, corrected skeleton from D3 is a
  golden reference. The *diff* between agent draft and reviewed version is
  the error catalogue.
- **Caption/cleanup steps (Phase 2 pipeline):** reviewed caption files vs.
  raw OCR — already on disk.

Every review you do anyway produces eval data. Copy the pair (input, golden
output) into `eval/golden/<step-name>/` as you go; five minutes per week.

## Folder layout

```
eval/
  README.md                  <- this file
  golden/
    matching/                <- per item: input.md + expected.md
    codegen/
  runs/
    2026-07-XX-matching/     <- one folder per eval run
      results.md             <- scores + verdict
```

## Scoring — keep it crude and useful

Three-level score per item, judged by you (or a strong model as pre-grader
with you spot-checking — grading is cheap, generation is what we're pricing):

- **PASS** — usable as-is or trivial edit
- **MINOR** — right approach, needs small corrections a reviewer would catch
  quickly
- **FAIL** — wrong match / non-compiling / framework violation / invented
  API

Bar per step (adjust with experience):
- Matching: ≥ 90% PASS, zero FAILs on "invented a step that doesn't exist"
- Codegen: ≥ 70% PASS, zero FAILs on framework violations; MINORs
  acceptable because the human review gate stays in place

## Run procedure (mostly scriptable later, manual first)

1. Pick the step and 5–10 golden items.
2. Run each item through each candidate tier (e.g. GPT 5.4 vs Sonnet vs
   GPT 5.5) with the **identical prompt file** — the prompt is part of what
   is being tested; never hand-tune per model.
3. Score, write `runs/<date>-<step>/results.md`: table items x models,
   PASS/MINOR/FAIL, plus credit cost per run if visible.
4. Verdict line: cheapest tier clearing the bar → record in PROGRESS.md
   Decision Log.

## Triggers for re-running

- New model tier appears or prices change
- A pipeline prompt/rule file changes materially (house-rules,
  framework-rules, schema)
- Quality drift suspected in daily use — two bad outputs in a row from a
  step is a trigger, not an annoyance

## What NOT to do

- No sprawling benchmark suites — 5–10 items per step, period.
- No per-model prompt tuning — that destroys comparability and creates
  maintenance debt.
- No silent tier downgrades in daily work without an eval run; "it felt
  fine" is how quality drift starts.