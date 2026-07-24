---
mode: agent
description: Generate a compilable test-code skeleton from an approved test case
---
Generate test code for one approved test case:
`out/${input:testcase:filename without extension}.md`.

**Read first, in this order:** `style/matching-rules.md`,
`style/codegen-rules.md`, and `step-inventory.json`. These three files are
the complete rule set and the complete knowledge of what exists — do not
read the test suite's source code, and do not use any step, method, or
property that is not in the inventory.

**Process:**
1. Walk the test case step by step and match each written step per the
   preference order in matching-rules.md. Property edges
   (`page-object-property` entries) are valid navigation steps.
2. Compose the test class per codegen-rules.md, mirroring our structure
   and naming. Use matched shared steps and base steps where availability
   allows; compositions from page-object calls where the rules say so.
3. Unmatched steps become
   `// TODO: <what element/action is needed, on which page>` —
   the file must remain compilable with TODOs in place.
4. Proposals (including new helper/shared-step suggestions per
   codegen-rules) go only in the single fenced proposal block at the end.
   Max 3, each with signature, what it wraps, and which steps would use it.

**Output:** write the test class to the location and with the name that
codegen-rules.md prescribes. Then report: a match table (written step →
category → id used or TODO), the TODO count, and the proposal count.

**Boundaries:** one test case per run. Do not modify the inventory, the
rule files, or any existing suite code. If a written step is ambiguous or
the rules genuinely do not cover a situation, mark it
`[HUMAN CALL: <the two options and your lean>]` in the report and choose
the more conservative option (TODO over invention) in the code.