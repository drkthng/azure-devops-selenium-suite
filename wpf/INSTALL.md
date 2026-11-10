# WPF test tooling – install

## 1. Copy files into the repo (RMS_ZEN)

| File in this package | Goes to | Action |
|---|---|---|
| `tools/TestIndex/Program.cs` | `tools/TestIndex/Program.cs` | new |
| `tools/TestIndex/TestIndex.csproj` | `tools/TestIndex/TestIndex.csproj` | new |
| `tools/TestIndex/Directory.Build.props` | `tools/TestIndex/` | new (isolates the tool from repo build settings) |
| `tools/TestIndex/Directory.Build.targets` | `tools/TestIndex/` | new (same) |
| `tools/TestIndex/nuget.config` | `tools/TestIndex/` | new (offline restore, no feeds needed) |
| `tools/testindex.ps1` | `tools/testindex.ps1` | new – the only entry point agents use |
| `tools/build.ps1` | `tools/build.ps1` | new – errors-only build for agents |
| – | the step-index tool the local agent generated earlier | delete it, so there is only one tool |
| – | `tools/copilot_tokens.ps1` | keep, apply the small patch in section 5 |

Add to `.gitignore`:

```
.testindex/
tools/TestIndex/out/
tools/TestIndex/obj/
tools/TestIndex/bin/
```

Check once: if a `.csproj` sits directly in the repo root, it would glob `tools/**/*.cs` into its build.
In that case add `<Compile Remove="tools/**" />` to that project, or move `tools/TestIndex` outside the repo
and adjust `$proj` in `testindex.ps1`.

## 2. First run (in the repo root)

```powershell
dotnet --list-sdks                # csproj targets net10.0; adjust TargetFramework if needed
.\tools\testindex.ps1 stats       # builds the tool on first use, then indexes the repo
```

Plausibility check: `unbound steps in existing features` should be close to 0 and `INVALID` should list
nothing or only genuinely broken patterns. If either is high, look at 3–5 examples before using it in the skill.

Smoke tests:

```powershell
.\tools\testindex.ps1 find "open customer dialog" -n 5 --kind when
.\tools\testindex.ps1 check Features\<some existing>.feature      # expect "N bound, 0 problem step(s)"
.\tools\build.ps1                                               # add -MSBuild for legacy .NET Framework projects
```

## 3. create-testcase skill – add this section to its SKILL.md

```markdown
## Reuse lookup (mandatory, replaces code search)
- NEVER grep or read step definition files to find existing steps.
- Find steps: `.\tools\testindex.ps1 find "<intent>" --kind given|when|then -n 8`
- Workflow:
  1. Write the .feature using only phrasings returned by `find`.
  2. Run `.\tools\testindex.ps1 check <file.feature>`.
  3. UNBOUND steps: first rephrase to one of the "similar" suggestions.
  4. Only steps that remain UNBOUND go to planning for new bindings.
  5. After adding bindings, `check` must report 0 problem steps.
- Build only via `.\tools\build.ps1`. Never run dotnet build/msbuild directly, never read raw build logs.
- Edit existing files with minimal targeted edits. Never rewrite a whole file.
- One test case per chat session.
```

## 4. Model routing – executor and scout agents

In every agent file the conductor delegates to (executor, ui-scout), set the model explicitly in the
frontmatter. Without it, a subagent inherits the main chat model (Opus):

```yaml
---
description: ...            # keep as is
tools: [...]                # keep as is
model: Claude Sonnet 5.5 (copilot)
---
```

Use the exact model name shown in the model picker. Planner stays on Opus; executor on Sonnet 5.5
(or Haiku 5.5 once the plans are precise enough that it doesn't loop on build errors).

## 5. Patch for tools/copilot_tokens.ps1 (session titles)

Titles are stored as `customTitle`, so the sessions list currently shows `""`.

a) In `$candidateRe`, add `customTitle` to the alternation:

```powershell
... |generatedTitle|customTitle|creationDate|timestamp)"'
```

b) In `Invoke-Record`, directly before the `if ($k.Count -eq 1 -and $k[0] -eq 'generatedTitle')` block, insert:

```powershell
    if ($k.Count -eq 1 -and $k[0] -eq 'customTitle') {
        if ($v -is [string] -and $v) { $script:title = $v }
        return
    }
```

c) In the `kind -eq 0` snapshot block, after the `generatedTitle` line, insert:

```powershell
            if ($v.customTitle -is [string] -and $v.customTitle) { $script:title = $v.customTitle }
```

## 6. Measure

Fresh chat, one typical test case, then:

```powershell
.\tools\copilot_tokens.ps1 -Paths "<...>\chatSessions"
```

Baseline to beat: session e743ede2, at least 555 credits.
