namespace CslaStarter.Data;

/// <summary>The runtime connection string, for components that need a short-lived context of their own.</summary>
public sealed record AppDatabaseConnection(string ConnectionString);
