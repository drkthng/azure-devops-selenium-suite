# Handover — AI-assisted test case & test code generation

Context document for continuing this workstream in
`D:\AiProjects\azure-devops-selenium-suite`. Written 2026-08-05, summarising
the design sessions that produced the current setup. Read this first if you
are an agent or a person picking the work up; it explains not just what exists
but *why* it is shaped that way, since several rules exist to prevent specific
failures already encountered.

---

## 1. What this is

A pipeline that turns source material (Confluence work instructions, user
stories) into reviewed Azure DevOps test cases, and from those into compilable
Selenium test-code skeletons. Motivation: the customer's bottleneck is the
supply of new test cases that can then be automated.

Three phases, each independently useful:

| Phase | Scope | Status |
|---|---|---|
| 1 | Source material → test-case text files, human-reviewed | done, in use |
| 2 | Approved test cases → Test Case work items in ADO | done, working |
| 3 | Approved test cases → Selenium code skeletons | rules done, D3 next |

---

## 2. Non-negotiable principles

These recur in every prompt and rule file. They exist because of concrete
problems hit during development, not as generic caution.

- **Never guess, always mark.** Invented UI labels, field names, step methods
  or definitions are worse than visible gaps: they look correct and waste the
  reviewer's trust. Markers in use: `[UNVERIFIED: …]`, `[OCR UNCLEAR: …]`,
  `[NEEDS SCREENSHOT: …]`, `[HUMAN CALL: … options + lean]`.
- **The human review gate never moves.** Review happens on the generated text
  files *before* anything reaches ADO or the suite. Automation pushes only
  already-approved artefacts. Rollback must always be "don't push", never
  "bulk-delete work items".
- **Fix upstream, never patch downstream.** If the inventory, captions or
  source data are wrong, fix the tool that produces them. Working around a
  faulty foundation in a later step corrupts everything built on it. (This was
  violated once — an agent tried to encode inventory gaps into eval golden
  data instead of extending the inventory tool. Watch for this pattern.)
- **Scripts where possible, agents where necessary.** Deterministic extraction
  (PDF images, OCR, code parsing) is scripted: zero tokens, repeatable,
  reviewable. Agents only do judgement work, and read compact generated
  artefacts instead of globbing source.
- **General corrections go into the rule files, not the output.** A recurring
  mistake is fixed once in `house-rules.md` / `codegen-rules.md`, then the
  output is regenerated to verify the fix took.
- **Conform by default, propose separately.** Generated artefacts follow
  existing conventions. Alternative approaches go in a fenced proposal block
  at the end of the file — never mixed into working code, max 3 per file,
  each with concrete reasoning.
- **One unit of work per agent run.** One work instruction, one test case.
  Long multi-item runs stalled in practice (45 min hang, no output).
- **Every artefact a later step depends on needs an explicit file path in the
  prompt.** A table produced "in the report" lives in chat and dies with it.
  This already cost one walk-through table.

---

## 3. Repository layout

Working folder `testcase-agent/` (Phase 1/2 assets):

```
copilot-instructions.md        agent standing rules (grounding, glossary policy)
README.md                      folder structure + what belongs in each file
knowledge/
  work-instructions/           Confluence exports as PDF
  screenshot-captions/         UI labels extracted from PDF images
    _images/                   extracted PNGs (scratch/evidence)
  user-stories/                stories driving new test cases
  glossary.md                  customer terms; has a "Needs confirmation" section
style/
  exemplars/                   ~10 real ADO test cases — the imitation target
  testcase-schema.md           output contract (fields, step structure)
  house-rules.md               naming/style rules, derived + human-corrected
  matching-rules.md            Phase 3: how written steps map to existing code
  codegen-rules.md             Phase 3: output contract for generated test code
prompts/                       transport mirror of all prompt files
tools/                         scripts (OCR pipeline, ADO importer)
out/                           generated test-case drafts (review happens here)
eval/                          golden data for model-tier evaluation
PROGRESS.md                    roadmap, open questions, decision log
```

Prompt files live in `prompts/` as the readable mirror and in
`.github/prompts/` to be invocable as slash-commands. Naming convention:
verb-first, kebab-case, `.prompt.md`.

