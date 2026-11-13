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
- **Layout:** `MainLayout` hosts `CslaLanguageToggle`, `CslaToastHost`, `CslaLiveRegion` and `CslaSessionTimeout` once for the app.
- **Sample pages** (`Pages/Sample/`): `/projects`, `/projects/new`, `/projects/{id}`, `/users`, `/users/{id}` and `/profile`. They are unstyled compositions of the components. Inject `Csla.IDataPortal<T>` by its full name (`Csla` and Blazor both define `[Inject]`).
- **Endpoints:** `/culture/set` (GET) and `/account/signout` (POST, antiforgery).
- **Razor files:** new components follow the `X.razor` + `X.razor.cs` rule. The error page component is `ErrorPage` at `/Error`, because a type named `Error` fails CA1716.
- **Settings:**
  - `appsettings.json` holds no secrets.
  - `appsettings.Development.json` points at the local docker containers and uses `Authentication:Mode = MockOidc`.

Guides: [components](../../docs/claude/components.md), [add a page](../../docs/claude/recipes/add-a-page.md), [security](../../docs/claude/security.md), [architecture](../../docs/claude/architecture.md), [conventions](../../docs/claude/conventions.md).
