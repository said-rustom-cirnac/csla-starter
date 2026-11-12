# Security

The template is built to a Protected B baseline (ITSG-33). Nothing below is optional in an app made from it.

## Sign-in

- `Authentication:Mode` is either `Entra` (the default) or `MockOidc`.
  - **Entra** uses Microsoft.Identity.Web with the `AzureAd` section (Instance, TenantId, ClientId, CallbackPath).
  - **MockOidc** is for local development and tests. It uses `ghcr.io/navikt/mock-oauth2-server` (see `docker-compose.yml` and `deploy/mock-oidc/mock-oidc.json`). **The app refuses to start with MockOidc in Production.**
- **Cookie:** `__Host-CslaStarter`, Secure, HttpOnly, SameSite=Lax, an 8-hour absolute lifetime, no sliding expiration.
- **User identity:** users are keyed on Entra **tenant id + object id** (`tid` + `oid`), never on name or email. `EntraClaims` reads both raw and mapped claim names.
- **On every authenticated request, `RoleClaimsTransformation`:**
  1. Finds or creates the app user through `IUserDirectory`. A first sign-in creates the user **with no roles** and writes a `SignIn` security event.
  2. Adds an identity carrying the app user id claim (`csla_starter:user_id`), the preferred language (`csla_starter:lang`), one `ClaimTypes.Role` claim per role code ("1" = Admin), and an inactive flag for deactivated users.
  3. Skips itself when the claims are already there (idempotent). Lookups are cached for 5 minutes; call `IUserDirectory.Invalidate(tid, oid)` after changing someone's roles.
- **Fallback policy (deny by default):** the user must be signed in, resolved to an app user, and active. Endpoints opt out with `.AllowAnonymous()`: health, culture switch, static assets.
- **Revalidation:** `UserRevalidatingAuthenticationStateProvider` re-checks every 10 minutes that a circuit's user is still active.

## The first Admin

```bash
dotnet CslaStarter.Web.dll bootstrap-admin --tenant <tid> --object-id <oid> --name "<display name>"
```

- It runs once, from the deployment pipeline, with `ConnectionStrings:Owner`.
- Exit codes: 0 done, 1 bad arguments, **2 refused because an Admin already exists**.
- It writes an `AdminBootstrapped` security event.
- There is no other way to create an Admin outside the app: no "first user is Admin", no admin ids in config.

## Response headers

`UseCslaStarterSecurityHeaders` runs first in the pipeline:

- **CSP:** `default-src 'self'; ... script-src 'self' 'nonce-{nonce}'; style-src 'self'; ... frame-ancestors 'none' ...` (the full string is in `SecurityHeaders.ContentSecurityPolicy`).
  - The nonce is 128 random bits per request, as hex. `CspNonce` is a scoped service.
  - `App.razor` puts the nonce in `<meta name="csp-nonce">` (read by GC Design System's runtime) and on `<ImportMap>`.
  - Every inline script must carry the nonce. `Csp_EveryInlineScriptCarriesTheNonce` checks this.
- **Also set:** `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, a deny-all `Permissions-Policy`, `Cross-Origin-Opener-Policy: same-origin`, and HSTS outside Development.

## Sessions

`SessionTimeoutMonitor` (scoped per circuit) tracks inbound circuit activity:

- **Active** until `Session:WarningAfter` (18 minutes).
- **Warning** until `Session:SignOutAfter` (20 minutes).
- **Expired** after that. This state is final.

The session-timeout component (components phase) shows the warning and signs the user out.

## Keys and secrets

- **Data Protection keys** live in PostgreSQL (`data_protection_keys`), so every instance shares them. Outside Development they must be encrypted at rest:
  - `DataProtection:CertificateThumbprint` for IIS;
  - `DataProtection:KeyVaultKeyId` for Azure.
  
  Otherwise the app refuses to start.
- **No secrets in committed settings.** `appsettings.Development.json` holds only the passwords of the local docker containers.

## Security events

`ISecurityEventLog` writes `security_event` rows and structured logs (EventId 1000, with the kind as a property). The kinds are SignIn, SignOut, AccessDenied, RoleChanged, SessionTimeout, SensitiveReveal and AdminBootstrapped. The log is a singleton with its own short-lived context, so writing an event never saves a caller's pending changes.

## Dependency injection rule

Integration tests build the host with `ValidateScopes` and `ValidateOnBuild`, like Development, so a singleton can never capture a scoped service.
