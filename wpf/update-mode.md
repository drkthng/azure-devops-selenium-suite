## Update mode (existing test case)
Trigger: the request references an existing scenario, feature file or work item ID.

1. Locate: find the scenario by work item tag or title (search the .feature files only).
   Read only that scenario plus its Background, not the whole folder.
2. Diff first: list the required changes as a short table
   (step to change / add / remove, reason) before touching any file.
3. Edit in place with minimal targeted edits. Never rewrite the feature file.
   Keep tags (@TestsWorkItem etc.), scenario title and Examples structure unless the
   change requires otherwise.
4. Reuse: new or changed steps only via `.\tools\testindex.ps1 find`, same rules as create mode.
5. Verify: `.\tools\testindex.ps1 check <file>` must report 0 problems,
   then `.\tools\build.ps1` only if bindings or page objects changed.
6. No UI scouting unless a step needs an element that doesn't exist in the page objects.
7. Planner model only if new bindings/page objects are needed; pure step
   rephrasing/reordering runs entirely on the cheap model.