---

## 4. Phase 1 — source material → test cases (done)

**Bootstrapping order matters**, since files depend on each other:
exemplars in place → `house-rules.md` derived from them → `testcase-schema.md`
→ `glossary.md` seeded → only then generation.

**Screenshots.** Copilot vision is disabled by customer org policy. Solution:
`tools/extract_and_ocr.py` extracts embedded images from the work-instruction
PDFs (PyMuPDF), runs **local** OCR (`winocr`, the built-in Windows engine —
offline, no data egress), and writes raw text; an agent pass then cleans it
into caption files, marking unreadable labels `[OCR UNCLEAR]` rather than
repairing them by guess. No image ever goes to a model. If vision gets enabled
later, only the OCR step changes.

**Glossary is self-maintaining.** The agent adds customer-specific terms it
can define from source, queues unclear ones under "Needs confirmation", and
must announce every change — never edits silently.

**Two structural defects found and fixed via `house-rules.md`** (both worth
watching for in new output):
1. Titles were phrased as user actions ("do not copy RFQ basket when …")
   instead of expected-outcome assertions ("RFQ basket is not copied when …").
2. Every step received an expected result. Correct behaviour: a test asserts
   the one crucial behaviour it exists to verify; setup and navigation steps
   are actions without expected results. `testcase-schema.md` therefore makes
   `expected` optional per step.

Both were fixed in the rule files, not in the output, then regenerated to
confirm.

**Slash-commands:** `/caption-instruction` (runs the OCR script, then produces
clean captions for one PDF), `/generate-testcases` (one work instruction →
test-case drafts in `out/`).

---

## 5. Phase 2 — push to ADO (done, working)

Importer script reads an approved `out/` file and creates Test Case work items
via the ADO REST API.

**Why a script and not the MCP server:** the script needs only one narrow
permission (PAT scoped to Work Items read/write); MCP additionally requires an
org policy toggle that is outside our control — and the org had already
blocked vision. A script also behaves identically every run, which matters
when writing into a real project at a specific Area Path rather than a
sandbox. MCP remains a later ergonomics upgrade, not a dependency.

**Field mapping:** title → `System.Title`; steps → `Microsoft.VSTS.TCM.Steps`
(XML string; each step an `action` plus an `expectedResult` only where the
source file specifies one — this is why optional expected results matter);
Area Path → `System.AreaPath`; source/coverage → tag or description.
Step text must be HTML-escaped: XML inside JSON.

**Safety behaviour built into the tool:** dry-run is the default, `--create`
required to write; target org/project/area printed before creating; PAT from
environment variable only, config git-ignored; create-only, no update or
delete; created work item IDs and URLs echoed for verification.

---

## 6. Phase 3 — test code generation (current work)

**Architecture — the token-economy core:** a script parses the Selenium
solution into a compact inventory; agents reason over that inventory and
**never read the suite's source code**. This is the single most important
design constraint in Phase 3.

**D1 — step inventory (done).** `tools/build-step-inventory`, Roslyn-based
(semantic resolution is required — fluent chains, inheritance scope and call
graphs cannot be done with regex). Produces `step-inventory.json` (source of
truth) and a rendered `.md`.

Entry kinds — the layering is the point, because matching must reuse at the
*highest* available abstraction rather than recompose something that already
exists one level up:
- `page-object-method` — fine-grained UI capability, e.g.
  `opportunityPage.SelectQuery("…")`
- `page-object-property` — properties exposing sub-surfaces; these are
  navigation edges just like fluent methods (added in v2 after the walk-through
  showed method-only entries could not connect several real page surfaces)
- `shared-step` — business-level composites in the test-case projects, with a
  `composes` list of the page-object calls they wrap
- `base-step` — methods on test base classes, with `availability` recording
  which classes inherit them (they are not callable outside that tree)

Fields: `id`, `kind`, `project`, `class`, `signature`, `returns` (fluent target
— this yields a walkable navigation graph), `purpose`, `composes`,
`availability`, `usages`.

`purpose` is filled from evidence only — XML doc comments, or unambiguous
method names; anything unclear goes to a `needs-description` list for a
one-time cheap-model enrichment pass, and stays marked if genuinely unclear.

