namespace CslaStarter.Domain.Platform.Users;

/// <summary>A user row as the DAL reads and stages it. <see cref="Version"/> is the row's version.</summary>
public sealed record UserDto(Guid Id, string DisplayName, uint Version);
