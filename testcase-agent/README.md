# tc-agent

A context folder for generating Azure DevOps test cases with an AI coding agent
(GitHub Copilot in agent mode). The agent reads the knowledge and style files in
this folder and writes new test-case drafts that match how our experienced
colleagues write them. A human reviews every draft before it is approved for
automation.

This is **not a code project**. It is a structured collection of reference
material. The AI does the writing; this folder gives it everything it needs to
write like us.

---

## Why this folder exists

Copilot knows nothing about our customer: not our test-case naming conventions,
not the D365 modules, not the phrasing of our steps. Asking it to "write test
cases" in a plain chat produces generic output that does not look like ours.

This folder solves that. When opened in VS Code / Visual Studio, the agent can
read every file in it at once. We tell it which files to read, point it at a
user story, and it produces a draft grounded in our conventions — without us
pasting anything into the chat each time.

It does not need to be a real Git repository for the proof of concept. A plain
local folder is enough. Git becomes useful later for sharing and history.

---

## Folder structure

```
tc-agent/
  README.md                      <- this file
  copilot-instructions.md        <- master behaviour for the agent

  knowledge/                     <- what the agent must KNOW (domain facts)
    work-instructions/           <- Confluence pages saved as text
    screenshot-captions/         <- UI details transcribed from screenshots
    user-stories/                <- the stories that drive NEW test cases
    glossary.md                  <- D365 modules, abbreviations, system names

  style/                         <- how the agent must WRITE (form and tone)
    exemplars/                   <- real existing ADO test cases to imitate
    testcase-schema.md           <- the required output structure (the contract)
    house-rules.md               <- naming/style rules derived from exemplars

  prompts/
    generate-testcase.prompt.md  <- the reusable instruction for daily use

  out/                           <- generated test-case drafts land here
  eval/                          <- comparisons of generated vs. human cases
```

The split is deliberate: **`knowledge/`** is the subject matter (what the system
does), **`style/`** is the shape of the answer (how we write it down). Keeping
them apart means we can improve "what it knows" and "how it writes" independently.

---

## What goes into each markdown file

### `copilot-instructions.md`
The agent's standing instructions — read on every task. Should contain, in plain
prose:
- One paragraph on the goal: turn source material into ADO test-case drafts that
  a human will review.
- The order to read files in: style rules and schema first, then glossary, then
  the specific user story.
- Hard rules: always follow `testcase-schema.md`; never invent field names or
  UI labels that are not in the knowledge files; mark anything uncertain rather
  than guessing.
- Where to write output (`out/`) and the file-naming pattern.

### `knowledge/work-instructions/*.md`
One file per Confluence page or procedure. Each file should hold:
- A title line naming the procedure (e.g. "Order cancellation in D365").
- The step-by-step process as plain text, in the order a user performs it.
- Any business rules, permissions, or conditions mentioned on the page.
- A source line at the top: which Confluence page and its date, so we can tell
  when it goes stale.

### `knowledge/screenshot-captions/*.md`
The text content of screenshots, since the agent cannot reliably read images
from disk. One file per screen or flow. Each should list:
- The screen or dialog name.
- The visible field labels, button labels, and menu items, in the order they
  appear.
- Any state shown (e.g. "Status field reads 'Confirmed'").
Keep it factual — these labels become the words in the test steps.

### `knowledge/user-stories/*.md`
One file per user story that should produce test cases. Each should contain:
- The story ID and title.
- The "As a … I want … so that …" text.
- The acceptance criteria, listed individually so each can be traced to a test.
This is the input the agent turns into new test cases.

### `knowledge/glossary.md`
A single file mapping our terms to plain meaning, so the agent uses the right
vocabulary. Each entry: the term, what it is, and any abbreviation. Cover D365
module names, internal system names, role names, and recurring abbreviations.

### `style/exemplars/*.md`
10–20 of our **good existing** test cases, exported from ADO, one per file.
Include the title and the steps as action/expected pairs, exactly as they are in
ADO. Do not clean them up or rewrite them — their real form is the point. These
are the single biggest lever on output quality: the agent imitates them.

### `style/testcase-schema.md`
The output contract — the exact structure every generated test case must follow.
Should define each field (ID, Title, Area Path, Preconditions, Steps as
action/expected pairs, Source, Coverage), say which are required, and show one
filled-in example. This is the file that lets a later step push drafts into ADO
automatically, so keep it precise.

### `style/house-rules.md`
The naming and style conventions, **derived from the exemplars** (ask the agent:
"read these test cases and write down the naming and structure rules they
follow"). Should capture things like: how titles are phrased (verb-first?), how
granular a single test case is, how steps and expected results are worded, and
common do/don't. This file grows over time: recurring review corrections get
written back here so the agent improves each round.

### `prompts/generate-testcase.prompt.md`
The reusable instruction we give for each new test case, so we type one line
instead of re-explaining every time. Should tell the agent to read the style
files, schema, and glossary, then generate test cases for a named user story
into `out/`, following the schema. Written so it can be reused for any story by
swapping the story reference.

---

## How to use this folder

**One-time setup**
1. Create the folder and open it in VS Code / Visual Studio.
2. Export 10–20 good test cases from ADO into `style/exemplars/`.
3. Save relevant Confluence pages into `knowledge/work-instructions/`; have the
   agent transcribe screenshots into `knowledge/screenshot-captions/`.
4. Fill in `glossary.md`.
5. Ask the agent to derive `style/house-rules.md` from the exemplars.
6. Write `testcase-schema.md`, `copilot-instructions.md`, and the prompt.

**For each new test case (the daily loop)**
1. Drop the user story into `knowledge/user-stories/`.
2. *(optional)* Ask the agent to check the story for gaps and ambiguities;
   clarify with the author before generating.
3. Run the prompt: the agent reads the style files, schema, and glossary, then
   writes drafts into `out/`.
4. *(optional)* Have a second model review the draft against the schema and
   exemplars before a human looks at it.
5. A human reviews the draft. Recurring corrections go back into
   `house-rules.md` so the next round is better.

---

## Scope (proof of concept)

For now: source files in, test-case text files out — no API access. Copy/paste
happens only at the edges (exporting cases, pasting screenshots). Once this shows
value, the natural next steps are writing drafts directly into ADO via its API,
and later generating C#/Selenium/Playwright skeletons from approved cases.
