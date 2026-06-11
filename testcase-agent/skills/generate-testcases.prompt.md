---
mode: agent
description: Generate test cases from one work instruction
---
Your task is to create test cases from a single work instruction:
`knowledge/work-instructions/${input:instruction:filename without extension}.pdf`.

First read the style files, schema, and glossary so you follow our conventions.
The screenshots from this PDF have already been captioned — read
`knowledge/screenshot-captions/${input:instruction}.md` for the exact field
names, button labels, and states; do not guess any UI label that is not in
the captions or the PDF text.

Generate the test cases this one instruction implies, following the schema,
and write them to `out/${input:instruction}.md`. Where something is genuinely
missing from both the PDF text and the captions, mark it
`[UNVERIFIED: <what's missing>]` and continue. Do this for this instruction
only — do not touch the others.

When done, list any `[UNVERIFIED]` markers.