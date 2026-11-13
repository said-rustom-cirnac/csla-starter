using Csla;
using Csla.Serialization;

namespace CslaStarter.Business.Drafts;

/// <summary>
/// Turns a business object graph (root, children, dirty and new flags, broken rules) into draft bytes and back, with
/// CSLA's own serializer. Restored objects belong to the given <see cref="ApplicationContext"/>.
/// </summary>
public static class DraftCodec
{
    public static byte[] Serialize(ApplicationContext applicationContext, object graph)
    {
        ArgumentNullException.ThrowIfNull(applicationContext);
        ArgumentNullException.ThrowIfNull(graph);
        return applicationContext.GetRequiredService<ISerializationFormatter>().Serialize(graph);
    }

    /// <summary>
    /// The restored object, or null when the payload can't be read as a <typeparamref name="T"/> (typically a draft saved
    /// before a deploy that changed the class): an unusable draft is no draft.
    /// </summary>
    public static T? TryDeserialize<T>(ApplicationContext applicationContext, byte[] payload)
        where T : class
    {
        try
        {
            return Deserialize<T>(applicationContext, payload);
        }
#pragma warning disable CA1031 // The serializer throws many exception types for a stale or corrupt payload.
        catch (Exception ex) when (ex is not (OutOfMemoryException or ArgumentNullException))
#pragma warning restore CA1031
        {
            return null;
        }
    }

    public static T Deserialize<T>(ApplicationContext applicationContext, byte[] payload)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(applicationContext);
        ArgumentNullException.ThrowIfNull(payload);
        return applicationContext.GetRequiredService<ISerializationFormatter>().Deserialize(payload) as T
            ?? throw new InvalidOperationException($"The draft is not a {typeof(T).Name}.");
    }
}
