# Roadmap — AI-assisted test case & test code generation

Living plan for this workstream. Last updated 2026-08-05.

---

## 1. What this repo is

**This repo is the brain.** It holds the roadmap, the prompts that drive the
work, and the research into what is worth building at the customer. It does not
hold the details.

**The customer machine is where the work happens.** The knowledge base, the
derived rule files, the tools, the generated output and the working state live
there, driven by an agent the developer works with directly.

| | Here (brain) | Customer machine |
|---|---|---|
| Roadmap, value research | ✅ | — |
| Prompts (authoring + archive) | ✅ | invoked copies |
| `knowledge/`, `style/`, `tools/`, `out/` | — | ✅ |
| `PROGRESS.md` (working state, decision log) | — | to be created |

Divergence between the two is expected, not drift. This repo does not mirror
the customer folder and should not try to.

---

## 2. Non-negotiable principles

These recur in every prompt and rule file. They exist because of concrete
problems already hit, not as generic caution. Absorbed from `HANDOVER.md`, which
was a one-time transport of earlier design thinking and has been removed now
that its content lives here.

- **Never guess, always mark.** Invented UI labels, field names, step methods or
  definitions are worse than visible gaps: they look correct and waste the
  reviewer's trust. Markers in use: `[UNVERIFIED: …]`, `[OCR UNCLEAR: …]`,
  `[NEEDS SCREENSHOT: …]`, `[HUMAN CALL: … options + lean]`.
- **The human review gate never moves.** Review happens on the generated text
  files *before* anything reaches ADO or the suite. Automation pushes only
  already-approved artefacts. Rollback must always be "don't push", never
  "bulk-delete work items".
- **Fix upstream, never patch downstream.** If the inventory, captions or source
  data are wrong, fix the tool that produces them. Working around a faulty
  foundation in a later step corrupts everything built on it. *This was violated
  once* — an agent tried to encode inventory gaps into eval golden data instead
  of extending the inventory tool. Watch for the pattern.
- **Scripts where possible, agents where necessary.** Deterministic extraction
  (PDF images, OCR, code parsing) is scripted: zero tokens, repeatable,
  reviewable. Agents only do judgement work, and read compact generated
  artefacts instead of globbing source.
- **General corrections go into the rule files, not the output.** A recurring
  mistake is fixed once in `house-rules.md` / `codegen-rules.md`, then the output
  is regenerated to verify the fix took.
- **Conform by default, propose separately.** Generated artefacts follow existing
  conventions. Alternative approaches go in a fenced proposal block at the end of
  the file — never mixed into working code, max 3 per file, each with concrete
  reasoning.
- **One unit of work per agent run.** One work instruction, one test case.
  Long multi-item runs stalled in practice: 45-minute hang, no output.
- **Every artefact a later step depends on needs an explicit file path in the
  prompt.** A table produced "in the report" lives in chat and dies with it.
  This already cost one walk-through table — see §4.1.

Two standing constraints of the same weight:

- **Token economy (Phase 3 core).** A script parses the Selenium solution into a
  compact inventory; agents reason over that inventory and **never read the
  suite's source code**. This is the single most important design constraint in
  Phase 3.
- **Golden inputs must match runtime exactly.** Eval inputs contain only what the
  step will actually see — i.e. the inventory as it is. Never enrich golden data
  with knowledge the production agent will not have; that measures models against
  information they were never given.

---

## 3. Where things stand

Status reflects what exists on the customer machine, not what a document claims.

| | Deliverable | Status |
|---|---|---|
| **Phase 1** | Source material → test-case text files | **done, in use** |
| **Phase 2** | Approved test cases → ADO Test Case work items | **done, working** |
| **Phase 3** | Approved test cases → Selenium code skeletons | in progress |
| D1 | Step inventory (`tools/build-step-inventory`, Roslyn) | **done** |
| D1b | Inventory audit tool (`tools/audit-step-inventory`) | **done** — checks inventory completeness and usefulness. Built but never documented until now |
| D2 | `matching-rules.md` + `codegen-rules.md`, human-reviewed | **done** |
| D3 | `/generate-testcode` — prompt written, **never run** | **next action** |
| D4 | Locator discovery (DOM dump → agent picks locators) | not started — blocked on Q4 |
| D5 | Metric snapshot for the weekly management update | not started — blocked on Q6 |
| D6 | Model-tier / effort evals | not started — `eval/` is empty at the customer |

