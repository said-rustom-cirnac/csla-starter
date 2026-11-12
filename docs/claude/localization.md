# Localization (en-CA / fr-CA)

## Resources

- **Text lives in `src/CslaStarter.Core/Resources`:**
  - `Labels.resx` / `Labels.fr.resx`: field names, button and short UI labels.
  - `Messages.resx` / `Messages.fr.resx`: rule, error and UI sentences, with `{0}` placeholders.
- **Generated classes:** strongly-typed public classes `CslaStarter.Core.Resources.Labels` and `Messages` are generated at build, so reference keys as `nameof(Labels.Common_Save)`.
- **Both languages always:** every key exists, non-empty, in both languages. `ResourceTests.Resources_HaveEveryKeyInBothLanguages` fails otherwise.
- **Key names** are `Area_Thing`, for example `Project_Name`, `Common_Save`, `Error_Reference`.

## Field names

Business properties use `[LocalizedDisplayName(nameof(Labels.Project_Name))]`:

- It subclasses `DisplayNameAttribute` and reads `Labels` in `CultureInfo.CurrentUICulture`.
- CSLA re-reads `FriendlyName` on every access, so labels follow the user's language with nothing cached.

## Choosing the culture

- **Order:** the culture cookie (`.AspNetCore.Culture`), then the signed-in user's preferred language (the `csla_starter:lang` claim from their profile), then `en-CA`.
- **Switching:** `GET /culture/set?culture=fr-CA&redirectUri=/current/path` sets the cookie and redirects. Only local paths are allowed; anything else goes to `/`. Unsupported cultures are ignored.
- **The page must reload,** because a Blazor circuit's culture is fixed when it starts. The language toggle saves the open form's draft first, so switching never loses edits (drafts arrive with the realtime phase).
- **The page language** comes from `<html lang="@CultureInfo.CurrentUICulture.Name">` in `App.razor`. GC Design System components read it once at load.
