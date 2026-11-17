# CslaStarter.Web

The host and composition root, the `Csla*` component kit and the pages. Today `Program.cs` maps only `/health/live`, and `Platform/` and `Features/` are empty.

- References `CslaStarter.Domain` and `CslaStarter.Data`. Only host code uses `Data`; pages and components never use a `Data` type.
- `wwwroot/gcds` is the vendored GC Design System. It is the host's own static content, so its address is `/gcds/...` (no `_content/` prefix); no page uses it yet. `scripts/update-gcds.sh` writes it; never edit it by hand.
- Components are `X.razor` (markup only) plus `X.razor.cs`, with no inline `style` attribute.
- Tests: `tests/CslaStarter.Architecture.Tests` checks the references, boundaries, `.razor` conventions and design-system assets.

Structure and boundaries: [architecture](../../docs/handbook/architecture.md).
