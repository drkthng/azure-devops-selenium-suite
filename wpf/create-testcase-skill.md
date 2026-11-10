---
name: create-testcase
description: 'End-to-end workflow for creating or updating a Gherkin test case in an RMS_ZEN NewSystemTests feature file from a manual test description (title plus Given/When/Then). Maps steps to existing step definitions via the testindex tool, inserts or edits the scenario, runs it against the live ZEN, and on any roadblock (undefined step, missing page object, broken locator) switches to zen-ui-scout to find the UI elements and add page objects and steps, then continues. Opus plans and verifies; GPT-6 Luna workers edit. Use when the user says "create a new test case", "add a scenario like the others", gives a manual test case for a feature file, or says "update/change the scenario <title or work item>" (Update mode).'
argument-hint: 'Feature file path, test case title, and Given/When/Then steps (or the scenario/work item to update)'
---

# Create Test Case

Builds on two skills. Load them lazily:
- **Conductor**: `c:\Users\xgdth\.copilot\skills\conductor\SKILL.md`. Read at start. It covers the worker model `GPT-6 Luna (copilot)`, the brief template, the quality checklist, and the anti-patterns.
  Workers must be launched as the conductor's worker agent, whose `.agent.md` pins `model: GPT-6 Luna (copilot)`. Never launch a plain subagent for worker tasks: it inherits the main model (Opus).
- **ZEN UI Scout**: `c:\Users\xgdth\.copilot\skills\zen-ui-scout\SKILL.md`. Read only when Phase C is entered (first roadblock). It covers live UI Automation scouting, the safety rules, and the page-object conventions.

Also read the repo memory `/memories/repo/new-system-tests.md` for known pitfalls.

All `.\tools\...` commands run from the repo root `C:\Develop\RMS_ZEN`.

## Typical Input

```text
Create a new test case in <abs path>.feature, similar to the others.
Title (improve it): <title>
Given: <device config>, Zen is running
When: <actions>
Then: <expected result>
...
Ignore the exact wording; map it to existing step definitions.
```

## Phase A: Write the Scenario (Conductor)

1. **Anchor.** Read only the Background and the 2 sibling scenarios with the same device config (find them by their `@deviceConfig` tag), not the whole file. Note:
   - the tag block per scenario
   - the step phrasing and indentation
2. **Map steps (tool, not worker).** For every user step run
   `.\tools\testindex.ps1 find "<intent>" --kind given|when|then -n 5`.
   `find` already returns regex, file:line and usage count; pick the binding from that.
   - Only for assertion steps: read the bound method at file:line (that method only) to confirm that actual is really compared with expected, and whether with `equals` or `contains`.
   - If the scenario references test data, check once how it resolves.
3. **Decide.** Use only steps that really exist and really assert. Prefer bindings with high usage counts. For each user step without a match, write a new step text in the style of the siblings and note it as a **planned roadblock**.
4. **Insert.** Insert the scenario yourself if it's small; otherwise use an edit worker. Apply these conventions unless the user says otherwise:
   - Use `@state:implementing`. Omit `@TC:`.
   - Copy `@azureTestConfig`, `@env` and `@deviceConfig` from siblings with the same device config.
   - Add `@testData:<basename>`. Load steps take the basename, not `Images/...` paths.
   - Do not repeat Background steps.
   - Improve the title: describe the expected behaviour plus the config, like the siblings do.
5. **Check.** Run `.\tools\testindex.ps1 check <file.feature>`.
   - UNBOUND: first rephrase to one of the "similar" suggestions, then re-check.
   - Steps that remain UNBOUND are the planned roadblocks for Phase C.
   - AMBIGUOUS: pick the intended binding and rephrase so only one matches.
   Re-read the inserted block once.

## Phase B: Run Until Roadblock

6. **Pre-flight (read-only).** Check these, and report any mismatch to the user:
   - **ZEN process:** ZEN is running, and the mode in the window title matches.
   - **Device config:** the active MTB configuration matches `@deviceConfig`.
   - **Test data:** the files are reachable through `RMS_TEST_DATA_ROOT`, `%TEMP%\systemTestsTestData`, or `GIT_ACCESS_TOKEN`.

   If the user is unavailable, continue whenever the mismatch is unlikely to affect the element under test, and state that assumption in the final report.
7. **Build** only if sources changed:
   `.\tools\build.ps1 -MSBuild Testing\NewSystemTests\<TestProject>.csproj`
   <!-- TODO: replace <TestProject> with the real test project file name. -->
   Never run dotnet build or msbuild directly, never read raw build logs.