---

## 4. What needs doing at the customer

Ordered by what blocks what.

1. **Recover the walk-through match table — do this first, while context is
   warm.** The D3 first-run protocol depends on comparing generated output
   against the known-correct match table from the D2 walk-through. That table is
   **not on disk anywhere**: `eval/golden/matching/` was seeded, then correctly
   halted by `stop-prompt.md` pending the inventory fix, and never rebuilt. It
   may still be in the customer agent's context; if it is, write it to disk
   before the next reset. If it is gone, the D3 run loses its answer key and the
   walk-through has to be redone.
2. **Run `bootstrap-progress-log.prompt.md`** to create `PROGRESS.md` and make it
   self-maintaining. Everything below this line gets easier once the customer
   folder has durable memory.
3. **Pin the input paths in `generate-testcode.prompt.md`** before the first run.
   It currently names `step-inventory.json` with no directory, and the rule files
   with no `style/` prefix. Q5 flags a wrong workspace as the most likely cause
   of a confusing first failure — and this is the file about to run first.
4. **Run D3 and diff its match table against the walk-through table.**
   Disagreements are the most informative signal available at this stage.
5. **Rebuild `eval/golden/matching/`** from the (now corrected) walk-through
   table against the current inventory, including the property entries added in
   inventory v2.
6. **Give the ADO importer a memory of what it pushed.** IDs are echoed to
   console, not persisted; create-only with no dedup means a re-run on the same
   `out/` file silently creates duplicates — the one failure that "rollback =
   don't push" cannot undo. A one-line append to `pushed.jsonl`
   (out-file → work item ID) closes it and doubles as traceability.
7. **Define the `out/` ↔ ADO lifecycle after import.** Once a case is in ADO, ADO
   is the record; if a human edits it there, `out/` goes stale — and D3 generates
   code from `out/`. Decide which is the source of truth for codegen.

---

## 5. Prompt library hygiene (here)

Not urgent, but it degrades every time a prompt is added.

- **Three generations of the same prompt sit side by side, none marked current:**
  `analyze-work-instructions-prompt.md` (all instructions at once) →
  `scoped-testcase-creation-prompt.md` (one at a time) →
  `skills/generate-testcases.prompt.md` (one at a time + captions). The oldest
  contradicts the "one unit of work per agent run" rule it caused.
- **One-shot historical corrections are mixed in with reusable prompts:**
  `stop-prompt.md`, `update-houserules.txt`,
  `update-codegen-rules-and-seed-eval.prompt.md`. Valuable as the record of *why*
  rules are shaped as they are — but they read as runnable and two of them
  are not. An `archive/` subfolder would separate record from tooling.
- **Naming convention** (verb-first, kebab-case, `.prompt.md`) holds for 7 of 12.
- **No `.github/prompts/` mirror exists**, so nothing here is slash-invocable;
  only 3 of 12 files carry the `mode: agent` frontmatter that makes them so.
  `skills/` is a third location not described anywhere.
- **`copilot/` at repo root is an orphaned earlier generation.**
  `test-case-author.agent.md` is a screenshot/vision-based test-case author —
  the approach org policy forbids, and the whole reason the OCR pipeline exists.
  Nothing marks it superseded.

---

## 6. Decisions

Append-only. A reversed decision gets a new entry naming the one it supersedes.

