namespace CslaStarter.Data.Platform.Users;

/// <summary>A person who signs in through Entra ID, keyed on tenant id and object id. The account is created at first sign-in.</summary>
public sealed class AppUser
{
    // The column lengths, shared by the configuration and the code that writes the columns.
    public const int TenantIdMaxLength = 100;
    public const int ObjectIdMaxLength = 100;
    public const int DisplayNameMaxLength = 200;

    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string TenantId { get; set; } = string.Empty;

    public string ObjectId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>The row's version: PostgreSQL's <c>xmin</c>, which changes on every update.</summary>
    public uint Version { get; set; }
}
