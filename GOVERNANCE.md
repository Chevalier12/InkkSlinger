# Governance

InkkSlinger is maintainer-led. The project’s primary objective is practical WPF
parity in a MonoGame/DesktopGL host, validated by tests and real behavior.

## Decision Making

- The maintainer has final say on design and merges.
- For behavior questions, WPF parity is the default reference. Where WPF behavior
  is unclear or impractical, the maintainer will document a project-specific
  behavior.
- Breaking changes should be rare and must be justified by parity correctness,
  test coverage, or long-term maintainability.

## Roadmap And Priorities

- `TODO.md` is the source of truth for parity work and control coverage.
- Issues and PRs should reference the relevant `TODO.md` item when applicable.

## Review Standards

PRs are evaluated on:

- Correctness and test coverage (especially regressions)
- Parity alignment (expected behavior clearly stated)
- Performance impact (measurements for hot paths)
- Maintainability (avoids unnecessary complexity)

## Releases

There is no fixed release cadence. The default branch should remain buildable
and tests should remain green.

## Significant Contributor Commercial Grant

The maintainer may grant a free lifetime commercial license to significant
contributors (see `LICENSE`).

How this is evaluated (non-binding guidance):

- Sustained, high-quality contributions over time (not one-off drive-bys)
- Material improvements to project health (bug fixes, parity fixes, tests,
  triage, review help)
- Demonstrated care for regressions and parity correctness

Any such grant is provided in writing and is discretionary.

