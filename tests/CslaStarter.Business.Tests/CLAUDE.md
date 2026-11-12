# CslaStarter.Business.Tests

Unit tests for business objects, rules, authorization and localization. These tests need no database and no web host.

## Rules

- Test CSLA rules with `Csla.Testing` (`CslaTestHost`, `BusinessRuleTester`, `AuthorizationRuleTester`), with fake DALs for data portal flows.
- For every role, test property and action rules for allowed and denied access.
- When a test changes `CultureInfo.CurrentUICulture`, it restores it in `finally`.

Guide: [testing](../../docs/claude/testing.md).