**D2/rules (done).** `matching-rules.md` and `codegen-rules.md` were derived by
the customer-side agent through a structured walk-through of a real test case
against the real inventory, then human-reviewed. Matching preference order:

1. direct match to a shared step
2. near-match to a shared step (parameter/value differs — a *behaviour*
   difference is not a match)
3. base-step match, only where `availability` permits
4. composition from 2–5 page-object calls, verified against `returns`
5. TODO — nothing suitable exists; describe precisely what is missing

`codegen-rules.md` mirrors the suite's real test-class structure and adds the
generation rules: unmatched steps become `// TODO: <what is needed, on which
page>` while keeping the file compilable; new helpers or shared steps are
**never created inline but may be proposed** in the fenced block, with
signature, what they wrap, and which steps would use them.

**D3 — next.** `/generate-testcode` prompt file: reads matching-rules,
codegen-rules and the inventory only; walks one approved `out/` test case;
emits a compilable class plus a match table, TODO count and proposal count.
Ambiguity resolves conservatively (TODO over invention) and is reported as
`[HUMAN CALL]`.

First run protocol: strong model tier, and use **the same test case as the
walk-through**, whose correct match table is already known — it is a built-in
answer key. Disagreements between the generated match table and the
walk-through table are the most informative signal available at this stage.

**D4 — locator discovery (later, deliberately split off).** Codegen stays
cheap and deterministic; resolving TODOs by driving a browser is the most
expensive and most permission-hungry activity and must never block generation.
Preferred mechanism: navigate to the target page using existing steps, dump
the DOM once, let an agent pick locators from the static dump — rather than
interactive puppeteering. Needs Playwright MCP or a scripted harness; MCP
policy still unconfirmed.

**D5** — metric snapshot for the weekly management update.
**D6** — model-tier evals.

---

## 7. Evals — how cheap can we go

Purpose: decide per pipeline step the cheapest model tier that still clears
the quality bar, rather than guessing under a credit-based pricing system.

Golden data comes free from work already done: reviewed matching decisions,
human-corrected skeletons (the diff to the agent draft is the error
catalogue), reviewed caption files. `eval/golden/matching/` is seeded from the
walk-through table.

**Critical rule:** golden inputs must contain exactly what the step will see at
runtime — i.e. the inventory as it actually is. Never enrich golden inputs
with knowledge the production agent will not have; that measures models against
information they were never given.

Scoring is deliberately crude — PASS / MINOR / FAIL — because 5–10 items per
step only resolve large quality gaps, which is all that tier decisions need.
Never hand-tune prompts per model: the prompt is part of what is being tested.

Suggested default tiers until evals say otherwise: matching = cheapest capable
model; codegen = strong tier; proposals and hard cases = strongest, on demand;
extraction steps = no model at all.

---

## 8. Open items

- **D3** — write and first-run `/generate-testcode`; compare its match table
  against the walk-through table.
- **Q4** — locator discovery mechanics; whether Playwright MCP is permitted.
- **Q5** — confirm where this workstream lives relative to the suite repo;
  all prompt paths are relative to the open workspace, and a wrong workspace
  is the most likely cause of a confusing first failure.
- **Q6** — confirm the management metrics (proposed: % steps auto-matched,
  time from approved case to compilable skeleton).
- **Q7** — model tier defaults, pending eval results.
- Transport `eval/README.md` to the customer folder; the golden data exists
  there but its explanation does not.
- Follow up on any availability conflicts or new-shared-step candidates
  surfaced by the walk-through — these are architecture decisions about the
  suite and should not be actioned by an agent unilaterally.

---

## 9. Further AI opportunities at the customer

Independent of this pipeline, roughly by value/effort:

- **Failure-log triage** from pipeline runs — cluster failures by symptom,
  suggest causes. Low effort, works immediately on existing logs.
- **User-story linting** — check stories for testability, ambiguity and
  missing negative cases before test-case generation. Improves the input to
  this very pipeline.
- **Deduplication and gap analysis** of the existing ADO test suite.
- **Selenium → Playwright migration assistance.**
- **Test-code review bot** applying the suite's conventions.
- **Onboarding assistant** over the knowledge folder for new colleagues.
