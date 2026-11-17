namespace CslaStarter.Data.Platform.Users;

/// <summary>A person who signs in through Entra ID, keyed on tenant id and object id. The account is created at first sign-in.</summary>
public sealed class AppUser
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string TenantId { get; set; } = string.Empty;

    public string ObjectId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>The row's version: PostgreSQL's <c>xmin</c>, which changes on every update.</summary>
    public uint Version { get; set; }
}
