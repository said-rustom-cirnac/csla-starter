# CslaStarter.Web

The host and composition root: `Program.cs`, sign-in, security headers, endpoints and pages. It is the only project that references every other.

**May reference:** all src projects.

## Rules

- Global interactive server rendering with prerendering **off** (`new InteractiveServerRenderMode(prerender: false)` in `App.razor`).
- Keep `Program.cs` thin. Each layer exposes an `Add…` extension method that registers its own services.
- Pages follow the razor pair rule (`X.razor` + `X.razor.cs`). The error page component is `ErrorPage`, at route `/Error`; a type named `Error` fails CA1716.

Guides: [architecture](../../docs/claude/architecture.md), [conventions](../../docs/claude/conventions.md).
