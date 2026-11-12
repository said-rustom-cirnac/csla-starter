namespace CslaStarter.Core.Security;

public enum SecurityEventKind
{
    SignIn = 1,
    SignOut = 2,
    AccessDenied = 3,
    RoleChanged = 4,
    SessionTimeout = 5,
    SensitiveReveal = 6,
    AdminBootstrapped = 7,
}
