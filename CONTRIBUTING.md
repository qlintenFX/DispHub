# Contributing to DisplayHub

Thanks for contributing.

## Getting started

1. Fork and clone the repo
2. Create a branch from `main`
3. Build locally:

```powershell
dotnet build
```

4. Run locally:

```powershell
dotnet run
```

## Branch and commit guidance

- One feature/fix per branch
- Keep commits focused and reviewable
- Use clear commit messages (`feat:`, `fix:`, `chore:`)

## Pull requests

Please include:

- Problem summary
- What changed
- Why this approach
- Manual test notes
- Screenshots for UI changes

Link the issue in the PR description (e.g. `Closes #123`).

## Issue triage flow (maintainers)

Use this flow to keep incoming issues actionable and welcoming:

1. **Initial intake**: New issues should carry `needs-triage`.
2. **Reproduce / validate**:
   - Confirm the report uses the template fields.
   - For bugs, verify steps are reproducible.
   - For feature requests, confirm the problem statement is clear.
3. **Classify**:
   - Add a primary type label such as `bug`, `enhancement`, or `documentation`.
   - Route questions or open-ended discussion topics to Discussions.
4. **Refine**:
   - Remove `needs-triage` once first maintainer triage is complete.
   - Add `needs-info` when required details are missing.
   - Mark duplicates quickly and link the canonical issue.
5. **Queue / close**:
   - Keep valid items open with clear next steps.
   - Close invalid/out-of-scope items with a short explanation.

Security reports must be handled via `SECURITY.md` and not triaged in public issue threads.

## `good first issue` criteria

Only apply `good first issue` when the task is truly newcomer-friendly:

- Small, self-contained scope (single concern, limited files).
- Clear acceptance criteria and expected behavior.
- No deep domain risk (for example, avoid delicate hotkey/display state internals).
- A maintainer can review promptly and provide guidance.
- Helpful references are included (relevant files, issue context, and repro details if applicable).

`good first issue` can be paired with `help wanted`, but should not be used for urgent production fixes.

## Label usage

Expected baseline labels:

- `needs-triage`: Newly created items awaiting maintainer classification.
- `bug`: Reproducible defect.
- `enhancement`: Product or UX improvement request.
- `documentation`: Docs-only or docs-heavy changes.
- `good first issue`: Scoped newcomer task with clear instructions.
- `help wanted`: Maintainers welcome community implementation help.
- `needs-info`: Waiting on reporter details before work can continue.
- `question`: Support or usage question (usually moved to Discussions).
- `duplicate`: Already tracked elsewhere.
- `priority:low`, `priority:medium`, `priority:high`: Relative scheduling signal.

When triaging, aim for one type label plus optional onboarding/priority/status labels.

## PR review lifecycle

Recommended lifecycle for pull requests:

1. **Open PR** (draft or ready) with linked issue and test notes.
2. **CI pass** (`dotnet build` and relevant tests green).
3. **Maintainer triage**:
   - Validate scope and labeling.
   - Confirm no unrelated refactors slipped in.
4. **Review round(s)**:
   - Request changes with actionable feedback.
   - Contributor updates and resolves comments.
5. **Approval**:
   - At least one maintainer approval before merge.
6. **Merge + follow-up**:
   - Prefer squash merge for focused history.
   - Ensure linked issues auto-close or are updated.

## Scope discipline

- Avoid unrelated refactors in the same PR
- Keep behavior changes explicit
- Reuse existing patterns and helpers when possible

## Code quality

Before opening a PR:

```powershell
dotnet build
```

If tests are present for your area, run them too.

## Community expectations

By participating, you agree to follow `CODE_OF_CONDUCT.md`.
