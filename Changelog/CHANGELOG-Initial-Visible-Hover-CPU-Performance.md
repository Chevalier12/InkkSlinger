# Changelog: Initial Visible Hover CPU Work

Date: 2026-02-14

## Summary
This update delivers huge optimizations and major performance boosts across hover identity handling, reuse validation, and redraw scoping.

## What Improved
- Hover identity is now stabilized around `ListBoxItem` boundaries to reduce noisy transitions.
- Hover reuse now works with non-zero scroll offsets through transform-safe checks.
- Hover redraw work is narrowed using bounded dirty-region marking for old/new hover identities.
- Targeted performance and invalidation tests were expanded to lock in these gains.

## Current Status
CPU usage is still higher than desired in the known hotspot scenario. Further optimizations are planned and will be delivered in a follow-up pass.