| Date | Decision | Why |
|---|---|---|
| earlier | ADO import is a script, not the MCP server | The script needs one narrow permission (PAT scoped to Work Items read/write); MCP additionally requires an org policy toggle outside our control. A script also behaves identically every run, which matters when writing into a real project at a specific Area Path. MCP remains a later ergonomics upgrade, not a dependency |
| earlier | Agents reason over the step inventory and never read the suite's source | Token economy — the core Phase 3 constraint. See §2 |
| earlier | Local OCR (`winocr`, offline) instead of model vision | Copilot vision is disabled by customer org policy; no image ever leaves the machine. If vision is enabled later, only the OCR step changes |
| 2026-08-05 | This repo is the brain; details live at the customer | Work happens on the customer machine with its own agent; this repo holds plan, prompts and research. Mirroring would create sync burden with no benefit |
| 2026-08-05 | `HANDOVER.md` removed; its principles absorbed into §2 | It was a one-time transport of chat-window thinking, not a living document. Recoverable from git history at `2b9849e` if ever needed |
| 2026-08-05 | Roadmap here, `PROGRESS.md` at the customer | Split by location, not by content. The plan belongs where planning happens; working state belongs where the work happens |
| 2026-08-05 | Decision log is append-only; entries are never rewritten | Entries are historical facts. A file mixing mutable status with immutable history invites silent rewriting of the history |
| 2026-08-05 | Bootstrap run of `PROGRESS.md`: Opus 5 at default effort | The hard part is restraint — refusing to invent rationale — not the file sweep. Tier-sensitive despite looking mechanical. `max` effort buys unneeded depth and raises scope-creep risk |
| 2026-08-05 | Eval cost unit is the Copilot request multiplier, not tokens | The customer runs Copilot on credits; $/MTok is the wrong frame |

---

## 7. Open questions

Numbers carried forward from the original design notes. **Q1–Q3 are
unrecovered** — they predate that document; do not reuse their numbers.

| # | Question | Blocks |
|---|---|---|
| Q4 | Locator discovery mechanics — is Playwright MCP permitted by org policy? | D4 |
| Q5 | Workspace-relative paths: which folder are prompts resolved against? | D3 first run |
| Q6 | Which management metrics? Proposed: % steps auto-matched, time from approved case to compilable skeleton | D5 |
| Q7 | Model tier defaults — provisional answers in §8 below, pending eval | D6 |
| Q8 | Does Copilot expose reasoning effort as a knob, or only model choice? | Shape of the whole eval — see §8 |

---

## 8. Model tiers — provisional (answers Q7 until evals say otherwise)

| Step | Tier | Reasoning |
|---|---|---|
| Extraction (PDF, OCR, Roslyn) | none | already scripted; zero tokens |
| OCR → caption cleanup | Sonnet | evidence-bound, high volume, answer is in the source |
| Test-case generation | **the one to eval** | style imitation + honest gap-marking; where quality actually shows |
| Matching | Sonnet | near-mechanical once rules + inventory exist |
| Codegen (D3) | Opus 5 | first run especially — it is the answer-key comparison |
| Proposals / hard cases | Opus 5, on demand | |

**Effort is probably the more important axis than tier.** On Opus 5, `low` and
`medium` effort are strong enough that "Opus 5 at medium" may beat "Sonnet at
high" on both quality and spend for the middle of the pipeline. That inverts the
eval design: sweep effort on one model before sweeping models. **This is
contingent on Q8** — if Copilot exposes only model choice, tier is the only lever
and the eval stays a tier eval. Confirm before building the eval around an axis
that cannot be controlled.

Cross-vendor ranking (Opus/Sonnet vs the GPT 5.x line available in Copilot) is
not predictable from general benchmarks here: what is being measured is
adherence to "never invent, always mark", which does not track capability
leaderboards. The eval is the only instrument that answers it — an argument for
building it sooner than "later".

---

## 9. Value ideas beyond this pipeline

Roughly by value/effort. **The developer has further ideas not yet captured —
this section is incomplete and expects input.**

- **Failure-log triage** from pipeline runs — cluster failures by symptom,
  suggest causes. Low effort, works immediately on existing logs.
- **User-story linting** — check stories for testability, ambiguity and missing
  negative cases. Improves the input to this very pipeline.
- **Deduplication and gap analysis** of the existing ADO test suite.
- **Selenium → Playwright migration assistance.**
- **Test-code review bot** applying the suite's conventions.
- **Onboarding assistant** over the knowledge folder for new colleagues.
