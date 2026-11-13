namespace CslaStarter.Core.Security;

/// <summary>
/// The value is masked on screen: the server renders a mask, so the real value never reaches the browser until a user
/// with one of <see cref="RevealRoles"/> (UserRoleType codes) reveals it, which writes a security event.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SensitiveAttribute : Attribute
{
#pragma warning disable CA1819 // Attribute arguments must be arrays.
    public int[] RevealRoles { get; init; } = [];
#pragma warning restore CA1819
}
