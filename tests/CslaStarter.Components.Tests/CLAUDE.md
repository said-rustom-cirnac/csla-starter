# CslaStarter.Components.Tests

bUnit tests for the `Csla*` components, rendered against real business objects with fake DALs. No database and no browser.

## Rules

- Start from `ComponentTestContext` (a `BunitContext`): it wires CSLA through `CslaTestHost` with the business tests' fakes, the real change hub and presence tracker, an in-memory draft store, a recording security event log and a `FakeTimeProvider`.
- Drive live updates through `context.Hub.DispatchAsync(...)`; time through `context.Time.Advance(...)`.
- Keep the logic worth testing (diffs, merges, hints, debounce) in plain classes and test those without rendering.
- JS interop runs in loose mode; assert the calls that matter with `JSInterop.VerifyInvoke`.

Guide: [testing](../../docs/claude/testing.md).