8. **Run** the scenario:
   `<RUN COMMAND>`
   <!-- TODO: paste the command from zen-ui-scout's "Running the Scenario First" section here,
        so Phase B does not need to load the scout skill. -->
   Classify every step as done, error, undefined, or skipped.
9. **Passed:** go to Phase D. Otherwise take the **first** non-done step and classify the roadblock:

| Roadblock | Action |
|---|---|
| `undefined` step | Phase C, scout for the missing element. |
| `error` from a locator or element-not-found | Phase C, re-scout the element and fix the page object locator. |
| `error` from an assertion with wrong expected data | Check the expectation live (Phase C, step 11). Fix the scenario data only if the app behaviour is clearly right. Otherwise report a possible product bug. |
| Test data missing | Ask the user to provide it, or to load the file manually in ZEN. Continue scouting from the live state. Do not chase the data. |
| Environment problem, such as ZEN not running or the wrong config | Stop and ask the user. |

## Phase C: Resolve Roadblock (ZEN UI Scout)

Read the zen-ui-scout skill now, if not done yet.

10. ZEN is now in the state the passing steps produced, because teardown keeps ZEN, tracks, and panels. If needed, bring it to the state the blocked step needs. Use allowed scout actions, or ask the user for manual steps such as file dialogs.
11. Follow zen-ui-scout steps 3 to 8:
    - **Locate** the element with a read-only scan.
    - **Confirm the behaviour live.** Apply the trigger and read IsEnabled or the value before and after. This checks the scenario's expectation against the real app before any code is written.
    - **Map** to the owning page object class and its helper conventions.
    - **Choose the locator.** Prefer AutomationId. Avoid Name, because it is localized. Use the parent of a labelled TextBlock when the button has no id.
    - **Edit the page object**, then the step definition, in the owning step-definition class. Assertions compare actual with expected; unknown parameters throw.
12. If the real UI label differs from the planned step text, align the step text with the UI, for example "Crop Area" instead of "Crop".
13. Run `.\tools\testindex.ps1 check <file.feature>` (must report 0 problems), then return to step 7: rebuild, re-run, and handle the next roadblock. Stop after **3** cycles without progress on the same step, then report to the user.

## Phase D: Finish

14. **Restore ZEN** to its original UI state: switch off toggles you turned on and collapse panels you expanded, if practical.
15. **Update memory** only with facts no tool can derive: locator pitfalls, environment quirks. Never step regexes or step lists (testindex has them). Keep `/memories/repo/new-system-tests.md` under ~60 lines; merge or drop stale entries. Add lessons about the workflow itself to the zen-ui-scout or create-testcase skill files only if they changed the outcome.
16. **Report** briefly:
    - the files changed, as links
    - the steps reused and the steps added
    - the live-verified behaviour
    - what was **not** validated, for example steps skipped because of missing test data
    - open points such as missing AutomationIds for production XAML, a config mismatch, or test data

    Offer a short commit message in this format:

```text
Add <area> <behaviour> system test

- Add scenario: <title> (<deviceConfig>, @state:implementing)
- Add <PageObjectClass>.<Property> page object (<locator summary>)
- Add step "<step regex text>"
```

## Update Mode (existing test case)

Trigger: the request references an existing scenario, feature file or work item ID. Replaces Phase A; Phases B to D apply unchanged.

U1. **Locate** the scenario by work item tag or title (search the .feature files only). Read only that scenario plus its Background, not the whole file.
U2. **Diff first.** List the required changes as a short table (step to change / add / remove, reason) before touching any file.
U3. **Edit in place** with minimal targeted edits. Keep tags (`@TestsWorkItem` etc.), scenario title and Examples structure unless the change requires otherwise.
U4. **Reuse:** new or changed steps only via `.\tools\testindex.ps1 find`, then `check` must report 0 problems.
U5. **Scope the run:** skip Phase C unless a step needs an element that doesn't exist in the page objects. Build only if bindings or page objects changed.
U6. **Model:** the planner model only if new bindings or page objects are needed. Pure step rephrasing, reordering or data changes run entirely on the worker model.

For the commit message, use `Update <area> <behaviour> system test` and list the changed steps.

## Rules

- Never grep or read step definition files to find existing steps; use `.\tools\testindex.ps1 find`.
- Build only via `.\tools\build.ps1`.
- Edit existing files with minimal targeted edits. Never rewrite a whole file.
- One test case per chat session.
- Never run git commands that change state (commit, push, reset). Read-only git is fine.
- Only one worker drives the ZEN UI at a time. Edit workers never share a file in parallel.
- Keep page-object and step changes minimal, and follow the owning file's conventions and `.github/instructions/csharp.instructions.md`.
- Do not edit production (non-test) code. Report missing AutomationIds instead.
