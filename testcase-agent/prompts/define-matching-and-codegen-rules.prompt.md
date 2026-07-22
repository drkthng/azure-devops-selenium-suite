Your task is to prepare the rules for AI-assisted test-code generation by analyzing our real code and drafting two rule documents. You will gather evidence, apply the decision framework below, and produce drafts plus a summary for human review. Do not generate any test code in this session — the output is rules, not code.

Phase 1 — Gather evidence. Read step-inventory.json. Select and list: 3 shared steps with their composes chains, 5 page-object methods across different pages, 2 base-steps with their availability. Then pick one approved test case from out/ (prefer one whose domain overlaps with the shared steps you selected) and one existing test-case class from the suite that is typical of our current style. State what you picked and why.

Phase 2 — The matching walk-through (this drafts the matching rules). Go through the chosen out/ test case step by step. For each written step, decide which category it falls into, using this preference order as the framework:

Direct match, shared step — a shared step's purpose covers the written step. Highest preference: reuse at the highest abstraction available.
Near-match, shared step — a shared step almost fits but parameters/values differ (e.g. "create opportunity with X" vs CreateWithDefaults). Record what the gap is. Rule of thumb: if the gap is a parameter value, it's a near-match worth reusing or extending; if the gap is different behaviour, it is not a match.
Base-step match — only valid if the target test class sits inside that base class's availability. If a base-step would fit but availability blocks it, record that explicitly as a finding — do not silently fall through.
Composition from page-object methods — the step doesn't exist as a composite, but can be built from 2–5 existing page-object calls. List the exact chain using returns to verify it navigates correctly.
TODO — required elements/actions don't exist in the inventory at all. Describe precisely what's missing (which page, which element, which action).

Produce a table: written step → category → matched id(s) or gap description. Where you genuinely cannot decide between two categories, mark it [HUMAN CALL] with both options and your lean — do not just pick one.

Phase 3 — Draft style/matching-rules.md from what Phase 2 taught you: the preference order above, plus the concrete boundary cases you actually encountered, each with the real example from the walk-through. Rules grounded in real cases, not hypotheticals. If the walk-through surfaced a situation the framework above doesn't cover, add it to an "open questions" section at the end of the file rather than inventing a rule.

Phase 4 — Draft style/codegen-rules.md (the output contract for future code generation) by reading the existing test-case class you picked in Phase 1. Mirror our reality, do not invent style: class structure, naming, setup/teardown pattern, how steps are called, assertion style. Then add the generation-specific rules: unresolved steps become // TODO: <what element/action is needed, on which page> comments that keep the file compilable; alternative-approach suggestions go only into a single fenced comment block at the end of the file (max 3 per file, each with a concrete reason) — never mixed into working code; generated code conforms to the framework by default.

Phase 5 — Report for review, then stop. Summarize: the walk-through table, every [HUMAN CALL], every availability conflict, the open questions from Phase 3, and anything in our existing test class style that seems inconsistent (flag, don't fix). Both rule files are drafts pending human approval — say so at their top. Do not proceed to any code generation.

Branch instructions, depending on what you find:

If no shared step matches anything in the chosen test case: don't force it — pick a second out/ test case with better domain overlap and redo Phase 2. If still nothing, report that our shared-step layer doesn't cover this domain yet; that finding shapes the rules (more category-4/5 cases expected) and belongs in the summary.
If the written steps are much coarser than inventory entries (one written step = many actions): propose in the summary whether this should become a new shared step rather than inline composition — that's a rule candidate for matching-rules.md, marked as proposal.
If the written steps are much finer than our shared steps (several written steps = one shared step): record the many-to-one mapping as its own rule pattern with the real example.
If the inventory seems wrong or incomplete during the walk-through (a method you'd expect is missing, a purpose line is misleading): stop matching on that step, list it under "inventory findings" in the summary — the fix belongs in the inventory tool, not in improvised matching.