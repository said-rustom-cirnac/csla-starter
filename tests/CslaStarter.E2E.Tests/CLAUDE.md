# CslaStarter.E2E.Tests

End-to-end tests in a real browser: Playwright (Chromium), the app on Kestrel over HTTPS, PostgreSQL and mock-oauth2-server (the fake Entra ID) in Testcontainers, and an axe-core WCAG 2.1 AA scan.

## Running them

They skip unless `E2E=1`. Running them needs, once:
- Playwright's Chromium: `pwsh tests/CslaStarter.E2E.Tests/bin/Debug/net10.0/playwright.ps1 install --with-deps chromium` (after a build);
- a trusted development certificate: `dotnet dev-certs https --trust`;
- Docker (the tests pull `postgres:18` and `ghcr.io/navikt/mock-oauth2-server:6.1.5`).

Then: `E2E=1 dotnet test --project tests/CslaStarter.E2E.Tests`. The `E2E` workflow runs them on every push to `claude` and nightly, apart from CI.

## Rules

- **Users:** the mock signs in whoever you type (the subject becomes the Entra object id); `E2EEnvironment` seeds `ada` (Admin) and `bea` (Technical) in the database first.
- **Waiting:** wait with Playwright's auto-waiting assertions (`Expect(...)`), never fixed sleeps. Pages render over the circuit (no prerender), so wait for their content (`main form, main table`, no `[aria-busy=true]`), not for the network. Click Save through `SaveAsync`, which waits until it's enabled.
- **Two app instances:** `BaseUrl` has the normal session limits; `ShortSessionUrl` (warning after 20 s, sign-out after 40 s) is for the timeout test only.
- **GC Design System:** the components need its assets in a browser. They are committed under `src/CslaStarter.Components/wwwroot/gcds`, so the tests run against what ships.
- **Selectors:** select by the components' stable attributes (`gcds-input[name=Name]`, `gcds-button[data-kind=Save]`, `data-csla-*`), never by text, so the tests work in both languages.

Guide: [testing](../../docs/claude/testing.md).
