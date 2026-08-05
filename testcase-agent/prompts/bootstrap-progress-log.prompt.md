---
mode: agent
description: Write PROGRESS.md — the durable working memory for this folder — and keep it current from then on
---
This folder has no durable memory. You are currently holding a large amount of
context about this workstream — decisions we made and the reasons behind them,
what each tool does and what its last run reported, approaches we tried and
dropped. All of that is stored nowhere, and it is gone at your next reset. Your
task is to write it to disk as `PROGRESS.md`, then keep it current from now on.
Do not generate test cases or test code in this session — the output is the
state file.

Write it for two readers: yourself at the start of a cold session, needing to
orient in thirty seconds without re-asking settled questions; and a colleague
picking this up in six months with none of the context.

**This is not reconstruction.** Write down what you actually know. Where you do
not know, **ask me** — I am working alongside you and can answer directly.
Asking is cheaper and safer than inferring, and a fabricated rationale is worse
than a missing one because it becomes a source of truth that misleads every
later session.

**Hard rules.**
- **Never invent a date.** A date must come from a commit or a dated document.
  Otherwise write `undated`.
- **Never invent a rationale.** If a decision was made and you do not know why,
  write the decision and set the rationale to `ask: <the exact question you need
  answered>`.
- Anything you inferred rather than knew or read, mark `[INFERRED: <basis>]`.
- Never change this file silently — announce every change in your reply.
- **Match length to substance.** This is a working file, not a report. No filler
  sections, no redundant summaries, no stating the same fact in two places.
- **Deliver exactly this scope.** Write the sections below and stop. Do not add
  sections of your own, reorganise other files, or start on work that the log
  merely records as open.

**Phase 1 — Sweep.** Go over what is on disk and what you know about it, one
line each: `style/*.md`, `prompts/` and `skills/`, each tool in `tools/` (what
it does and what its most recent run reported), `knowledge/`, `out/`, `eval/`.
State this before writing anything.

**Phase 2 — Write `PROGRESS.md`** with these sections:

1. **Current state.** A short paragraph, then a table: area → artefacts, with
   **explicit paths** → state. This is the thirty-second orientation.
2. **Decision log.** One entry per decision: date (or `undated`), the decision,
   why, and where it came from. Cover at minimum every decision that a file on
   disk exists *because of* — a rule in `style/`, a tool in `tools/`, a prompt
   that supersedes an earlier one. State at the top of the section: *entries are
   never rewritten or deleted; a reversed decision gets a new entry naming the
   one it supersedes.*
3. **Open questions.** Question → why it matters → what is blocked until it is
   answered → who can answer it.
4. **Session log.** Append-only: date, what was done, what changed on disk. Seed
   it with one entry for this session.
5. **Needs confirmation.** Everything you could not ground, each as the precise
   question you need answered. Same channel as the glossary's section of that
   name — this is how open items reach me.

**Phase 3 — Make it self-maintaining.** A state file nobody updates is worse
than none, because it is trusted while being stale. Add a short section to
`copilot-instructions.md`: when a session makes a decision, changes a rule file,
opens an open question or answers one, it appends the entry to `PROGRESS.md`
**and says so explicitly in its reply**. Mirror the wording and spirit of the
glossary rule already in that file — that pattern works here, reuse it rather
than inventing a second one.

**Phase 4 — Report, then stop.** Give me: the decision-log entries written and
which are `[INFERRED]`; every `ask:` and every Needs-confirmation item, as a
single list I can answer in one pass; and anything on disk that nothing accounts
for. The file is a draft pending my review — say so at its top.

**Branch instructions.**
- If what you know about a decision contradicts what a file on disk implies,
  record both and flag the contradiction. Do not reconcile it silently.
- If a tool behaves differently from what its prompt file specified, that is a
  finding: record it under open questions. The fix belongs in the tool, not in
  a note that works around it.
- If answering an open question would take real work, it stays an open question.
  Do not research it in this session.
