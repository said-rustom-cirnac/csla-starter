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

    public static T Deserialize<T>(ApplicationContext applicationContext, byte[] payload)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(applicationContext);
        ArgumentNullException.ThrowIfNull(payload);
        return applicationContext.GetRequiredService<ISerializationFormatter>().Deserialize(payload) as T
            ?? throw new InvalidOperationException($"The draft is not a {typeof(T).Name}.");
    }
}
