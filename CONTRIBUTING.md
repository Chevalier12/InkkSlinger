# Contributing

Thanks for investing time in InkkSlinger. This project is parity-driven and
regression-sensitive, so contributions are expected to be test-backed and
behavior-focused.

Licensing note: contributions are accepted under the repository `LICENSE`.

## What To Contribute

- Bug reports with minimal repros
- WPF parity gaps (with a documented expected behavior)
- Fixes with tests
- Performance improvements with measurements
- New control implementations (prefer small, incremental PRs)

## Before You Open A PR

1. Check `TODO.md` for parity tracking and control coverage.
2. Search existing issues/PRs to avoid duplication.
3. Prefer opening an issue first for anything large or behavior-ambiguous.

## Bug Reports

Include:

- What you expected vs what happened
- A minimal repro (ideally a view in `Views/` plus any small code changes)
- Screenshots/video if visual
- Logs/stack traces if exceptions
- Platform/runtime info (`dotnet --info`, OS, GPU if rendering-related)

## Development Setup

Build:

```powershell
dotnet restore InkkSlinger.sln
dotnet build InkkSlinger.sln -v minimal
```

Tests:

```powershell
dotnet test InkkSlinger.Tests/InkkSlinger.Tests.csproj -v minimal
```

## Pull Request Expectations

- Keep PRs focused (one behavior change per PR is ideal).
- Add/adjust tests in `InkkSlinger.Tests/` for bug fixes and parity changes.
- Match existing naming/style conventions in the touched area.
- If you change observable behavior, update docs/comments where the behavior is
  described.

## Commit / PR Hygiene

- Write a PR description that explains the behavior change and why it matches
  WPF parity (or why it intentionally differs).
- If the PR is performance-related, include a short before/after measurement.

## Where To Add Things

- Routed events: `UI/Events/`
- Dependency properties: `UI/Core/`
- Controls: `UI/Controls/`
- Layout: `UI/Managers/LayoutManager.cs` and relevant panels in `UI/Controls/`
- Markup loading: `UI/Xaml/`
- Tests: `InkkSlinger.Tests/`

## Contributor Commercial Grant

The maintainer may grant a free lifetime commercial license to significant
contributors (see `LICENSE` and `USAGE-PERMISSION-POLICY.md`). If you think your
contribution level qualifies, open a GitHub issue to discuss it.

