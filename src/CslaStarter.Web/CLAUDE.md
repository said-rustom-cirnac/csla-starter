# CslaStarter.Web

The host and composition root: `Program.cs`, `App.razor` and the pages. It is the only project that references every other.

**May reference:** all src projects.

## Rules

- **Rendering:** global interactive server rendering with prerendering **off** (`new InteractiveServerRenderMode(prerender: false)` in `App.razor`).
- **`Program.cs` stays thin.** Each layer exposes an `Add…`/`Use…`/`Map…` extension.
- **Pipeline order:** security headers → exception handler and HSTS → HTTPS → authentication → request localization → authorization → antiforgery.
- **`App.razor`:**
  - It carries `<meta name="csp-nonce">`, `nonce` on `<ImportMap>`, and `lang` from the UI culture.
  - Any new inline script needs the nonce.
- **Commands:** `bootstrap-admin` is handled at the top of `Program.cs`, before the host is built.
- **Razor files:** new components follow the `X.razor` + `X.razor.cs` rule. The error page component is `ErrorPage` at `/Error`, because a type named `Error` fails CA1716.
- **Settings:**
  - `appsettings.json` holds no secrets.
  - `appsettings.Development.json` points at the local docker containers and uses `Authentication:Mode = MockOidc`.

Guides: [security](../../docs/claude/security.md), [architecture](../../docs/claude/architecture.md), [conventions](../../docs/claude/conventions.md).
