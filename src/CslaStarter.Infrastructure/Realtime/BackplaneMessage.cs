using System.Text.Json;
using System.Text.Json.Serialization;
using CslaStarter.Core.Realtime;

namespace CslaStarter.Infrastructure.Realtime;

/// <summary>
/// A change notice as it travels between instances: the notice's own fields plus the sending instance, so an instance
/// can drop its own echo. Never field data. Well under PostgreSQL's 8000-byte NOTIFY limit.
/// </summary>
internal sealed record BackplaneMessage(string Instance, string Type, string Key, ChangeKind Kind, uint? Version, Guid? UserId, string UserName, string? Origin)
{
    /// <summary>NOTIFY payloads must stay under 8000 bytes; anything near it is not a change notice.</summary>
    public const int MaxPayloadLength = 7000;

    private const int MaxUserNameLength = 200;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public static string Serialize(string instance, EntityChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var userName = change.ChangedByName.Length > MaxUserNameLength ? change.ChangedByName[..MaxUserNameLength] : change.ChangedByName;
        return JsonSerializer.Serialize(
            new BackplaneMessage(instance, change.EntityType, change.EntityKey, change.Kind, change.Version, change.ChangedByUserId, userName, change.OriginId),
            Json);
    }

    public static BackplaneMessage? TryDeserialize(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<BackplaneMessage>(payload, Json) is { Instance.Length: > 0, Type.Length: > 0, Key.Length: > 0 } message ? message : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public EntityChange ToChange() => new(Type, Key, Kind, Version, UserId, UserName ?? string.Empty, Origin);
}

/// <summary>A presence message as it travels between instances, with the sending instance.</summary>
internal sealed record PresenceWire(string Instance, PresenceAction Action, string Type, string? Key, Guid UserId, string Name, string Circuit, PresenceMode Mode)
{
    private const int MaxNameLength = 200;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public static string Serialize(string instance, PresenceMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var entry = message.Entry;
        var name = entry.DisplayName.Length > MaxNameLength ? entry.DisplayName[..MaxNameLength] : entry.DisplayName;
        return JsonSerializer.Serialize(new PresenceWire(instance, message.Action, message.EntityType, message.EntityKey, entry.UserId, name, entry.CircuitId, entry.Mode), Json);
    }

    public static PresenceWire? TryDeserialize(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<PresenceWire>(payload, Json) is { Instance.Length: > 0, Type.Length: > 0, Circuit.Length: > 0 } wire ? wire : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public PresenceMessage ToMessage() => new(Action, Type, Key, new PresenceEntry(UserId, Name ?? string.Empty, Circuit, Mode));
}
